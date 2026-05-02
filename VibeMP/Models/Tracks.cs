namespace VibeMP.Models
{
    public class Track
    {
        public string FilePath { get; set; }
        public string FileName { get; set; }
        public string Title { get; set; }
        public string? Artist { get; set; }
        public string? Album { get; set; }
        public TimeSpan Duration { get; set; }

        // more portable and lighter mem usage than System.Windows.Media
        public byte[]? AlbumArt { get; set; }

        public float? Bpm { get; set; }

        public Track(string filePath)
        {
            FilePath = filePath;
            FileName = System.IO.Path.GetFileName(filePath);
            Title = FileName;
        }
    }
}