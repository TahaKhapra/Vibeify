using NAudio.Wave;
using VibeMP.Core.Interfaces;
using VibeMP.Services.BpmAnalysis;

namespace VibeMP.Audio
{
    public class NAudioEngine : IAudioEngine
    {
        private IWavePlayer? _outputDevice;
        private AudioFileReader? _audioFile;
        private Models.PlaybackState _state = Models.PlaybackState.Stopped;

        public event EventHandler<float>? BpmDetected;
        public event EventHandler? PlaybackFinished;

        public Models.PlaybackState State => _state;
        public TimeSpan TotalTime => _audioFile?.TotalTime ?? TimeSpan.Zero;

        public float Volume
        {
            get => _audioFile?.Volume ?? 0;
            set { if (_audioFile != null) _audioFile.Volume = value; }
        }

        public TimeSpan CurrentTime
        {
            get => _audioFile?.CurrentTime ?? TimeSpan.Zero;
            set { if (_audioFile != null) _audioFile.CurrentTime = value; }
        }

        public void Load(string filePath)
        {
            Stop();

            try
            {
                _audioFile = new AudioFileReader(filePath);
                _outputDevice = new WaveOutEvent();
                _outputDevice.Init(_audioFile);

                if (_outputDevice != null)
                {
                    _outputDevice.PlaybackStopped += OnPlaybackStopped;
                
                }

                // to run in the background
                _ = Task.Run(() => RunBpmAnalysis(filePath));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Playback Error: {ex.Message}");
            }
        }

        public void Play()
        {
            if (_outputDevice != null)
            {
                _outputDevice.Play();
                _state = Models.PlaybackState.Playing;
            }
        }

        public void Pause()
        {
            _outputDevice?.Pause();
            _state = Models.PlaybackState.Paused;
        }

        public void Stop()
        {
            _outputDevice?.Stop();
            _state = Models.PlaybackState.Stopped;
            Dispose();
        }

        private async Task RunBpmAnalysis(string path)
        {
            var analyzer = new BpmAnalyzer();
            float result = await Task.Run(() => analyzer.Analyze(path));

            BpmDetected?.Invoke(this, result);
        }

        private void OnPlaybackStopped(object? sender, StoppedEventArgs e)
        {
            PlaybackFinished?.Invoke(this, EventArgs.Empty);
        }

        public void Dispose()
        {
            _outputDevice?.Dispose();
            _audioFile?.Dispose();
            _outputDevice = null;
            _audioFile = null;
        }
    }
}