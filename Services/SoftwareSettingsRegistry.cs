using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Text.RegularExpressions;

namespace LazyMigrate.Services
{
    public class SoftwareSettingsRegistry
    {
        private readonly Dictionary<string, SoftwareSettingsProfile> _profiles;
        private readonly Action<string>? _progressCallback;

        public SoftwareSettingsRegistry(Action<string>? progressCallback = null)
        {
            _progressCallback = progressCallback;
            _profiles = new Dictionary<string, SoftwareSettingsProfile>(StringComparer.OrdinalIgnoreCase);
            InitializeBasicProfiles();
        }

        private void InitializeBasicProfiles()
        {
            // Visual Studio Code
            _profiles["Visual Studio Code"] = new SoftwareSettingsProfile
            {
                SoftwareName = "Visual Studio Code",
                AlternativeNames = new List<string> { "VS Code", "Code", "VSCode", "Microsoft Visual Studio Code" },
                PublisherNames = new List<string> { "Microsoft", "Microsoft Corporation" },
                ExecutableNames = new List<string> { "code.exe", "code-insiders.exe" },
                MatchPriority = 1,
                ConfigPaths = new List<SettingsPath>
                {
                    new SettingsPath { Path = "%APPDATA%\\Code\\User\\settings.json", Type = SettingsPathType.AppData, Description = "Configuration principale", Priority = 1 },
                    new SettingsPath { Path = "%APPDATA%\\Code\\User\\keybindings.json", Type = SettingsPathType.AppData, Description = "Raccourcis clavier", Priority = 1 },
                    new SettingsPath { Path = "%APPDATA%\\Code\\User\\snippets", Type = SettingsPathType.AppData, Description = "Snippets personnalisés", IsDirectory = true, Priority = 2 },
                    new SettingsPath { Path = "%APPDATA%\\Code\\User\\extensions", Type = SettingsPathType.AppData, Description = "Extensions installées", IsDirectory = true, Priority = 2 }
                },
                ExcludePatterns = new List<string> { "logs/", "CachedExtensions/", "workspaceStorage/", "*.log" },
                Strategy = RestoreStrategy.OverwriteExisting,
                Notes = "Sauvegarde complète de la configuration VS Code"
            };

            // Google Chrome
            _profiles["Google Chrome"] = new SoftwareSettingsProfile
            {
                SoftwareName = "Google Chrome",
                AlternativeNames = new List<string> { "Chrome", "Google Chrome Browser" },
                PublisherNames = new List<string> { "Google", "Google LLC", "Google Inc." },
                ExecutableNames = new List<string> { "chrome.exe" },
                MatchPriority = 1,
                ConfigPaths = new List<SettingsPath>
                {
                    new SettingsPath { Path = "%LOCALAPPDATA%\\Google\\Chrome\\User Data\\Default\\Bookmarks", Type = SettingsPathType.LocalAppData, Description = "Marque-pages", Priority = 1 },
                    new SettingsPath { Path = "%LOCALAPPDATA%\\Google\\Chrome\\User Data\\Default\\Preferences", Type = SettingsPathType.LocalAppData, Description = "Préférences", Priority = 1 },
                    new SettingsPath { Path = "%LOCALAPPDATA%\\Google\\Chrome\\User Data\\Default\\Extensions", Type = SettingsPathType.LocalAppData, Description = "Extensions", IsDirectory = true, Priority = 2 }
                },
                ExcludePatterns = new List<string> { "Cache/", "Code Cache/", "GPUCache/", "*.log", "*.tmp" },
                Strategy = RestoreStrategy.BackupAndReplace,
                Notes = "Sauvegarde des marque-pages et extensions Chrome"
            };

            // Steam
            _profiles["Steam"] = new SoftwareSettingsProfile
            {
                SoftwareName = "Steam",
                AlternativeNames = new List<string> { "Valve Steam", "Steam Client" },
                PublisherNames = new List<string> { "Valve", "Valve Corporation" },
                ExecutableNames = new List<string> { "steam.exe" },
                MatchPriority = 1,
                ConfigPaths = new List<SettingsPath>
                {
                    new SettingsPath { Path = "%PROGRAMFILES(X86)%\\Steam\\config", Type = SettingsPathType.ProgramData, Description = "Configuration Steam", IsDirectory = true, Priority = 1 },
                    new SettingsPath { Path = "%PROGRAMFILES(X86)%\\Steam\\userdata", Type = SettingsPathType.ProgramData, Description = "Données utilisateur", IsDirectory = true, Priority = 2, IsRequired = false }
                },
                ExcludePatterns = new List<string> { "logs/", "dumps/", "appcache/", "*.log", "*.dmp" },
                Strategy = RestoreStrategy.AskUser,
                RequiresElevation = true,
                Notes = "Configuration Steam"
            };

            ReportProgress($"Registry initialisé avec {_profiles.Count} profils de base");
        }

