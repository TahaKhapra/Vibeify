using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using VibeMP.Audio;
using VibeMP.Core.Interfaces;
using VibeMP.Data;
using VibeMP.Models;
using VibeMP.Services;

namespace VibeMP.ViewModels
{
    public partial class PendingItem : ObservableObject
    {
        public string DisplayName { get; set; } = string.Empty;
        public string FullPath { get; set; } = string.Empty;
        public bool IsFolder { get; set; }

        [ObservableProperty]
        private int _progressPercent = 0;
    }

    public partial class MainViewModel : ObservableObject
    {
        #region Private Fields
        private readonly ILibraryRepository _repository = new LibraryRepository();
        private readonly IAudioEngine _audioEngine = new NAudioEngine();
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

        // Custom validation error binding to feed user notification text blocks safely
        [ObservableProperty]
        private string? _validationErrorMessage;
        #endregion

        #region Media Player & Volume Properties
        [ObservableProperty]
        private double _playbackVolume = 0.75;

        [ObservableProperty]
        private bool _isShuffleEnabled;

        [ObservableProperty]
        private bool _isRepeatEnabled;

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
            _audioEngine.Volume = 0.75f;
            _audioEngine.PlaybackFinished += (s, e) => PlayNextTrack();
            LoadCategoriesFromDb();

            _playbackTimer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(500),
            };
            _playbackTimer.Tick += PlaybackTimer_Tick;
            _playbackTimer.Start();

            if (_repository.HasTracks())
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

            if (
                Application.Current.MainWindow is Views.MainWindow mainWindow
                && mainWindow.IsDraggingProgress
            )
                return;

            TimeSpan totalDuration = _audioEngine.TotalTime;
            PlaybackDurationSeconds =
                totalDuration.TotalSeconds > 0 ? totalDuration.TotalSeconds : 1;
            PlaybackDurationString = string.Format(
                "{0}:{1:D2}",
                (int)totalDuration.TotalMinutes,
                totalDuration.Seconds
            );

            double currentEnginePosition = _audioEngine.CurrentTime.TotalSeconds;
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
            _audioEngine.CurrentTime = TimeSpan.FromSeconds(seconds);

