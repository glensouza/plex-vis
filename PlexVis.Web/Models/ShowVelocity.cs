namespace PlexVis.Web.Models;

public class ShowVelocity
{
    public string ShowTitle { get; set; } = string.Empty;
    public int SeasonNum { get; set; }
    public int EpisodeNum { get; set; }
    public string EpisodeTitle { get; set; } = string.Empty;
    public double AvgDaysToWatch { get; set; }

    // Velocity labels based on design system
    public string VelocityLabel => this.AvgDaysToWatch switch
    {
        <= 2 => "⚡ Fast",
        <= 7 => "🐢 Steady",
        <= 30 => "🕸️ Stale",
        _ => "💀 Archived"
    };

    // CSS class for velocity badge styling
    public string VelocityClass => this.AvgDaysToWatch switch
    {
        <= 2 => "badge-fast",
        <= 7 => "badge-steady",
        <= 30 => "badge-stale",
        _ => "badge-archived"
    };

    // CSS class for velocity card left border
    public string CardClass => this.AvgDaysToWatch switch
    {
        <= 2 => "velocity-fast",
        <= 7 => "velocity-steady",
        <= 30 => "velocity-stale",
        _ => "velocity-archived"
    };
}
