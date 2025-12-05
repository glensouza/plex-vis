using System.Text.RegularExpressions;
using Dapper;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;
using PlexVis.Web.Configuration;
using PlexVis.Web.Models;

namespace PlexVis.Web.Services;

public partial class PlexDataService(IOptions<PlexSettings> plexSettings, ILogger<PlexDataService> plexLogger)
{
    private readonly PlexSettings settings = plexSettings.Value;
    private readonly Lock cacheLock = new();
    private string? cachedDatabasePath;
    private DateTime cacheTimestamp;
    private static readonly TimeSpan CacheExpiration = TimeSpan.FromHours(1);

    /// <summary>
    /// Gets the path to the database file to use.
    /// If DatabasePath is configured, uses that directly.
    /// Otherwise, if DatabaseDirectory is configured, discovers the latest backup database file.
    /// Note: This method may perform file system operations on cache miss or expiration.
    /// The result is cached for 1 hour to minimize I/O overhead.
    /// </summary>
    public string? GetDatabasePath()
    {
        // If a direct path is specified and exists, use it
        if (!string.IsNullOrEmpty(this.settings.DatabasePath) && File.Exists(this.settings.DatabasePath))
        {
            return this.settings.DatabasePath;
        }

        // If a directory is specified, discover the latest backup
        if (string.IsNullOrEmpty(this.settings.DatabaseDirectory) || !Directory.Exists(this.settings.DatabaseDirectory))
        {
            return null;
        }

        lock (this.cacheLock)
        {
            // Invalidate cache if expired
            if (this.cachedDatabasePath != null && DateTime.UtcNow - this.cacheTimestamp > CacheExpiration)
            {
                plexLogger.LogDebug("Database path cache expired, refreshing");
                this.cachedDatabasePath = null;
            }

            if (this.cachedDatabasePath != null)
            {
                return this.cachedDatabasePath;
            }

            string? discoveredPath = this.DiscoverLatestBackupDatabase(this.settings.DatabaseDirectory);
            if (discoveredPath != null)
            {
                this.cachedDatabasePath = discoveredPath;
                this.cacheTimestamp = DateTime.UtcNow;
            }
            else
            {
                // Do not update cacheTimestamp; retry discovery on next call
                plexLogger.LogDebug("No backup database found; will retry discovery on next call.");
            }

            return this.cachedDatabasePath;
        }

    }

    /// <summary>
    /// Clears the cached database path, forcing a fresh discovery on the next call to GetDatabasePath().
    /// This method is thread-safe.
    /// </summary>
    public void RefreshDatabasePath()
    {
        lock (this.cacheLock)
        {
            this.cachedDatabasePath = null;
            plexLogger.LogInformation("Database path cache cleared");
        }
    }

    /// <summary>
    /// Discovers the latest Plex backup database file in the specified directory.
    /// Plex backup files follow the naming convention: com.plexapp.plugins.library.db-YYYY-MM-DD
    /// </summary>
    private string? DiscoverLatestBackupDatabase(string directory)
    {
        try
        {
            // Pattern matches Plex backup database files like:
            // com.plexapp.plugins.library.db-2023-10-15
            // com.plexapp.plugins.library.db-2024-01-20
            string[] backupFiles = Directory.GetFiles(directory, "com.plexapp.plugins.library.db-*");

            if (backupFiles.Length == 0)
            {
                plexLogger.LogWarning("No backup database files found in directory: {Directory}", directory);
                return null;
            }

            // Sort by the date in the filename to get the latest backup
            // The date format YYYY-MM-DD sorts correctly when sorted alphabetically
            string latestBackup = backupFiles
                .Select(f => new { FullPath = f, FileName = Path.GetFileName(f) })
                .Where(f => BackupFileDatePattern().IsMatch(f.FileName))
                .OrderByDescending(f => f.FileName)
                .Select(f => f.FullPath)
                .FirstOrDefault() ?? string.Empty;

            if (string.IsNullOrEmpty(latestBackup))
            {
                plexLogger.LogWarning("No valid backup database files with date pattern found in directory: {Directory}", directory);
                return null;
            }

            plexLogger.LogInformation("Discovered latest backup database: {BackupPath}", latestBackup);
            if (File.Exists(latestBackup))
            {
                return latestBackup;
            }

            plexLogger.LogWarning("Latest backup file does not exist: {BackupPath}", latestBackup);
            return null;
        }
        catch (Exception ex)
        {
            plexLogger.LogError(ex, "Error discovering backup database files in directory: {Directory}", directory);
            return null;
        }
    }

