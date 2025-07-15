
namespace QuickMigrate.Services
{
    public class SoftwareScanner
    {
        private readonly IProgress<ScanProgress>? _progress;
        private CancellationToken _cancellationToken;

        public SoftwareScanner(IProgress<ScanProgress>? progress = null)
        {
            _progress = progress;
        }

        public async Task<List<SoftwareInfo>> ScanInstalledSoftwareAsync(CancellationToken cancellationToken = default)
        {
            _cancellationToken = cancellationToken;
            var softwareList = new List<SoftwareInfo>();

            try
            {
                _progress?.Report(new ScanProgress { Message = "Analyse du registre Windows...", Percentage = 10 });
                await Task.Delay(500, cancellationToken); // Simule le temps de traitement

                // Scan du registre 64-bit
                var registry64 = await ScanRegistryAsync(RegistryView.Registry64);
                softwareList.AddRange(registry64);

                _progress?.Report(new ScanProgress { Message = "Analyse du registre 32-bit...", Percentage = 40 });
                await Task.Delay(500, cancellationToken);

                // Scan du registre 32-bit  
                var registry32 = await ScanRegistryAsync(RegistryView.Registry32);
                softwareList.AddRange(registry32);

                _progress?.Report(new ScanProgress { Message = "Analyse des dossiers Program Files...", Percentage = 70 });
                await Task.Delay(500, cancellationToken);

                // Scan des dossiers Program Files
                var programFiles = await ScanProgramFilesAsync();
                softwareList.AddRange(programFiles);

                _progress?.Report(new ScanProgress { Message = "Nettoyage des doublons...", Percentage = 90 });
                await Task.Delay(300, cancellationToken);

                // Nettoyer les doublons
                var cleanedList = CleanDuplicates(softwareList);

                _progress?.Report(new ScanProgress { Message = $"Analyse terminée - {cleanedList.Count} logiciels trouvés", Percentage = 100 });

                return cleanedList;
            }
            catch (OperationCanceledException)
            {
                _progress?.Report(new ScanProgress { Message = "Analyse annulée", Percentage = 0 });
                throw;
            }
            catch (Exception ex)
            {
                _progress?.Report(new ScanProgress { Message = $"Erreur: {ex.Message}", Percentage = 0 });
                throw;
            }
        }

        private async Task<List<SoftwareInfo>> ScanRegistryAsync(RegistryView registryView)
        {
            return await Task.Run(() =>
            {
                var softwareList = new List<SoftwareInfo>();

                try
                {
                    using var baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, registryView);
                    using var uninstallKey = baseKey.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall");

                    if (uninstallKey == null) return softwareList;

                    foreach (var subKeyName in uninstallKey.GetSubKeyNames())
                    {
                        _cancellationToken.ThrowIfCancellationRequested();

                        try
                        {
                            using var subKey = uninstallKey.OpenSubKey(subKeyName);
                            if (subKey == null) continue;

                            var software = CreateSoftwareFromRegistry(subKey);
                            if (software != null && IsValidSoftware(software))
                            {
                                softwareList.Add(software);
                            }
                        }
                        catch (Exception)
                        {
                            // Ignorer les erreurs de clés individuelles
                            continue;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Erreur scan registre {registryView}: {ex.Message}");
                }

                return softwareList;
            });
        }

        private SoftwareInfo? CreateSoftwareFromRegistry(RegistryKey key)
        {
            var displayName = key.GetValue("DisplayName")?.ToString();
            if (string.IsNullOrWhiteSpace(displayName)) return null;

            var software = new SoftwareInfo
            {
                Name = displayName,
                Publisher = key.GetValue("Publisher")?.ToString() ?? "Inconnu",
                Version = key.GetValue("DisplayVersion")?.ToString() ?? "Inconnue",
                InstallPath = key.GetValue("InstallLocation")?.ToString() ?? string.Empty,
                UninstallString = key.GetValue("UninstallString")?.ToString() ?? string.Empty,
                IconPath = key.GetValue("DisplayIcon")?.ToString() ?? string.Empty,
                Category = CategorizeSOFTWARE(displayName)
            };

            // Date d'installation
            var installDateStr = key.GetValue("InstallDate")?.ToString();
            if (!string.IsNullOrEmpty(installDateStr) && installDateStr.Length == 8)
            {
                if (DateTime.TryParseExact(installDateStr, "yyyyMMdd", null,
                    System.Globalization.DateTimeStyles.None, out var installDate))
                {
                    software.InstallDate = installDate;
                }
            }

            // Taille estimée (en KB dans le registre)
            var sizeObj = key.GetValue("EstimatedSize");
            if (sizeObj != null && long.TryParse(sizeObj.ToString(), out var size))
            {
                software.EstimatedSize = size * 1024; // Convertir KB vers bytes
            }

            return software;
        }

        private async Task<List<SoftwareInfo>> ScanProgramFilesAsync()
        {
            return await Task.Run(() =>
            {
                var softwareList = new List<SoftwareInfo>();
                var programFilesPaths = new[]
                {
                    Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                    Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86)
                };

                foreach (var basePath in programFilesPaths.Where(Directory.Exists))
                {
                    try
                    {
                        var directories = Directory.GetDirectories(basePath);
                        foreach (var dir in directories.Take(50)) // Limiter pour éviter la lenteur
                        {
                            _cancellationToken.ThrowIfCancellationRequested();

                            var dirInfo = new DirectoryInfo(dir);
                            var software = CreateSoftwareFromDirectory(dirInfo);
                            if (software != null)
                            {
                                softwareList.Add(software);
                            }
                        }
                    }
                    catch (Exception)
                    {
                        // Ignorer les erreurs d'accès aux dossiers
                        continue;
                    }
                }

                return softwareList;
            });
        }