        public SoftwareSettingsProfile? GetProfile(string softwareName, string publisher = "")
        {
            // Recherche directe
            if (_profiles.TryGetValue(softwareName, out var directProfile))
                return directProfile;

            // Recherche par score de matching
            var candidates = new List<(SoftwareSettingsProfile Profile, int Score)>();

            foreach (var profile in _profiles.Values)
            {
                var score = profile.CalculateMatchScore(softwareName, publisher);
                if (score >= 50)
                {
                    candidates.Add((profile, score));
                }
            }

            var bestMatch = candidates
                .OrderByDescending(c => c.Score)
                .ThenByDescending(c => c.Profile.MatchPriority)
                .FirstOrDefault();

            if (bestMatch.Profile != null)
            {
                ReportProgress($"Match trouvé pour '{softwareName}': {bestMatch.Profile.SoftwareName} (score: {bestMatch.Score})");
                return bestMatch.Profile;
            }

            // Si aucun profil trouvé, essayer la détection automatique
            return TryAutoDetectProfile(softwareName, publisher);
        }

        public SoftwareSettingsProfile? GetProfile(SoftwareInfo software)
        {
            return GetProfile(software.Name, software.Publisher);
        }

        private SoftwareSettingsProfile? TryAutoDetectProfile(string softwareName, string publisher)
        {
            ReportProgress($"Tentative de détection automatique pour '{softwareName}'...");

            var profile = new SoftwareSettingsProfile
            {
                SoftwareName = softwareName,
                AlternativeNames = GenerateAlternativeNames(softwareName),
                PublisherNames = new List<string> { publisher },
                ConfigPaths = new List<SettingsPath>(),
                ExcludePatterns = GetDefaultExcludePatterns(),
                Strategy = RestoreStrategy.BackupAndReplace,
                Notes = $"Profil auto-détecté le {DateTime.Now:yyyy-MM-dd HH:mm}"
            };

            // Détecter dans les emplacements standards
            DetectInStandardLocations(softwareName, publisher, profile);

            if (profile.ConfigPaths.Count > 0)
            {
                // Ajouter le profil au cache pour les prochaines fois
                _profiles[softwareName] = profile;
                ReportProgress($"Profil auto-détecté créé pour '{softwareName}' ({profile.ConfigPaths.Count} chemins)");
                return profile;
            }

            ReportProgress($"Aucune configuration détectée pour '{softwareName}'");
            return null;
        }

