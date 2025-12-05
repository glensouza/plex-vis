namespace PlexVis.Web.Models;

/// <summary>
/// Represents a movie that has duplicate video files.
/// </summary>
public class DuplicateMovie
{
    /// <summary>
    /// Gets or sets the unique identifier for the movie's metadata item.
    /// </summary>
    public int MetadataItemId { get; set; }

    /// <summary>
    /// Gets or sets the title of the movie.
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the release year of the movie.
    /// </summary>
    public int Year { get; set; }

    /// <summary>
    /// Gets or sets the number of duplicate video files found for the movie.
    /// </summary>
    public int FileCount { get; set; }

    /// <summary>
    /// Gets or sets the list of details for each duplicate media file.
    /// </summary>
    public List<MediaFileDetail> Files { get; set; } = [];
}
