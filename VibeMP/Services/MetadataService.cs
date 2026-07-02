using System.IO;
using VibeMP.Models;

namespace VibeMP.Services
{
    public class MetadataService
    {
        public async Task<Track> GetTrackMetadataAsync(string filePath)
        {
            return await Task.Run(() =>
            {
                if (!File.Exists(filePath))
                    throw new FileNotFoundException("Audio file not found", filePath);

                var track = new Track { FilePath = filePath };

                try
                {
                    using (var tFile = TagLib.File.Create(filePath))
                    {
                        track.Title = !string.IsNullOrEmpty(tFile.Tag.Title)
                            ? tFile.Tag.Title
                            : Path.GetFileNameWithoutExtension(filePath);

                        track.Artist = !string.IsNullOrEmpty(tFile.Tag.FirstPerformer)
                            ? tFile.Tag.FirstPerformer
                            : (
                                !string.IsNullOrEmpty(tFile.Tag.FirstAlbumArtist)
                                    ? tFile.Tag.FirstAlbumArtist
                                    : "Unknown Artist"
                            );

                        if (tFile.Tag.Pictures != null && tFile.Tag.Pictures.Length > 0)
                        {
                            var pic =
                                tFile.Tag.Pictures.FirstOrDefault(p =>
                                    p.Type == TagLib.PictureType.FrontCover
                                ) ?? tFile.Tag.Pictures.FirstOrDefault();

                            if (pic != null && pic.Data != null && pic.Data.Count > 1000)
                            {
                                track.AlbumArtBytes = pic.Data.ToArray();
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine(
                        $"Error parsing metadata for {filePath}: {ex.Message}"
                    );
                    track.Title = Path.GetFileNameWithoutExtension(filePath);
                    track.Artist = "Unknown Artist";
                }

                return track;
            });
        }
    }
}
