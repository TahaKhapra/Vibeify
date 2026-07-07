using System.IO;
using VibeMP.Data;
using VibeMP.Services.BpmAnalysis;

namespace VibeMP.Services
{
    public class LibraryManager
    {
        private readonly BpmAnalyzer _analyzer = new();
        private readonly MetadataService _metadataService = new();
        private readonly string[] _supportedExtensions = { ".mp3", ".flac", ".wav", ".m4a" };

        public LibraryManager()
        {
            using var db = new LibraryContext();
            db.Database.EnsureCreated();
        }

        public async Task ImportFilesAsync(
            IEnumerable<string> filePaths,
            Action<string, int, int> progressCallback
        )
        {
            var pathsList = filePaths.ToList();
            int total = pathsList.Count;
            int current = 0;

            await Task.Run(async () =>
            {
                using var db = new LibraryContext();

                var categories = db.Categories.ToList();

                foreach (var path in pathsList)
                {
                    current++;
                    progressCallback?.Invoke(path, current, total);

                    try
                    {
                        if (db.Tracks.Any(t => t.FilePath == path))
                        {
                            System.Diagnostics.Debug.WriteLine(
                                $"[SKIP] Already in DB: {Path.GetFileName(path)}"
                            );
                            continue;
                        }

                        var newTrack = await _metadataService.GetTrackMetadataAsync(path);

                        float calculatedBpm = _analyzer.Analyze(path);
                        int? assignedCategoryId = categories
                            .OrderBy(c => Math.Abs(calculatedBpm - c.TargetBpm))
                            .FirstOrDefault()
                            ?.Id;

                        newTrack.Bpm = calculatedBpm;
                        newTrack.CategoryId = assignedCategoryId;

                        db.Tracks.Add(newTrack);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[ANALYZE ERROR] {ex.Message}");
                    }
                }

                try
                {
                    db.SaveChanges();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[ERROR]: {ex.Message}");
                }
            });
        }

        public async Task ImportPathsAsync(
            IEnumerable<string> paths,
            Action<string, int, int>? progressCallback = null
        )
        {
            var files = new List<string>();

            foreach (var path in paths)
            {
                string fileName = Path.GetFileName(path);

                if (fileName.StartsWith("._"))
                    continue;

                if (File.Exists(path))
                {
                    string ext = Path.GetExtension(path).ToLower();
                    if (_supportedExtensions.Contains(ext))
                    {
                        files.Add(path);
                    }
                }
                else if (Directory.Exists(path))
                {
                    var audioFiles = Directory
                        .EnumerateFiles(path, "*.*", SearchOption.AllDirectories)
                        .Where(f =>
                            _supportedExtensions.Contains(Path.GetExtension(f).ToLower())
                            && !Path.GetFileName(f).StartsWith("._")
                        );

                    files.AddRange(audioFiles);
                }
            }

            await ImportFilesAsync(files, progressCallback!);
        }
    }
}
