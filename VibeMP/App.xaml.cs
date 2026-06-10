using System.Windows;
using VibeMP.Data;
using VibeMP.Models;

namespace VibeMP
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            InitializeDatabase();
        }

        private void InitializeDatabase()
        {
            using var db = new LibraryContext();

            db.Database.EnsureCreated();

            if (!db.Categories.Any())
            {
                db.Categories.AddRange(
                    new VibeCategory
                    {
                        Name = "Studying",
                        MinBpm = 70f,
                        MaxBpm = 90f,
                        IsPreset = true,
                    },
                    new VibeCategory
                    {
                        Name = "Gaming",
                        MinBpm = 110f,
                        MaxBpm = 130f,
                        IsPreset = true,
                    }
                );

                db.SaveChanges();
            }
        }
    }
}
