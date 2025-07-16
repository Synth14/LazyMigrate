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

        [ObservableProperty]
        private int _selectedCount = 0;

        public MainViewModel()
        {
            _scanner = new SoftwareScanner(new Progress<ScanProgress>(OnScanProgress));

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

        [RelayCommand]
        private void SelectAll()
        {
            foreach (var software in SoftwareList)
            {
                software.IsSelected = true;
                software.IncludeSettings = true; // Par défaut, inclure les settings
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
                Title = "Sauvegarder la configuration QuickMigrate",
                Filter = "Fichiers QuickMigrate (*.qm)|*.qm|Fichiers JSON (*.json)|*.json",
                DefaultExt = "qm",
                FileName = $"QuickMigrate_Backup_{DateTime.Now:yyyyMMdd_HHmmss}"
            };

            if (saveFileDialog.ShowDialog() == true)
            {
                try
                {
                    await ExportToFileAsync(selectedSoftware, saveFileDialog.FileName);

                    MessageBox.Show(
                        $"Export réussi !\n\n" +
                        $"Fichier: {saveFileDialog.FileName}\n" +
                        $"Logiciels exportés: {selectedSoftware.Count}\n" +
                        $"Avec settings: {selectedSoftware.Count(s => s.IncludeSettings)}",
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
            var exportData = new QuickMigrateExport
            {
                ExportDate = DateTime.Now,
                ComputerName = Environment.MachineName,
                UserName = Environment.UserName,
                OperatingSystem = Environment.OSVersion.ToString(),
                TotalSoftware = selectedSoftware.Count,
                Software = new List<ExportedSoftware>()
            };

            // Traiter chaque logiciel séparément pour éviter les problèmes avec async dans Select
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
            await Task.Delay(1); // Pour éviter le warning async

            if (!software.IncludeSettings) return new List<string>();

            var settingsPaths = new List<string>();

            try
            {
                // Ajouter les chemins détectés par le scanner s'ils existent
                if (software.SettingsPaths.Any())
                {
                    settingsPaths.AddRange(software.SettingsPaths.Select(sp => sp.Path));
                }
                else
                {
                    // Patterns génériques basés sur le nom du logiciel
                    var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                    var localAppDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                    var documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

                    var cleanName = software.Name.Replace(" ", "").Replace(".", "");
                    var possiblePaths = new[]
                    {
                        Path.Combine(appDataPath, software.Name),
                        Path.Combine(appDataPath, cleanName),
                        Path.Combine(localAppDataPath, software.Name),
                        Path.Combine(localAppDataPath, cleanName),
                        Path.Combine(documentsPath, software.Name)
                    };

                    foreach (var path in possiblePaths)
                    {
                        if (Directory.Exists(path) || File.Exists(path))
                        {
                            settingsPaths.Add(path);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erreur détection settings pour {software.Name}: {ex.Message}");
            }

            return settingsPaths;
        }
    }
}