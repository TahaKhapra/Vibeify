using System.IO;
using Microsoft.EntityFrameworkCore;
using VibeMP.Data;
using VibeMP.Models;
using VibeMP.Services.BpmAnalysis;

namespace VibeMP.Services
{
    public class LibraryManager
    {
        private readonly BpmAnalyzer _analyzer = new();
        private readonly string[] _supportedExtensions = { ".mp3", ".flac", ".wav", ".m4a" };

        public LibraryManager()
        {
            using var db = new LibraryContext();
            db.Database.EnsureCreated();
        }

        public async Task ImportFolderAsync(string folderPath, Action<string, int, int>? onProgress = null)
        {
            if (!Directory.Exists(folderPath)) return;

            var allFiles = Directory.GetFiles(folderPath, "*.*", SearchOption.AllDirectories);
            var audioFiles = allFiles
                .Where(f => _supportedExtensions.Contains(Path.GetExtension(f).ToLower()))
                .ToList();

            int totalFiles = audioFiles.Count;
            int processedCount = 0;

            await Task.Run(async () =>
            {
                foreach (var file in audioFiles)
                {
                    processedCount++;

                    onProgress?.Invoke(Path.GetFileName(file), processedCount, totalFiles);

                    try
                    {
                        using var db = new LibraryContext();

                        if (await db.Tracks.AnyAsync(t => t.FilePath == file)) continue;

                        float bpm = _analyzer.Analyze(file);

                        var track = new Track
                        {
                            FilePath = file,
                            Title = Path.GetFileNameWithoutExtension(file),
                            Bpm = bpm,
                            DateAnalyzed = DateTime.Now
                        };

                        db.Tracks.Add(track);
                        await db.SaveChangesAsync();
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error analyzing {file}: {ex.Message}");
                    }
                }
            });
        }
    }
}