            PlaybackProgressSeconds = seconds;
            PlaybackPositionString = string.Format(
                "{0}:{1:D2}",
                (int)(seconds / 60),
                (int)(seconds % 60)
            );
        }

        partial void OnPlaybackVolumeChanged(double value)
        {
            if (_audioEngine != null)
            {
                _audioEngine.Volume = (float)value;
            }
        }
        #endregion

        #region Database Data Hydration Operations
        public void LoadDashboardRows()
        {
            var dbCategories = _repository.GetAllCategories();

            Categories.Clear();
            foreach (var cat in dbCategories)
            {
                cat.CategoryTracks.Clear();
                Categories.Add(cat);
            }

            if (Categories.Count == 0)
                return;

            var allTracks = _repository.GetAllTracks();
            foreach (var track in allTracks)
            {
                var closestCategory = Categories
                    .OrderBy(c => Math.Abs(track.Bpm - c.TargetBpm))
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
            var dbCategories = _repository.GetAllCategories();

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
                        TargetBpm = 120,
                        IsPreset = true,
                    }
                );
                Categories.Add(
                    new VibeCategory
                    {
                        Name = "Studying",
                        TargetBpm = 80,
                        IsPreset = true,
                    }
                );
            }
        }

        private void SaveCategoriesToDb()
        {
            _repository.SaveCategories(Categories);
        }

        private void UpdateDashboardTracks()
        {
            DashboardTracks.Clear();
            if (SelectedCategory?.CategoryTracks != null)
            {
                foreach (var track in SelectedCategory.CategoryTracks)
                {
                    DashboardTracks.Add(track);
                }
            }
        }
        #endregion

        public void UpdateCategoryBpmDirectly(VibeCategory category, string text)
        {
            if (category == null) return;

            if (!int.TryParse(text, out int parsedBpm) || parsedBpm < 30 || parsedBpm > 250)
            {
                category.RowErrorMessage = "Please type a valid number between 30 and 250.";
            }
            else
            {
                category.RowErrorMessage = null;
                category.TargetBpm = parsedBpm;
            }

            CompleteOnboardingCommand?.NotifyCanExecuteChanged();
            CloseSettingsCommand?.NotifyCanExecuteChanged();
        }

        #region Core Music Import Asynchronous Pipelines
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
                            new PendingItem
                            {
                                FullPath = p,
                                DisplayName = Path.GetFileName(p),
                                IsFolder = false,
                            }
                        );
                    }
                    else if (
                        Directory.Exists(p) && !PendingImportPaths.Any(item => item.FullPath == p)
                    )
                    {
                        PendingImportPaths.Add(
                            new PendingItem
                            {
                                FullPath = p,
                                DisplayName = $"{Path.GetFileName(p)}",
                                IsFolder = true,
                            }
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

                            var matchingUiItem = PendingImportPaths.FirstOrDefault(item =>
                                filePath.StartsWith(
                                    item.FullPath,
                                    StringComparison.OrdinalIgnoreCase
                                )
                            );

                            if (matchingUiItem != null && matchingUiItem.IsFolder)
                            {
                                matchingUiItem.ProgressPercent = (int)(
                                    ((double)current / total) * 100
                                );
                            }
                        });
                    },
                    (filePath, trackPercentage) =>
                    {
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            var matchingUiItem = PendingImportPaths.FirstOrDefault(item =>
                                item.FullPath == filePath
                            );

                            if (matchingUiItem != null && !matchingUiItem.IsFolder)
                            {
                                matchingUiItem.ProgressPercent = trackPercentage;
                            }
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

                if (_activeImportCount == 0 && PendingImportPaths.Count > 0)
                {
                    WeakReferenceMessenger.Default.Send(
                        new NotificationToast
                        {
                            Title = "Track(s) Analyzed",
                            Message = "All tracks have been successfully analyzed.",
                        }
                    );
                }

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

        private bool CanCloseSettings()
        {
            return Categories.Count >= 2 && !Categories.Any(c => !string.IsNullOrEmpty(c.RowErrorMessage));
        }

        [RelayCommand(CanExecute = nameof(CanCloseSettings))]
        private async Task CloseSettingsAsync()
        {
            if (Categories.Count < 2)
            {
                WeakReferenceMessenger.Default.Send(
                    new NotificationToast
                    {
                        Title = "Save Failed",
                        Message = "You must have at least 2 categories.",
                    }
                );
                return;
            }

            IsProcessing = true;

            var dbCategories = _repository.GetAllCategories();
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
                    || dbMatch.TargetBpm != uiCategory.TargetBpm
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

            bool hasZeroBpmAnomalies = false;

            await Task.Run(() =>
            {
                var originalDbCategories = _repository.GetAllCategories();
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
                        if (dbMatch != null && dbMatch.TargetBpm != uiCategory.TargetBpm)
                        {
                            requiresReassignment = true;
                        }
                    }
                }

                SaveCategoriesToDb();

                if (requiresReassignment)
                {
                    var updatedCategories = _repository.GetAllCategories();
                    var allTracks = _repository.GetAllTracks();

                    foreach (var track in allTracks)
                    {
                        var closestCategory = updatedCategories
                            .OrderBy(c => Math.Abs(track.Bpm - c.TargetBpm))
                            .FirstOrDefault();

                        track.CategoryId = closestCategory?.Id;
                    }
                    _repository.UpdateTrackCategoryIds(allTracks);
                }

                var zeroTracks = _repository.GetAllTracks().Where(t => t.Bpm <= 0).ToList();
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
        private bool CanCompleteOnboarding()
        {
            return Categories.Count >= 2
                   && !Categories.Any(c => !string.IsNullOrEmpty(c.RowErrorMessage))
                   && PendingImportPaths.Count > 0;
        }

        [RelayCommand(CanExecute = nameof(CanCompleteOnboarding))]
        private void CompleteOnboarding()
        {
            if (Categories.Count < 2)
            {
                WeakReferenceMessenger.Default.Send(
                    new NotificationToast
                    {
                        Title = "Setup Incomplete",
                        Message = "You must have at least 2 categories to continue.",
                    }
                );
                return;
            }

            if (!CanCompleteOnboarding())
            {
                WeakReferenceMessenger.Default.Send(new NotificationToast
                {
                    Title = "Setup Incomplete",
                    Message = "Please fix the invalid BPM values before continuing."
                });
                return;
            }

            SaveCategoriesToDb();

            var allTracks = _repository.GetAllTracks();
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
            var allTracks = _repository.GetAllTracks();
            var dbCategories = _repository.GetAllCategories();

            foreach (var uiTrack in ZeroBpmTracks)
            {
                var dbTrack = allTracks.FirstOrDefault(t => t.FilePath == uiTrack.FilePath);
                if (dbTrack != null)
                {
                    dbTrack.Bpm = uiTrack.Bpm;
                    dbTrack.CategoryId = dbCategories
                        .OrderBy(c => Math.Abs(uiTrack.Bpm - c.TargetBpm))
                        .FirstOrDefault()
                        ?.Id;
                }
            }

            _repository.UpdateTrackCategoryIds(allTracks);
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
                    TargetBpm = 100,
                    IsPreset = false,
                }
            );
        }

        [RelayCommand]
        private void DeleteCategory(VibeCategory category)
        {
            if (category == null)
                return;

            _repository.DeleteCategory(category.Id);
            Categories.Remove(category);

            if (SelectedCategory?.Id == category.Id)
            {
                SelectHome();
            }
            LoadDashboardRows();
        }

        [RelayCommand]
        private void IncrementBpm(VibeCategory category)
        {
            if (category != null)
            {
                category.TargetBpm++;
                int index = Categories.IndexOf(category);
                Categories[index] = category;
                UpdateDashboardTracks();
            }
        }

        [RelayCommand]
        private void DecrementBpm(VibeCategory category)
        {
            if (category != null && category.TargetBpm > 1)
            {
                category.TargetBpm--;
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
                _audioEngine.Load(SelectedTrack.FilePath);
                _audioEngine.Play();
                IsPlaying = true;
                _lastReportedEnginePosition = 0;
                PlaybackProgressSeconds = 0;
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
                _audioEngine.Play();
            }
            else
            {
                _audioEngine.Pause();
            }
        }

        [RelayCommand]
        private void ToggleShuffle()
        {
            IsShuffleEnabled = !IsShuffleEnabled;
        }

        [RelayCommand]
        private void ToggleRepeat()
        {
            IsRepeatEnabled = !IsRepeatEnabled;
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

            if (IsShuffleEnabled && tracks.Count > 1)
            {
                int nextIndex;
                var random = new Random();
                do
                {
                    nextIndex = random.Next(tracks.Count);
                } while (nextIndex == currentIndex);

                PlaySpecificTrack(tracks[nextIndex]);
                return;
            }

            if (currentIndex >= 0 && currentIndex < tracks.Count - 1)
            {
                PlaySpecificTrack(tracks[currentIndex + 1]);
            }
            else
            {
                if (IsRepeatEnabled && tracks.Count > 0)
                {
                    PlaySpecificTrack(tracks[0]);
                }
                else
                {
                    IsPlaying = false;
                    _audioEngine.Stop();
                    PlaybackProgressSeconds = 0;
                    _lastReportedEnginePosition = 0;
                }
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
                _audioEngine.CurrentTime = TimeSpan.Zero;
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

        [RelayCommand]
        private async Task AddFolderAsync()
        {
            try
            {
                var dialog = new Microsoft.Win32.OpenFolderDialog
                {
                    Title = "Select Music Folder",
                    Multiselect = false,
                };

                if (dialog.ShowDialog(Application.Current.MainWindow) == true)
                {
                    string cleanPath = dialog.FolderName.TrimEnd(
                        Path.DirectorySeparatorChar,
                        Path.AltDirectorySeparatorChar
                    );

                    await ImportPathsAsync(new[] { cleanPath });
                    HasPendingImports = PendingImportPaths.Count > 0;
                }
            }
            catch (Exception ex)
            {
                StatusText = $"Folder dialog error: {ex.Message}";
            }
        }
        #endregion
    }
}
