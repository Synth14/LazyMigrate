using LazyMigrate.Services.Settings;

namespace LazyMigrate.Services
{
    /// <summary>
    /// Détecteur de settings refactorisé en services modulaires avec optimisations de performance
    /// </summary>
    public class SettingsDetector
    {
        private readonly Action<string>? _progressCallback;
        private string _logFilePath;

        // Services modulaires
        private readonly NameVariationService _nameVariationService;
        private readonly List<IPathGenerator> _pathGenerators;
        private readonly FileScannerService _fileScannerService;
        private readonly RegistryScannerService _registryScannerService;
        private readonly PathExpansionService _pathExpansionService;

        public SettingsDetector(Action<string>? progressCallback = null)
        {
            _progressCallback = progressCallback;
            _logFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SettingsDetector_Debug.txt");

            // Initialiser les services modulaires
            _nameVariationService = new NameVariationService();
            _fileScannerService = new FileScannerService(progressCallback);
            _registryScannerService = new RegistryScannerService();
            _pathExpansionService = new PathExpansionService();

            // Générateurs de chemins modulaires
            _pathGenerators = new List<IPathGenerator>
            {
                new AppDataPathGenerator(),
                new DocumentsPathGenerator(),
                new SpecialPathsGenerator(),
                new LauncherPathsGenerator()  // Nouveau générateur pour launchers
            };

            InitializeLogFile();
        }

        private void InitializeLogFile()
        {
            try
            {
                var initMessage = $"=== DEBUG SETTINGS DETECTOR - {DateTime.Now:yyyy-MM-dd HH:mm:ss} ===\n";
                initMessage += $"Chemin du fichier log: {_logFilePath}\n";
                initMessage += $"Dossier de base: {AppDomain.CurrentDomain.BaseDirectory}\n\n";

                File.WriteAllText(_logFilePath, initMessage);
                LogDebug("✅ SettingsDetector initialisé - fichier de log créé");
            }
            catch (Exception ex)
            {
                // Essayer un chemin alternatif si le premier échoue
                try
                {
                    _logFilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "SettingsDetector_Debug.txt");
                    File.WriteAllText(_logFilePath, $"=== DEBUG SETTINGS DETECTOR (Desktop) - {DateTime.Now:yyyy-MM-dd HH:mm:ss} ===\nErreur chemin original: {ex.Message}\n\n");
                    LogDebug("✅ SettingsDetector initialisé - fichier de log créé sur Desktop");
                }
                catch
                {
                    // Si même le Desktop échoue, utiliser un chemin temp
                    _logFilePath = Path.Combine(Path.GetTempPath(), "SettingsDetector_Debug.txt");
                }
            }
        }

        private void LogDebug(string message)
        {
            try
            {
                var timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
                var logMessage = $"[{timestamp}] {message}\n";
                File.AppendAllText(_logFilePath, logMessage);

                // Aussi afficher dans l'interface si possible
                _progressCallback?.Invoke(message);
            }
            catch { }
        }

        public async Task<List<SettingsFile>> DetectSettingsAsync(SoftwareInfo software)
        {
            var settingsFiles = new List<SettingsFile>();
            var softwareName = software.Name;
            var publisher = software.Publisher;

            _progressCallback?.Invoke($"🔍 Détection avancée pour {softwareName}...");

            try
            {
                // 1. Générer toutes les variations possibles du nom
                var cleanNames = _nameVariationService.GenerateNameVariations(softwareName);
                var publisherNames = _nameVariationService.GenerateNameVariations(publisher);

                // DEBUG: Afficher les variations générées pour Persona 5 Tactica
                if (softwareName.Contains("Persona", StringComparison.OrdinalIgnoreCase))
                {
                    _progressCallback?.Invoke($"  🔧 DEBUG Variations pour {softwareName}: {string.Join(", ", cleanNames.Take(10))}");
                }

                // 2. Construire tous les chemins possibles à tester avec les générateurs modulaires
                var searchPaths = new List<string>();

                foreach (var generator in _pathGenerators)
                {
                    var generatedPaths = generator.GenerateAllPaths(software, cleanNames, publisherNames);
                    searchPaths.AddRange(generatedPaths);
                }

                // DEBUG: Afficher quelques chemins SEGA/P5T pour Persona
                if (softwareName.Contains("Persona", StringComparison.OrdinalIgnoreCase))
                {
                    var segaPaths = searchPaths.Where(p => p.Contains("SEGA") || p.Contains("P5T")).Take(5);
                    _progressCallback?.Invoke($"  🔧 DEBUG Chemins SEGA: {string.Join(" | ", segaPaths)}");
                }

                // 3. Tester tous les chemins avec feedback de progression optimisé
                var foundPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var distinctPaths = searchPaths.Distinct().ToList();

                int processedCount = 0;
                foreach (var pathPattern in distinctPaths)
                {
                    try
                    {
                        if (softwareName.Contains("Persona"))
                            File.AppendAllText(_logFilePath, pathPattern);

                        var expandedPath = _pathExpansionService.ExpandWildcardPath(pathPattern);

                        // DEBUG: Tester spécifiquement les chemins SEGA pour Persona
                        if (softwareName.Contains("Persona", StringComparison.OrdinalIgnoreCase) &&
                            (pathPattern.Contains("SEGA") || pathPattern.Contains("P5T")))
                        {
                            _progressCallback?.Invoke($"  🔧 DEBUG Test: {expandedPath} - Existe: {Directory.Exists(expandedPath)}");
                        }

                        if (Directory.Exists(expandedPath))
                        {
                            var dirFiles = await _fileScannerService.ScanDirectoryForSettings(expandedPath, softwareName);
                            if (dirFiles.Any())
                            {
                                settingsFiles.AddRange(dirFiles);
                                foundPaths.Add(pathPattern);
                            }
                        }
                        else if (File.Exists(expandedPath) && _fileScannerService.IsSettingsFile(expandedPath, softwareName))
                        {
                            var fileInfo = new FileInfo(expandedPath);
                            settingsFiles.Add(new SettingsFile
                            {
                                RelativePath = Path.GetFileName(expandedPath),
                                FullPath = expandedPath,
                                Size = fileInfo.Length,
                                LastModified = fileInfo.LastWriteTime,
                                IsDirectory = false,
                                FileType = _fileScannerService.GetFileType(expandedPath)
                            });
                            foundPaths.Add(pathPattern);
                        }
                    }
                    catch
                    {
                        // Ignorer les erreurs d'accès
                    }

                    // OPTIMISATION: Feedback de progression tous les 50 chemins
                    processedCount++;
                    if (processedCount % 50 == 0)
                    {
                        _progressCallback?.Invoke($"  📂 Testé {processedCount}/{distinctPaths.Count} chemins...");
                    }
                }

                // 4. Scanner le registre Windows
                var registrySettings = await _registryScannerService.ScanRegistryForSettings(cleanNames, publisherNames);
                settingsFiles.AddRange(registrySettings);

                // 5. Recherche fuzzy globale dans les dossiers communs (avec optimisation chunks)
                var fuzzySettings = await _fileScannerService.ScanFuzzyGlobal(cleanNames, softwareName);
                settingsFiles.AddRange(fuzzySettings);

                // 6. Filtrer et prioriser les résultats
                var filteredSettings = FilterAndPrioritizeSettings(settingsFiles, software);

                var summary = GetSettingsSummary(filteredSettings);
                _progressCallback?.Invoke($"  ✅ {summary}");

                return filteredSettings;
            }
            catch (Exception ex)
            {
                _progressCallback?.Invoke($"  ❌ Erreur: {ex.Message}");
                return new List<SettingsFile>();
            }
        }

