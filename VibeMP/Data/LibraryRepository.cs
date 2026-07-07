using VibeMP.Core.Interfaces;
using VibeMP.Models;

namespace VibeMP.Data
{
    public class LibraryRepository : ILibraryRepository
    {
        public List<Track> GetAllTracks()
        {
            using var db = new LibraryContext();
            return db.Tracks.ToList();
        }

        public List<VibeCategory> GetAllCategories()
        {
            using var db = new LibraryContext();
            return db.Categories.ToList();
        }

        public void SaveCategories(IEnumerable<VibeCategory> categories)
        {
            using var db = new LibraryContext();
            foreach (var uiCategory in categories)
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

        public void DeleteCategory(int categoryId)
        {
            using var db = new LibraryContext();
            var category = db.Categories.Find(categoryId);
            if (category != null)
            {
                var associatedTracks = db.Tracks.Where(t => t.CategoryId == categoryId);
                foreach (var track in associatedTracks)
                {
                    track.CategoryId = null;
                }
                db.Categories.Remove(category);
                db.SaveChanges();
            }
        }

        public void UpdateTrackCategoryIds(IEnumerable<Track> tracks)
        {
            using var db = new LibraryContext();
            foreach (var uiTrack in tracks)
            {
                var dbTrack = db.Tracks.Find(uiTrack.Id);
                if (dbTrack != null)
                {
                    dbTrack.Bpm = uiTrack.Bpm;
                    dbTrack.CategoryId = uiTrack.CategoryId;
                }
            }
            db.SaveChanges();
        }

        public void SaveChanges()
        {
            using var db = new LibraryContext();
            db.SaveChanges();
        }

        public bool HasTracks()
        {
            using var db = new LibraryContext();
            return db.Tracks.Any();
        }
    }
}
