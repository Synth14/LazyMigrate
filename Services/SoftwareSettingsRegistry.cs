
using System.Text.RegularExpressions;

namespace QuickMigrate.Services
{
    public class SoftwareSettingsRegistry
    {
        private readonly Dictionary<string, SoftwareSettingsProfile> _profiles;
        private readonly IProgress<string>? _progress;

        public SoftwareSettingsRegistry(IProgress<string>? progress = null)
        {
            _progress = progress;
            _profiles = new Dictionary<string, SoftwareSettingsProfile>(StringComparer.OrdinalIgnoreCase);
            InitializeProfiles();
        }

        private void InitializeProfiles()
        {
            // Visual Studio Code
            _profiles["Visual Studio Code"] = new SoftwareSettingsProfile
            {
                SoftwareName = "Visual Studio Code",
                AlternativeNames = new List<string> { "VS Code", "Code" },
                ConfigPaths = new List<SettingsPath>
                {
                    new() { Path = "%APPDATA%\\Code\\User\\settings.json", Type = SettingsPathType.AppData, Description = "Configuration principale", Priority = 1 },
                    new() { Path = "%APPDATA%\\Code\\User\\keybindings.json", Type = SettingsPathType.AppData, Description = "Raccourcis clavier", Priority = 1 },
                    new() { Path = "%APPDATA%\\Code\\User\\snippets", Type = SettingsPathType.AppData, Description = "Snippets personnalisés", IsDirectory = true, Priority = 2 },
                    new() { Path = "%APPDATA%\\Code\\User\\extensions", Type = SettingsPathType.AppData, Description = "Extensions installées", IsDirectory = true, Priority = 2 }
                },
                ExcludePatterns = new List<string> { "logs/", "CachedExtensions/", "workspaceStorage/", "*.log" },
                Strategy = RestoreStrategy.OverwriteExisting,
                Notes = "Sauvegarde complète de la configuration VS Code"
            };

            // Google Chrome
            _profiles["Google Chrome"] = new SoftwareSettingsProfile
            {
                SoftwareName = "Google Chrome",
                AlternativeNames = new List<string> { "Chrome" },
                ConfigPaths = new List<SettingsPath>
                {
                    new() { Path = "%LOCALAPPDATA%\\Google\\Chrome\\User Data\\Default\\Bookmarks", Type = SettingsPathType.LocalAppData, Description = "Marque-pages", Priority = 1 },
                    new() { Path = "%LOCALAPPDATA%\\Google\\Chrome\\User Data\\Default\\Preferences", Type = SettingsPathType.LocalAppData, Description = "Préférences", Priority = 1 },
                    new() { Path = "%LOCALAPPDATA%\\Google\\Chrome\\User Data\\Default\\Extensions", Type = SettingsPathType.LocalAppData, Description = "Extensions", IsDirectory = true, Priority = 2 },
                    new() { Path = "%LOCALAPPDATA%\\Google\\Chrome\\User Data\\Default\\Local Extension Settings", Type = SettingsPathType.LocalAppData, Description = "Settings des extensions", IsDirectory = true, Priority = 3 }
                },
                ExcludePatterns = new List<string> { "Cache/", "Code Cache/", "GPUCache/", "*.log", "*.tmp" },
                Strategy = RestoreStrategy.BackupAndReplace,
                Notes = "Sauvegarde des marque-pages et extensions Chrome"
            };

            // Steam
            _profiles["Steam"] = new SoftwareSettingsProfile
            {
                SoftwareName = "Steam",
                ConfigPaths = new List<SettingsPath>
                {
                    new() { Path = "%PROGRAMFILES(X86)%\\Steam\\config", Type = SettingsPathType.ProgramData, Description = "Configuration Steam", IsDirectory = true, Priority = 1 },
                    new() { Path = "%PROGRAMFILES(X86)%\\Steam\\userdata", Type = SettingsPathType.ProgramData, Description = "Données utilisateur", IsDirectory = true, Priority = 2, IsRequired = false }
                },
                ExcludePatterns = new List<string> { "logs/", "dumps/", "appcache/", "*.log", "*.dmp" },
                Strategy = RestoreStrategy.AskUser,
                RequiresElevation = true,
                Notes = "Configuration Steam (attention aux sauvegardes de jeux volumineuses)"
            };

            // Git
            _profiles["Git"] = new SoftwareSettingsProfile
            {
                SoftwareName = "Git",
                ConfigPaths = new List<SettingsPath>
                {
                    new() { Path = "%USERPROFILE%\\.gitconfig", Type = SettingsPathType.UserProfile, Description = "Configuration Git globale", Priority = 1 },
                    new() { Path = "%USERPROFILE%\\.ssh", Type = SettingsPathType.UserProfile, Description = "Clés SSH", IsDirectory = true, Priority = 1 },
                    new() { Path = "%USERPROFILE%\\.gitignore_global", Type = SettingsPathType.UserProfile, Description = "Gitignore global", Priority = 2, IsRequired = false }
                },
                Strategy = RestoreStrategy.BackupAndReplace,
                Notes = "Configuration Git et clés SSH (sensibles !)"
            };

            // Discord
            _profiles["Discord"] = new SoftwareSettingsProfile
            {
                SoftwareName = "Discord",
                ConfigPaths = new List<SettingsPath>
                {
                    new() { Path = "%APPDATA%\\discord\\settings.json", Type = SettingsPathType.AppData, Description = "Paramètres Discord", Priority = 1 },
                    new() { Path = "%APPDATA%\\discord\\Local State", Type = SettingsPathType.AppData, Description = "État local", Priority = 2 }
                },
                ExcludePatterns = new List<string> { "Cache/", "logs/", "*.log", "*.tmp" },
                Strategy = RestoreStrategy.OverwriteExisting,
                Notes = "Configuration Discord (sans cache)"
            };

            // 7-Zip
            _profiles["7-Zip"] = new SoftwareSettingsProfile
            {
                SoftwareName = "7-Zip",
                ConfigPaths = new List<SettingsPath>
                {
                    new() { Path = "%APPDATA%\\7-Zip", Type = SettingsPathType.AppData, Description = "Configuration 7-Zip", IsDirectory = true, Priority = 1, IsRequired = false }
                },
                Strategy = RestoreStrategy.OverwriteExisting,
                Notes = "Configuration 7-Zip (paramètres d'interface)"
            };

            ReportProgress($"Registry initialisé avec {_profiles.Count} profils de logiciels");
        }

