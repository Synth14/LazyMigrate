using QuickMigrate.Services;

namespace LazyMigrate.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        private readonly SoftwareScanner _scanner;
        private readonly SoftwareSettingsRegistry _settingsRegistry; // ← AJOUT
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

        [ObservableProperty]
        private int _selectedCount = 0;

        public MainViewModel()
        {
            _scanner = new SoftwareScanner(new Progress<ScanProgress>(OnScanProgress));

            // ← AJOUT : Créer le registry des settings
            _settingsRegistry = new SoftwareSettingsRegistry(message =>
            {
                Application.Current.Dispatcher.Invoke(() => {
                    ScanStatus = message;
                });
            });

            // Écouter les changements de sélection
            SoftwareList.CollectionChanged += (s, e) =>
            {
                if (e.NewItems != null)
                {
                    foreach (SoftwareInfo item in e.NewItems)
                    {
                        item.PropertyChanged += OnSoftwareItemChanged;
                    }
                }
                if (e.OldItems != null)
                {
                    foreach (SoftwareInfo item in e.OldItems)
                    {
                        item.PropertyChanged -= OnSoftwareItemChanged;
                    }
                }
            };
        }

        private void OnSoftwareItemChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(SoftwareInfo.IsSelected))
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
                SelectedCount = 0;

                _scanCancellationTokenSource = new CancellationTokenSource();

                // 1. Scanner les logiciels installés
                var scannedSoftware = await _scanner.ScanInstalledSoftwareAsync(_scanCancellationTokenSource.Token);

                // 2. Créer le détecteur intelligent
                var preciseDetector = new PreciseSettingsDetector(message =>
                {
                    Application.Current.Dispatcher.Invoke(() => {
                        ScanStatus = message;
                    });
                });

                ScanStatus = "Détection intelligente des configurations...";

                var processedCount = 0;
                var totalSoftware = scannedSoftware.Count;

                // 3. Analyser chaque logiciel avec l'IA
                foreach (var software in scannedSoftware)
                {
                    processedCount++;
                    ScanStatus = $"Analyse {processedCount}/{totalSoftware}: {software.Name}";

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
        private string FormatFileSize(long bytes)
        {
            if (bytes < 1024) return $"{bytes} B";
            if (bytes < 1024 * 1024) return $"{bytes / 1024:F1} KB";
            return $"{bytes / (1024 * 1024):F1} MB";
        }
        [RelayCommand]
        private void StopScan()
        {
            _scanCancellationTokenSource?.Cancel();
            ScanStatus = "Arrêt de l'analyse...";
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

        private async Task ExportToFileAsync(List<SoftwareInfo> selectedSoftware, string fileName)
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

        private async Task<List<string>> GetSettingsPathsAsync(SoftwareInfo software)
        {
            await Task.CompletedTask; 

            if (!software.IncludeSettings) return new List<string>();

            return software.SettingsPaths.Select(sp => sp.Path).ToList();
        }
    }
}