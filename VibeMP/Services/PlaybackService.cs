using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using VibeMP.Core.Interfaces;
using VibeMP.Models;

namespace VibeMP.Services
{
    public partial class PlaybackService : ObservableObject
    {
        private readonly IAudioEngine _audioEngine;
        private readonly ILibraryRepository _repository;
        private readonly DispatcherTimer _playbackTimer;
        private double _lastReportedEnginePosition = 0;
        private DateTime _lastSeekTime = DateTime.MinValue;

        public PlaybackService(IAudioEngine audioEngine, ILibraryRepository repository)
        {
            _audioEngine = audioEngine;
            _repository = repository;
            _audioEngine.Volume = 0.75f;
            _audioEngine.PlaybackFinished += (s, e) => PlayNextTrack();

            _playbackTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
            _playbackTimer.Tick += PlaybackTimer_Tick;
            _playbackTimer.Start();
        }

        [ObservableProperty]
        private Track? _selectedTrack;

        [ObservableProperty]
        private bool _isPlaying;

        [ObservableProperty]
        private bool _isShuffleEnabled;

        [ObservableProperty]
        private bool _isRepeatEnabled;

        [ObservableProperty]
        private double _playbackProgressSeconds;

        [ObservableProperty]
        private double _playbackDurationSeconds = 1;

        [ObservableProperty]
        private string? _playbackPositionString;

        [ObservableProperty]
        private string? _playbackDurationString;

        public double Volume
        {
            get => _audioEngine.Volume;
            set
            {
                _audioEngine.Volume = (float)value;
                OnPropertyChanged();
            }
        }

        public void PlaySpecificTrack(Track track, Action onTrackChanged)
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
                onTrackChanged?.Invoke();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Playback failed: {ex.Message}");
                IsPlaying = false;
            }
        }

        public void TogglePlayback()
        {
            if (SelectedTrack == null || string.IsNullOrEmpty(SelectedTrack.FilePath))
                return;
            IsPlaying = !IsPlaying;
            if (IsPlaying)
                _audioEngine.Play();
            else
                _audioEngine.Pause();
        }

        public void ToggleShuffle()
        {
            IsShuffleEnabled = !IsShuffleEnabled;
        }

        public void ToggleRepeat()
        {
            IsRepeatEnabled = !IsRepeatEnabled;
        }

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

        public void PlayNextTrack()
        {
            if (SelectedTrack == null)
                return;
            var categories = _repository.GetAllCategories();
            var allTracks = _repository.GetAllTracks();

            foreach (var t in allTracks)
            {
                var match = categories.OrderBy(c => Math.Abs(t.Bpm - c.TargetBpm)).FirstOrDefault();
                match?.CategoryTracks.Add(t);
            }

            var currentCategory = categories.FirstOrDefault(c =>
                c.CategoryTracks.Any(ct => ct.FilePath == SelectedTrack.FilePath)
            );
            if (currentCategory == null)
                return;

            var tracks = currentCategory.CategoryTracks;

            int currentIndex =
                tracks
                    .Select((track, index) => new { track, index })
                    .FirstOrDefault(x => x.track.FilePath == SelectedTrack.FilePath)
                    ?.index
                ?? -1;

            if (IsShuffleEnabled && tracks.Count > 1)
            {
                int nextIndex;
                var random = new Random();
                do
                {
                    nextIndex = random.Next(tracks.Count);
                } while (nextIndex == currentIndex);
                PlaySpecificTrack(tracks[nextIndex], null!);
                return;
            }

            if (currentIndex >= 0 && currentIndex < tracks.Count - 1)
            {
                PlaySpecificTrack(tracks[currentIndex + 1], null!);
            }
            else if (IsRepeatEnabled && tracks.Count > 0)
            {
                PlaySpecificTrack(tracks[0], null!);
            }
            else
            {
                IsPlaying = false;
                _audioEngine.Stop();
                PlaybackProgressSeconds = 0;
                _lastReportedEnginePosition = 0;
            }
        }

        public void PlayPreviousTrack()
        {
            if (SelectedTrack == null)
                return;
            var categories = _repository.GetAllCategories();
            var allTracks = _repository.GetAllTracks();
            foreach (var t in allTracks)
                categories
                    .OrderBy(c => Math.Abs(t.Bpm - c.TargetBpm))
                    .FirstOrDefault()
                    ?.CategoryTracks.Add(t);

            var currentCategory = categories.FirstOrDefault(c =>
                c.CategoryTracks.Any(ct => ct.FilePath == SelectedTrack.FilePath)
            );
            if (currentCategory == null)
                return;

            var tracks = currentCategory.CategoryTracks;

            int currentIndex =
                tracks
                    .Select((track, index) => new { track, index })
                    .FirstOrDefault(x => x.track.FilePath == SelectedTrack.FilePath)
                    ?.index
                ?? -1;

            if (currentIndex > 0)
                PlaySpecificTrack(tracks[currentIndex - 1], null!);
            else
            {
                _audioEngine.CurrentTime = TimeSpan.Zero;
                PlaybackProgressSeconds = 0;
                _lastReportedEnginePosition = 0;
            }
        }

        private void PlaybackTimer_Tick(object? sender, EventArgs e)
        {
            if (SelectedTrack == null || !IsPlaying)
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
                PlaybackProgressSeconds += delta;
            if (PlaybackProgressSeconds > PlaybackDurationSeconds)
                PlaybackProgressSeconds = PlaybackDurationSeconds;

            PlaybackPositionString = string.Format(
                "{0}:{1:D2}",
                (int)(PlaybackProgressSeconds / 60),
                (int)(PlaybackProgressSeconds % 60)
            );
        }
    }
}
