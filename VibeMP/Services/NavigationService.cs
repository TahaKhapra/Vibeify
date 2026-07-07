using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using VibeMP.Models;

namespace VibeMP.Services
{
    public partial class NavigationService : ObservableObject
    {
        [ObservableProperty]
        private AppViewState _currentViewState = AppViewState.Onboarding;

        [ObservableProperty]
        private bool _isHomeSelected = true;

        public void NavigateTo(AppViewState state)
        {
            CurrentViewState = state;
            if (state == AppViewState.Settings || state == AppViewState.Onboarding)
            {
                IsHomeSelected = false;
            }
        }

        public bool EvaluateSettingsClosureAllowed(ObservableCollection<VibeCategory> categories)
        {
            return categories.Count >= 2
                && !categories.Any(c => !string.IsNullOrEmpty(c.RowErrorMessage));
        }

        public bool EvaluateOnboardingAllowed(
            ObservableCollection<VibeCategory> categories,
            int pendingTracksCount
        )
        {
            return categories.Count >= 2
                && !categories.Any(c => !string.IsNullOrEmpty(c.RowErrorMessage))
                && pendingTracksCount > 0;
        }
    }
}
