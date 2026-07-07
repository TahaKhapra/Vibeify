using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using VibeMP.Audio;
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
        private readonly LibraryManager _libraryManager;
        #endregion

        #region Services Sub-Controllers
        [ObservableProperty]
        private PlaybackService _playback;

        [ObservableProperty]
        private MusicImportService _importer;

        [ObservableProperty]
        private LibrarySetupService _setupEngine;

        [ObservableProperty]
        private LibraryDisplayService _displayEngine;

        [ObservableProperty]
        private NavigationService _navigation;
        #endregion

        #region UI State Properties
        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(CompleteOnboardingCommand))]
        [NotifyCanExecuteChangedFor(nameof(CloseSettingsCommand))]
        private bool _isProcessing;

        [ObservableProperty]
        private string _statusText = "";
        #endregion

        #region Progress & Tracking Properties
        [ObservableProperty]
        private int _scanProgressValue = 0;

        [ObservableProperty]
        private int _scanProgressMax = 100;

        [ObservableProperty]
        private bool _hasPendingImports;

        [ObservableProperty]
        private string? _validationErrorMessage;
        #endregion

        #region Collections & Selection Properties
        public ObservableCollection<PendingItem> PendingImportPaths => Importer.PendingImportPaths;
        public ObservableCollection<Track> ZeroBpmTracks => SetupEngine.ZeroBpmTracks;
        public ObservableCollection<VibeCategory> Categories => DisplayEngine.Categories;
        public ObservableCollection<Track> DashboardTracks => DisplayEngine.DashboardTracks;

        public AppViewState CurrentViewState => Navigation.CurrentViewState;
        public bool IsHomeSelected => Navigation.IsHomeSelected;

        [ObservableProperty]
        private VibeCategory? _selectedCategory;
        #endregion

        #region Constructor & Initialization
        public MainViewModel()
        {
            var repository = new LibraryRepository();

            _libraryManager = new LibraryManager(repository);
            Playback = new PlaybackService(new NAudioEngine(), repository);
            Importer = new MusicImportService(_libraryManager);
            SetupEngine = new LibrarySetupService(_libraryManager);
            DisplayEngine = new LibraryDisplayService(_libraryManager);
            Navigation = new NavigationService();
            Navigation.PropertyChanged += (s, e) => OnPropertyChanged(e.PropertyName);

            DisplayEngine.LoadInitialCategories();

            if (_libraryManager.HasTracks())
            {
                LoadDashboardRows();
                Navigation.NavigateTo(AppViewState.Dashboard);
            }
            else
            {
                Navigation.NavigateTo(AppViewState.Onboarding);
            }
        }
        #endregion

        #region Property Changed Event Handlers
        partial void OnSelectedCategoryChanged(VibeCategory? value)
        {
            if (value != null)
            {
                Navigation.IsHomeSelected = false;
            }
            DisplayEngine.SyncDashboardTracks(value);
        }
        #endregion

        #region Pass-through Media Player Control APIs
        public void SeekToPosition(double seconds) => Playback.SeekToPosition(seconds);
        #endregion

        #region Database Data Hydration Operations
        public void LoadDashboardRows()
        {
            DisplayEngine.HydrateDashboardRows(
                SelectedCategory,
                out bool isHome,
                out var updatedSelection
            );
            Navigation.IsHomeSelected = isHome;

            // Sync selection context back without triggering recurring loop cycles
            if (SelectedCategory != updatedSelection)
            {
                _selectedCategory = updatedSelection;
                OnPropertyChanged(nameof(SelectedCategory));
            }
        }

        private void SaveCategoriesToDb()
        {
            _libraryManager.SaveCategories(Categories);
        }
        #endregion

        public void UpdateCategoryBpmDirectly(VibeCategory category, string text)
        {
            if (category == null)
                return;

            if (!int.TryParse(text, out int parsedBpm) || parsedBpm < 30 || parsedBpm > 250)
            {
                category.RowErrorMessage = "Please type a valid number between 30 and 250.";
            }
            else
            {
                category.RowErrorMessage = null;
                category.TargetBpm = parsedBpm;
            }

            OnboardingAndSettingsCanExecuteRefresh();
        }

        #region Core Music Import Asynchronous Pipelines
        public async Task ImportPathsAsync(IEnumerable<string> paths)
        {
            IsProcessing = true;
            StatusText = "Initializing analysis...";

            await Importer.ImportPathsAsync(
                paths,
                () =>
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        if (CurrentViewState == AppViewState.Dashboard)
                        {
                            LoadDashboardRows();
                        }
                    });
                }
            );

            IsProcessing = Importer.IsProcessing;
            StatusText = Importer.StatusText;
            HasPendingImports = Importer.HasPendingImports;
            ScanProgressMax = Importer.ScanProgressMax;
            ScanProgressValue = Importer.ScanProgressValue;

            OnboardingAndSettingsCanExecuteRefresh();
        }
        #endregion

        #region Navigation Relay Commands
        [RelayCommand]
        private void SelectHome()
        {
            SelectedCategory = null;
            Navigation.IsHomeSelected = true;
        }

        [RelayCommand]
        private void SelectCategory(VibeCategory category)
        {
            if (category == null)
                return;

            SelectedCategory = category;
            Navigation.IsHomeSelected = false;
            DisplayEngine.SyncDashboardTracks(category);
        }

        [RelayCommand]
        private async Task OpenSettings()
        {
            SelectedCategory = null;
            Navigation.NavigateTo(AppViewState.Settings);
            StatusText = "Manage your music library configurations.";
            await Task.CompletedTask;
        }

        private bool CanCloseSettings() => Navigation.EvaluateSettingsClosureAllowed(Categories);

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

            bool structuralChangesMade = SetupEngine.ReviewSettingsChanges(Categories);

            if (!structuralChangesMade && PendingImportPaths.Count == 0)
            {
                IsProcessing = false;
                Navigation.IsHomeSelected = true;
                Navigation.NavigateTo(AppViewState.Dashboard);
                return;
            }

            bool hasZeroBpmAnomalies = false;

            await Task.Run(() =>
            {
                var originalDbCategories = _libraryManager.GetAllCategories();
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
                    var updatedCategories = _libraryManager.GetAllCategories();
                    var allTracks = _libraryManager.GetAllTracks();

                    foreach (var track in allTracks)
                    {
                        var closestCategory = updatedCategories
                            .OrderBy(c => Math.Abs(track.Bpm - c.TargetBpm))
                            .FirstOrDefault();

                        track.CategoryId = closestCategory?.Id;
                    }
                    _libraryManager.UpdateTrackCategoryIds(allTracks);
                }

                string outMsg;
                hasZeroBpmAnomalies = SetupEngine.CheckOnboardingTracks(Categories, out outMsg);
            });

            LoadDashboardRows();
            IsProcessing = false;

            if (hasZeroBpmAnomalies)
            {
                Navigation.NavigateTo(AppViewState.BpmFixer);
            }
            else
            {
                PendingImportPaths.Clear();
                Navigation.IsHomeSelected = true;
                Navigation.NavigateTo(AppViewState.Dashboard);
            }
        }
        #endregion

        #region Library Setup Relay Commands
        private bool CanCompleteOnboarding() =>
            Navigation.EvaluateOnboardingAllowed(Categories, PendingImportPaths.Count);

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
                WeakReferenceMessenger.Default.Send(
                    new NotificationToast
                    {
                        Title = "Setup Incomplete",
                        Message = "Please fix the invalid BPM values before continuing.",
                    }
                );
                return;
            }

            var allTracks = _libraryManager.GetAllTracks();
            if (allTracks.Count == 0)
            {
                StatusText = "Please connect or drop a music library folder first!";
                return;
            }

            string onboardingMessage;
            bool hasZeroBpmAnomalies = SetupEngine.CheckOnboardingTracks(
                Categories,
                out onboardingMessage
            );

            if (hasZeroBpmAnomalies)
            {
                Navigation.NavigateTo(AppViewState.BpmFixer);
                StatusText = onboardingMessage;
            }
            else
            {
                LoadDashboardRows();
                Navigation.NavigateTo(AppViewState.Dashboard);
            }
        }

        private void OnboardingAndSettingsCanExecuteRefresh()
        {
            Application.Current?.Dispatcher?.Invoke(() =>
            {
                CompleteOnboardingCommand?.NotifyCanExecuteChanged();
                CloseSettingsCommand?.NotifyCanExecuteChanged();
            });
        }

        [RelayCommand]
        private void SaveManualBpms()
        {
            SetupEngine.SaveCorrectedBpms();

            LoadDashboardRows();
            Navigation.NavigateTo(AppViewState.Dashboard);
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

            OnboardingAndSettingsCanExecuteRefresh();
        }

        [RelayCommand]
        private void DeleteCategory(VibeCategory category)
        {
            if (category == null)
                return;

            _libraryManager.DeleteCategory(category.Id);
            Categories.Remove(category);

            if (SelectedCategory?.Id == category.Id)
            {
                SelectHome();
            }
            LoadDashboardRows();

            OnboardingAndSettingsCanExecuteRefresh();
        }

        [RelayCommand]
        private void IncrementBpm(VibeCategory category)
        {
            if (category != null)
            {
                category.TargetBpm++;
                int index = Categories.IndexOf(category);
                Categories[index] = category;
                LoadDashboardRows();
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
                LoadDashboardRows();
            }
        }
        #endregion

        #region Media Player Audio Service Bridging Commands
        [RelayCommand]
        private void PlaySpecificTrack(Track track) =>
            Playback.PlaySpecificTrack(track, () => OnPropertyChanged(nameof(Playback)));

        [RelayCommand]
        private void TogglePlayback() => Playback.TogglePlayback();

        [RelayCommand]
        private void ToggleShuffle() => Playback.ToggleShuffle();

        [RelayCommand]
        private void ToggleRepeat() => Playback.ToggleRepeat();

        [RelayCommand]
        private void PlayNextTrack() => Playback.PlayNextTrack();

        [RelayCommand]
        private void PlayPreviousTrack() => Playback.PlayPreviousTrack();

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
                HasPendingImports = Importer.HasPendingImports;
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
                    HasPendingImports = Importer.HasPendingImports;
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