        public SoftwareSettingsProfile? GetProfile(string softwareName)
        {
            // Recherche directe
            if (_profiles.TryGetValue(softwareName, out var profile))
                return profile;

            // Recherche par correspondance
            return _profiles.Values.FirstOrDefault(p => p.MatchesSoftware(softwareName));
        }

        public async Task<List<SettingsFile>> DetectSettingsAsync(SoftwareInfo software)
        {
            var profile = GetProfile(software.Name);
            if (profile == null)
            {
                ReportProgress($"Aucun profil trouvé pour {software.Name}");
                return new List<SettingsFile>();
            }

            ReportProgress($"Détection des settings pour {software.Name}...");
            var settingsFiles = new List<SettingsFile>();

            foreach (var configPath in profile.ConfigPaths.OrderBy(p => p.Priority))
            {
                try
                {
                    var expandedPath = ExpandEnvironmentPath(configPath.Path);

                    if (configPath.IsDirectory)
                    {
                        if (Directory.Exists(expandedPath))
                        {
                            var directoryFiles = await ScanDirectoryAsync(expandedPath, profile.ExcludePatterns);
                            settingsFiles.AddRange(directoryFiles);
                            ReportProgress($"Trouvé dossier: {configPath.Description} ({directoryFiles.Count} fichiers)");
                        }
                        else if (configPath.IsRequired)
                        {
                            ReportProgress($"Dossier requis manquant: {configPath.Description}");
                        }
                    }
                    else
                    {
                        if (File.Exists(expandedPath))
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

                            settingsFiles.Add(settingsFile);
                            ReportProgress($"Trouvé fichier: {configPath.Description} ({settingsFile.SizeFormatted})");
                        }
                        else if (configPath.IsRequired)
                        {
                            ReportProgress($"Fichier requis manquant: {configPath.Description}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    ReportProgress($"Erreur lors de l'analyse de {configPath.Path}: {ex.Message}");
                }
            }

            ReportProgress($"Détection terminée pour {software.Name}: {settingsFiles.Count} fichiers trouvés");
            return settingsFiles;
        }

        private async Task<List<SettingsFile>> ScanDirectoryAsync(string directoryPath, List<string> excludePatterns)
        {
            var files = new List<SettingsFile>();
            var basePath = directoryPath;

            try
            {
                await Task.Run(() =>
                {
                    ScanDirectoryRecursive(directoryPath, basePath, files, excludePatterns, 0);
                });
            }
            catch (Exception ex)
            {
                ReportProgress($"Erreur scan dossier {directoryPath}: {ex.Message}");
            }

            return files;
        }

        private void ScanDirectoryRecursive(string currentPath, string basePath, List<SettingsFile> files, List<string> excludePatterns, int depth)
        {
            if (depth > 10) return; // Éviter récursion infinie

            try
            {
                // Vérifier exclusions
                var relativePath = Path.GetRelativePath(basePath, currentPath);
                if (IsExcluded(relativePath, excludePatterns))
                    return;

                // Scanner les fichiers
                foreach (var filePath in Directory.GetFiles(currentPath))
                {
                    var fileRelativePath = Path.GetRelativePath(basePath, filePath);
                    if (!IsExcluded(fileRelativePath, excludePatterns))
                    {
                        var fileInfo = new FileInfo(filePath);
                        if (fileInfo.Length < 50 * 1024 * 1024) // Limite 50MB par fichier
                        {
                            files.Add(new SettingsFile
                            {
                                RelativePath = fileRelativePath,
                                FullPath = filePath,
                                Size = fileInfo.Length,
                                LastModified = fileInfo.LastWriteTime,
                                IsDirectory = false,
                                FileType = GetFileType(filePath)
                            });
                        }
                    }
                }

                // Scanner les sous-dossiers
                foreach (var subDir in Directory.GetDirectories(currentPath))
                {
                    ScanDirectoryRecursive(subDir, basePath, files, excludePatterns, depth + 1);
                }
            }
            catch (UnauthorizedAccessException)
            {
                // Ignorer les dossiers protégés
            }
            catch (Exception ex)
            {
                ReportProgress($"Erreur scan récursif {currentPath}: {ex.Message}");
            }
        }

        private bool IsExcluded(string relativePath, List<string> excludePatterns)
        {
            foreach (var pattern in excludePatterns)
            {
                if (pattern.EndsWith("/") || pattern.EndsWith("\\"))
                {
                    // Pattern de dossier
                    var dirPattern = pattern.TrimEnd('/', '\\');
                    if (relativePath.Contains(dirPattern, StringComparison.OrdinalIgnoreCase))
                        return true;
                }
                else if (pattern.Contains("*") || pattern.Contains("?"))
                {
                    // Pattern avec wildcards
                    var regex = "^" + Regex.Escape(pattern).Replace("\\*", ".*").Replace("\\?", ".") + "$";
                    if (Regex.IsMatch(Path.GetFileName(relativePath), regex, RegexOptions.IgnoreCase))
                        return true;
                }
                else
                {
                    // Pattern exact
                    if (relativePath.Contains(pattern, StringComparison.OrdinalIgnoreCase))
                        return true;
                }
            }
            return false;
        }

        private SettingsFileType GetFileType(string filePath)
        {
            var extension = Path.GetExtension(filePath).ToLowerInvariant();
            return extension switch
            {
                ".json" or ".xml" or ".ini" or ".config" or ".conf" or ".cfg" or ".yaml" or ".yml" => SettingsFileType.Configuration,
                ".db" or ".sqlite" or ".sqlite3" => SettingsFileType.Database,
                ".reg" => SettingsFileType.Registry,
                ".log" or ".tmp" or ".temp" => SettingsFileType.Cache,
                _ => SettingsFileType.UserData
            };
        }

        private string ExpandEnvironmentPath(string path)
        {
            var expanded = Environment.ExpandEnvironmentVariables(path);

            // Remplacements spéciaux
            expanded = expanded.Replace("%PROGRAMFILES(X86)%", Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86));
            expanded = expanded.Replace("%PROGRAMFILES%", Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles));
            expanded = expanded.Replace("%APPDATA%", Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData));
            expanded = expanded.Replace("%LOCALAPPDATA%", Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData));
            expanded = expanded.Replace("%USERPROFILE%", Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));

            return expanded;
        }

        private void ReportProgress(string message)
        {
            _progress?.Report(message);
        }

        public IReadOnlyCollection<SoftwareSettingsProfile> GetAllProfiles()
        {
            return _profiles.Values.ToList().AsReadOnly();
        }

        public void AddProfile(SoftwareSettingsProfile profile)
        {
            _profiles[profile.SoftwareName] = profile;
            ReportProgress($"Profil ajouté: {profile.SoftwareName}");
        }
    }
}