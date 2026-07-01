using System.IO;
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
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            string dbFolder = Path.Combine(appData, "Vibeify");
            Directory.CreateDirectory(dbFolder);

            string dbPath = Path.Combine(dbFolder, "vibeify.db");
            optionsBuilder.UseSqlite($"Data Source={dbPath}");
        }
    }
}
