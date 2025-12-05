namespace PlexVis.Web.Models;

/// <summary>
/// Represents a movie that has duplicate video files.
/// </summary>
public class DuplicateMovie
{
    public int MetadataItemId { get; set; }
    public string Title { get; set; } = string.Empty;
    public int Year { get; set; }
    public int FileCount { get; set; }
    public List<MediaFileDetail> Files { get; set; } = [];
}
