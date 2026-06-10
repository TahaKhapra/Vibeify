using System;
using System.IO;
using System.Threading.Tasks;
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

                var track = new Track
                {
                    FilePath = filePath,
                    DateAnalyzed = DateTime.Now
                };

                try
                {
                    using (var tFile = TagLib.File.Create(filePath))
                    {
                        track.Title = !string.IsNullOrEmpty(tFile.Tag.Title)
                                      ? tFile.Tag.Title
                                      : Path.GetFileNameWithoutExtension(filePath);
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error parsing metadata: {ex.Message}");
                    track.Title = Path.GetFileNameWithoutExtension(filePath);
                }

                return track;
            });
        }
    }
}