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

            /*            string appData = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                        string dbFolder = Path.Combine(appData, "Vibeify");
                        string dbPath = Path.Combine(dbFolder, "vibeify.db");

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
