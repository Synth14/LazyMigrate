using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace LazyMigrate.Services
{
    public class SmartSettingsRegistry
    {
        private readonly Dictionary<string, SoftwareSettingsProfile> _knownProfiles;
        private readonly AutoSettingsDetector _autoDetector;
        private readonly IProgress<string>? _progress;
        private readonly string _cacheFilePath;

        public SmartSettingsRegistry(IProgress<string>? progress = null)
        {
            _progress = progress;
            _knownProfiles = new Dictionary<string, SoftwareSettingsProfile>(StringComparer.OrdinalIgnoreCase);
            _autoDetector = new AutoSettingsDetector(progress);

            // Fichier de cache pour les profils découverts
            var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var LazyMigrateDir = Path.Combine(appDataPath, "LazyMigrate");
            Directory.CreateDirectory(LazyMigrateDir);
            _cacheFilePath = Path.Combine(LazyMigrateDir, "discovered_profiles.json");

            LoadKnownProfiles();
            LoadCachedProfiles();
        }

        private void LoadKnownProfiles()
        {
            // Garder quelques profils critiques en dur pour les logiciels très courants
            // Mais beaucoup moins qu'avant !

            AddKnownProfile(new SoftwareSettingsProfile
            {
                SoftwareName = "Visual Studio Code",
                AlternativeNames = new List<string> { "VS Code", "Code", "VSCode" },
                PublisherNames = new List<string> { "Microsoft" },
                ConfigPaths = new List<SettingsPath>
                {
                    new() { Path = "%APPDATA%\\Code\\User", Type = SettingsPathType.AppData,
                           Description = "Dossier utilisateur VS Code", IsDirectory = true, Priority = 1 }
                },
                ExcludePatterns = new List<string> { "logs/", "CachedExtensions/", "workspaceStorage/" },
                Strategy = RestoreStrategy.BackupAndReplace,
                Notes = "Profil prédéfini VS Code"
            });

            AddKnownProfile(new SoftwareSettingsProfile
            {
                SoftwareName = "Google Chrome",
                AlternativeNames = new List<string> { "Chrome" },
                PublisherNames = new List<string> { "Google" },
                ConfigPaths = new List<SettingsPath>
                {
                    new() { Path = "%LOCALAPPDATA%\\Google\\Chrome\\User Data\\Default",
                           Type = SettingsPathType.LocalAppData, Description = "Profil Chrome par défaut",
                           IsDirectory = true, Priority = 1 }
                },
                ExcludePatterns = new List<string> { "Cache/", "Code Cache/", "GPUCache/" },
                Strategy = RestoreStrategy.BackupAndReplace,
                Notes = "Profil prédéfini Chrome"
            });

            _progress?.Report($"Profils prédéfinis chargés: {_knownProfiles.Count}");
        }

        private void LoadCachedProfiles()
        {
            try
            {
                if (File.Exists(_cacheFilePath))
                {
                    var json = File.ReadAllText(_cacheFilePath);
                    var cachedProfiles = JsonSerializer.Deserialize<List<SoftwareSettingsProfile>>(json);

                    if (cachedProfiles != null)
                    {
                        foreach (var profile in cachedProfiles)
                        {
                            // Ajouter seulement si pas déjà connu (priorité aux prédéfinis)
                            if (!_knownProfiles.ContainsKey(profile.SoftwareName))
                            {
                                _knownProfiles[profile.SoftwareName] = profile;
                            }
                        }

                        _progress?.Report($"Profils en cache chargés: {cachedProfiles.Count}");
                    }
                }
            }
            catch (Exception ex)
            {
                _progress?.Report($"Erreur chargement cache: {ex.Message}");
            }
        }

        private async Task SaveCachedProfilesAsync()
        {
            try
            {
                // Sauvegarder seulement les profils auto-découverts (pas les prédéfinis)
                var autoDiscoveredProfiles = _knownProfiles.Values
                    .Where(p => p.Notes?.Contains("auto-généré") == true)
                    .ToList();

                var json = JsonSerializer.Serialize(autoDiscoveredProfiles, new JsonSerializerOptions
                {
                    WriteIndented = true
                });

                await File.WriteAllTextAsync(_cacheFilePath, json);
                _progress?.Report($"Cache sauvegardé: {autoDiscoveredProfiles.Count} profils");
            }
            catch (Exception ex)
            {
                _progress?.Report($"Erreur sauvegarde cache: {ex.Message}");
            }
        }

        public async Task<SoftwareSettingsProfile?> GetProfileAsync(SoftwareInfo software)
        {
            // 1. Chercher dans les profils connus (cache + prédéfinis)
            var knownProfile = FindKnownProfile(software);
            if (knownProfile != null)
            {
                _progress?.Report($"Profil connu trouvé pour {software.Name}");
                return knownProfile;
            }

            // 2. Auto-détection pour les nouveaux logiciels
            _progress?.Report($"Auto-détection pour {software.Name}...");
            var autoProfile = await _autoDetector.DetectSettingsAsync(software);

            if (autoProfile.ConfigPaths.Any())
            {
                // Sauvegarder le profil découvert
                _knownProfiles[software.Name] = autoProfile;
                await SaveCachedProfilesAsync();

                _progress?.Report($"Nouveau profil créé pour {software.Name} ({autoProfile.ConfigPaths.Count} chemins)");
                return autoProfile;
            }

            _progress?.Report($"Aucune configuration trouvée pour {software.Name}");
            return null;
        }

        private SoftwareSettingsProfile? FindKnownProfile(SoftwareInfo software)
        {
            // Recherche directe
            if (_knownProfiles.TryGetValue(software.Name, out var directMatch))
                return directMatch;

            // Recherche par correspondance (logique simplifiée du matching précédent)
            foreach (var profile in _knownProfiles.Values)
            {
                if (profile.AlternativeNames.Any(alt =>
                    software.Name.Contains(alt, StringComparison.OrdinalIgnoreCase) ||
                    alt.Contains(software.Name, StringComparison.OrdinalIgnoreCase)))
                {
                    return profile;
                }

                if (profile.PublisherNames.Any(pub =>
                    software.Publisher.Contains(pub, StringComparison.OrdinalIgnoreCase)))
                {
                    return profile;
                }
            }

            return null;
        }

        public async Task<List<SettingsFile>> DetectSettingsAsync(SoftwareInfo software)
        {
            var profile = await GetProfileAsync(software);
            if (profile == null)
            {
                return new List<SettingsFile>();
            }

            _progress?.Report($"Analyse des settings pour {software.Name}...");
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
                            _progress?.Report($"  ✓ Dossier: {configPath.Description} ({directoryFiles.Count} fichiers)");
                        }
                    }
                    else
                    {
                        if (File.Exists(expandedPath))
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

                            _progress?.Report($"  ✓ Fichier: {configPath.Description}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    _progress?.Report($"  ✗ Erreur {configPath.Path}: {ex.Message}");
                }
            }

            _progress?.Report($"Détection terminée: {settingsFiles.Count} fichiers trouvés");
            return settingsFiles;
        }

        // Méthodes utilitaires (reprises du code original)
        private async Task<List<SettingsFile>> ScanDirectoryAsync(string directoryPath, List<string> excludePatterns)
        {
            // Votre logique existante de scan des dossiers
            var files = new List<SettingsFile>();
            // ... (copier la logique de votre code existant)
            await Task.CompletedTask;
            return files;
        }

        private SettingsFileType GetFileType(string filePath)
        {
            // Votre logique existante
            var extension = Path.GetExtension(filePath).ToLowerInvariant();
            return extension switch
            {
                ".json" or ".xml" or ".ini" => SettingsFileType.Configuration,
                ".db" or ".sqlite" => SettingsFileType.Database,
                _ => SettingsFileType.UserData
            };
        }

        private string ExpandEnvironmentPath(string path)
        {
            // Votre logique existante
            return Environment.ExpandEnvironmentVariables(path);
        }

        private void AddKnownProfile(SoftwareSettingsProfile profile)
        {
            _knownProfiles[profile.SoftwareName] = profile;
        }

        public IReadOnlyCollection<SoftwareSettingsProfile> GetAllProfiles()
        {
            return _knownProfiles.Values.ToList().AsReadOnly();
        }

        // Méthode pour forcer la re-détection d'un logiciel
        public async Task<SoftwareSettingsProfile?> RefreshProfileAsync(SoftwareInfo software)
        {
            _knownProfiles.Remove(software.Name);
            return await GetProfileAsync(software);
        }

        // Méthode pour nettoyer le cache
        public async Task ClearCacheAsync()
        {
            try
            {
                if (File.Exists(_cacheFilePath))
                {
                    File.Delete(_cacheFilePath);
                }

                // Garder seulement les profils prédéfinis
                var predefinedProfiles = _knownProfiles.Values
                    .Where(p => !p.Notes?.Contains("auto-généré") == true)
                    .ToList();

                _knownProfiles.Clear();
                foreach (var profile in predefinedProfiles)
                {
                    _knownProfiles[profile.SoftwareName] = profile;
                }

                _progress?.Report("Cache nettoyé");
            }
            catch (Exception ex)
            {
                _progress?.Report($"Erreur nettoyage cache: {ex.Message}");
            }
        }
    }
}