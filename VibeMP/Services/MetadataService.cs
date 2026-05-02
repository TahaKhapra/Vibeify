using System.IO;
using VibeMP.Core.Interfaces;
using VibeMP.Models;

namespace VibeMP.Services
{
    public class MetadataService : IMetadataService
    {
        public async Task<Track> GetTrackMetadataAsync(string filePath)
        {
            return await Task.Run(() =>
            {
                if (!File.Exists(filePath))
                    throw new FileNotFoundException("Audio file not found", filePath);

                var track = new Track(filePath);

                try
                {
                    using (var tFile = TagLib.File.Create(filePath))
                    {
                        track.Title = !string.IsNullOrEmpty(tFile.Tag.Title)
                                      ? tFile.Tag.Title
                                      : Path.GetFileNameWithoutExtension(filePath);

                        track.Artist = !string.IsNullOrEmpty(tFile.Tag.FirstPerformer)
                                       ? tFile.Tag.FirstPerformer
                                       : "Unknown Artist";

                        track.Album = tFile.Tag.Album ?? "Unknown Album";
                        track.Duration = tFile.Properties.Duration;

                        if (tFile.Tag.Pictures != null && tFile.Tag.Pictures.Length > 0)
                        {
                            var pic = tFile.Tag.Pictures[0];
                            track.AlbumArt = pic.Data.Data;
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error parsing metadata: {ex.Message}");
                }

                return track;
            });
        }
    }
}