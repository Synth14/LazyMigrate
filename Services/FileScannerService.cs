namespace LazyMigrate.Services
{
    /// <summary>
    /// Service pour scanner les fichiers et dossiers à la recherche de settings
    /// </summary>
    public class FileScannerService
    {
        private readonly Action<string>? _progressCallback;

        public FileScannerService(Action<string>? progressCallback = null)
        {
            _progressCallback = progressCallback;
        }

        public async Task<List<SettingsFile>> ScanDirectoryForSettings(string directoryPath, string softwareName)
        {
            var settingsFiles = new List<SettingsFile>();

            try
            {
                await Task.Run(() =>
                {
                    // Profondeur augmentée pour les jeux avec des structures complexes
                    var maxDepth = directoryPath.ToLowerInvariant().Contains("save") ? 4 : 2;
                    ScanDirectoryRecursive(directoryPath, settingsFiles, softwareName, 0, maxDepth);
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
                                          .Take(10); // Augmenté pour capturer plus de sous-dossiers

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
            var parentPath = Path.GetDirectoryName(directoryPath)?.ToLowerInvariant() ?? "";

            // Dossiers à scanner
            var importantDirs = new[] {
                "config", "settings", "user", "data", "saves", "save", "savegames",
                "profiles", "preferences", "default", "saved", "steam", "epic"
            };
            if (importantDirs.Any(important => dirName.Contains(important))) return true;

            // Scanner si le nom du dossier ressemble au logiciel
            if (dirName.Contains(softwareLower.Replace(" ", ""))) return true;

            // Scanner les dossiers qui ressemblent à des IDs utilisateur (Steam, Epic, etc.)
            if (IsUserIdDirectory(dirName)) return true;

            // Scanner les dossiers numériques dans les dossiers de sauvegarde (ex: 100, 200, 300)
            if (parentPath.Contains("save") || parentPath.Contains("savegame"))
            {
                if (dirName.All(char.IsDigit) && dirName.Length >= 1 && dirName.Length <= 5)
                {
                    return true;
                }
            }

            // Scanner les dossiers avec des noms courts dans les contextes de jeux
            if (dirName.Length <= 4 && dirName.All(c => char.IsLetterOrDigit(c)))
            {
                // Dans des contextes de jeux (SEGA, Steam, etc.)
                if (parentPath.Contains("sega") || parentPath.Contains("steam") ||
                    parentPath.Contains("epic") || parentPath.Contains("save"))
                {
                    return true;
                }
            }

            // Éviter les dossiers volumineux
            var avoidDirs = new[] { "cache", "temp", "log", "crash", "backup", "update", "installer" };
            if (avoidDirs.Any(avoid => dirName.Contains(avoid))) return false;

            return true;
        }

        private bool IsUserIdDirectory(string directoryName)
        {
            // Vérifier si c'est un ID utilisateur (Steam = 17 chiffres, Epic = format spécifique)
            if (directoryName.All(char.IsDigit))
            {
                // Steam ID (17 chiffres), ou autres IDs numériques courts
                return directoryName.Length >= 8 && directoryName.Length <= 20;
            }

            // GUID ou ID alphanumériques
            if (directoryName.Length >= 8 && directoryName.Length <= 40)
            {
                var alphaNumericCount = directoryName.Count(c => char.IsLetterOrDigit(c) || c == '-');
                return alphaNumericCount == directoryName.Length;
            }

            return false;
        }

        public bool IsSettingsFile(string filePath, string softwareName)
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

            // Extensions de sauvegarde de jeux
            var saveExtensions = new[] {
                ".sav", ".save", ".dat", ".bin", ".gam", ".sg", ".slot", ".usr", ".pro", ".profile"
            };

            if (saveExtensions.Contains(extension)) return true;

            // Noms de fichiers typiques
            var configNames = new[] {
                "settings", "preferences", "config", "options", "user", "profile",
                "bookmarks", "history", "state", "session", "workspace", "save", "data"
            };

            if (configNames.Any(name => fileName.Contains(name))) return true;

            // Fichiers de sauvegarde typiques (sans extension ou avec extensions inhabituelles)
            var saveNames = new[] {
                "save", "savegame", "savedata", "gamesave", "slot", "checkpoint", "progress", "game"
            };

            if (saveNames.Any(name => fileName.Contains(name))) return true;

            // Fichiers spécifiques au logiciel
            if (fileName.Contains(softwareLower.Replace(" ", ""))) return true;

            // Fichiers dans des dossiers de sauvegarde (même sans extension évidente)
            var directoryPath = Path.GetDirectoryName(filePath)?.ToLowerInvariant() ?? "";
            if (directoryPath.Contains("save") || directoryPath.Contains("savegame") || directoryPath.Contains("saves"))
            {
                // Dans un dossier de sauvegarde, accepter plus de types de fichiers
                if (extension == "" || // Fichiers sans extension
                    extension == ".tmp" || // Fichiers temporaires de sauvegarde
                    fileName.All(c => char.IsDigit(c) || c == '.')) // Fichiers avec noms numériques
                {
                    return true;
                }
            }

            // Fichiers avec des noms numériques dans des dossiers de sauvegarde (ex: 100, 200, 300)
            if (fileName.All(char.IsDigit) && fileName.Length >= 1 && fileName.Length <= 5)
            {
                return true;
            }

            return false;
        }

        public SettingsFileType GetFileType(string filePath)
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

        public async Task<List<SettingsFile>> ScanFuzzyGlobal(List<string> cleanNames, string originalSoftwareName)
        {
            var settingsFiles = new List<SettingsFile>();

            try
            {
                // Dossiers racines à scanner avec recherche fuzzy
                var basePaths = new[]
                {
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "My Games"),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Saved Games"),
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)
                }.Where(Directory.Exists);

                int processedPaths = 0;
                foreach (var basePath in basePaths)
                {
                    try
                    {
                        var directories = Directory.GetDirectories(basePath);

                        // Traiter par chunks de 30 pour maintenir la responsivité
                        var chunks = directories.Take(100).Chunk(30);

                        foreach (var chunk in chunks)
                        {
                            foreach (var directory in chunk)
                            {
                                var dirName = Path.GetFileName(directory);

                                // Recherche fuzzy : ce dossier correspond-il au logiciel ?
                                if (IsFuzzyMatch(dirName, cleanNames, originalSoftwareName))
                                {
                                    var dirFiles = await ScanDirectoryForSettings(directory, originalSoftwareName);
                                    if (dirFiles.Any())
                                    {
                                        settingsFiles.AddRange(dirFiles);
                                        _progressCallback?.Invoke($"  🔍 Correspondance: {Path.GetFileName(basePath)}\\{dirName}");
                                    }
                                }
                            }

                            // Petite pause pour maintenir la responsivité de l'UI
                            await Task.Delay(5);
                        }
                    }
                    catch
                    {
                        // Ignorer les erreurs d'accès aux dossiers
                    }

                    processedPaths++;
                    if (processedPaths % 2 == 0)
                    {
                        _progressCallback?.Invoke($"  📂 Scan fuzzy: {processedPaths}/{basePaths.Count()} dossiers");
                    }
                }
            }
            catch
            {
                // Ignorer les erreurs
            }

            return settingsFiles;
        }

        private bool IsFuzzyMatch(string directoryName, List<string> cleanNames, string originalSoftwareName)
        {
            var dirNameLower = directoryName.ToLowerInvariant();
            var originalLower = originalSoftwareName.ToLowerInvariant();

            // 1. Correspondance exacte avec une des variations (déjà testée normalement)
            foreach (var cleanName in cleanNames)
            {
                if (dirNameLower.Equals(cleanName.ToLowerInvariant(), StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            // 2. Le nom du dossier contient une partie significative du logiciel
            var significantWords = ExtractSignificantWords(originalLower);

            foreach (var word in significantWords)
            {
                if (dirNameLower.Contains(word) && word.Length > 3)
                    return true;
            }

            // 3. Le nom du logiciel contient le nom du dossier (ou vice versa)
            var dirWords = ExtractSignificantWords(dirNameLower);
            foreach (var dirWord in dirWords)
            {
                if (dirWord.Length > 3 && originalLower.Contains(dirWord))
                    return true;
            }

            // 4. Recherche par similarité textuelle pour les noms courts/similaires
            foreach (var cleanName in cleanNames.Take(3))
            {
                if (CalculateLevenshteinSimilarity(dirNameLower, cleanName.ToLowerInvariant()) > 0.75)
                    return true;
            }

            // 5. Patterns spéciaux (initiales, abréviations courantes)
            if (IsAbbreviationMatch(dirNameLower, originalLower))
                return true;

            return false;
        }

        private List<string> ExtractSignificantWords(string text)
        {
            // Mots à ignorer car trop génériques
            var commonWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "game", "games", "software", "app", "application", "program", "tool", "tools",
                "studio", "studios", "edition", "version", "the", "and", "or", "for", "with",
                "inc", "llc", "corp", "corporation", "ltd", "limited", "co", "company"
            };

            return text.Split(new[] { ' ', '-', '_', '.', '(', ')', '[', ']' }, StringSplitOptions.RemoveEmptyEntries)
                      .Where(word => word.Length > 2 && !commonWords.Contains(word))
                      .Select(word => word.ToLowerInvariant())
                      .Distinct()
                      .ToList();
        }

        private double CalculateLevenshteinSimilarity(string text1, string text2)
        {
            if (string.IsNullOrEmpty(text1) || string.IsNullOrEmpty(text2))
                return 0;

            var maxLength = Math.Max(text1.Length, text2.Length);
            if (maxLength == 0) return 1;

            var distance = CalculateLevenshteinDistance(text1, text2);
            return 1.0 - (double)distance / maxLength;
        }

        private int CalculateLevenshteinDistance(string text1, string text2)
        {
            var matrix = new int[text1.Length + 1, text2.Length + 1];

            for (int i = 0; i <= text1.Length; i++)
                matrix[i, 0] = i;

            for (int j = 0; j <= text2.Length; j++)
                matrix[0, j] = j;

            for (int i = 1; i <= text1.Length; i++)
            {
                for (int j = 1; j <= text2.Length; j++)
                {
                    var cost = text1[i - 1] == text2[j - 1] ? 0 : 1;
                    matrix[i, j] = Math.Min(
                        Math.Min(matrix[i - 1, j] + 1, matrix[i, j - 1] + 1),
                        matrix[i - 1, j - 1] + cost);
                }
            }

            return matrix[text1.Length, text2.Length];
        }

        private bool IsAbbreviationMatch(string directoryName, string softwareName)
        {
            // Vérifier si le nom du dossier pourrait être une abréviation
            if (directoryName.Length < softwareName.Length / 3) // Le dossier est beaucoup plus court
            {
                var softwareWords = ExtractSignificantWords(softwareName);
                var initials = string.Join("", softwareWords.Select(w => w.FirstOrDefault()));

                if (directoryName.Equals(initials, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }
    }
}