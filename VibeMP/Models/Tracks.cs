using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.IO;
using System.Windows.Media.Imaging;
using VibeMP.Core.Interfaces;

namespace VibeMP.Models
{
    public class Track : ITrackMetadata
    {
        [Key]
        public int Id { get; set; }
        public string FilePath { get; set; } = string.Empty;
        public string? Title { get; set; }
        public string? Artist { get; set; }
        public string? Album { get; set; }
        public double Bpm { get; set; }
        public int? CategoryId { get; set; }
        public byte[]? AlbumArtBytes { get; set; }
        public DateTime DateAnalyzed { get; set; }

        [NotMapped]
        public TimeSpan Duration { get; set; } = TimeSpan.Zero;

        [NotMapped]
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
