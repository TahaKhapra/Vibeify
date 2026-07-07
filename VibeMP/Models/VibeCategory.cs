using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using CommunityToolkit.Mvvm.ComponentModel;

namespace VibeMP.Models
{
    /// <summary>
    /// Repräsentiert eine Musik-Kategorie in der Datenbank.
    /// Sortiert Lieder basierend auf einem einzigen BPM-Wert.
    /// </summary>
    public partial class VibeCategory : ObservableObject
    {
        [Key]
        public int Id { get; set; }

        [ObservableProperty]
        private string _name = string.Empty;

        /// <summary>
        /// Der Ziel-BPM-Wert für diese Kategorie.
        /// Lieder werden automatisch der Kategorie zugeordnet, deren Zielwert am nächsten liegt.
        /// </summary>
        [ObservableProperty]
        private float _targetBpm;

        public bool IsPreset { get; set; }

        /// <summary>
        /// Eine temporäre Liste im Arbeitsspeicher, die alle Lieder dieser Kategorie hält.
        /// Wird nicht in der Datenbank gespeichert, um die Performance zu verbessern.
        /// </summary>
        [NotMapped]
        public ObservableCollection<Track> CategoryTracks { get; } = new();

        [ObservableProperty]
        [NotMapped]
        private string? _rowErrorMessage;
    }
}
