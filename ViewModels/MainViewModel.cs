using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using LazyMigrate.Models;
using LazyMigrate.Services;
using MessageBox = System.Windows.MessageBox;
using SaveFileDialog = Microsoft.Win32.SaveFileDialog;

namespace LazyMigrate.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        private readonly SoftwareScanner _scanner;
        private CancellationTokenSource? _scanCancellationTokenSource;
        private CancellationTokenSource? _downloadCancellationTokenSource;

        #region Properties

        [ObservableProperty]
        private ObservableCollection<SoftwareInfo> _softwareList = new();

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

        #region Event Handlers

        private void OnSoftwareItemChanged(object? sender, PropertyChangedEventArgs e)
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
                var preciseDetector = new PreciseSettingsDetector(message =>
                {
                    System.Windows.Application.Current.Dispatcher.Invoke(() => {
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

        [RelayCommand]
        private async Task DownloadAsync()
        {
            var selectedSoftware = SoftwareList.Where(s => s.IsSelected).ToList();

            if (!selectedSoftware.Any())
            {
                MessageBox.Show("Aucun logiciel sélectionné pour le téléchargement.", "Information",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var folderDialog = new FolderBrowserDialog
            {
                Description = "Choisir le dossier de téléchargement des installateurs",
                UseDescriptionForTitle = true,
                ShowNewFolderButton = true,
                SelectedPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "LazyMigrate Downloads")
            };

            if (folderDialog.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    IsDownloading = true;
                    DownloadProgress = 0;
                    DownloadStatus = "Initialisation des téléchargements...";
                    _downloadCancellationTokenSource = new CancellationTokenSource();

                    // Créer le téléchargeur avec callbacks de progression
                    var downloader = new SoftwareDownloader(
                        folderDialog.SelectedPath,
                        // Callback de progression générale
                        message => System.Windows.Application.Current.Dispatcher.Invoke(() => {
                            DownloadStatus = message;
                        }),
                        // Callback de progression détaillée
                        progress => System.Windows.Application.Current.Dispatcher.Invoke(() => {
                            CurrentDownloadSoftware = progress.SoftwareName;
                            DownloadProgress = progress.ProgressPercent;
                            DownloadSpeed = progress.Speed;
                            DownloadSpeedFormatted = FormatSpeed(progress.Speed);
                            DownloadStatus = $"📥 {progress.SoftwareName}: {progress.ProgressPercent}% ({FormatBytes(progress.DownloadedBytes)}/{FormatBytes(progress.TotalBytes)})";
                        })
                    );

                    // Lancer les téléchargements
                    var results = await downloader.DownloadSoftwareAsync(selectedSoftware, _downloadCancellationTokenSource.Token);

                    // Afficher le résumé détaillé
                    await ShowDownloadSummaryAsync(results, folderDialog.SelectedPath);
                }
                catch (OperationCanceledException)
                {
                    DownloadStatus = "Téléchargements annulés par l'utilisateur";
                    MessageBox.Show("Téléchargements annulés.", "Information",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    DownloadStatus = $"Erreur: {ex.Message}";
                    MessageBox.Show($"Erreur lors des téléchargements:\n{ex.Message}", "Erreur",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
                finally
                {
                    IsDownloading = false;
                    DownloadProgress = 0;
                    CurrentDownloadSoftware = "";
                    DownloadSpeedFormatted = "";
                    _downloadCancellationTokenSource?.Dispose();
                    _downloadCancellationTokenSource = null;
                }
            }
        }

        #endregion

        #region Private Methods

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

            // Utiliser les chemins déjà détectés lors du scan
            return software.SettingsPaths.Select(sp => sp.Path).ToList();
        }

        private async Task ShowDownloadSummaryAsync(List<DownloadResult> results, string downloadFolder)
        {
            var successful = results.Where(r => r.Status == Models.DownloadStatus.Success).ToList();
            var failed = results.Where(r => r.Status == Models.DownloadStatus.Error).ToList();
            var alreadyExists = results.Where(r => r.Status == Models.DownloadStatus.AlreadyExists).ToList();
            var noSource = results.Where(r => r.Status == Models.DownloadStatus.NoSourceFound).ToList();

            var totalSize = successful.Concat(alreadyExists).Sum(r => r.FileSize);
            var newDownloads = successful.Where(r => !string.IsNullOrEmpty(r.FilePath)).ToList();

            // Créer le résumé détaillé
            var summary = new System.Text.StringBuilder();
            summary.AppendLine("🎉 TÉLÉCHARGEMENTS TERMINÉS !");
            summary.AppendLine();
            summary.AppendLine($"📊 RÉSUMÉ :");
            summary.AppendLine($"  ✅ Téléchargés avec succès: {successful.Count}");
            summary.AppendLine($"  📁 Déjà présents: {alreadyExists.Count}");
            summary.AppendLine($"  🔍 Sans source trouvée: {noSource.Count}");
            summary.AppendLine($"  ❌ Échecs: {failed.Count}");
            summary.AppendLine();
            summary.AppendLine($"💾 ESPACE DISQUE :");
            summary.AppendLine($"  📦 Taille totale: {FormatBytes(totalSize)}");
            summary.AppendLine($"  ⬇️ Nouveaux téléchargements: {FormatBytes(newDownloads.Sum(r => r.FileSize))}");
            summary.AppendLine();
            summary.AppendLine($"📂 DOSSIER : {downloadFolder}");

            if (successful.Any())
            {
                summary.AppendLine();
                summary.AppendLine("✅ TÉLÉCHARGEMENTS RÉUSSIS :");
                foreach (var result in successful.Take(10))
                {
                    var fileName = Path.GetFileName(result.FilePath);
                    var source = result.Source?.IsOfficial == true ? "🛡️ Officiel" : "🌐 Web";
                    summary.AppendLine($"  • {fileName} ({FormatBytes(result.FileSize)}) - {source}");
                }
                if (successful.Count > 10)
                {
                    summary.AppendLine($"  ... et {successful.Count - 10} autres");
                }
            }

            if (failed.Any())
            {
                summary.AppendLine();
                summary.AppendLine("❌ ÉCHECS :");
                foreach (var result in failed.Take(5))
                {
                    summary.AppendLine($"  • {result.Software.Name}: {result.ErrorMessage}");
                }
            }

            if (noSource.Any())
            {
                summary.AppendLine();
                summary.AppendLine("🔍 AUCUNE SOURCE TROUVÉE :");
                foreach (var result in noSource.Take(5))
                {
                    summary.AppendLine($"  • {result.Software.Name}");
                }
            }

            // Afficher le résumé dans une MessageBox pour l'instant
            // TODO: Implémenter DownloadSummaryWindow
            MessageBox.Show(summary.ToString(), "Résumé des téléchargements",
                MessageBoxButton.OK, MessageBoxImage.Information);

            // Proposer d'ouvrir le dossier
            if (newDownloads.Count > 0 || alreadyExists.Count > 0)
            {
                var openFolder = MessageBox.Show("Voulez-vous ouvrir le dossier de téléchargement ?",
                    "Ouvrir le dossier", MessageBoxButton.YesNo, MessageBoxImage.Question);

                if (openFolder == MessageBoxResult.Yes)
                {
                    try
                    {
                        System.Diagnostics.Process.Start("explorer.exe", downloadFolder);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Impossible d'ouvrir le dossier: {ex.Message}", "Erreur",
                            MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                }
            }

            // Mettre à jour le statut final
            DownloadStatus = $"✅ Terminé: {successful.Count} téléchargés, {failed.Count} échecs";
        }

        #endregion

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