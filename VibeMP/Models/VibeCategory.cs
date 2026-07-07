using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using CommunityToolkit.Mvvm.ComponentModel;

namespace VibeMP.Models
{
    public partial class VibeCategory : ObservableObject
    {
        [Key]
        public int Id { get; set; }

        [ObservableProperty]
        private string _name = string.Empty;

        [ObservableProperty]
        private float _targetBpm;

        public bool IsPreset { get; set; }

        [NotMapped]
        public ObservableCollection<Track> CategoryTracks { get; } = new();
    }
}
