namespace QuickMigrate.Services
{
    public class PreciseSettingsDetector
    {
        private readonly Action<string>? _progressCallback;

        private readonly string[] _excludePatterns = new[]
        {
            "cache", "Cache", "logs", "Logs", "temp", "Temp", "tmp", "backup", "crash",
            "GPUCache", "Code Cache", "ShaderCache", "DawnCache", "FontCache",
            "CachedData", "CachedExtensions", "workspaceStorage", "globalStorage",
            "installation", "uninstall", "update", "downloads", "Downloads"
        };
        private readonly string[] _excludeExtensions = new[]
        {
            ".log", ".tmp", ".temp", ".bak", ".old", ".cache", ".lock", ".pid",
            ".exe", ".dll", ".msi", ".zip", ".rar", ".7z", ".dmp", ".crash"
        };
        private readonly string[] _highValueTargets = new[]
        {
            "settings.json", "preferences.json", "config.json", "user.json",
            "bookmarks", "Bookmarks", "settings.xml", "config.xml",
            "user.config", "app.config", "preferences.plist",
            ".gitconfig", ".ssh", "settings.ini", "config.ini"
        };
        private readonly string[] _highValueFolders = new[]
        {
            "User", "user", "Settings", "settings", "Config", "config",
            "Preferences", "preferences", "profiles", "Profiles"
        };

        public PreciseSettingsDetector(Action<string>? progressCallback = null)
        {
            _progressCallback = progressCallback;
        }

        public async Task<List<SettingsFile>> DetectSettingsAsync(SoftwareInfo software)
        {
            var settingsFiles = new List<SettingsFile>();
            var softwareName = software.Name;

            ReportProgress($"🎯 Analyse précise pour {softwareName}...");

            // 1. Générer les noms nettoyés
            var cleanNames = GenerateCleanNames(softwareName);
            var publisherNames = GenerateCleanNames(software.Publisher);

            // 2. Générer seulement les chemins les plus probables
            var priorityPaths = GeneratePriorityPaths(cleanNames, publisherNames, software.InstallPath);

            // 3. Tester et scorer chaque chemin
            var scoredFindings = new List<(SettingsFile file, int score)>();

            foreach (var path in priorityPaths)
            {
                try
                {
                    var expandedPath = ExpandEnvironmentPath(path);

                    if (Directory.Exists(expandedPath))
                    {
                        var dirFindings = await AnalyzeDirectory(expandedPath, softwareName);
                        scoredFindings.AddRange(dirFindings);
                    }
                    else if (File.Exists(expandedPath))
                    {
                        var fileScore = ScoreFile(expandedPath, softwareName);
                        if (fileScore >= 50) // Seuil minimum
                        {
                            var fileInfo = new FileInfo(expandedPath);
                            var settingsFile = new SettingsFile
                            {
                                RelativePath = Path.GetFileName(expandedPath),
                                FullPath = expandedPath,
                                Size = fileInfo.Length,
                                LastModified = fileInfo.LastWriteTime,
                                IsDirectory = false,
                                FileType = GetFileType(expandedPath)
                            };
                            scoredFindings.Add((settingsFile, fileScore));
                        }
                    }
                }
                catch
                {
                    // Ignorer les erreurs d'accès
                }
            }

            // 4. Garder seulement les meilleurs résultats
            var bestFindings = scoredFindings
                .Where(f => f.score >= 60) // Seuil de qualité
                .OrderByDescending(f => f.score)
                .Take(10) // Max 10 fichiers par logiciel
                .Select(f => f.file)
                .ToList();

            if (bestFindings.Any())
            {
                ReportProgress($"  ✅ {bestFindings.Count} settings de qualité trouvés");
            }

            return bestFindings;
        }

