namespace PlexVis.Web.Models;

/// <summary>
/// Represents a Plex user account from the database.
/// </summary>
public class PlexAccount
{
    /// <summary>
    /// Gets or sets the unique identifier for the account.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Gets or sets the display name of the account.
    /// </summary>
    public string Name { get; set; } = string.Empty;
}
