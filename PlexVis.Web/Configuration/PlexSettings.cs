namespace PlexVis.Web.Configuration;

public class PlexSettings
{
    /// <summary>
    /// Direct path to a specific Plex database file.
    /// If set, this takes precedence over DatabaseDirectory.
    /// </summary>
    public string DatabasePath { get; set; } = string.Empty;

    /// <summary>
    /// Directory containing Plex database files.
    /// When set, the service will automatically discover and use the latest backup database file.
    /// Backup files are expected to follow Plex's naming convention: com.plexapp.plugins.library.db-YYYY-MM-DD
    /// </summary>
    public string DatabaseDirectory { get; set; } = string.Empty;

    public string ServerUrl { get; set; } = string.Empty;
    public string Token { get; set; } = string.Empty;
}
