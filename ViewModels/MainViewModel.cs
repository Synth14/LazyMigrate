using System.Linq;

namespace LazyMigrate.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        private readonly SoftwareScanner _scanner;
        private CancellationTokenSource? _scanCancellationTokenSource;
        private CancellationTokenSource? _downloadCancellationTokenSource;

        #region Properties
        public ObservableCollection<SoftwareWithDownload> SoftwareWithDownloads { get; set; }
        [ObservableProperty]
        private ObservableCollection<SoftwareWithDownload> _softwareList = new();

        [ObservableProperty]
        private string _scanStatus = "Prêt à analyser";

        [ObservableProperty]
        private int _scanProgress = 0;
        
        [ObservableProperty]
        private bool _isScanning = false;

        [ObservableProperty]
        private bool _isDownloading = false;

        [ObservableProperty]
        private string _downloadStatus = "";

        [ObservableProperty]
        private int _downloadProgress = 0;

        [ObservableProperty]
        private string _currentDownloadSoftware = "";

        [ObservableProperty]
        private long _downloadSpeed = 0;

        [ObservableProperty]
        private string _downloadSpeedFormatted = "";

        [ObservableProperty]
        private int _totalFound = 0;

        [ObservableProperty]
        private int _selectedCount = 0;

        #endregion

        public MainViewModel()
        {
            _scanner = new SoftwareScanner(new Progress<ScanProgress>(OnScanProgress));

            // Écouter les changements de sélection
            SoftwareList.CollectionChanged += (s, e) =>
            {
                if (e.NewItems != null)
                {
                    foreach (SoftwareWithDownload item in e.NewItems)
                    {
                        item.PropertyChanged += OnSoftwareItemChanged;
                    }
                }
                if (e.OldItems != null)
                {
                    foreach (SoftwareWithDownload item in e.OldItems)
                    {
                        item.PropertyChanged -= OnSoftwareItemChanged;
                    }
                }
            };
        }

        #region Event Handlers

        private void OnSoftwareItemChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(SoftwareWithDownload.IsSelected))
            {
                UpdateSelectedCount();
            }
        }

        private void UpdateSelectedCount()
        {
            SelectedCount = SoftwareList.Count(s => s.IsSelected);
        }

        private void OnScanProgress(ScanProgress progress)
        {
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                ScanStatus = progress.Message;
                ScanProgress = progress.Percentage;
            });
        }

        #endregion

        #region Commands

        [RelayCommand]
        private async Task StartScanAsync()
        {
            if (IsScanning || IsDownloading) return;

            try
            {
                IsScanning = true;
                SoftwareList.Clear();
                TotalFound = 0;
                SelectedCount = 0;

                _scanCancellationTokenSource = new CancellationTokenSource();

                // 1. Scanner les logiciels installés
                var scannedSoftware = await _scanner.ScanInstalledSoftwareAsync(_scanCancellationTokenSource.Token);

                // 2. Créer le détecteur intelligent
                var preciseDetector = new SettingsDetector(message =>
                {
                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    {
                        ScanStatus = message;
                    });
                });

                ScanStatus = "Détection intelligente des configurations...";

                var processedCount = 0;
                var totalSoftware = scannedSoftware.Count;

                // 3. Analyser chaque logiciel
                foreach (var software in scannedSoftware)
                {
                    processedCount++;
                    ScanStatus = $"Analyse {processedCount}/{totalSoftware}: {software.Name}";
                    try
                    {
                        var debugPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SettingsDetector_Debug.txt");
                        File.AppendAllText(debugPath, $"[MAIN] Traitement: {software.Name} (Publisher: {software.Publisher})\n");
                    }
                    catch { }
                    try
                    {
                        // Détection intelligente des settings
                        var settingsFiles = await preciseDetector.DetectSettingsAsync(software);

                        software.SettingsPaths = settingsFiles.Select(sf => new SettingsPath
                        {
                            Path = sf.FullPath,
                            Description = $"{sf.FileType} - {FormatFileSize(sf.Size)}",
                            IsDirectory = sf.IsDirectory,
                            Type = SettingsPathType.UserData,
                            Priority = 1
                        }).ToList();

                        // Auto-cocher si des settings sont trouvés
                        software.IncludeSettings = software.SettingsPaths.Any();
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Erreur détection pour {software.Name}: {ex.Message}");
                        software.SettingsPaths = new List<SettingsPath>();
                        software.IncludeSettings = false;
                    }

                    SoftwareList.Add(software);

                    // Petite pause pour éviter de saturer le système
                    if (processedCount % 10 == 0)
                        await Task.Delay(100, _scanCancellationTokenSource.Token);
                }

                TotalFound = SoftwareList.Count;
                var settingsCount = SoftwareList.Count(s => s.SettingsPaths.Any());
                ScanStatus = $"✅ Analyse terminée - {TotalFound} logiciels, {settingsCount} avec settings détectés";
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
        private void StopOperation()
        {
            if (IsDownloading)
            {
                _downloadCancellationTokenSource?.Cancel();
                DownloadStatus = "Arrêt des téléchargements...";
            }

            if (IsScanning)
            {
                _scanCancellationTokenSource?.Cancel();
                ScanStatus = "Arrêt de l'analyse...";
            }
        }

        [RelayCommand]
        private void SelectAll()
        {
            foreach (var software in SoftwareList)
            {
                software.IsSelected = true;
                software.IncludeSettings = true;
            }
        }
        partial void OnIsDownloadingChanged(bool value)
        {
            // Quand on commence à télécharger, mettre à jour le status principal
            if (value)
            {
                ScanStatus = DownloadStatus;
                ScanProgress = DownloadProgress;
            }
        }

        partial void OnDownloadStatusChanged(string value)
        {
            // Si on est en train de télécharger, mettre à jour le status principal
            if (IsDownloading)
            {
                ScanStatus = value;
            }
        }

        partial void OnDownloadProgressChanged(int value)
        {
            // Si on est en train de télécharger, mettre à jour la progression principale
            if (IsDownloading)
            {
                ScanProgress = value;
            }
        }
        [RelayCommand]
        private void UnselectAll()
        {
            foreach (var software in SoftwareList)
            {
                software.IsSelected = false;
                software.IncludeSettings = false;
            }
        }

        [RelayCommand]
        private async Task ExportAsync()
        {
            var selectedSoftware = SoftwareList.Where(s => s.IsSelected).ToList();

            if (!selectedSoftware.Any())
            {
                MessageBox.Show("Aucun logiciel sélectionné pour l'export.", "Information",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var saveFileDialog = new SaveFileDialog
            {
                Title = "Sauvegarder la configuration LazyMigrate",
                Filter = "Fichiers LazyMigrate (*.qm)|*.qm|Fichiers JSON (*.json)|*.json",
                DefaultExt = "qm",
                FileName = $"LazyMigrate_Backup_{DateTime.Now:yyyyMMdd_HHmmss}"
            };

            if (saveFileDialog.ShowDialog() == true)
            {
                try
                {
                    await ExportToFileAsync(selectedSoftware, saveFileDialog.FileName);

                    var settingsCount = selectedSoftware.Count(s => s.IncludeSettings && s.SettingsPaths.Any());

                    MessageBox.Show(
                        $"Export réussi !\n\n" +
                        $"Fichier: {saveFileDialog.FileName}\n" +
                        $"Logiciels exportés: {selectedSoftware.Count}\n" +
                        $"Avec settings détectés: {settingsCount}",
                        "Export terminé",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Erreur lors de l'export: {ex.Message}", "Erreur",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

 
        #endregion

        #region Private Methods

        private async Task ExportToFileAsync(List<SoftwareWithDownload> selectedSoftware, string fileName)
        {
            var exportData = new LazyMigrateExport
            {
                ExportDate = DateTime.Now,
                ComputerName = Environment.MachineName,
                UserName = Environment.UserName,
                OperatingSystem = Environment.OSVersion.ToString(),
                TotalSoftware = selectedSoftware.Count,
                Software = new List<ExportedSoftware>()
            };

            foreach (var software in selectedSoftware)
            {
                var settingsPaths = await GetSettingsPathsAsync(software);

                var exportedSoftware = new ExportedSoftware
                {
                    Name = software.Name,
                    Publisher = software.Publisher,
                    Version = software.Version,
                    Category = software.Category,
                    InstallPath = software.InstallPath,
                    UninstallString = software.UninstallString,
                    EstimatedSize = software.EstimatedSize,
                    InstallDate = software.InstallDate,
                    IncludeSettings = software.IncludeSettings,
                    SettingsPaths = settingsPaths,
                    ExecutablePaths = software.ExecutablePaths,
                    DownloadSources = software.DownloadSources.Select(ds => new ExportedDownloadSource
                    {
                        Type = ds.Type.ToString(),
                        Url = ds.Url,
                        IsOfficial = ds.IsOfficial,
                        Notes = ds.Notes
                    }).ToList()
                };

                exportData.Software.Add(exportedSoftware);
            }

            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNameCaseInsensitive = true
            };

            var json = JsonSerializer.Serialize(exportData, options);
            await File.WriteAllTextAsync(fileName, json);
        }

        private async Task<List<string>> GetSettingsPathsAsync(SoftwareWithDownload software)
        {
            await Task.CompletedTask;

            if (!software.IncludeSettings) return new List<string>();

            // Utiliser les chemins déjà détectés lors du scan
            return software.SettingsPaths.Select(sp => sp.Path).ToList();
        }
        [RelayCommand]
        private void OpenDownloadLink(string url)
        {
            if (string.IsNullOrEmpty(url))
                return;

            try
            {
                // Ouvrir le lien dans le navigateur par défaut
                var psi = new ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                };
                Process.Start(psi);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Impossible d'ouvrir le lien:\n{ex.Message}",
                    "Erreur", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        [RelayCommand]
        private async Task SearchDownloadsAsync()
        {
            var selectedSoftware = SoftwareList.Where(s => s.IsSelected).ToList();

            if (!selectedSoftware.Any())
            {
                MessageBox.Show("Aucun logiciel sélectionné pour la recherche.", "Information",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                IsDownloading = true;
                DownloadStatus = "🔍 Recherche des liens de téléchargement...";

                // Marquer tous comme "en recherche"
                foreach (var software in selectedSoftware)
                {
                    software.DownloadStatus = Models.Download.DownloadStatus.Searching;
                    software.OnPropertyChanged(nameof(software.DownloadStatusText));
                    software.OnPropertyChanged(nameof(software.BestDownloadUrl));
                }

                var detector = new Services.Download.DownloadDetector(message =>
                {
                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    {
                        DownloadStatus = message;
                    });
                });

                var results = await detector.FindDownloadLinksForMultipleSoftwareAsync(selectedSoftware);

                // Mettre à jour les statuts individuels
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    for (int i = 0; i < selectedSoftware.Count && i < results.Count; i++)
                    {
                        var software = selectedSoftware[i];
                        var result = results[i];

                        software.DownloadResult = result;
                        software.DownloadStatus = result.IsSuccess ? Models.Download.DownloadStatus.Found : Models.Download.DownloadStatus.NotFound;

                        // Forcer la mise à jour de l'UI
                        software.OnPropertyChanged(nameof(software.DownloadStatusText));
                        software.OnPropertyChanged(nameof(software.BestDownloadUrl));
                    }
                });

                var successCount = results.Count(r => r.IsSuccess);
                DownloadStatus = $"✅ Recherche terminée: {successCount}/{results.Count} liens trouvés";

                MessageBox.Show($"Recherche terminée !\n\nLiens trouvés: {successCount}/{results.Count}\n\nVoir les colonnes 'Download' et 'Lien' pour les détails.",
                    "Recherche de téléchargements", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                DownloadStatus = $"❌ Erreur: {ex.Message}";

                // Marquer tous comme erreur
                foreach (var software in selectedSoftware)
                {
                    software.DownloadStatus = Models.Download.DownloadStatus.Error;
                    software.OnPropertyChanged(nameof(software.DownloadStatusText));
                    software.OnPropertyChanged(nameof(software.BestDownloadUrl));
                }

                MessageBox.Show($"Erreur lors de la recherche:\n{ex.Message}", "Erreur",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsDownloading = false;
            }
        }

        #region Utility Methods

        private string FormatBytes(long bytes)
        {
            if (bytes < 1024) return $"{bytes} B";
            if (bytes < 1024 * 1024) return $"{bytes / 1024:F1} KB";
            if (bytes < 1024 * 1024 * 1024) return $"{bytes / (1024 * 1024):F1} MB";
            return $"{bytes / (1024L * 1024 * 1024):F1} GB";
        }

        private string FormatSpeed(long bytesPerSecond)
        {
            if (bytesPerSecond < 1024) return $"{bytesPerSecond} B/s";
            if (bytesPerSecond < 1024 * 1024) return $"{bytesPerSecond / 1024:F1} KB/s";
            return $"{bytesPerSecond / (1024 * 1024):F1} MB/s";
        }

        private string FormatFileSize(long bytes)
        {
            return FormatBytes(bytes);
        }

        #endregion
    }
}
#endregion
