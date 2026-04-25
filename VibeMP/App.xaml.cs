using System.Configuration;
using System.Data;
using System.Windows;
using VibeMP.Views;

namespace VibeMP
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            new MainWindow().Show();
        }
    }

}
