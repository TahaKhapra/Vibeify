using System.ComponentModel.DataAnnotations;

namespace VibeMP.Models
{
    public class Track
    {
        [Key]
        public string FilePath { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public float Bpm { get; set; }
        public int? CategoryId { get; set; }
    }
}