    // Regex pattern to match the date suffix in Plex backup filenames
    // Regex pattern matches: com.plexapp.plugins.library.db-YYYY-MM-DD
    // - YYYY: 4 digits
    // - MM: 01-12
    // - DD: 01-31
    // Note: This pattern allows invalid dates like 2024-02-31. Since Plex generates these
    // filenames with valid dates, this is acceptable for our use case.
    [GeneratedRegex(@"com\.plexapp\.plugins\.library\.db-\d{4}-(0[1-9]|1[0-2])-(0[1-9]|[12][0-9]|3[01])$")]
    private static partial Regex BackupFileDatePattern();

    /// <summary>
    /// Gets whether a database is configured and available.
    /// Note: This property calls GetDatabasePath() which may perform file system operations
    /// on cache miss or expiration. Results are cached for 1 hour.
    /// </summary>
    public bool IsDatabaseConfigured => this.GetDatabasePath() != null;

    private SqliteConnection CreateConnection()
    {
        string? databasePath = this.GetDatabasePath();
        if (string.IsNullOrEmpty(databasePath))
        {
            throw new InvalidOperationException("Database path is not configured or database file not found.");
        }

        SqliteConnectionStringBuilder builder = new()
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadOnly
        };
        return new SqliteConnection(builder.ConnectionString);
    }

    /// <summary>
    /// Gets all Plex user accounts from the database.
    /// </summary>
    /// <returns>A collection of PlexAccount objects representing available user accounts.</returns>
    public async Task<IEnumerable<PlexAccount>> GetAccountsAsync()
    {
        if (!this.IsDatabaseConfigured)
        {
            plexLogger.LogWarning("Plex database not configured or not found");
            return [];
        }

        const string sql = """
            SELECT DISTINCT 
                account_id AS Id,
                COALESCE(
                    (SELECT name FROM accounts WHERE id = metadata_item_settings.account_id),
                    'User ' || account_id
                ) AS Name
            FROM metadata_item_settings
            WHERE account_id IS NOT NULL
            ORDER BY Name;
            """;

        try
        {
            await using SqliteConnection connection = this.CreateConnection();
            IEnumerable<PlexAccount> results = await connection.QueryAsync<PlexAccount>(sql);
            return results;
        }
        catch (Exception ex)
        {
            plexLogger.LogError(ex, "Error querying Plex accounts");
            return [];
        }
    }

    /// <summary>
    /// Gets viewing velocity data for TV shows, optionally filtered by account.
    /// </summary>
    /// <param name="accountId">Optional account ID to filter results. If null, shows data for all accounts.</param>
    /// <returns>A collection of ShowVelocity objects representing viewing velocity for shows.</returns>
    public async Task<IEnumerable<ShowVelocity>> GetViewingVelocityAsync(int? accountId = null)
    {
        if (!this.IsDatabaseConfigured)
        {
            plexLogger.LogWarning("Plex database not configured or not found");
            return [];
        }

        string accountFilter = accountId.HasValue ? "AND settings.account_id = @AccountId" : "";
        string accountJoinFilter = accountId.HasValue ? "AND settings.account_id = @AccountId" : "";

        string sql = $"""
            WITH 
            -- Shows that have at least one watched episode
            ShowsWithWatchedEpisodes AS (
                SELECT DISTINCT tvshow.id AS ShowID
                FROM metadata_items episode
                JOIN metadata_items season ON episode.parent_id = season.id
                JOIN metadata_items tvshow ON season.parent_id = tvshow.id
                JOIN metadata_item_settings settings ON episode.guid = settings.guid
                WHERE episode.metadata_type = 4
                  AND settings.view_count > 0
                  {accountFilter}
            ),
            -- Calculate lag for ALL episodes (watched and unwatched)
            -- Watched: time from added_at to last_viewed_at
            -- Unwatched: time from added_at to now (treating today as the "watch" date)
            AllEpisodeLag AS (
                SELECT 
                    tvshow.id AS ShowID,
                    tvshow.title AS ShowTitle,
                    CASE 
                        WHEN settings.view_count > 0 AND settings.last_viewed_at IS NOT NULL 
                        THEN settings.last_viewed_at - episode.added_at
                        ELSE strftime('%s', 'now') - episode.added_at
                    END AS LagSeconds
                FROM metadata_items episode
                JOIN metadata_items season ON episode.parent_id = season.id
                JOIN metadata_items tvshow ON season.parent_id = tvshow.id
                LEFT JOIN metadata_item_settings settings ON episode.guid = settings.guid
                    {accountJoinFilter}
                WHERE episode.metadata_type = 4
                  AND episode.added_at IS NOT NULL
            ),
            ShowVelocity AS (
                SELECT 
                    ShowID,
                    ShowTitle,
                    AVG(LagSeconds) AS AvgLagSeconds
                FROM AllEpisodeLag
                WHERE LagSeconds >= 0
                GROUP BY ShowID
            ),
            -- Shows that have at least one unwatched episode (the next episode to watch)
            NextEpisodes AS (
                SELECT 
                    tvshow.id AS ShowID,
                    tvshow.title AS ShowTitle,
                    season."index" AS SeasonNum,
                    episode."index" AS EpisodeNum,
                    episode.title AS EpisodeTitle,
                    MIN(season."index" * 1000 + episode."index") as GlobalIndex
                FROM metadata_items episode
                JOIN metadata_items season ON episode.parent_id = season.id
                JOIN metadata_items tvshow ON season.parent_id = tvshow.id
                LEFT JOIN metadata_item_settings settings ON episode.guid = settings.guid
                    {accountJoinFilter}
                WHERE episode.metadata_type = 4
                  AND (settings.view_count IS NULL OR settings.view_count = 0)
                GROUP BY tvshow.id
            )
            SELECT 
                v.ShowTitle,
                n.SeasonNum,
                n.EpisodeNum,
                n.EpisodeTitle,
                ROUND(v.AvgLagSeconds / 86400.0, 1) AS AvgDaysToWatch
            FROM ShowVelocity v
            -- Must have at least one unwatched episode
            INNER JOIN NextEpisodes n ON v.ShowID = n.ShowID
            -- Must have at least one watched episode
            INNER JOIN ShowsWithWatchedEpisodes w ON v.ShowID = w.ShowID
            ORDER BY v.AvgLagSeconds ASC
            LIMIT 20;
            """;

        try
        {
            await using SqliteConnection connection = this.CreateConnection();
            IEnumerable<ShowVelocity> results = await connection.QueryAsync<ShowVelocity>(sql, new { AccountId = accountId });
            return results;
        }
        catch (Exception ex)
        {
            plexLogger.LogError(ex, "Error querying viewing velocity");
            return [];
        }
    }

    public async Task<LibraryStats> GetLibraryStatsAsync()
    {
        if (!this.IsDatabaseConfigured)
        {
            plexLogger.LogWarning("Plex database not configured or not found");
            return new LibraryStats();
        }

        const string sql = """
            SELECT 
                (SELECT COUNT(*) FROM metadata_items WHERE metadata_type = 1) AS TotalMovies,
                (SELECT COUNT(*) FROM metadata_items WHERE metadata_type = 2) AS TotalShows,
                (SELECT COUNT(*) FROM metadata_items WHERE metadata_type = 4) AS TotalEpisodes,
                (SELECT COUNT(DISTINCT m.id) FROM metadata_items m 
                 JOIN metadata_item_settings s ON m.guid = s.guid 
                 WHERE m.metadata_type = 1 AND s.view_count > 0) AS WatchedMovies,
                (SELECT COUNT(DISTINCT m.id) FROM metadata_items m 
                 JOIN metadata_item_settings s ON m.guid = s.guid 
                 WHERE m.metadata_type = 4 AND s.view_count > 0) AS WatchedEpisodes,
                COALESCE((SELECT ROUND(SUM(size) / 1073741824.0, 2) FROM media_parts), 0) AS TotalSizeGB;
            """;

        try
        {
            await using SqliteConnection connection = this.CreateConnection();
            LibraryStats? stats = await connection.QuerySingleOrDefaultAsync<LibraryStats>(sql);
            return stats ?? new LibraryStats();
        }
        catch (Exception ex)
        {
            plexLogger.LogError(ex, "Error querying library stats");
            return new LibraryStats();
        }
    }

    public async Task<IEnumerable<RecentlyWatched>> GetRecentlyWatchedAsync(int limit = 10)
    {
        if (!this.IsDatabaseConfigured)
        {
            plexLogger.LogWarning("Plex database not configured or not found");
            return [];
        }

        const string sql = """
            SELECT 
                m.title AS Title, 
                s.view_count AS ViewCount, 
                datetime(s.last_viewed_at, 'unixepoch', 'localtime') as LastWatched,
                m.metadata_type AS MetadataType
            FROM metadata_items m
            JOIN metadata_item_settings s ON m.guid = s.guid
            WHERE s.view_count > 0
            ORDER BY s.last_viewed_at DESC
            LIMIT @Limit;
            """;

        try
        {
            await using SqliteConnection connection = this.CreateConnection();
            IEnumerable<RecentlyWatched> results = await connection.QueryAsync<RecentlyWatched>(sql, new { Limit = limit });
            return results;
        }
        catch (Exception ex)
        {
            plexLogger.LogError(ex, "Error querying recently watched");
            return [];
        }
    }

    /// <summary>
    /// Gets movies that have multiple video files (potential duplicates).
    /// Excludes movies that only have exactly 2 files where one is 4K and the other is not (legitimate quality variants).
    /// </summary>
    /// <returns>A collection of DuplicateMovie objects with file counts and details.</returns>
    public async Task<IEnumerable<DuplicateMovie>> GetDuplicateMoviesAsync()
    {
        if (!this.IsDatabaseConfigured)
        {
            plexLogger.LogWarning("Plex database not configured or not found");
            return [];
        }

        // First, get movies with duplicates along with file details
        const string sql = """
            SELECT 
                m.id AS MetadataItemId,
                m.title AS Title, 
                m.year AS Year,
                p.file AS FilePath,
                ROUND(p.size / 1073741824.0, 2) AS SizeGb,
                COALESCE(i.width, 0) AS Width,
                COALESCE(i.height, 0) AS Height
            FROM metadata_items m
            JOIN media_items i ON m.id = i.metadata_item_id
            JOIN media_parts p ON i.id = p.media_item_id
            WHERE m.metadata_type = 1
            AND m.id IN (
                SELECT m2.id
                FROM metadata_items m2
                JOIN media_items i2 ON m2.id = i2.metadata_item_id
                JOIN media_parts p2 ON i2.id = p2.media_item_id
                WHERE m2.metadata_type = 1
                GROUP BY m2.id
                HAVING COUNT(p2.id) > 1
            )
            ORDER BY m.title, p.file;
            """;

        try
        {
            await using SqliteConnection connection = this.CreateConnection();
            IEnumerable<DuplicateMovieRow> rows = await connection.QueryAsync<DuplicateMovieRow>(sql);

            // Group by movie and filter out legitimate 4K+non-4K pairs
            Dictionary<int, DuplicateMovie> movieDict = [];
            
            foreach (DuplicateMovieRow row in rows)
            {
                int metadataItemId = row.MetadataItemId;
                
                if (!movieDict.TryGetValue(metadataItemId, out DuplicateMovie? movie))
                {
                    movie = new DuplicateMovie
                    {
                        MetadataItemId = metadataItemId,
                        Title = row.Title ?? string.Empty,
                        Year = row.Year ?? 0,
                        Files = []
                    };
                    movieDict[metadataItemId] = movie;
                }

                movie.Files.Add(CreateMediaFileDetailFromMovieRow(row));
            }

            // Filter out movies with exactly 2 files where one is 4K and the other is not
            List<DuplicateMovie> filteredMovies = movieDict.Values
                .Where(m => !IsLegitimate4KVariant(m.Files))
                .ToList();

            foreach (DuplicateMovie movie in filteredMovies)
            {
                movie.FileCount = movie.Files.Count;
            }

            return filteredMovies.OrderByDescending(m => m.FileCount).ThenBy(m => m.Title);
        }
        catch (Exception ex)
        {
            plexLogger.LogError(ex, "Error querying duplicate movies");
            return [];
        }
    }

    /// <summary>
    /// Gets TV episodes that have multiple video files (potential duplicates).
    /// Excludes episodes that only have exactly 2 files where one is 4K and the other is not (legitimate quality variants).
    /// </summary>
    /// <returns>A collection of DuplicateEpisode objects with file counts and details.</returns>
    public async Task<IEnumerable<DuplicateEpisode>> GetDuplicateEpisodesAsync()
    {
        if (!this.IsDatabaseConfigured)
        {
            plexLogger.LogWarning("Plex database not configured or not found");
            return [];
        }

        const string sql = """
            SELECT 
                e.id AS MetadataItemId,
                show.title AS ShowTitle,
                s."index" AS SeasonNumber,
                e."index" AS EpisodeNumber,
                e.title AS EpisodeTitle,
                p.file AS FilePath,
                ROUND(p.size / 1073741824.0, 2) AS SizeGb,
                COALESCE(i.width, 0) AS Width,
                COALESCE(i.height, 0) AS Height
            FROM metadata_items e
            JOIN metadata_items s ON e.parent_id = s.id
            JOIN metadata_items show ON s.parent_id = show.id
            JOIN media_items i ON e.id = i.metadata_item_id
            JOIN media_parts p ON i.id = p.media_item_id
            WHERE e.metadata_type = 4
            AND e.id IN (
                SELECT e2.id
                FROM metadata_items e2
                JOIN media_items i2 ON e2.id = i2.metadata_item_id
                JOIN media_parts p2 ON i2.id = p2.media_item_id
                WHERE e2.metadata_type = 4
                GROUP BY e2.id
                HAVING COUNT(p2.id) > 1
            )
            ORDER BY show.title, s."index", e."index", p.file;
            """;

        try
        {
            await using SqliteConnection connection = this.CreateConnection();
            IEnumerable<DuplicateEpisodeRow> rows = await connection.QueryAsync<DuplicateEpisodeRow>(sql);

            Dictionary<int, DuplicateEpisode> episodeDict = [];
            
            foreach (DuplicateEpisodeRow row in rows)
            {
                int metadataItemId = row.MetadataItemId;
                
                if (!episodeDict.TryGetValue(metadataItemId, out DuplicateEpisode? episode))
                {
                    episode = new DuplicateEpisode
                    {
                        MetadataItemId = metadataItemId,
                        ShowTitle = row.ShowTitle ?? string.Empty,
                        SeasonNumber = row.SeasonNumber ?? 0,
                        EpisodeNumber = row.EpisodeNumber ?? 0,
                        EpisodeTitle = row.EpisodeTitle ?? string.Empty,
                        Files = []
                    };
                    episodeDict[metadataItemId] = episode;
                }

                episode.Files.Add(CreateMediaFileDetailFromEpisodeRow(row));
            }

            // Filter out episodes with exactly 2 files where one is 4K and the other is not
            List<DuplicateEpisode> filteredEpisodes = episodeDict.Values
                .Where(e => !IsLegitimate4KVariant(e.Files))
                .ToList();

            foreach (DuplicateEpisode episode in filteredEpisodes)
            {
                episode.FileCount = episode.Files.Count;
            }

            return filteredEpisodes.OrderByDescending(e => e.FileCount).ThenBy(e => e.ShowTitle).ThenBy(e => e.SeasonNumber).ThenBy(e => e.EpisodeNumber);
        }
        catch (Exception ex)
        {
            plexLogger.LogError(ex, "Error querying duplicate episodes");
            return [];
        }
    }

    /// <summary>
    /// Creates a MediaFileDetail instance from a DuplicateMovieRow.
    /// </summary>
    private static MediaFileDetail CreateMediaFileDetailFromMovieRow(DuplicateMovieRow row)
    {
        return new MediaFileDetail
        {
            FilePath = row.FilePath ?? string.Empty,
            SizeGb = row.SizeGb ?? 0,
            Width = row.Width ?? 0,
            Height = row.Height ?? 0
        };
    }

    /// <summary>
    /// Creates a MediaFileDetail instance from a DuplicateEpisodeRow.
    /// </summary>
    private static MediaFileDetail CreateMediaFileDetailFromEpisodeRow(DuplicateEpisodeRow row)
    {
        return new MediaFileDetail
        {
            FilePath = row.FilePath ?? string.Empty,
            SizeGb = row.SizeGb ?? 0,
            Width = row.Width ?? 0,
            Height = row.Height ?? 0
        };
    }

    /// <summary>
    /// Determines if a set of files represents a legitimate 4K variant (exactly 2 files: one 4K, one non-4K).
    /// </summary>
    private static bool IsLegitimate4KVariant(List<MediaFileDetail> files)
    {
        if (files.Count != 2)
        {
            return false;
        }

        int fourKCount = files.Count(f => f.Is4K);
        return fourKCount == 1;
    }

    /// <summary>
    /// Gets items that Plex sees but hasn't matched to an agent (local:// items).
    /// </summary>
    /// <returns>A collection of UnmatchedItem objects.</returns>
    public async Task<IEnumerable<UnmatchedItem>> GetUnmatchedItemsAsync()
    {
        if (!this.IsDatabaseConfigured)
        {
            plexLogger.LogWarning("Plex database not configured or not found");
            return [];
        }

        const string sql = """
            SELECT 
                id AS Id, 
                title AS Title, 
                datetime(added_at, 'unixepoch', 'localtime') AS AddedAt
            FROM metadata_items 
            WHERE guid LIKE 'local://%' 
            AND metadata_type IN (1, 2, 8)
            ORDER BY added_at DESC;
            """;

        try
        {
            await using SqliteConnection connection = this.CreateConnection();
            IEnumerable<UnmatchedItem> results = await connection.QueryAsync<UnmatchedItem>(sql);
            return results;
        }
        catch (Exception ex)
        {
            plexLogger.LogError(ex, "Error querying unmatched items");
            return [];
        }
    }

    /// <summary>
    /// Gets items marked as soft-deleted in the Plex database.
    /// </summary>
    /// <returns>A collection of SoftDeletedItem objects.</returns>
    public async Task<IEnumerable<SoftDeletedItem>> GetSoftDeletedItemsAsync()
    {
        if (!this.IsDatabaseConfigured)
        {
            plexLogger.LogWarning("Plex database not configured or not found");
            return [];
        }

        const string sql = """
            SELECT 
                id AS Id, 
                title AS Title,
                metadata_type AS MetadataType,
                datetime(deleted_at, 'unixepoch', 'localtime') AS DeletedAt
            FROM metadata_items 
            WHERE deleted_at IS NOT NULL
            ORDER BY deleted_at DESC;
            """;

        try
        {
            await using SqliteConnection connection = this.CreateConnection();
            IEnumerable<SoftDeletedItem> results = await connection.QueryAsync<SoftDeletedItem>(sql);
            return results;
        }
        catch (Exception ex)
        {
            plexLogger.LogError(ex, "Error querying soft-deleted items");
            return [];
        }
    }

    /// <summary>
    /// DTO for mapping duplicate movie query results from the database.
    /// </summary>
    private sealed class DuplicateMovieRow
    {
        /// <summary>
        /// Gets or sets the metadata item identifier.
        /// </summary>
        public int MetadataItemId { get; set; }

        /// <summary>
        /// Gets or sets the movie title.
        /// </summary>
        public string? Title { get; set; }

        /// <summary>
        /// Gets or sets the movie release year.
        /// </summary>
        public int? Year { get; set; }

        /// <summary>
        /// Gets or sets the file path.
        /// </summary>
        public string? FilePath { get; set; }

        /// <summary>
        /// Gets or sets the file size in gigabytes.
        /// </summary>
        public double? SizeGb { get; set; }

        /// <summary>
        /// Gets or sets the video width in pixels.
        /// </summary>
        public int? Width { get; set; }

        /// <summary>
        /// Gets or sets the video height in pixels.
        /// </summary>
        public int? Height { get; set; }
    }

    /// <summary>
    /// DTO for mapping duplicate episode query results from the database.
    /// </summary>
    private sealed class DuplicateEpisodeRow
    {
        /// <summary>
        /// Gets or sets the metadata item identifier.
        /// </summary>
        public int MetadataItemId { get; set; }

        /// <summary>
        /// Gets or sets the TV show title.
        /// </summary>
        public string? ShowTitle { get; set; }

        /// <summary>
        /// Gets or sets the season number.
        /// </summary>
        public int? SeasonNumber { get; set; }

        /// <summary>
        /// Gets or sets the episode number.
        /// </summary>
        public int? EpisodeNumber { get; set; }

        /// <summary>
        /// Gets or sets the episode title.
        /// </summary>
        public string? EpisodeTitle { get; set; }

        /// <summary>
        /// Gets or sets the file path.
        /// </summary>
        public string? FilePath { get; set; }

        /// <summary>
        /// Gets or sets the file size in gigabytes.
        /// </summary>
        public double? SizeGb { get; set; }

        /// <summary>
        /// Gets or sets the video width in pixels.
        /// </summary>
        public int? Width { get; set; }

        /// <summary>
        /// Gets or sets the video height in pixels.
        /// </summary>
        public int? Height { get; set; }
    }
}
