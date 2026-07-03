using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using VibeMP.Data;
using VibeMP.Models;
using VibeMP.Services;
using VibeMP.Views;

namespace VibeMP.ViewModels
{
    public class PendingItem
    {
        public string DisplayName { get; set; } = string.Empty;
        public string FullPath { get; set; } = string.Empty;
    }

    public partial class MainViewModel : ObservableObject
    {
        #region Private Fields
        private readonly LibraryManager _libraryManager = new();
        private readonly System.Windows.Threading.DispatcherTimer _playbackTimer;
        private int _activeImportCount = 0;
        #endregion

        #region UI State Properties
        [ObservableProperty]
        private AppViewState _currentViewState = AppViewState.Onboarding;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(CompleteOnboardingCommand))]
        [NotifyCanExecuteChangedFor(nameof(CloseSettingsCommand))]
        private bool _isProcessing;

        [ObservableProperty]
        private string _statusText = "";

        [ObservableProperty]
        private bool _isHomeSelected = true;
        #endregion

        #region Progress & Tracking Properties
        [ObservableProperty]
        private int _scanProgressValue = 0;

        [ObservableProperty]
        private int _scanProgressMax = 100;

        [ObservableProperty]
        private bool _hasPendingImports;
        #endregion

        #region Media Player & Volume Properties
        private System.Windows.Media.MediaPlayer _mediaPlayer = new();

        [ObservableProperty]
        private bool _isPlaying;

        [ObservableProperty]
        private double _playbackProgressSeconds = 0;

        [ObservableProperty]
        private double _playbackDurationSeconds = 1;

        [ObservableProperty]
        private string? _playbackPositionString = null;

        [ObservableProperty]
        private string? _playbackDurationString = null;

        // Variables to neutralize the WPF FLAC 8-second jumping bug
        private DateTime _lastSeekTime = DateTime.MinValue;
        private double _lastReportedEnginePosition = 0;
        #endregion

        #region Collections & Selection Properties
        public ObservableCollection<PendingItem> PendingImportPaths { get; } = new();
        public ObservableCollection<Track> ZeroBpmTracks { get; } = new();
        public ObservableCollection<VibeCategory> Categories { get; } = new();
        public ObservableCollection<Track> DashboardTracks { get; } = new();

        [ObservableProperty]
        private VibeCategory? _selectedCategory;

        [ObservableProperty]
        private Track? _selectedTrack;
        #endregion

        #region Constructor & Initialization
        public MainViewModel()
        {
            _mediaPlayer.MediaEnded += (s, e) => PlayNextTrack();
            LoadCategoriesFromDb();

            _playbackTimer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(500),
            };
            _playbackTimer.Tick += PlaybackTimer_Tick;
            _playbackTimer.Start();

            using var db = new LibraryContext();
            bool hasExistingLibrary = db.Tracks.Any();

            if (hasExistingLibrary)
            {
                LoadDashboardRows();
                CurrentViewState = AppViewState.Dashboard;
            }
            else
            {
                CurrentViewState = AppViewState.Onboarding;
            }
        }
        #endregion

        #region Property Changed Event Handlers
        partial void OnSelectedCategoryChanged(VibeCategory? value)
        {
            if (value != null)
            {
                IsHomeSelected = false;
            }
            UpdateDashboardTracks();
        }
        #endregion

        #region Background Playback Timer Tick
        private void PlaybackTimer_Tick(object? sender, EventArgs e)
        {
            if (SelectedTrack == null || !IsPlaying || Application.Current.MainWindow == null)
                return;

            if (Application.Current.MainWindow is MainWindow mainWindow && mainWindow.IsDraggingProgress)
                return;

            if (_mediaPlayer.NaturalDuration.HasTimeSpan)
            {
                PlaybackDurationSeconds = _mediaPlayer.NaturalDuration.TimeSpan.TotalSeconds;
                var duration = _mediaPlayer.NaturalDuration.TimeSpan;
                PlaybackDurationString = string.Format(
                    "{0}:{1:D2}",
                    (int)duration.TotalMinutes,
                    duration.Seconds
                );
            }

            double currentEnginePosition = _mediaPlayer.Position.TotalSeconds;
            double delta = currentEnginePosition - _lastReportedEnginePosition;
            _lastReportedEnginePosition = currentEnginePosition;

            if ((DateTime.Now - _lastSeekTime).TotalMilliseconds < 800)
                return;

            if (delta > 0 && delta < 3.0)
            {
                PlaybackProgressSeconds += delta;
            }

            if (PlaybackProgressSeconds > PlaybackDurationSeconds)
                PlaybackProgressSeconds = PlaybackDurationSeconds;

            PlaybackPositionString = string.Format(
                "{0}:{1:D2}",
                (int)(PlaybackProgressSeconds / 60),
                (int)(PlaybackProgressSeconds % 60)
            );
        }
        #endregion

        #region Public Media Player Control Audio APIs
        public void SeekToPosition(double seconds)
        {
            if (SelectedTrack == null)
                return;

            _lastSeekTime = DateTime.Now;

            _mediaPlayer.Position = TimeSpan.FromSeconds(seconds);

            PlaybackProgressSeconds = seconds;
            PlaybackPositionString = string.Format(
                "{0}:{1:D2}",
                (int)(seconds / 60),
                (int)(seconds % 60)
            );
        }
        #endregion

        #region Database Data Hydration Operations
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
        #endregion

        #region Core Import and Orchestration Async Pipelines
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
        #endregion

        #region Navigation Relay Commands
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
        private async Task OpenSettings()
        {
            SelectedCategory = null;
            IsHomeSelected = false;
            CurrentViewState = AppViewState.Settings;
            StatusText = "Manage your music library configurations.";
            await Task.CompletedTask;
        }

        private bool CanCloseSettings() => !IsProcessing;

        [RelayCommand(CanExecute = nameof(CanCloseSettings))]
        private async Task CloseSettingsAsync()
        {
            IsProcessing = true;

            using (var dbCheck = new LibraryContext())
            {
                var dbCategories = dbCheck.Categories.ToList();
                bool structuralChangesMade = false;

                foreach (var uiCategory in Categories)
                {
                    if (uiCategory.Id == 0)
                    {
                        structuralChangesMade = true;
                        break;
                    }

                    var dbMatch = dbCategories.FirstOrDefault(c => c.Id == uiCategory.Id);
                    if (
                        dbMatch == null
                        || dbMatch.Name != uiCategory.Name
                        || dbMatch.MaxBpm != uiCategory.MaxBpm
                    )
                    {
                        structuralChangesMade = true;
                        break;
                    }
                }

                if (!structuralChangesMade && PendingImportPaths.Count == 0)
                {
                    IsProcessing = false;
                    IsHomeSelected = true;
                    CurrentViewState = AppViewState.Dashboard;
                    return;
                }
            }

            bool hasZeroBpmAnomalies = false;

            await Task.Run(() =>
            {
                using var db = new LibraryContext();
                var originalDbCategories = db.Categories.ToList();
                bool requiresReassignment = false;

                foreach (var uiCategory in Categories)
                {
                    if (uiCategory.Id == 0)
                    {
                        requiresReassignment = true;
                    }
                    else
                    {
                        var dbMatch = originalDbCategories.FirstOrDefault(c =>
                            c.Id == uiCategory.Id
                        );
                        if (dbMatch != null && dbMatch.MaxBpm != uiCategory.MaxBpm)
                        {
                            requiresReassignment = true;
                        }
                    }
                }

                SaveCategoriesToDb();

                if (requiresReassignment)
                {
                    var updatedCategories = db.Categories.ToList();
                    var allTracks = db.Tracks.ToList();

                    foreach (var track in allTracks)
                    {
                        var closestCategory = updatedCategories
                            .OrderBy(c => Math.Abs(track.Bpm - c.MaxBpm))
                            .FirstOrDefault();

                        track.CategoryId = closestCategory?.Id;
                    }
                }

                db.SaveChanges();

                var zeroTracks = db.Tracks.Where(t => t.Bpm <= 0).ToList();
                if (zeroTracks.Count > 0)
                {
                    hasZeroBpmAnomalies = true;
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        ZeroBpmTracks.Clear();
                        foreach (var track in zeroTracks)
                        {
                            track.Bpm = 80;
                            ZeroBpmTracks.Add(track);
                        }
                    });
                }
            });

            LoadDashboardRows();
            IsProcessing = false;

            if (hasZeroBpmAnomalies)
            {
                CurrentViewState = AppViewState.BpmFixer;
            }
            else
            {
                PendingImportPaths.Clear();
                IsHomeSelected = true;
                CurrentViewState = AppViewState.Dashboard;
            }
        }
        #endregion

        #region Library Setup & Triage Relay Commands
        private bool CanCompleteOnboarding() => !IsProcessing && PendingImportPaths.Count > 0;

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
        #endregion

        #region Music Category Management Relay Commands
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
        #endregion

        #region Media Player Audio Control Relay Commands
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

                PlaybackProgressSeconds = 0;
                _lastReportedEnginePosition = 0;

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
                PlaybackProgressSeconds = 0;
                _lastReportedEnginePosition = 0;
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
                PlaybackProgressSeconds = 0;
                _lastReportedEnginePosition = 0;
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
        #endregion
    }
}