using System.ComponentModel.DataAnnotations;

namespace VibeMP.Models
{
    public class VibeCategory
    {
        [Key]
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public float MinBpm { get; set; }
        public float MaxBpm { get; set; }
        public bool IsPreset { get; set; }
    }
}