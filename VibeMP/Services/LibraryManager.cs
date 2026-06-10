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

        public async Task ImportFilesAsync(
            IEnumerable<string> filePaths,
            Action<string, int, int>? onProgress = null
        )
        {
            var audioFiles = filePaths
                .Where(f =>
                    _supportedExtensions.Contains(
                        Path.GetExtension(f),
                        StringComparer.OrdinalIgnoreCase
                    )
                )
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

                        if (await db.Tracks.AnyAsync(t => t.FilePath == file))
                            continue;

                        float bpm = _analyzer.Analyze(file);

                        var track = new Track
                        {
                            FilePath = file,
                            Title = Path.GetFileNameWithoutExtension(file),
                            Bpm = bpm,
                            DateAnalyzed = DateTime.Now,
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

        public async Task ImportPathsAsync(IEnumerable<string> paths)
        {
            var files = new List<string>();

            foreach (var path in paths)
            {
                if (File.Exists(path))
                {
                    files.Add(path);
                }
                else if (Directory.Exists(path))
                {
                    files.AddRange(
                        Directory.EnumerateFiles(path, "*.*", SearchOption.AllDirectories)
                    );
                }
            }

            await ImportFilesAsync(files);
        }
    }
}
