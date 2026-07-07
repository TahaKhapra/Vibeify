using System.IO;
using Microsoft.EntityFrameworkCore;
using VibeMP.Models;

namespace VibeMP.Data
{
    public class LibraryContext : DbContext
    {
        public DbSet<Track> Tracks { get; set; }
        public DbSet<VibeCategory> Categories { get; set; }

        public string DbPath { get; }

        public LibraryContext()
        {
            var folder = Environment.SpecialFolder.LocalApplicationData;
            var path = Environment.GetFolderPath(folder);
            var appFolder = Path.Combine(path, "Vibeify");

            Directory.CreateDirectory(appFolder);
            DbPath = Path.Combine(appFolder, "vibeify.db");
        }

        protected override void OnConfiguring(DbContextOptionsBuilder options) =>
            options.UseSqlite($"Data Source={DbPath}");

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Track>().HasKey(t => t.Id);
        }
    }
}
