using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VibeMP.Data;
using VibeMP.Services;

namespace VibeMP.ViewModels
{
    // A clean wrapper so the UI can display just the title while keeping the file path underneath
    public class PendingItem
    {
        public string DisplayName { get; set; } = string.Empty;
        public string FullPath { get; set; } = string.Empty;
    }

    public partial class MainViewModel : ObservableObject
    {
        private readonly LibraryManager _libraryManager = new();

        private int _gamingCategoryId = -1;
        private int _studyingCategoryId = -1;

        [ObservableProperty]
        private string _gamingName = "Gaming";

        [ObservableProperty]
        private string _studyingName = "Studying";

        [ObservableProperty]
        private int _gamingBpm = 120; // Fallback

        [ObservableProperty]
        private int _studyingBpm = 80; // Fallback

        [ObservableProperty]
        private bool _isOnboarded = false;

        [ObservableProperty]
        private string _statusText = "";

        [ObservableProperty]
        private int _scanProgressValue = 0;

        [ObservableProperty]
        private int _scanProgressMax = 100;

        public ObservableCollection<PendingItem> PendingImportPaths { get; } = new();

        [ObservableProperty]
        private bool _hasPendingImports;

        public MainViewModel()
        {
            LoadCategoriesFromDb();
        }

        private void LoadCategoriesFromDb()
        {
            using var db = new LibraryContext();

            var gaming = db.Categories.FirstOrDefault(c => c.Name == "Gaming");
            var studying = db.Categories.FirstOrDefault(c => c.Name == "Studying");

            if (gaming != null)
            {
                _gamingCategoryId = gaming.Id;
                GamingName = gaming.Name;
                GamingBpm = (int)gaming.MaxBpm;
            }

            if (studying != null)
            {
                _studyingCategoryId = studying.Id;
                StudyingName = studying.Name;
                StudyingBpm = (int)studying.MaxBpm;
            }
        }

        private void SaveCategoriesToDb()
        {
            using var db = new LibraryContext();

            if (_gamingCategoryId > 0)
            {
                var gaming = db.Categories.Find(_gamingCategoryId);
                if (gaming != null)
                {
                    gaming.Name = GamingName;
                    gaming.MaxBpm = GamingBpm;
                }
            }

            if (_studyingCategoryId > 0)
            {
                var studying = db.Categories.Find(_studyingCategoryId);
                if (studying != null)
                {
                    studying.Name = StudyingName;
                    studying.MaxBpm = StudyingBpm;
                }
            }

            db.SaveChanges();
        }

        [RelayCommand]
        private void CompleteOnboarding()
        {
            SaveCategoriesToDb();
            IsOnboarded = true;
        }

        [RelayCommand]
        private void IncrementGamingBpm() => GamingBpm++;

        [RelayCommand]
        private void DecrementGamingBpm()
        {
            if (GamingBpm > 1)
                GamingBpm--;
        }

        [RelayCommand]
        private void IncrementStudyingBpm() => StudyingBpm++;

        [RelayCommand]
        private void DecrementStudyingBpm()
        {
            if (StudyingBpm > 1)
                StudyingBpm--;
        }

        [RelayCommand]
        private void AddFiles()
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Multiselect = true,
                Title = "Select Music Files",
                Filter = "Audio Files|*.mp3;*.flac;*.wav;*.m4a",
            };

            if (dialog.ShowDialog() == true)
            {
                foreach (var file in dialog.FileNames)
                {
                    if (!PendingImportPaths.Any(p => p.FullPath == file))
                    {
                        PendingImportPaths.Add(
                            new PendingItem
                            {
                                FullPath = file,
                                DisplayName = Path.GetFileName(file),
                            }
                        );
                    }
                }

                HasPendingImports = PendingImportPaths.Count > 0;
            }
        }

        public async Task ExecuteFileImportAsync(IEnumerable<string> filePaths)
        {
            ScanProgressValue = 0;

            await _libraryManager.ImportFilesAsync(
                filePaths,
                (filePath, current, total) =>
                {
                    string friendlyName = Path.GetFileName(filePath);
                    StatusText = $"Importing song {current} of {total}: {friendlyName}";
                    ScanProgressMax = total;
                    ScanProgressValue = current;
                }
            );

            StatusText = "Library up to date.";
        }

        public async Task ImportPathsAsync(IEnumerable<string> paths)
        {
            var discoveredFiles = new List<string>();

            foreach (var p in paths)
            {
                if (File.Exists(p))
                {
                    discoveredFiles.Add(p);
                    if (!PendingImportPaths.Any(item => item.FullPath == p))
                    {
                        PendingImportPaths.Add(
                            new PendingItem { FullPath = p, DisplayName = Path.GetFileName(p) }
                        );
                    }
                }
                else if (Directory.Exists(p))
                {
                    try
                    {
                        var directoryFiles = Directory
                            .EnumerateFiles(p, "*.*", SearchOption.AllDirectories)
                            .Where(f =>
                                new[] { ".mp3", ".flac", ".wav", ".m4a" }.Contains(
                                    Path.GetExtension(f).ToLower()
                                )
                            );

                        foreach (var file in directoryFiles)
                        {
                            discoveredFiles.Add(file);
                            if (!PendingImportPaths.Any(item => item.FullPath == file))
                            {
                                PendingImportPaths.Add(
                                    new PendingItem
                                    {
                                        FullPath = file,
                                        DisplayName = Path.GetFileName(file),
                                    }
                                );
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine(
                            $"Directory exploration error: {ex.Message}"
                        );
                    }
                }
            }

            HasPendingImports = PendingImportPaths.Count > 0;

            if (discoveredFiles.Count > 0)
            {
                ScanProgressValue = 0;
                await _libraryManager.ImportFilesAsync(
                    discoveredFiles,
                    (filePath, current, total) =>
                    {
                        string friendlyName = Path.GetFileName(filePath);
                        StatusText = $"Importing song {current} of {total}: {friendlyName}";
                        ScanProgressMax = total;
                        ScanProgressValue = current;
                    }
                );

                StatusText = "All songs successfully added.";
            }
        }
    }
}
