using VibeMP.Models;

namespace VibeMP.Core.Interfaces
{
    public interface IAudioEngine : IDisposable
    {
        void Load(string filePath);
        void Play();
        void Pause();
        void Stop();

        PlaybackState State { get; }
        TimeSpan CurrentTime { get; set; }
        TimeSpan TotalTime { get; }
        float Volume { get; set; }

        event EventHandler<float> BpmDetected;
        event EventHandler PlaybackFinished;
    }
}
