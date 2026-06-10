using Microsoft.EntityFrameworkCore;
using VibeMP.Models;

namespace VibeMP.Data
{
    public class LibraryContext : DbContext
    {
        public DbSet<Track> Tracks { get; set; }
        public DbSet<VibeCategory> Categories { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseSqlite("Data Source=vibeify.db");
        }
    }
}