        private List<string> GeneratePriorityPaths(List<string> cleanNames, List<string> publisherNames, string installPath)
        {
            var paths = new List<string>();

            foreach (var name in cleanNames.Take(3)) // Limiter aux 3 meilleurs noms
            {
                // 1. AppData - Patterns les plus courants
                paths.Add($"%APPDATA%\\{name}");
                paths.Add($"%APPDATA%\\{name}\\User");
                paths.Add($"%APPDATA%\\{name}\\settings.json");
                paths.Add($"%APPDATA%\\{name}\\config.json");
                paths.Add($"%APPDATA%\\{name}\\preferences.json");

                // 2. LocalAppData - Browsers et apps modernes
                paths.Add($"%LOCALAPPDATA%\\{name}");
                paths.Add($"%LOCALAPPDATA%\\{name}\\User Data");
                paths.Add($"%LOCALAPPDATA%\\{name}\\User Data\\Default");

                // 3. UserProfile - Dotfiles style
                paths.Add($"%USERPROFILE%\\.{name.ToLower()}");
                paths.Add($"%USERPROFILE%\\.{name.ToLower()}rc");
                paths.Add($"%USERPROFILE%\\.config\\{name.ToLower()}");

                // 4. Documents - User data
                paths.Add($"%USERPROFILE%\\Documents\\{name}");

                // 5. Avec publisher (que si différent du nom)
                foreach (var publisher in publisherNames.Take(2))
                {
                    if (!cleanNames.Contains(publisher, StringComparer.OrdinalIgnoreCase))
                    {
                        paths.Add($"%APPDATA%\\{publisher}\\{name}");
                        paths.Add($"%LOCALAPPDATA%\\{publisher}\\{name}");
                    }
                }
            }

            // 6. Program Files (configs portables)
            if (!string.IsNullOrEmpty(installPath) && Directory.Exists(installPath))
            {
                paths.Add(Path.Combine(installPath, "config"));
                paths.Add(Path.Combine(installPath, "settings"));
                paths.Add(Path.Combine(installPath, "user"));
            }

            return paths.Distinct().ToList();
        }

        private async Task<List<(SettingsFile, int)>> AnalyzeDirectory(string directoryPath, string softwareName)
        {
            var findings = new List<(SettingsFile, int)>();

            await Task.Run(() =>
            {
                try
                {
                    // 1. Vérifier si le dossier lui-même est exclu
                    if (IsExcludedPath(directoryPath))
                        return;

                    // 2. Scanner les fichiers du dossier principal
                    var mainFiles = Directory.GetFiles(directoryPath)
                                           .Where(f => !IsExcludedFile(f))
                                           .Take(20);

                    foreach (var file in mainFiles)
                    {
                        var score = ScoreFile(file, softwareName);
                        if (score >= 50)
                        {
                            var fileInfo = new FileInfo(file);
                            if (fileInfo.Length < 50 * 1024 * 1024) // Max 50MB
                            {
                                findings.Add((new SettingsFile
                                {
                                    RelativePath = Path.GetRelativePath(directoryPath, file),
                                    FullPath = file,
                                    Size = fileInfo.Length,
                                    LastModified = fileInfo.LastWriteTime,
                                    IsDirectory = false,
                                    FileType = GetFileType(file)
                                }, score));
                            }
                        }
                    }

                    // 3. Scanner les sous-dossiers importants seulement
                    var subdirs = Directory.GetDirectories(directoryPath)
                                          .Where(d => !IsExcludedPath(d))
                                          .Where(d => _highValueFolders.Any(hvf =>
                                              Path.GetFileName(d).Contains(hvf, StringComparison.OrdinalIgnoreCase)))
                                          .Take(3);

                    foreach (var subdir in subdirs)
                    {
                        var subdirFiles = Directory.GetFiles(subdir, "*", SearchOption.TopDirectoryOnly)
                                                  .Where(f => !IsExcludedFile(f))
                                                  .Take(10);

                        foreach (var file in subdirFiles)
                        {
                            var score = ScoreFile(file, softwareName) + 10; // Bonus pour sous-dossier important
                            if (score >= 60)
                            {
                                var fileInfo = new FileInfo(file);
                                if (fileInfo.Length < 20 * 1024 * 1024) // Max 20MB pour sous-dossiers
                                {
                                    findings.Add((new SettingsFile
                                    {
                                        RelativePath = Path.GetRelativePath(directoryPath, file),
                                        FullPath = file,
                                        Size = fileInfo.Length,
                                        LastModified = fileInfo.LastWriteTime,
                                        IsDirectory = false,
                                        FileType = GetFileType(file)
                                    }, score));
                                }
                            }
                        }
                    }
                }
                catch
                {
                    // Ignorer erreurs d'accès
                }
            });

            return findings;
        }

