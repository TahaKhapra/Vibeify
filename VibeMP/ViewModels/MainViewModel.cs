using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VibeMP.Data;
using VibeMP.Models;
using VibeMP.Services;

namespace VibeMP.ViewModels
{
    public class PendingItem
    {
        public string DisplayName { get; set; } = string.Empty;
        public string FullPath { get; set; } = string.Empty;
    }

    public partial class MainViewModel : ObservableObject
    {
        private readonly LibraryManager _libraryManager = new();

        [ObservableProperty]
        private AppViewState _currentViewState = AppViewState.Onboarding;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(CompleteOnboardingCommand))]
        private bool _isProcessing;

        [ObservableProperty]
        private string _statusText = "";

        [ObservableProperty]
        private int _scanProgressValue = 0;

        [ObservableProperty]
        private int _scanProgressMax = 100;

        [ObservableProperty]
        private bool _hasPendingImports;

        public ObservableCollection<PendingItem> PendingImportPaths { get; } = new();
        public ObservableCollection<Track> ZeroBpmTracks { get; } = new();

        // Dynamic collection for the UI list
        public ObservableCollection<VibeCategory> Categories { get; } = new();

        private bool CanCompleteOnboarding() => !IsProcessing && PendingImportPaths.Count > 0;

        public MainViewModel()
        {
            LoadCategoriesFromDb();
        }

        private void LoadCategoriesFromDb()
        {
            using var db = new LibraryContext();
            var dbCategories = db.Categories.ToList();

            Categories.Clear();
            foreach (var category in dbCategories)
            {
                Categories.Add(category);
            }

            if (Categories.Count == 0)
            {
                Categories.Add(
                    new VibeCategory
                    {
                        Name = "Gaming",
                        MaxBpm = 120,
                        MinBpm = 0,
                        IsPreset = true,
                    }
                );
                Categories.Add(
                    new VibeCategory
                    {
                        Name = "Studying",
                        MaxBpm = 80,
                        MinBpm = 0,
                        IsPreset = true,
                    }
                );
            }
        }

        private void SaveCategoriesToDb()
        {
            using var db = new LibraryContext();

            foreach (var uiCategory in Categories)
            {
                if (uiCategory.Id > 0)
                {
                    var dbCategory = db.Categories.Find(uiCategory.Id);
                    if (dbCategory != null)
                    {
                        dbCategory.Name = uiCategory.Name;
                        dbCategory.MaxBpm = uiCategory.MaxBpm;
                    }
                }
                else
                {
                    // Brand new category added via the plus button
                    db.Categories.Add(uiCategory);
                }
            }

            db.SaveChanges();
        }

        [RelayCommand]
        private void AddCategory()
        {
            Categories.Add(
                new VibeCategory
                {
                    Name = "New Category",
                    MaxBpm = 100,
                    MinBpm = 0,
                    IsPreset = false,
                }
            );
        }

        [RelayCommand]
        private void IncrementBpm(VibeCategory category)
        {
            if (category != null)
            {
                category.MaxBpm++;
                // Explicitly poke the collection to refresh UI targets
                int index = Categories.IndexOf(category);
                Categories[index] = category;
            }
        }

        [RelayCommand]
        private void DecrementBpm(VibeCategory category)
        {
            if (category != null && category.MaxBpm > 1)
            {
                category.MaxBpm--;
                int index = Categories.IndexOf(category);
                Categories[index] = category;
            }
        }

        [RelayCommand(CanExecute = nameof(CanCompleteOnboarding))]
        private void CompleteOnboarding()
        {
            SaveCategoriesToDb();

            using var db = new LibraryContext();
            var allTracks = db.Tracks.ToList();

            if (allTracks.Count == 0)
            {
                StatusText = "Please connect or drop a music library folder first!";
                return;
            }

            var zeroTracks = allTracks.Where(t => t.Bpm <= 0).ToList();

            if (zeroTracks.Count > 0)
            {
                ZeroBpmTracks.Clear();
                foreach (var track in zeroTracks)
                {
                    track.Bpm = 80;
                    ZeroBpmTracks.Add(track);
                }
                CurrentViewState = AppViewState.BpmFixer;
                StatusText = $"{zeroTracks.Count} tracks defaulted to 80 BPM. Adjust if needed!";
            }
            else
            {
                CurrentViewState = AppViewState.Dashboard;
            }
        }

        [RelayCommand]
        private void SaveManualBpms()
        {
            using var db = new LibraryContext();
            var dbCategories = db.Categories.ToList();

            foreach (var uiTrack in ZeroBpmTracks)
            {
                var dbTrack = db.Tracks.Find(uiTrack.FilePath);
                if (dbTrack != null)
                {
                    dbTrack.Bpm = uiTrack.Bpm;

                    dbTrack.CategoryId = dbCategories
                        .OrderBy(c => Math.Abs(uiTrack.Bpm - c.MaxBpm))
                        .FirstOrDefault()
                        ?.Id;
                }
            }

            db.SaveChanges();

            CurrentViewState = AppViewState.Dashboard;
            StatusText = "Library successfully verified.";
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

        public async Task ImportPathsAsync(IEnumerable<string> paths)
        {
            try
            {
                IsProcessing = true;
                ScanProgressValue = 0;

                foreach (var p in paths)
                {
                    if (Path.GetFileName(p).StartsWith("._"))
                        continue;

                    if (File.Exists(p) && !PendingImportPaths.Any(item => item.FullPath == p))
                    {
                        PendingImportPaths.Add(
                            new PendingItem { FullPath = p, DisplayName = Path.GetFileName(p) }
                        );
                    }
                    else if (
                        Directory.Exists(p) && !PendingImportPaths.Any(item => item.FullPath == p)
                    )
                    {
                        PendingImportPaths.Add(
                            new PendingItem { FullPath = p, DisplayName = $"{Path.GetFileName(p)}" }
                        );
                    }
                }

                await _libraryManager.ImportPathsAsync(
                    paths,
                    (filePath, current, total) =>
                    {
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            string friendlyName = Path.GetFileName(filePath);
                            StatusText =
                                $"Importing & Analyzing song {current} of {total}: {friendlyName}";
                            ScanProgressMax = total;
                            ScanProgressValue = current;
                        });
                    }
                );

                HasPendingImports = PendingImportPaths.Count > 0;
                StatusText = "All songs successfully added and analyzed.";
            }
            catch (Exception ex)
            {
                StatusText = $"Import failed: {ex.Message}";
            }
            finally
            {
                try
                {
                    IsProcessing = false;
                    Application.Current?.Dispatcher?.Invoke(() =>
                    {
                        CompleteOnboardingCommand?.NotifyCanExecuteChanged();
                    });
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[UI DISPATCHER CRASH]: {ex.Message}");
                }
            }
        }
    }
}
