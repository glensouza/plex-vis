namespace PlexVis.Web.Models;

/// <summary>
/// Represents a TV episode that has duplicate video files.
/// </summary>
public class DuplicateEpisode
{
    public int MetadataItemId { get; set; }
    public string ShowTitle { get; set; } = string.Empty;
    public int SeasonNumber { get; set; }
    public int EpisodeNumber { get; set; }
    public string EpisodeTitle { get; set; } = string.Empty;
    public int FileCount { get; set; }
    public List<MediaFileDetail> Files { get; set; } = [];

    /// <summary>
    /// Gets a formatted episode identifier (e.g., "S01E05").
    /// </summary>
    public string EpisodeIdentifier => $"S{this.SeasonNumber:D2}E{this.EpisodeNumber:D2}";
}
