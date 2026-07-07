using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using VibeMP.Models;

namespace VibeMP.Services
{
    public partial class LibrarySetupService : ObservableObject
    {
        private readonly LibraryManager _libraryManager;

        public ObservableCollection<Track> ZeroBpmTracks { get; } = new();

        public LibrarySetupService(LibraryManager libraryManager)
        {
            _libraryManager = libraryManager;
        }

        public bool CheckOnboardingTracks(
            ObservableCollection<VibeCategory> categories,
            out string statusText
        )
        {
            _libraryManager.SaveCategories(categories);
            var allTracks = _libraryManager.GetAllTracks();

            var zeroTracks = allTracks.Where(t => t.Bpm <= 0).ToList();
            if (zeroTracks.Count > 0)
            {
                ZeroBpmTracks.Clear();
                foreach (var track in zeroTracks)
                {
                    track.Bpm = 80;
                    ZeroBpmTracks.Add(track);
                }
                statusText = $"{zeroTracks.Count} tracks defaulted to 80 BPM. Adjust if needed!";
                return true;
            }

            statusText = "";
            return false;
        }

        public bool ReviewSettingsChanges(ObservableCollection<VibeCategory> categories)
        {
            var dbCategories = _libraryManager.GetAllCategories();
            bool structuralChangesMade = false;

            foreach (var uiCategory in categories)
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
            return structuralChangesMade;
        }

        public void SaveCorrectedBpms()
        {
            var allTracks = _libraryManager.GetAllTracks();
            var dbCategories = _libraryManager.GetAllCategories();

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
            _libraryManager.UpdateTrackCategoryIds(allTracks);
        }
    }
}