        private SoftwareInfo? CreateSoftwareFromDirectory(DirectoryInfo directory)
        {
            try
            {
                // Chercher des exécutables
                var executables = directory.GetFiles("*.exe", SearchOption.TopDirectoryOnly);
                if (!executables.Any()) return null;

                var software = new SoftwareInfo
                {
                    Name = directory.Name,
                    Publisher = "Détecté Program Files",
                    Version = "Inconnue",
                    InstallPath = directory.FullName,
                    Category = "Program Files",
                    InstallDate = directory.CreationTime
                };

                // Essayer de récupérer des infos du premier exe
                try
                {
                    var mainExe = executables.First();
                    var versionInfo = System.Diagnostics.FileVersionInfo.GetVersionInfo(mainExe.FullName);

                    if (!string.IsNullOrEmpty(versionInfo.CompanyName))
                        software.Publisher = versionInfo.CompanyName;

                    if (!string.IsNullOrEmpty(versionInfo.FileVersion))
                        software.Version = versionInfo.FileVersion;

                    if (!string.IsNullOrEmpty(versionInfo.FileDescription))
                        software.Name = versionInfo.FileDescription;
                }
                catch
                {
                    // Ignorer les erreurs de lecture des versions
                }

                // Taille du dossier (simplifié)
                try
                {
                    software.EstimatedSize = directory.EnumerateFiles("*", SearchOption.TopDirectoryOnly)
                        .Take(100) // Limiter pour éviter la lenteur
                        .Sum(f => f.Length);
                }
                catch
                {
                    software.EstimatedSize = 0;
                }

                return software;
            }
            catch
            {
                return null;
            }
        }

        private bool IsValidSoftware(SoftwareInfo software)
        {
            // Filtrer les mises à jour Windows et autres entrées indésirables
            var excludePatterns = new[]
            {
                "Security Update", "Hotfix", "Update for", "Microsoft Visual C++ 20",
                "Microsoft .NET Framework", "Windows SDK", "KB"
            };

            return !excludePatterns.Any(pattern =>
                software.Name.Contains(pattern, StringComparison.OrdinalIgnoreCase));
        }

        private string CategorizeSOFTWARE(string name)
        {
            var categories = new Dictionary<string, string[]>
            {
                ["Développement"] = new[] { "Visual Studio", "VS Code", "Git", "Docker", "Node.js", "Python", "JetBrains" },
                ["Navigateurs"] = new[] { "Chrome", "Firefox", "Edge", "Opera", "Brave" },
                ["Multimédia"] = new[] { "VLC", "Photoshop", "GIMP", "Audacity", "Spotify", "iTunes" },
                ["Jeux"] = new[] { "Steam", "Epic Games", "Battle.net", "Origin", "Ubisoft" },
                ["Bureautique"] = new[] { "Office", "LibreOffice", "Notepad++", "PDF", "Word", "Excel" },
                ["Utilitaires"] = new[] { "WinRAR", "7-Zip", "CCleaner", "Malwarebytes", "TeamViewer" }
            };

            foreach (var category in categories)
            {
                if (category.Value.Any(keyword =>
                    name.Contains(keyword, StringComparison.OrdinalIgnoreCase)))
                {
                    return category.Key;
                }
            }

            return "Autre";
        }

        private List<SoftwareInfo> CleanDuplicates(List<SoftwareInfo> softwareList)
        {
            return softwareList
                .GroupBy(s => s.Name.ToLowerInvariant())
                .Select(group => group.OrderByDescending(s => !string.IsNullOrEmpty(s.Version)).First())
                .OrderBy(s => s.Name)
                .ToList();
        }
    }
}