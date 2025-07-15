using System.Windows;

namespace QuickMigrate
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Configuration globale de QuickMigrate
            // Logging, culture, etc.
        }

        protected override void OnExit(ExitEventArgs e)
        {
            // Nettoyage avant fermeture
            base.OnExit(e);
        }
    }
}