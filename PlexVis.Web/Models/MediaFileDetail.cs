namespace PlexVis.Web.Models;

/// <summary>
/// Represents details about a media file version.
/// </summary>
public class MediaFileDetail
{
    public string FilePath { get; set; } = string.Empty;
    public double SizeGb { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }

    /// <summary>
    /// Gets a human-readable resolution label (e.g., "4K", "1080p", "720p").
    /// </summary>
    public string ResolutionLabel
    {
        get
        {
            if (this.Width >= 3840 || this.Height >= 2160)
            {
                return "4K";
            }
            if (this.Width >= 1920 || this.Height >= 1080)
            {
                return "1080p";
            }
            if (this.Width >= 1280 || this.Height >= 720)
            {
                return "720p";
            }
            if (this.Width >= 720 || this.Height >= 480)
            {
                return "480p";
            }
            return $"{this.Width}x{this.Height}";
        }
    }

    /// <summary>
    /// Gets whether this file is considered 4K resolution.
    /// </summary>
    public bool Is4K => this.Width >= 3840 || this.Height >= 2160;
}
