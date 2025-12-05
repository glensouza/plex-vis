namespace PlexVis.Web.Models;

/// <summary>
/// Represents a media item that Plex hasn't matched to an agent.
/// </summary>
public class UnmatchedItem
{
    /// <summary>
    /// Gets or sets the unique identifier for the item.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Gets or sets the title of the unmatched item.
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the date when the item was added to the library.
    /// </summary>
    public DateTime? AddedAt { get; set; }
}
