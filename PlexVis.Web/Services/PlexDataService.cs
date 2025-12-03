using Dapper;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;
using PlexVis.Web.Configuration;
using PlexVis.Web.Models;

namespace PlexVis.Web.Services;

public class PlexDataService
{
    private readonly PlexSettings _settings;
    private readonly ILogger<PlexDataService> _logger;

    public PlexDataService(IOptions<PlexSettings> settings, ILogger<PlexDataService> logger)
    {
        _settings = settings.Value;
        _logger = logger;
    }

    public bool IsDatabaseConfigured => !string.IsNullOrEmpty(_settings.DatabasePath) && File.Exists(_settings.DatabasePath);

    private SqliteConnection CreateConnection()
    {
        SqliteConnectionStringBuilder builder = new()
        {
            DataSource = _settings.DatabasePath,
            Mode = SqliteOpenMode.ReadOnly
        };
        return new SqliteConnection(builder.ConnectionString);
    }

    public async Task<IEnumerable<ShowVelocity>> GetViewingVelocityAsync()
    {
        if (!IsDatabaseConfigured)
        {
            _logger.LogWarning("Plex database not configured or not found");
            return [];
        }

        const string sql = """
            WITH 
            ShowVelocity AS (
                SELECT 
                    show.id AS ShowID,
                    show.title AS ShowTitle,
                    AVG(settings.last_viewed_at - strftime('%s', episode.originally_available_at)) AS AvgLagSeconds
                FROM metadata_items episode
                JOIN metadata_items season ON episode.parent_id = season.id
                JOIN metadata_items show ON season.parent_id = show.id
                JOIN metadata_item_settings settings ON episode.guid = settings.guid
                WHERE episode.metadata_type = 4
                  AND settings.view_count > 0
                  AND episode.originally_available_at IS NOT NULL
                  AND settings.last_viewed_at IS NOT NULL
                  AND settings.last_viewed_at >= strftime('%s', episode.originally_available_at)
                GROUP BY show.id
            ),
            NextEpisodes AS (
                SELECT 
                    show.id AS ShowID,
                    show.title AS ShowTitle,
                    season.index AS SeasonNum,
                    episode.index AS EpisodeNum,
                    episode.title AS EpisodeTitle,
                    MIN(season.index * 1000 + episode.index) as GlobalIndex
                FROM metadata_items episode
                JOIN metadata_items season ON episode.parent_id = season.id
                JOIN metadata_items show ON season.parent_id = show.id
                LEFT JOIN metadata_item_settings settings ON episode.guid = settings.guid
                WHERE episode.metadata_type = 4
                  AND (settings.view_count IS NULL OR settings.view_count = 0)
                  AND episode.originally_available_at IS NOT NULL
                GROUP BY show.id
            )
            SELECT 
                v.ShowTitle,
                n.SeasonNum,
                n.EpisodeNum,
                n.EpisodeTitle,
                ROUND(v.AvgLagSeconds / 86400.0, 1) AS AvgDaysToWatch
            FROM ShowVelocity v
            JOIN NextEpisodes n ON v.ShowID = n.ShowID
            ORDER BY AvgDaysToWatch ASC
            LIMIT 20;
            """;

        try
        {
            using SqliteConnection connection = CreateConnection();
            IEnumerable<ShowVelocity> results = await connection.QueryAsync<ShowVelocity>(sql);
            return results;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error querying viewing velocity");
            return [];
        }
    }

    public async Task<LibraryStats> GetLibraryStatsAsync()
    {
        if (!IsDatabaseConfigured)
        {
            _logger.LogWarning("Plex database not configured or not found");
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
            using SqliteConnection connection = CreateConnection();
            LibraryStats? stats = await connection.QuerySingleOrDefaultAsync<LibraryStats>(sql);
            return stats ?? new LibraryStats();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error querying library stats");
            return new LibraryStats();
        }
    }

    public async Task<IEnumerable<RecentlyWatched>> GetRecentlyWatchedAsync(int limit = 10)
    {
        if (!IsDatabaseConfigured)
        {
            _logger.LogWarning("Plex database not configured or not found");
            return [];
        }

        string sql = $"""
            SELECT 
                m.title AS Title, 
                s.view_count AS ViewCount, 
                datetime(s.last_viewed_at, 'unixepoch', 'localtime') as LastWatched,
                m.metadata_type AS MetadataType
            FROM metadata_items m
            JOIN metadata_item_settings s ON m.guid = s.guid
            WHERE s.view_count > 0
            ORDER BY s.last_viewed_at DESC
            LIMIT {limit};
            """;

        try
        {
            using SqliteConnection connection = CreateConnection();
            IEnumerable<RecentlyWatched> results = await connection.QueryAsync<RecentlyWatched>(sql);
            return results;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error querying recently watched");
            return [];
        }
    }
}
