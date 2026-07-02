using System.IO;
using System.Windows.Media.Imaging;

namespace VibeMP.Models
{
    public class Track
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Artist { get; set; } = "Unknown Artist";
        public double Bpm { get; set; }
        public string FilePath { get; set; } = string.Empty;
        public int? CategoryId { get; set; }
        public byte[]? AlbumArtBytes { get; set; }

        public BitmapImage? AlbumArt
        {
            get
            {
                if (AlbumArtBytes == null || AlbumArtBytes.Length == 0)
                    return null;

                try
                {
                    var bitmap = new BitmapImage();
                    using (var stream = new MemoryStream(AlbumArtBytes))
                    {
                        stream.Position = 0;
                        bitmap.BeginInit();

                        bitmap.CacheOption = BitmapCacheOption.OnLoad;

                        bitmap.StreamSource = stream;
                        bitmap.EndInit();
                    }
                    bitmap.Freeze();
                    return bitmap;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Image load error: {ex.Message}");
                    return null;
                }
            }
        }
    }
}
