using VibeMP.Models;

namespace VibeMP.Core.Interfaces
{
    public interface ILibraryRepository
    {
        List<Track> GetAllTracks();
        List<VibeCategory> GetAllCategories();
        void SaveCategories(IEnumerable<VibeCategory> categories);
        void DeleteCategory(int categoryId);
        void UpdateTrackCategoryIds(IEnumerable<Track> tracks);
        void SaveChanges();
        bool HasTracks();
    }
}
