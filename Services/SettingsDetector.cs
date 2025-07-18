namespace LazyMigrate.Services
{
    public class SettingsDetector
    {
        private readonly Action<string>? _progressCallback;

        public SettingsDetector(Action<string>? progressCallback = null)
        {
            _progressCallback = progressCallback;
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
                var cleanNames = GenerateNameVariations(softwareName);
                var publisherNames = GenerateNameVariations(publisher);

                // 2. Construire tous les chemins possibles à tester
                var searchPaths = new List<string>();

                searchPaths.AddRange(GenerateAppDataPaths(cleanNames, publisherNames));
                searchPaths.AddRange(GenerateLocalAppDataPaths(cleanNames, publisherNames));
                searchPaths.AddRange(GenerateUserProfilePaths(cleanNames));
                searchPaths.AddRange(GenerateDocumentsPaths(cleanNames, publisherNames));
                searchPaths.AddRange(GenerateProgramFilesPaths(software.InstallPath, cleanNames));
                searchPaths.AddRange(GenerateSpecializedPaths(software, cleanNames));

                // 3. Tester tous les chemins
                var foundPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var totalPaths = searchPaths.Distinct().Count();

                foreach (var pathPattern in searchPaths.Distinct())
                {
                    try
                    {
                        var expandedPath = ExpandEnvironmentPath(pathPattern);

                        if (Directory.Exists(expandedPath))
                        {
                            var dirFiles = await ScanDirectoryForSettings(expandedPath, softwareName);
                            if (dirFiles.Any())
                            {
                                settingsFiles.AddRange(dirFiles);
                                foundPaths.Add(pathPattern);
                            }
                        }
                        else if (File.Exists(expandedPath) && IsSettingsFile(expandedPath, softwareName))
                        {
                            var fileInfo = new FileInfo(expandedPath);
                            settingsFiles.Add(new SettingsFile
                            {
                                RelativePath = Path.GetFileName(expandedPath),
                                FullPath = expandedPath,
                                Size = fileInfo.Length,
                                LastModified = fileInfo.LastWriteTime,
                                IsDirectory = false,
                                FileType = GetFileType(expandedPath)
                            });
                            foundPaths.Add(pathPattern);
                        }
                    }
                    catch
                    {
                        // Ignorer les erreurs d'accès
                    }
                }

                // 4. Scanner le registre Windows
                var registrySettings = await ScanRegistryForSettings(cleanNames, publisherNames);
                settingsFiles.AddRange(registrySettings);

                // 5. Filtrer et prioriser les résultats
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

        private List<string> GenerateNameVariations(string name)
        {
            if (string.IsNullOrEmpty(name)) return new List<string>();

            var variations = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // Nom original
            variations.Add(name);

            // Supprimer parenthèses et contenu
            var withoutParens = Regex.Replace(name, @"\([^)]*\)", "").Trim();
            if (!string.IsNullOrEmpty(withoutParens)) variations.Add(withoutParens);

            // Supprimer versions et numéros
            var withoutVersion = Regex.Replace(withoutParens, @"\s+\d+(\.\d+)*", "").Trim();
            if (!string.IsNullOrEmpty(withoutVersion)) variations.Add(withoutVersion);

            // Supprimer mots courants
            var commonWords = new[] { "Microsoft", "Google", "LLC", "Inc", "Corporation", "Corp", "Ltd", "Software", "App", "Application", "Studio", "Studios", "Games", "Team" };
            var cleanName = withoutVersion;
            foreach (var word in commonWords)
            {
                cleanName = Regex.Replace(cleanName, $@"\b{Regex.Escape(word)}\b", "", RegexOptions.IgnoreCase).Trim();
            }
            if (!string.IsNullOrEmpty(cleanName)) variations.Add(cleanName);

            // Générer variations de formatage
            var baseNames = variations.ToList();
            foreach (var baseName in baseNames)
            {
                if (string.IsNullOrEmpty(baseName)) continue;

                variations.Add(baseName.Replace(" ", ""));      // Sans espaces
                variations.Add(baseName.Replace(" ", "_"));     // Avec underscores
                variations.Add(baseName.Replace(" ", "-"));     // Avec tirets
                variations.Add(baseName.ToLowerInvariant());    // Lowercase

                var firstWord = baseName.Split(' ').First().Trim();
                if (firstWord.Length > 2) variations.Add(firstWord);

                var words = baseName.Split(' ');
                if (words.Length > 1)
                {
                    var lastWord = words.Last().Trim();
                    if (lastWord.Length > 2) variations.Add(lastWord);
                }
            }

            return variations.Where(v => !string.IsNullOrEmpty(v) && v.Length > 1).ToList();
        }

        private List<string> GenerateAppDataPaths(List<string> cleanNames, List<string> publisherNames)
        {
            var paths = new List<string>();

            foreach (var name in cleanNames)
            {
                // Patterns directs
                paths.Add($@"%APPDATA%\{name}");
                paths.Add($@"%APPDATA%\{name}\config");
                paths.Add($@"%APPDATA%\{name}\settings");
                paths.Add($@"%APPDATA%\{name}\user");
                paths.Add($@"%APPDATA%\{name}\preferences");
                paths.Add($@"%APPDATA%\{name}\data");
                paths.Add($@"%APPDATA%\{name}\saves");

                // Avec éditeur
                foreach (var publisher in publisherNames.Take(3))
                {
                    paths.Add($@"%APPDATA%\{publisher}\{name}");
                    paths.Add($@"%APPDATA%\{publisher}\{name}\config");
                    paths.Add($@"%APPDATA%\{publisher}");
                }

                // Fichiers de config directs
                var configExts = new[] { ".conf", ".config", ".ini", ".json", ".xml", ".cfg", ".yaml", ".yml" };
                foreach (var ext in configExts)
                {
                    paths.Add($@"%APPDATA%\{name}{ext}");
                }
            }

            return paths;
        }

        private List<string> GenerateLocalAppDataPaths(List<string> cleanNames, List<string> publisherNames)
        {
            var paths = new List<string>();

            foreach (var name in cleanNames)
            {
                // Patterns standards
                paths.Add($@"%LOCALAPPDATA%\{name}");
                paths.Add($@"%LOCALAPPDATA%\{name}\User Data");
                paths.Add($@"%LOCALAPPDATA%\{name}\user");
                paths.Add($@"%LOCALAPPDATA%\{name}\config");
                paths.Add($@"%LOCALAPPDATA%\{name}\settings");

                // Patterns navigateurs/applications modernes
                paths.Add($@"%LOCALAPPDATA%\{name}\User Data\Default");
                paths.Add($@"%LOCALAPPDATA%\{name}\Profiles");

                // Avec éditeur
                foreach (var publisher in publisherNames.Take(3))
                {
                    paths.Add($@"%LOCALAPPDATA%\{publisher}\{name}");
                    paths.Add($@"%LOCALAPPDATA%\{publisher}");
                }

                // Packages Windows Store
                paths.Add($@"%LOCALAPPDATA%\Packages\{name}");
                paths.Add($@"%LOCALAPPDATA%\Packages\*{name}*\LocalState");
            }

            return paths;
        }

        private List<string> GenerateUserProfilePaths(List<string> cleanNames)
        {
            var paths = new List<string>();

            foreach (var name in cleanNames)
            {
                var lowerName = name.ToLowerInvariant();

                // Dotfiles Unix-style
                paths.Add($@"%USERPROFILE%\.{lowerName}");
                paths.Add($@"%USERPROFILE%\.{lowerName}rc");
                paths.Add($@"%USERPROFILE%\.config\{lowerName}");

                // Fichiers de config dans le profil
                var configExts = new[] { ".conf", ".config", ".ini", ".json", ".xml" };
                foreach (var ext in configExts)
                {
                    paths.Add($@"%USERPROFILE%\{lowerName}{ext}");
                    paths.Add($@"%USERPROFILE%\.{lowerName}{ext}");
                }

                // Dossiers dans user profile
                paths.Add($@"%USERPROFILE%\{name}");
                paths.Add($@"%USERPROFILE%\.{name}");
            }

            return paths;
        }

        private List<string> GenerateDocumentsPaths(List<string> cleanNames, List<string> publisherNames)
        {
            var paths = new List<string>();

            foreach (var name in cleanNames)
            {
                // Documents directs
                paths.Add($@"%USERPROFILE%\Documents\{name}");
                paths.Add($@"%USERPROFILE%\Documents\My {name}");

                // My Games - dossier standard pour les sauvegardes de jeux
                paths.Add($@"%USERPROFILE%\Documents\My Games\{name}");
                paths.Add($@"%USERPROFILE%\Documents\My Games\{name}\Saves");
                paths.Add($@"%USERPROFILE%\Documents\My Games\{name}\SaveGames");
                paths.Add($@"%USERPROFILE%\Documents\My Games\{name}\Config");
                paths.Add($@"%USERPROFILE%\Documents\My Games\{name}\Settings");

                // Saved Games (dossier Windows Vista+)
                paths.Add($@"%USERPROFILE%\Saved Games\{name}");
                paths.Add($@"%USERPROFILE%\Saved Games\{name}\Saves");
                paths.Add($@"%USERPROFILE%\Saved Games\{name}\Profiles");

                // Autres patterns de sauvegardes
                paths.Add($@"%USERPROFILE%\Documents\{name}\Saves");
                paths.Add($@"%USERPROFILE%\Documents\{name}\SaveGames");
                paths.Add($@"%USERPROFILE%\Documents\{name}\Profiles");
                paths.Add($@"%USERPROFILE%\Documents\{name}\Config");
                paths.Add($@"%USERPROFILE%\Documents\{name}\Settings");
                paths.Add($@"%USERPROFILE%\Documents\{name}\Data");

                // Avec éditeur
                foreach (var publisher in publisherNames.Take(2))
                {
                    paths.Add($@"%USERPROFILE%\Documents\{publisher}\{name}");
                    paths.Add($@"%USERPROFILE%\Documents\{publisher}");
                    paths.Add($@"%USERPROFILE%\Documents\My Games\{publisher}\{name}");
                    paths.Add($@"%USERPROFILE%\Saved Games\{publisher}\{name}");
                }
            }

            return paths;
        }

        private List<string> GenerateProgramFilesPaths(string installPath, List<string> cleanNames)
        {
            var paths = new List<string>();

            if (!string.IsNullOrEmpty(installPath) && Directory.Exists(installPath))
            {
                // Config près de l'exe (applications portables)
                paths.Add(Path.Combine(installPath, "config"));
                paths.Add(Path.Combine(installPath, "settings"));
                paths.Add(Path.Combine(installPath, "data"));
                paths.Add(Path.Combine(installPath, "user"));

                // Fichiers ini/config dans le dossier d'installation
                foreach (var name in cleanNames.Take(3))
                {
                    var configExts = new[] { ".ini", ".config", ".conf", ".cfg", ".json", ".xml" };
                    foreach (var ext in configExts)
                    {
                        paths.Add(Path.Combine(installPath, $"{name}{ext}"));
                    }
                }
            }

            return paths;
        }

        private List<string> GenerateSpecializedPaths(SoftwareInfo software, List<string> cleanNames)
        {
            var paths = new List<string>();
            var category = software.Category?.ToLowerInvariant() ?? "";
            var name = software.Name.ToLowerInvariant();

            // Jeux Steam
            if (name.Contains("steam") || category.Contains("jeu"))
            {
                paths.Add(@"%PROGRAMFILES(X86)%\Steam\userdata");
                paths.Add(@"%PROGRAMFILES%\Steam\userdata");
            }

            // Détection heuristique de jeux (sans dépendre de la catégorie)
            var gameIndicators = new[] { "game", "games", "play", "studio", "entertainment", "interactive" };
            var isLikelyGame = gameIndicators.Any(indicator => name.Contains(indicator)) ||
                             category.Contains("jeu") || category.Contains("game");

            if (isLikelyGame)
            {
                foreach (var cleanName in cleanNames.Take(3))
                {
                    // Emplacements courants pour les jeux
                    paths.Add($@"%USERPROFILE%\Documents\My Games\{cleanName}");
                    paths.Add($@"%USERPROFILE%\Saved Games\{cleanName}");
                    paths.Add($@"%APPDATA%\{cleanName}\Saves");
                    paths.Add($@"%LOCALAPPDATA%\{cleanName}\Saved Games");

                    // Epic Games Store
                    paths.Add($@"%LOCALAPPDATA%\EpicGamesLauncher\Saved\SaveGames");

                    // Origin
                    paths.Add($@"%USERPROFILE%\Documents\EA Games\{cleanName}");

                    // Ubisoft Connect
                    paths.Add($@"%USERPROFILE%\Documents\My Games\{cleanName}");
                }
            }

            // Navigateurs
            if (name.Contains("chrome") || name.Contains("firefox") || name.Contains("edge") || name.Contains("browser"))
            {
                foreach (var cleanName in cleanNames.Take(2))
                {
                    paths.Add($@"%LOCALAPPDATA%\{cleanName}\User Data\Default");
                    paths.Add($@"%APPDATA%\{cleanName}\Profiles");
                }
            }

            // Développement (IDE, éditeurs de code)
            var devIndicators = new[] { "visual studio", "code", "ide", "studio", "intellij", "eclipse", "atom", "sublime" };
            if (devIndicators.Any(indicator => name.Contains(indicator)) || category.Contains("développement"))
            {
                foreach (var cleanName in cleanNames.Take(2))
                {
                    paths.Add($@"%APPDATA%\{cleanName}\User");
                    paths.Add($@"%USERPROFILE%\.{cleanName.ToLowerInvariant()}");
                    paths.Add($@"%USERPROFILE%\.config\{cleanName.ToLowerInvariant()}");
                }
            }

            // Communication (Discord, Teams, Slack, etc.)
            var commIndicators = new[] { "discord", "teams", "slack", "skype", "zoom", "telegram", "whatsapp" };
            if (commIndicators.Any(indicator => name.Contains(indicator)) || category.Contains("communication"))
            {
                foreach (var cleanName in cleanNames.Take(2))
                {
                    paths.Add($@"%APPDATA%\{cleanName}");
                    paths.Add($@"%LOCALAPPDATA%\{cleanName}");
                }
            }

            // Multimédia (Adobe, OBS, VLC, etc.)
            var mediaIndicators = new[] { "adobe", "photoshop", "premiere", "obs", "vlc", "media", "player", "studio" };
            if (mediaIndicators.Any(indicator => name.Contains(indicator)) || category.Contains("multimédia"))
            {
                foreach (var cleanName in cleanNames.Take(2))
                {
                    paths.Add($@"%APPDATA%\Adobe\{cleanName}");
                    paths.Add($@"%USERPROFILE%\Documents\Adobe\{cleanName}");
                    paths.Add($@"%APPDATA%\{cleanName}");
                }
            }

            return paths;
        }

        private async Task<List<SettingsFile>> ScanDirectoryForSettings(string directoryPath, string softwareName)
        {
            var settingsFiles = new List<SettingsFile>();

            try
            {
                await Task.Run(() =>
                {
                    ScanDirectoryRecursive(directoryPath, settingsFiles, softwareName, 0, 2);
                });
            }
            catch
            {
                // Ignorer les erreurs d'accès
            }

            return settingsFiles;
        }

        private void ScanDirectoryRecursive(string directoryPath, List<SettingsFile> settingsFiles, string softwareName, int depth, int maxDepth)
        {
            if (depth > maxDepth || settingsFiles.Count > 50) return;

            try
            {
                // Scanner les fichiers du dossier courant
                var files = Directory.GetFiles(directoryPath)
                                   .Where(f => IsSettingsFile(f, softwareName))
                                   .Take(20);

                foreach (var file in files)
                {
                    try
                    {
                        var fileInfo = new FileInfo(file);
                        if (fileInfo.Length < 50 * 1024 * 1024) // Max 50MB
                        {
                            settingsFiles.Add(new SettingsFile
                            {
                                RelativePath = Path.GetRelativePath(directoryPath, file),
                                FullPath = file,
                                Size = fileInfo.Length,
                                LastModified = fileInfo.LastWriteTime,
                                IsDirectory = false,
                                FileType = GetFileType(file)
                            });
                        }
                    }
                    catch
                    {
                        // Ignorer les erreurs sur fichiers individuels
                    }
                }

                // Scanner les sous-dossiers pertinents
                if (depth < maxDepth)
                {
                    var subdirs = Directory.GetDirectories(directoryPath)
                                          .Where(d => ShouldScanSubdirectory(d, softwareName))
                                          .Take(5);

                    foreach (var subdir in subdirs)
                    {
                        ScanDirectoryRecursive(subdir, settingsFiles, softwareName, depth + 1, maxDepth);
                    }
                }
            }
            catch
            {
                // Ignorer les erreurs d'accès aux dossiers
            }
        }

        private bool ShouldScanSubdirectory(string directoryPath, string softwareName)
        {
            var dirName = Path.GetFileName(directoryPath).ToLowerInvariant();
            var softwareLower = softwareName.ToLowerInvariant();

            // Dossiers à scanner
            var importantDirs = new[] { "config", "settings", "user", "data", "saves", "profiles", "preferences", "default" };
            if (importantDirs.Any(important => dirName.Contains(important))) return true;

            // Scanner si le nom du dossier ressemble au logiciel
            if (dirName.Contains(softwareLower.Replace(" ", ""))) return true;

            // Éviter les dossiers volumineux
            var avoidDirs = new[] { "cache", "temp", "log", "crash", "backup", "update" };
            if (avoidDirs.Any(avoid => dirName.Contains(avoid))) return false;

            return true;
        }

        private async Task<List<SettingsFile>> ScanRegistryForSettings(List<string> cleanNames, List<string> publisherNames)
        {
            var settingsFiles = new List<SettingsFile>();

            try
            {
                await Task.Run(() =>
                {
                    ScanRegistryHive(Registry.CurrentUser, @"Software", cleanNames, publisherNames, settingsFiles);
                });
            }
            catch
            {
                // Ignorer les erreurs d'accès au registre
            }

            return settingsFiles;
        }

        private void ScanRegistryHive(RegistryKey baseKey, string subKeyPath, List<string> cleanNames, List<string> publisherNames, List<SettingsFile> settingsFiles)
        {
            try
            {
                using var softwareKey = baseKey.OpenSubKey(subKeyPath);
                if (softwareKey == null) return;

                // Chercher par nom de logiciel
                foreach (var name in cleanNames.Take(3))
                {
                    try
                    {
                        using var appKey = softwareKey.OpenSubKey(name);
                        if (appKey != null && appKey.GetValueNames().Length > 0)
                        {
                            settingsFiles.Add(new SettingsFile
                            {
                                RelativePath = $"Registry: {name}",
                                FullPath = $@"{baseKey.Name}\{subKeyPath}\{name}",
                                Size = appKey.GetValueNames().Length * 100,
                                LastModified = DateTime.Now,
                                IsDirectory = false,
                                FileType = SettingsFileType.Registry
                            });
                        }
                    }
                    catch { }
                }
            }
            catch
            {
                // Ignorer les erreurs d'accès
            }
        }

        private bool IsSettingsFile(string filePath, string softwareName)
        {
            var fileName = Path.GetFileName(filePath).ToLowerInvariant();
            var extension = Path.GetExtension(filePath).ToLowerInvariant();
            var softwareLower = softwareName.ToLowerInvariant();

            // Extensions de configuration connues
            var configExtensions = new[] {
                ".json", ".xml", ".ini", ".conf", ".config", ".cfg", ".yaml", ".yml",
                ".toml", ".plist", ".reg", ".dat", ".db", ".sqlite", ".sqlite3",
                ".properties", ".settings", ".prefs"
            };

            if (configExtensions.Contains(extension)) return true;

            // Noms de fichiers typiques
            var configNames = new[] {
                "settings", "preferences", "config", "options", "user", "profile",
                "bookmarks", "history", "state", "session", "workspace", "save", "data"
            };

            if (configNames.Any(name => fileName.Contains(name))) return true;

            // Fichiers spécifiques au logiciel
            if (fileName.Contains(softwareLower.Replace(" ", ""))) return true;

            return false;
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

        private SettingsFileType GetFileType(string filePath)
        {
            var extension = Path.GetExtension(filePath).ToLowerInvariant();
            return extension switch
            {
                ".json" or ".xml" or ".ini" or ".config" or ".conf" or ".cfg" or ".yaml" or ".yml" => SettingsFileType.Configuration,
                ".db" or ".sqlite" or ".sqlite3" => SettingsFileType.Database,
                ".reg" => SettingsFileType.Registry,
                _ => SettingsFileType.UserData
            };
        }

        private string ExpandEnvironmentPath(string path)
        {
            var expanded = Environment.ExpandEnvironmentVariables(path);
            expanded = expanded.Replace("%PROGRAMFILES(X86)%", Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86));
            expanded = expanded.Replace("%PROGRAMFILES%", Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles));
            expanded = expanded.Replace("%APPDATA%", Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData));
            expanded = expanded.Replace("%LOCALAPPDATA%", Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData));
            expanded = expanded.Replace("%USERPROFILE%", Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
            return expanded;
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