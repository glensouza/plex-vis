namespace PlexVis.Web.Models;

/// <summary>
/// Represents an item marked as soft-deleted in the Plex database.
/// </summary>
public class SoftDeletedItem
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public int MetadataType { get; set; }
    public DateTime? DeletedAt { get; set; }

    /// <summary>
    /// Returns a human-readable label for the metadata type.
    /// </summary>
    public string TypeLabel => this.MetadataType switch
    {
        1 => "Movie",
        2 => "Show",
        3 => "Season",
        4 => "Episode",
        8 => "Artist",
        9 => "Album",
        10 => "Track",
        _ => "Unknown"
    };
}
