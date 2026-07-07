using System.Collections.ObjectModel;
using VibeMP.Models;

namespace VibeMP.Services
{
    public class LibraryDisplayService
    {
        private readonly LibraryManager _libraryManager;

        public ObservableCollection<VibeCategory> Categories { get; } = new();
        public ObservableCollection<Track> DashboardTracks { get; } = new();

        public LibraryDisplayService(LibraryManager libraryManager)
        {
            _libraryManager = libraryManager;
        }

        public void HydrateDashboardRows(
            VibeCategory? selectedCategory,
            out bool isHomeSelected,
            out VibeCategory? updatedSelection
        )
        {
            var dbCategories = _libraryManager.GetAllCategories();
            Categories.Clear();

            foreach (var cat in dbCategories)
            {
                cat.CategoryTracks.Clear();
                Categories.Add(cat);
            }

            if (Categories.Count > 0)
            {
                var allTracks = _libraryManager.GetAllTracks();
                foreach (var track in allTracks)
                {
                    var closestCategory = Categories
                        .OrderBy(c => Math.Abs(track.Bpm - c.TargetBpm))
                        .FirstOrDefault();

                    closestCategory?.CategoryTracks.Add(track);
                }
            }

            if (selectedCategory == null)
            {
                isHomeSelected = true;
                updatedSelection = null;
            }
            else
            {
                updatedSelection = Categories.FirstOrDefault(c => c.Id == selectedCategory.Id);
                isHomeSelected = false;
            }

            SyncDashboardTracks(updatedSelection);
        }

        public void LoadInitialCategories()
        {
            var dbCategories = _libraryManager.GetAllCategories();
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

        public void SyncDashboardTracks(VibeCategory? selectedCategory)
        {
            DashboardTracks.Clear();
            if (selectedCategory?.CategoryTracks != null)
            {
                foreach (var track in selectedCategory.CategoryTracks)
                {
                    DashboardTracks.Add(track);
                }
            }
        }
    }
}
