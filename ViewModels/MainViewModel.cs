
namespace QuickMigrate.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        private readonly SoftwareScanner _scanner;
        private CancellationTokenSource? _scanCancellationTokenSource;

        [ObservableProperty]
        private ObservableCollection<SoftwareInfo> _softwareList = new();

        [ObservableProperty]
        private string _scanStatus = "Prêt à analyser";

        [ObservableProperty]
        private int _scanProgress = 0;

        [ObservableProperty]
        private bool _isScanning = false;

        [ObservableProperty]
        private int _totalFound = 0;

        public MainViewModel()
        {
            _scanner = new SoftwareScanner(new Progress<ScanProgress>(OnScanProgress));
        }

        private void OnScanProgress(ScanProgress progress)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                ScanStatus = progress.Message;
                ScanProgress = progress.Percentage;
            });
        }

        [RelayCommand]
        private async Task StartScanAsync()
        {
            if (IsScanning) return;

            try
            {
                IsScanning = true;
                SoftwareList.Clear();
                TotalFound = 0;

                _scanCancellationTokenSource = new CancellationTokenSource();

                var scannedSoftware = await _scanner.ScanInstalledSoftwareAsync(_scanCancellationTokenSource.Token);

                foreach (var software in scannedSoftware)
                {
                    SoftwareList.Add(software);
                }

                TotalFound = SoftwareList.Count;
                ScanStatus = $"Analyse terminée - {TotalFound} logiciels détectés";
            }
            catch (OperationCanceledException)
            {
                ScanStatus = "Analyse annulée";
            }
            catch (Exception ex)
            {
                ScanStatus = $"Erreur: {ex.Message}";
                MessageBox.Show($"Erreur lors de l'analyse: {ex.Message}", "Erreur",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsScanning = false;
                ScanProgress = 0;
            }
        }

        [RelayCommand]
        private void StopScan()
        {
            _scanCancellationTokenSource?.Cancel();
            ScanStatus = "Arrêt de l'analyse...";
        }
    }
}