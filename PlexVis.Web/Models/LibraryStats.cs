namespace PlexVis.Web.Models;

public class LibraryStats
{
    public int TotalMovies { get; set; }
    public int TotalShows { get; set; }
    public int TotalEpisodes { get; set; }
    public int WatchedMovies { get; set; }
    public int WatchedEpisodes { get; set; }
    public double TotalSizeGB { get; set; }
}
