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

            /*           var folder = Environment.SpecialFolder.LocalApplicationData;
                       var path = Environment.GetFolderPath(folder);
                       var appFolder = Path.Combine(path, "Vibeify");
                       var dbPath = Path.Combine(appFolder, "vibeify.db");

                       if (File.Exists(dbPath))
                       {
                           try
                           {
                               File.Delete(dbPath);
                           }
                           catch (Exception exc)
                           {
                               System.Diagnostics.Debug.WriteLine($"Could not auto-delete DB: {exc.Message}");
                           }
                       }*/

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
                        TargetBpm = 70f,
                        IsPreset = true,
                    },
                    new VibeCategory
                    {
                        Name = "Gaming",
                        TargetBpm = 130f,
                        IsPreset = true,
                    }
                );

                db.SaveChanges();
            }
        }
    }
}
