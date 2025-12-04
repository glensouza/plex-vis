namespace PlexVis.Web.Models;

public class RecentlyWatched
{
    public string Title { get; set; } = string.Empty;
    public int ViewCount { get; set; }
    public DateTime? LastWatched { get; set; }
    public int MetadataType { get; set; }

    public string TypeLabel => this.MetadataType switch
    {
        1 => "Movie",
        4 => "Episode",
        _ => "Media"
    };

    public string TypeIcon => this.MetadataType switch
    {
        1 => "🎬",
        4 => "📺",
        _ => "🎵"
    };
}