        private List<SettingsFile> FilterAndPrioritizeSettings(List<SettingsFile> settingsFiles, SoftwareInfo software)
        {
            // Filtrer les fichiers non pertinents
            var filtered = settingsFiles.Where(sf => ShouldIncludeSettingsFile(sf)).ToList();

            // Prioriser et limiter
            return filtered.OrderBy(sf => GetFilePriority(sf, software))
                          .ThenByDescending(sf => sf.LastModified)
                          .Take(15) // Max 15 fichiers par logiciel
                          .ToList();
        }

        private bool ShouldIncludeSettingsFile(SettingsFile settingsFile)
        {
            var fileName = Path.GetFileName(settingsFile.FullPath).ToLowerInvariant();

            // Exclure les fichiers temporaires et logs
            var excludeNames = new[] { "debug.log", "error.log", "crash.log", "temp.dat", "cache.dat" };
            if (excludeNames.Contains(fileName)) return false;

            // Exclure les extensions temporaires
            var excludeExts = new[] { ".tmp", ".temp", ".log", ".bak", ".old" };
            if (excludeExts.Contains(Path.GetExtension(fileName))) return false;

            // Limiter la taille
            if (settingsFile.Size > 20 * 1024 * 1024 || settingsFile.Size < 5) return false;

            return true;
        }

        private int GetFilePriority(SettingsFile settingsFile, SoftwareInfo software)
        {
            var fileName = Path.GetFileName(settingsFile.FullPath).ToLowerInvariant();

            // Priorité 1 = le plus important
            var importantFiles = new[] { "settings.json", "config.json", "preferences.json", "user.config" };
            if (importantFiles.Contains(fileName)) return 1;

            if (settingsFile.FileType == SettingsFileType.Configuration) return 2;
            if (settingsFile.FileType == SettingsFileType.UserData) return 3;
            if (settingsFile.FileType == SettingsFileType.Database) return 4;
            if (settingsFile.FileType == SettingsFileType.Registry) return 5;

            return 6;
        }

        private string GetSettingsSummary(List<SettingsFile> settingsFiles)
        {
            if (!settingsFiles.Any()) return "❌ Aucun setting";

            var totalSize = settingsFiles.Sum(sf => sf.Size);
            var configCount = settingsFiles.Count(sf => sf.FileType == SettingsFileType.Configuration);
            var dataCount = settingsFiles.Count(sf => sf.FileType == SettingsFileType.UserData);
            var dbCount = settingsFiles.Count(sf => sf.FileType == SettingsFileType.Database);
            var regCount = settingsFiles.Count(sf => sf.FileType == SettingsFileType.Registry);

            var summary = $"✅ {settingsFiles.Count} fichiers";

            var parts = new List<string>();
            if (configCount > 0) parts.Add($"{configCount} config");
            if (dataCount > 0) parts.Add($"{dataCount} data");
            if (dbCount > 0) parts.Add($"{dbCount} db");
            if (regCount > 0) parts.Add($"{regCount} reg");

            if (parts.Any()) summary += $" ({string.Join(", ", parts)})";
            summary += $" • {FormatFileSize(totalSize)}";

            return summary;
        }

        private string FormatFileSize(long bytes)
        {
            if (bytes < 1024) return $"{bytes} B";
            if (bytes < 1024 * 1024) return $"{bytes / 1024:F1} KB";
            if (bytes < 1024 * 1024 * 1024) return $"{bytes / (1024 * 1024):F1} MB";
            return $"{bytes / (1024L * 1024 * 1024):F1} GB";
        }
    }
}