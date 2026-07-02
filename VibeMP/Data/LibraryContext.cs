using Microsoft.EntityFrameworkCore;
using VibeMP.Models;

namespace VibeMP.Data
{
    public class LibraryContext : DbContext
    {
        public DbSet<Track> Tracks { get; set; }
        public DbSet<VibeCategory> Categories { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder options) =>
            options.UseSqlite(@"Data Source=C:\Users\tkhapra\Vibeify\vibeify.db");

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Track>().HasKey(t => t.Id);
        }
    }
}
