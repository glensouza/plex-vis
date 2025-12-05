namespace PlexVis.Web.Models;

/// <summary>
/// Represents a movie that has duplicate video files.
/// </summary>
public class DuplicateMovie
{
    public string Title { get; set; } = string.Empty;
    public int FileCount { get; set; }
}