        private void DetectInStandardLocations(string softwareName, string publisher, SoftwareSettingsProfile profile)
        {
            var cleanSoftwareName = CleanSoftwareName(softwareName);
            var cleanPublisher = CleanSoftwareName(publisher);

            var searchPaths = new List<(string Path, SettingsPathType Type, string Description)>
            {
                // AppData Roaming
                ($"%APPDATA%\\{cleanSoftwareName}", SettingsPathType.AppData, "Dossier AppData principal"),
                ($"%APPDATA%\\{cleanPublisher}\\{cleanSoftwareName}", SettingsPathType.AppData, "Dossier AppData éditeur"),
                ($"%APPDATA%\\{cleanPublisher}", SettingsPathType.AppData, "Dossier AppData éditeur seul"),
                
                // AppData Local
                ($"%LOCALAPPDATA%\\{cleanSoftwareName}", SettingsPathType.LocalAppData, "Dossier Local principal"),
                ($"%LOCALAPPDATA%\\{cleanPublisher}\\{cleanSoftwareName}", SettingsPathType.LocalAppData, "Dossier Local éditeur"),
                ($"%LOCALAPPDATA%\\{cleanPublisher}", SettingsPathType.LocalAppData, "Dossier Local éditeur seul"),
                
                // User Profile
                ($"%USERPROFILE%\\.{cleanSoftwareName.ToLowerInvariant()}", SettingsPathType.UserProfile, "Config dotfile"),
                ($"%USERPROFILE%\\{cleanSoftwareName}", SettingsPathType.UserProfile, "Dossier utilisateur"),
                
                // Documents
                ($"%USERPROFILE%\\Documents\\{cleanSoftwareName}", SettingsPathType.UserProfile, "Dossier Documents")
            };

            foreach (var (path, type, description) in searchPaths)
            {
                try
                {
                    var expandedPath = ExpandEnvironmentPath(path);
                    if (Directory.Exists(expandedPath) || File.Exists(expandedPath))
                    {
                        var isDirectory = Directory.Exists(expandedPath);
                        profile.ConfigPaths.Add(new SettingsPath
                        {
                            Path = path,
                            Type = type,
                            Description = description,
                            IsDirectory = isDirectory,
                            Priority = GetPathPriority(type),
                            IsRequired = false
                        });

                        ReportProgress($"  ✓ Trouvé: {description}");
                    }
                }
                catch
                {
                    // Ignorer les erreurs d'accès
                }
            }
        }

        private int GetPathPriority(SettingsPathType type)
        {
            return type switch
            {
                SettingsPathType.AppData => 1,
                SettingsPathType.LocalAppData => 2,
                SettingsPathType.UserProfile => 3,
                SettingsPathType.ProgramData => 4,
                _ => 5
            };
        }

        public async Task<List<SettingsFile>> DetectSettingsAsync(SoftwareInfo software)
        {
            var profile = GetProfile(software);
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
                            ReportProgress($"Trouvé fichier: {configPath.Description}");
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

        private string CleanSoftwareName(string name)
        {
            if (string.IsNullOrEmpty(name)) return "";

            var cleaned = name;
            cleaned = Regex.Replace(cleaned, @"\([^)]*\)", "").Trim();

            var invalidChars = Path.GetInvalidFileNameChars();
            foreach (var invalid in invalidChars)
            {
                cleaned = cleaned.Replace(invalid, ' ');
            }

            cleaned = Regex.Replace(cleaned, @"\s+", " ").Trim();
            return cleaned;
        }

        private List<string> GenerateAlternativeNames(string softwareName)
        {
            var alternatives = new List<string>();
            var cleaned = CleanSoftwareName(softwareName);

            alternatives.Add(cleaned);
            alternatives.Add(cleaned.Replace(" ", ""));
            alternatives.Add(cleaned.Replace(" ", "_"));

            var words = cleaned.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (words.Length > 1)
            {
                foreach (var word in words.Where(w => w.Length > 3))
                {
                    alternatives.Add(word);
                }
            }

            return alternatives.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        }

        private List<string> GetDefaultExcludePatterns()
        {
            return new List<string>
            {
                "cache/", "Cache/", "logs/", "Logs/", "temp/", "Temp/", "tmp/",
                "*.log", "*.tmp", "*.temp", "*.dmp", "*.crash",
                "GPUCache/", "Code Cache/", "ShaderCache/"
            };
        }

        // Vos méthodes existantes (ScanDirectoryAsync, GetFileType, ExpandEnvironmentPath, etc.)
        private async Task<List<SettingsFile>> ScanDirectoryAsync(string directoryPath, List<string> excludePatterns)
        {
            // Votre implémentation existante
            return new List<SettingsFile>();
        }

        private SettingsFileType GetFileType(string filePath)
        {
            var extension = Path.GetExtension(filePath).ToLowerInvariant();
            return extension switch
            {
                ".json" or ".xml" or ".ini" or ".config" or ".conf" or ".cfg" => SettingsFileType.Configuration,
                ".db" or ".sqlite" or ".sqlite3" => SettingsFileType.Database,
                ".reg" => SettingsFileType.Registry,
                ".log" or ".tmp" or ".temp" => SettingsFileType.Cache,
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

        private void ReportProgress(string message)
        {
            _progressCallback?.Invoke(message);
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