
namespace LazyMigrate
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : System.Windows.Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Configuration globale de LazyMigrate
            // Logging, culture, etc.
        }

        protected override void OnExit(ExitEventArgs e)
        {
            // Nettoyage avant fermeture
            base.OnExit(e);
        }
    }
}