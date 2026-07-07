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
    /// <summary>
    /// Ein einfacher Helfer, um den Namen und den Pfad von importierten Liedern kurz zu speichern.
    /// </summary>
    public class PendingItem
    {
        public string DisplayName { get; set; } = string.Empty;
        public string FullPath { get; set; } = string.Empty;
    }

    /// <summary>
    /// Der Haupt-Manager der Anwendung. Steuert das Laden der Daten, den Musik-Import,
    /// den aktuellen Zustand der UI und die Steuerung des Media-Players.
    /// </summary>
    public partial class MainViewModel : ObservableObject
    {
        #region Private Felder
        private readonly LibraryManager _libraryManager = new();
        private readonly System.Windows.Threading.DispatcherTimer _playbackTimer;
        private int _activeImportCount = 0;
        #endregion

        #region UI Zustand Eigenschaften
        /// <summary>
        /// Bestimmt, welcher Bildschirm (Onboarding, Fixer oder Dashboard) gerade angezeigt wird.
        /// </summary>
        [ObservableProperty]
        private AppViewState _currentViewState = AppViewState.Onboarding;

        /// <summary>
        /// Zeigt an, ob die App im Hintergrund arbeitet. Schaltet automatisch Buttons in der UI an/aus.
        /// </summary>
        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(CompleteOnboardingCommand))]
        [NotifyCanExecuteChangedFor(nameof(CloseSettingsCommand))]
        private bool _isProcessing;

        [ObservableProperty]
        private string _statusText = "";

        [ObservableProperty]
        private bool _isHomeSelected = true;
        #endregion

        #region Fortschritts-Eigenschaften
        [ObservableProperty]
        private int _scanProgressValue = 0;

        [ObservableProperty]
        private int _scanProgressMax = 100;

        [ObservableProperty]
        private bool _hasPendingImports;
        #endregion

        #region Media Player & Lautstärke Eigenschaften
        private readonly System.Windows.Media.MediaPlayer _mediaPlayer = new();

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

        #region Listen & Selektionen
        public ObservableCollection<PendingItem> PendingImportPaths { get; } = new();
        public ObservableCollection<Track> ZeroBpmTracks { get; } = new();
        public ObservableCollection<VibeCategory> Categories { get; } = new();
        public ObservableCollection<Track> DashboardTracks { get; } = new();

        [ObservableProperty]
        private VibeCategory? _selectedCategory;

        [ObservableProperty]
        private Track? _selectedTrack;
        #endregion

        #region Konstruktor & Initialisierung
        public MainViewModel()
        {
            _mediaPlayer.Volume = 0.75;
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

        #region Event Handler für Eigenschaftsänderungen
        partial void OnSelectedCategoryChanged(VibeCategory? value)
        {
            if (value != null)
            {
                IsHomeSelected = false;
            }
            UpdateDashboardTracks();
        }
        #endregion

        #region Hintergrund-Timer für die Musikwiedergabe
        /// <summary>
        /// Aktualisiert jede halbe Sekunde die Zeitleiste des Liedes im UI.
        /// Verhindert Ruckler und falsche Sprünge beim Vor- oder Zurückspulen.
        /// </summary>
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
                PlaybackDurationString = string.Format("{0}:{1:D2}", (int)duration.TotalMinutes, duration.Seconds);
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

        #region Media Player Audio APIs
        /// <summary>
        /// Spult das aktuelle Lied an die vom Nutzer gewählte Sekunde.
        /// </summary>
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

        /// <summary>
        /// Wird automatisch aufgerufen, wenn der Nutzer den Lautstärkeregler verschiebt.
        /// </summary>
        partial void OnPlaybackVolumeChanged(double value)
        {
            if (_mediaPlayer != null)
            {
                _mediaPlayer.Volume = value;
            }
        }
        #endregion

        #region Datenbank-Operationen
        /// <summary>
        /// Lädt alle Kategorien und Lieder aus der SQLite-Datenbank.
        /// Berechnet per Abstandsmessung (Math.Abs), welches Lied am besten in welche BPM-Kategorie passt.
        /// </summary>
        public void LoadDashboardRows()
        {
            using var db = new LibraryContext();
            var dbCategories = db.Categories.ToList();

            Categories.Clear();
            foreach (var cat in dbCategories)
            {
                cat.CategoryTracks.Clear();
                Categories.Add(cat);
            }

            if (Categories.Count == 0)
                return;

            var allTracks = db.Tracks.ToList();
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
            using var db = new LibraryContext();
            var dbCategories = db.Categories.ToList();

            Categories.Clear();
            foreach (var category in dbCategories)
            {
                Categories.Add(category);
            }

            if (Categories.Count == 0)
            {
                Categories.Add(new VibeCategory { Name = "Gaming", TargetBpm = 120, IsPreset = true });
                Categories.Add(new VibeCategory { Name = "Studying", TargetBpm = 80, IsPreset = true });
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
                        dbCategory.TargetBpm = uiCategory.TargetBpm;
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
            if (SelectedCategory?.CategoryTracks != null)
            {
                foreach (var track in SelectedCategory.CategoryTracks)
                {
                    DashboardTracks.Add(track);
                }
            }
        }
        #endregion

        #region Musik-Import Pipelines (Asynchron)
        /// <summary>
        /// Durchsucht Ordner und Dateien im Hintergrund, analysiert die BPM und speichert sie ab.
        /// Blockiert dabei nicht die Benutzeroberfläche.
        /// </summary>
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
                        PendingImportPaths.Add(new PendingItem { FullPath = p, DisplayName = Path.GetFileName(p) });
                    }
                    else if (Directory.Exists(p) && !PendingImportPaths.Any(item => item.FullPath == p))
                    {
                        PendingImportPaths.Add(new PendingItem { FullPath = p, DisplayName = $"{Path.GetFileName(p)}" });
                    }
                }

                await _libraryManager.ImportPathsAsync(paths, (filePath, current, total) =>
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        string friendlyName = Path.GetFileName(filePath);
                        StatusText = $"Importing & Analyzing song {current} of {total}: {friendlyName}";
                        ScanProgressMax = total;
                        ScanProgressValue = current;
                    });
                });

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

        #region Navigation Befehle
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

        /// <summary>
        /// Schließt die Einstellungen. Wenn der Nutzer BPM-Regler verändert hat, 
        /// werden alle Lieder vollautomatisch im Hintergrund neu sortiert.
        /// </summary>
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
                    if (dbMatch == null || dbMatch.Name != uiCategory.Name || dbMatch.TargetBpm != uiCategory.TargetBpm)
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
                        var dbMatch = originalDbCategories.FirstOrDefault(c => c.Id == uiCategory.Id);
                        if (dbMatch != null && dbMatch.TargetBpm != uiCategory.TargetBpm)
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
                            .OrderBy(c => Math.Abs(track.Bpm - c.TargetBpm))
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

        #region Onboarding & BPM-Nachkorrektur Befehle
        private bool CanCompleteOnboarding() => !IsProcessing && PendingImportPaths.Count > 0;

        /// <summary>
        /// Schließt die Ersteinrichtung ab. Falls Lieder mit 0 BPM gefunden wurden, 
        /// wird der Nutzer zum BPM-Fixer geleitet, um Fehler manuell zu korrigieren.
        /// </summary>
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

        /// <summary>
        /// Speichert die manuell korrigierten BPM-Werte des Nutzers zurück in die Datenbank.
        /// </summary>
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
                        .OrderBy(c => Math.Abs(uiTrack.Bpm - c.TargetBpm))
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

        #region BPM Steuerungs-Befehle (+ / - Buttons)
        [RelayCommand]
        private void AddCategory()
        {
            Categories.Add(new VibeCategory { Name = "New Category", TargetBpm = 100, IsPreset = false });
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

        #region Media Player Audio Steuerung (Play, Pause, Shuffle, Repeat)
        /// <summary>
        /// Öffnet eine Musikdatei und startet die Wiedergabe über den Windows Media Player.
        /// </summary>
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
        private void ToggleShuffle()
        {
            IsShuffleEnabled = !IsShuffleEnabled;
        }

        [RelayCommand]
        private void ToggleRepeat()
        {
            IsRepeatEnabled = !IsRepeatEnabled;
        }

        /// <summary>
        /// Berechnet das nächste Lied innerhalb der aktuellen Kategorie. 
        /// Berücksichtigt den Zufallsmodus (Shuffle) und die Endlos-Schleife (Repeat).
        /// </summary>
        [RelayCommand]
        private void PlayNextTrack()
        {
            if (SelectedTrack == null)
                return;

            var currentCategory = Categories.FirstOrDefault(c => c.CategoryTracks.Contains(SelectedTrack));
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
                    _mediaPlayer.Stop();
                    _mediaPlayer.Position = TimeSpan.Zero;
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

            var currentCategory = Categories.FirstOrDefault(c => c.CategoryTracks.Contains(SelectedTrack));
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