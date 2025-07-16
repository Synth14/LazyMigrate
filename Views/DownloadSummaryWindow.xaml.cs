
namespace LazyMigrate.Views
{
    public partial class DownloadSummaryWindow : Window
    {
        private readonly string _downloadFolder;
        private readonly string _summaryText;
        private readonly bool _hasDownloads;

        public DownloadSummaryWindow(string summaryText, string downloadFolder, bool hasDownloads)
        {
            InitializeComponent();

            _summaryText = summaryText;
            _downloadFolder = downloadFolder;
            _hasDownloads = hasDownloads;

            InitializeContent();
        }

        private void InitializeContent()
        {
            // Définir le contenu
            SummaryContent.Text = _summaryText;

            // Header dynamique
            var lines = _summaryText.Split('\n');
            if (lines.Length > 4)
            {
                SummaryHeader.Text = $"Voir les détails ci-dessous • {_downloadFolder}";
            }

            // Activer/désactiver le bouton selon qu'il y a des téléchargements
            OpenFolderButton.IsEnabled = _hasDownloads;
            if (!_hasDownloads)
            {
                OpenFolderButton.Content = "📂 Aucun fichier téléchargé";
                OpenFolderButton.Background = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(96, 96, 96));
            }
        }

        private void OpenFolderButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_hasDownloads && System.IO.Directory.Exists(_downloadFolder))
                {
                    Process.Start("explorer.exe", _downloadFolder);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Impossible d'ouvrir le dossier:\n{ex.Message}",
                    "Erreur", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void CopyLogButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Clipboard.SetText(_summaryText);

                // Feedback visuel temporaire
                var originalContent = CopyLogButton.Content;
                CopyLogButton.Content = "✅ Copié !";

                var timer = new System.Windows.Threading.DispatcherTimer();
                timer.Interval = TimeSpan.FromSeconds(2);
                timer.Tick += (s, args) =>
                {
                    CopyLogButton.Content = originalContent;
                    timer.Stop();
                };
                timer.Start();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Impossible de copier le texte:\n{ex.Message}",
                    "Erreur", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }
    }
}