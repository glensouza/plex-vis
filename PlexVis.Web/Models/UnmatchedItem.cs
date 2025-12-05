namespace PlexVis.Web.Models;

/// <summary>
/// Represents a media item that Plex hasn't matched to an agent.
/// </summary>
public class UnmatchedItem
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public DateTime? AddedAt { get; set; }
}