        private int ScoreFile(string filePath, string softwareName)
        {
            var fileName = Path.GetFileName(filePath).ToLowerInvariant();
            var extension = Path.GetExtension(filePath).ToLowerInvariant();
            var score = 0;

            // 1. Fichiers haute valeur (settings.json, etc.)
            if (_highValueTargets.Any(hvt => fileName.Contains(hvt.ToLowerInvariant())))
                score += 100;

            // 2. Extensions de config
            var goodExtensions = new[] { ".json", ".xml", ".ini", ".conf", ".config", ".yaml", ".yml", ".plist" };
            if (goodExtensions.Contains(extension))
                score += 50;

            // 3. Noms typiques
            var goodNames = new[] { "settings", "config", "preferences", "options", "user", "profile", "bookmarks" };
            if (goodNames.Any(name => fileName.Contains(name)))
                score += 30;

            // 4. Contient le nom du logiciel
            var cleanSoftware = softwareName.ToLowerInvariant().Replace(" ", "");
            if (fileName.Contains(cleanSoftware) || fileName.Contains(softwareName.ToLowerInvariant()))
                score += 20;

            // 5. Pénalités pour mauvais patterns
            if (_excludeExtensions.Contains(extension))
                score -= 100;

            if (_excludePatterns.Any(pattern => fileName.Contains(pattern.ToLowerInvariant())))
                score -= 50;

            // 6. Taille raisonnable (ni trop petit ni trop gros)
            try
            {
                var fileInfo = new FileInfo(filePath);
                if (fileInfo.Length > 0 && fileInfo.Length < 10 * 1024 * 1024) // Entre 0 et 10MB
                    score += 10;
                else if (fileInfo.Length > 100 * 1024 * 1024) // Plus de 100MB = suspect
                    score -= 30;
            }
            catch { }

            return Math.Max(0, score);
        }

        private bool IsExcludedPath(string path)
        {
            var pathLower = path.ToLowerInvariant();
            return _excludePatterns.Any(pattern => pathLower.Contains(pattern.ToLowerInvariant()));
        }

        private bool IsExcludedFile(string filePath)
        {
            var fileName = Path.GetFileName(filePath).ToLowerInvariant();
            var extension = Path.GetExtension(filePath).ToLowerInvariant();

            return _excludeExtensions.Contains(extension) ||
                   _excludePatterns.Any(pattern => fileName.Contains(pattern.ToLowerInvariant()));
        }

        private List<string> GenerateCleanNames(string name)
        {
            if (string.IsNullOrEmpty(name)) return new List<string>();

            var variations = new List<string> { name };

            // Supprimer parenthèses
            var withoutParens = System.Text.RegularExpressions.Regex.Replace(name, @"\([^)]*\)", "").Trim();
            if (withoutParens != name) variations.Add(withoutParens);

            // Supprimer versions et mots courants
            var withoutVersion = System.Text.RegularExpressions.Regex.Replace(withoutParens, @"\s+\d+(\.\d+)*", "").Trim();
            var commonWords = new[] { "Microsoft", "Google", "LLC", "Inc", "Corporation", "Software" };

            foreach (var word in commonWords)
            {
                withoutVersion = withoutVersion.Replace(word, "").Trim();
            }

            if (!string.IsNullOrEmpty(withoutVersion))
                variations.Add(withoutVersion);

            // Variations format
            foreach (var variation in variations.ToList())
            {
                variations.Add(variation.Replace(" ", ""));
                variations.Add(variation.Replace(" ", "_"));
            }

            return variations.Where(v => !string.IsNullOrEmpty(v) && v.Length > 1)
                           .Distinct(StringComparer.OrdinalIgnoreCase)
                           .Take(5) // Limiter à 5 variations max
                           .ToList();
        }

        private SettingsFileType GetFileType(string filePath)
        {
            var extension = Path.GetExtension(filePath).ToLowerInvariant();
            return extension switch
            {
                ".json" or ".xml" or ".ini" or ".config" or ".conf" or ".yaml" or ".yml" => SettingsFileType.Configuration,
                ".db" or ".sqlite" or ".sqlite3" => SettingsFileType.Database,
                ".reg" => SettingsFileType.Registry,
                ".plist" => SettingsFileType.Configuration,
                _ => SettingsFileType.UserData
            };
        }

        private string ExpandEnvironmentPath(string path)
        {
            var expanded = Environment.ExpandEnvironmentVariables(path);
            expanded = expanded.Replace("%APPDATA%", Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData));
            expanded = expanded.Replace("%LOCALAPPDATA%", Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData));
            expanded = expanded.Replace("%USERPROFILE%", Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
            return expanded;
        }

        private void ReportProgress(string message)
        {
            _progressCallback?.Invoke(message);
        }
    }
}