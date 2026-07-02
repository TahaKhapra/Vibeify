using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
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

        private int _activeImportCount = 0;

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

        public ObservableCollection<VibeCategory> Categories { get; } = new();

        public ObservableCollection<Track> DashboardTracks { get; } = new();

        [ObservableProperty]
        private VibeCategory? _selectedCategory;

        [ObservableProperty]
        private Track? _selectedTrack;

        private System.Windows.Media.MediaPlayer _mediaPlayer = new();

        [ObservableProperty]
        private bool _isPlaying;

        [ObservableProperty]
        private bool _isHomeSelected = true;

        partial void OnSelectedCategoryChanged(VibeCategory? value)
        {
            if (value != null)
            {
                IsHomeSelected = false;
            }
            UpdateDashboardTracks();
        }

        private bool CanCompleteOnboarding() => !IsProcessing && PendingImportPaths.Count > 0;

        public MainViewModel()
        {
            _mediaPlayer.MediaEnded += (s, e) => PlayNextTrack();
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
                var gaming = new VibeCategory
                {
                    Name = "Gaming",
                    MaxBpm = 120,
                    MinBpm = 0,
                    IsPreset = true,
                };
                var studying = new VibeCategory
                {
                    Name = "Studying",
                    MaxBpm = 80,
                    MinBpm = 0,
                    IsPreset = true,
                };
                Categories.Add(gaming);
                Categories.Add(studying);
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
                int index = Categories.IndexOf(category);
                Categories[index] = category;
                UpdateDashboardTracks();
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
                UpdateDashboardTracks();
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
                LoadDashboardRows();
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
                var dbTrack = db.Tracks.FirstOrDefault(t => t.FilePath == uiTrack.FilePath);
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

            LoadDashboardRows();
            CurrentViewState = AppViewState.Dashboard;
            StatusText = "Library successfully verified.";
        }

        [RelayCommand]
        private void SelectHome()
        {
            SelectedCategory = null;
            IsHomeSelected = true;
        }

        [RelayCommand]
        private void SelectCategory(VibeCategory category)
        {
            if (category == null)
                return;

            SelectedCategory = category;
            IsHomeSelected = false;
            UpdateDashboardTracks();
        }

        [RelayCommand]
        private void PlaySpecificTrack(Track track)
        {
            if (track == null || string.IsNullOrEmpty(track.FilePath))
                return;

            if (SelectedTrack != null && SelectedTrack.FilePath == track.FilePath)
            {
                TogglePlayback();
                return;
            }

            SelectedTrack = track;

            try
            {
                _mediaPlayer.Open(new Uri(SelectedTrack.FilePath));
                _mediaPlayer.Play();
                IsPlaying = true;
                System.Diagnostics.Debug.WriteLine($"Playing: {SelectedTrack.Title}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Playback failed: {ex.Message}");
                IsPlaying = false;
            }
        }

        [RelayCommand]
        private void TogglePlayback()
        {
            if (SelectedTrack == null || string.IsNullOrEmpty(SelectedTrack.FilePath))
                return;

            IsPlaying = !IsPlaying;

            if (IsPlaying)
            {
                _mediaPlayer.Play();
            }
            else
            {
                _mediaPlayer.Pause();
            }
        }

        [RelayCommand]
        private void PlayNextTrack()
        {
            if (SelectedTrack == null)
                return;

            var currentCategory = Categories.FirstOrDefault(c =>
                c.CategoryTracks.Contains(SelectedTrack)
            );
            if (currentCategory == null)
                return;

            var tracks = currentCategory.CategoryTracks;
            int currentIndex = tracks.IndexOf(SelectedTrack);

            if (currentIndex >= 0 && currentIndex < tracks.Count - 1)
            {
                PlaySpecificTrack(tracks[currentIndex + 1]);
            }
            else
            {
                IsPlaying = false;
                _mediaPlayer.Stop();
                _mediaPlayer.Position = TimeSpan.Zero;
            }
        }

        [RelayCommand]
        private void PlayPreviousTrack()
        {
            if (SelectedTrack == null)
                return;

            var currentCategory = Categories.FirstOrDefault(c =>
                c.CategoryTracks.Contains(SelectedTrack)
            );
            if (currentCategory == null)
                return;

            var tracks = currentCategory.CategoryTracks;
            int currentIndex = tracks.IndexOf(SelectedTrack);

            if (currentIndex > 0)
            {
                PlaySpecificTrack(tracks[currentIndex - 1]);
            }
            else
            {
                _mediaPlayer.Position = TimeSpan.Zero;
            }
        }

        [RelayCommand]
        private async Task AddFilesAsync()
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Multiselect = true,
                Title = "Select Music Files",
                Filter = "Audio Files|*.mp3;*.flac;*.wav;*.m4a",
            };

            if (dialog.ShowDialog() == true)
            {
                await ImportPathsAsync(dialog.FileNames);
                HasPendingImports = PendingImportPaths.Count > 0;
            }
        }

        public async Task ImportPathsAsync(IEnumerable<string> paths)
        {
            try
            {
                _activeImportCount++;
                IsProcessing = _activeImportCount > 0;

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

                if (CurrentViewState == AppViewState.Dashboard)
                {
                    LoadDashboardRows();
                }
            }
            catch (Exception ex)
            {
                StatusText = $"Import failed: {ex.Message}";
            }
            finally
            {
                _activeImportCount--;
                IsProcessing = _activeImportCount > 0;

                Application.Current?.Dispatcher?.Invoke(() =>
                {
                    CompleteOnboardingCommand?.NotifyCanExecuteChanged();
                });
            }
        }

        public void LoadDashboardRows()
        {
            using var db = new LibraryContext();
            var dbCategories = db.Categories.ToList();

            Categories.Clear();
            foreach (var cat in dbCategories)
            {
                if (cat.CategoryTracks != null)
                {
                    cat.CategoryTracks.Clear();
                }
                Categories.Add(cat);
            }

            if (Categories.Count == 0)
                return;

            var allTracks = db.Tracks.ToList();
            foreach (var track in allTracks)
            {
                var closestCategory = Categories
                    .OrderBy(c => Math.Abs(track.Bpm - c.MaxBpm))
                    .FirstOrDefault();

                closestCategory?.CategoryTracks.Add(track);
            }

            if (SelectedCategory == null)
            {
                IsHomeSelected = true;
            }
            else
            {
                SelectedCategory = Categories.FirstOrDefault(c => c.Id == SelectedCategory.Id);
                IsHomeSelected = false;
            }

            UpdateDashboardTracks();
        }

        private void UpdateDashboardTracks()
        {
            DashboardTracks.Clear();
            if (SelectedCategory != null && SelectedCategory.CategoryTracks != null)
            {
                foreach (var track in SelectedCategory.CategoryTracks)
                {
                    DashboardTracks.Add(track);
                }
            }
        }
    }
}
