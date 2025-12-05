using System.Text.RegularExpressions;
using Dapper;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;
using PlexVis.Web.Configuration;
using PlexVis.Web.Models;

namespace PlexVis.Web.Services;

public partial class PlexDataService
{
    private readonly PlexSettings settings;
    private readonly ILogger<PlexDataService> logger;
    private readonly object cacheLock = new();
    private string? cachedDatabasePath;
    private DateTime cacheTimestamp;
    private static readonly TimeSpan CacheExpiration = TimeSpan.FromHours(1);

    public PlexDataService(IOptions<PlexSettings> plexSettings, ILogger<PlexDataService> plexLogger)
    {
        this.settings = plexSettings.Value;
        this.logger = plexLogger;
    }

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
        if (!string.IsNullOrEmpty(this.settings.DatabaseDirectory) && Directory.Exists(this.settings.DatabaseDirectory))
        {
            lock (this.cacheLock)
            {
                // Invalidate cache if expired
                if (this.cachedDatabasePath != null && DateTime.UtcNow - this.cacheTimestamp > CacheExpiration)
                {
                    this.logger.LogDebug("Database path cache expired, refreshing");
                    this.cachedDatabasePath = null;
                }

                if (this.cachedDatabasePath == null)
                {
                    string? discoveredPath = this.DiscoverLatestBackupDatabase(this.settings.DatabaseDirectory);
                    if (discoveredPath != null)
                    {
                        this.cachedDatabasePath = discoveredPath;
                        this.cacheTimestamp = DateTime.UtcNow;
                    }
                    else
                    {
                        // Do not update cacheTimestamp; retry discovery on next call
                        this.logger.LogDebug("No backup database found; will retry discovery on next call.");
                    }
                }

                return this.cachedDatabasePath;
            }
        }

        return null;
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
            this.logger.LogInformation("Database path cache cleared");
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
                this.logger.LogWarning("No backup database files found in directory: {Directory}", directory);
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
                this.logger.LogWarning("No valid backup database files with date pattern found in directory: {Directory}", directory);
                return null;
            }

            this.logger.LogInformation("Discovered latest backup database: {BackupPath}", latestBackup);
            if (!File.Exists(latestBackup))
            {
                this.logger.LogWarning("Latest backup file does not exist: {BackupPath}", latestBackup);
                return null;
            }
            return latestBackup;
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "Error discovering backup database files in directory: {Directory}", directory);
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
    public bool IsDatabaseConfigured => GetDatabasePath() != null;

    private SqliteConnection CreateConnection()
    {
        string? databasePath = GetDatabasePath();
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

    public async Task<IEnumerable<ShowVelocity>> GetViewingVelocityAsync()
    {
        if (!this.IsDatabaseConfigured)
        {
            this.logger.LogWarning("Plex database not configured or not found");
            return [];
        }

        const string sql = """
            WITH 
            ShowVelocity AS (
                SELECT 
                    tvshow.id AS ShowID,
                    tvshow.title AS ShowTitle,
                    AVG(settings.last_viewed_at - episode.added_at) AS AvgLagSeconds
                FROM metadata_items episode
                JOIN metadata_items season ON episode.parent_id = season.id
                JOIN metadata_items tvshow ON season.parent_id = tvshow.id
                JOIN metadata_item_settings settings ON episode.guid = settings.guid
                WHERE episode.metadata_type = 4
                  AND settings.view_count > 0
                  AND episode.added_at IS NOT NULL
                  AND settings.last_viewed_at IS NOT NULL
                  AND settings.last_viewed_at > episode.added_at
                GROUP BY tvshow.id
            ),
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
                WHERE episode.metadata_type = 4
                  AND (settings.view_count IS NULL OR settings.view_count = 0)
                GROUP BY tvshow.id
            )
            SELECT 
                v.ShowTitle,
                COALESCE(n.SeasonNum, 0) AS SeasonNum,
                COALESCE(n.EpisodeNum, 0) AS EpisodeNum,
                COALESCE(n.EpisodeTitle, 'All caught up!') AS EpisodeTitle,
                ROUND(v.AvgLagSeconds / 86400.0, 1) AS AvgDaysToWatch
            FROM ShowVelocity v
            LEFT JOIN NextEpisodes n ON v.ShowID = n.ShowID
            ORDER BY AvgDaysToWatch ASC
            LIMIT 20;
            """;

        try
        {
            await using SqliteConnection connection = this.CreateConnection();
            IEnumerable<ShowVelocity> results = await connection.QueryAsync<ShowVelocity>(sql);
            return results;
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "Error querying viewing velocity");
            return [];
        }
    }

    public async Task<LibraryStats> GetLibraryStatsAsync()
    {
        if (!this.IsDatabaseConfigured)
        {
            this.logger.LogWarning("Plex database not configured or not found");
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
            this.logger.LogError(ex, "Error querying library stats");
            return new LibraryStats();
        }
    }

    public async Task<IEnumerable<RecentlyWatched>> GetRecentlyWatchedAsync(int limit = 10)
    {
        if (!this.IsDatabaseConfigured)
        {
            this.logger.LogWarning("Plex database not configured or not found");
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
            this.logger.LogError(ex, "Error querying recently watched");
            return [];
        }
    }
}
