using VibeMP.Models;

namespace VibeMP.Core.Interfaces
{
    public interface IMetadataService
    {
        Task<Track> GetTrackMetadataAsync(string filePath);
    }
}
