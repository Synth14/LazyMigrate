using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace LazyMigrate.Services
{
    public class AutoSettingsDetector
    {
        private readonly IProgress<string>? _progress;

        public AutoSettingsDetector(IProgress<string>? progress = null)
        {
            _progress = progress;
        }

        public async Task<SoftwareSettingsProfile> DetectSettingsAsync(SoftwareInfo software)
        {
            _progress?.Report($"Détection auto des settings pour {software.Name}...");

            var profile = new SoftwareSettingsProfile
            {
                SoftwareName = software.Name,
                AlternativeNames = GenerateAlternativeNames(software.Name),
                PublisherNames = new List<string> { software.Publisher },
                ConfigPaths = new List<SettingsPath>(),
                ExcludePatterns = GetDefaultExcludePatterns(),
                Strategy = RestoreStrategy.BackupAndReplace,
                Notes = $"Profil auto-généré le {DateTime.Now:yyyy-MM-dd}"
            };

            // 1. Chercher dans les dossiers standards
            await DetectInStandardLocationsAsync(software, profile);

            // 2. Chercher près de l'exe d'installation
            await DetectNearExecutableAsync(software, profile);

            // 3. Chercher dans le registre
            await DetectInRegistryAsync(software, profile);

            // 4. Analyser les processus en cours (si le logiciel est lancé)
            await DetectFromRunningProcessAsync(software, profile);

            _progress?.Report($"Détection terminée: {profile.ConfigPaths.Count} chemins trouvés");

            return profile;
        }

        private async Task DetectInStandardLocationsAsync(SoftwareInfo software, SoftwareSettingsProfile profile)
        {
            var softwareName = CleanSoftwareName(software.Name);
            var publisherName = CleanSoftwareName(software.Publisher);

            var searchPaths = new List<(string Path, SettingsPathType Type, string Description)>
            {
                // AppData Roaming
                ($"%APPDATA%\\{softwareName}", SettingsPathType.AppData, "Dossier principal AppData"),
                ($"%APPDATA%\\{publisherName}\\{softwareName}", SettingsPathType.AppData, "Dossier éditeur/logiciel AppData"),
                ($"%APPDATA%\\{publisherName}", SettingsPathType.AppData, "Dossier éditeur AppData"),
                
                // AppData Local
                ($"%LOCALAPPDATA%\\{softwareName}", SettingsPathType.LocalAppData, "Dossier principal Local"),
                ($"%LOCALAPPDATA%\\{publisherName}\\{softwareName}", SettingsPathType.LocalAppData, "Dossier éditeur/logiciel Local"),
                ($"%LOCALAPPDATA%\\{publisherName}", SettingsPathType.LocalAppData, "Dossier éditeur Local"),
                
                // User Profile (fichiers de config)
                ($"%USERPROFILE%\\.{softwareName.ToLower()}", SettingsPathType.UserProfile, "Config dotfile"),
                ($"%USERPROFILE%\\.{softwareName.ToLower()}rc", SettingsPathType.UserProfile, "Fichier rc"),
                ($"%USERPROFILE%\\{softwareName.ToLower()}.conf", SettingsPathType.UserProfile, "Fichier conf"),
                
                // Documents
                ($"%USERPROFILE%\\Documents\\{softwareName}", SettingsPathType.UserProfile, "Dossier Documents"),
                ($"%USERPROFILE%\\Documents\\{publisherName}\\{softwareName}", SettingsPathType.UserProfile, "Dossier Documents éditeur")
            };

            foreach (var (path, type, description) in searchPaths)
            {
                await CheckPathAsync(path, type, description, profile, 2);
            }
        }

        private async Task DetectNearExecutableAsync(SoftwareInfo software, SoftwareSettingsProfile profile)
        {
            if (string.IsNullOrEmpty(software.InstallPath) || !Directory.Exists(software.InstallPath))
                return;

            var installDir = new DirectoryInfo(software.InstallPath);

            // Chercher des dossiers de config près de l'exe
            var configFolders = new[] { "config", "settings", "data", "user", "profiles" };

            foreach (var folder in configFolders)
            {
                var configPath = Path.Combine(installDir.FullName, folder);
                if (Directory.Exists(configPath))
                {
                    await CheckPathAsync(configPath, SettingsPathType.ProgramData,
                        $"Dossier {folder} installation", profile, 3);
                }
            }

            // Chercher des fichiers de config
            var configFiles = installDir.GetFiles("*.ini", SearchOption.TopDirectoryOnly)
                .Concat(installDir.GetFiles("*.conf", SearchOption.TopDirectoryOnly))
                .Concat(installDir.GetFiles("*.config", SearchOption.TopDirectoryOnly))
                .Concat(installDir.GetFiles("*.xml", SearchOption.TopDirectoryOnly))
                .Concat(installDir.GetFiles("*.json", SearchOption.TopDirectoryOnly));

            foreach (var configFile in configFiles.Take(10)) // Limiter
            {
                await CheckPathAsync(configFile.FullName, SettingsPathType.ProgramData,
                    $"Fichier config: {configFile.Name}", profile, 4);
            }
        }

        private async Task DetectInRegistryAsync(SoftwareInfo software, SoftwareSettingsProfile profile)
        {
            // TODO: Chercher des clés de registre spécifiques au logiciel
            // Par exemple HKCU\Software\{Publisher}\{Software}
            await Task.CompletedTask;
        }

        private async Task DetectFromRunningProcessAsync(SoftwareInfo software, SoftwareSettingsProfile profile)
        {
            try
            {
                var processes = System.Diagnostics.Process.GetProcesses()
                    .Where(p => p.ProcessName.Contains(CleanSoftwareName(software.Name), StringComparison.OrdinalIgnoreCase))
                    .ToList();

                foreach (var process in processes.Take(3)) // Limiter
                {
                    try
                    {
                        var exePath = process.MainModule?.FileName;
                        if (!string.IsNullOrEmpty(exePath))
                        {
                            var exeDir = Path.GetDirectoryName(exePath);
                            if (!string.IsNullOrEmpty(exeDir))
                            {
                                await DetectNearPath(exeDir, profile);
                            }
                        }
                    }
                    catch
                    {
                        // Ignorer les erreurs d'accès aux processus
                    }
                }
            }
            catch
            {
                // Ignorer les erreurs de listage des processus
            }
        }

        private async Task DetectNearPath(string basePath, SoftwareSettingsProfile profile)
        {
            var configFolders = new[] { "config", "settings", "data" };
            foreach (var folder in configFolders)
            {
                var configPath = Path.Combine(basePath, folder);
                if (Directory.Exists(configPath))
                {
                    await CheckPathAsync(configPath, SettingsPathType.ProgramData,
                        $"Config près processus: {folder}", profile, 5);
                }
            }
        }

        private async Task CheckPathAsync(string path, SettingsPathType type, string description,
            SoftwareSettingsProfile profile, int priority)
        {
            try
            {
                var expandedPath = ExpandEnvironmentPath(path);
                var exists = File.Exists(expandedPath) || Directory.Exists(expandedPath);

                if (exists)
                {
                    var isDirectory = Directory.Exists(expandedPath);
                    var size = 0L;

                    if (isDirectory)
                    {
                        // Calculer la taille du dossier (superficiel)
                        var dirInfo = new DirectoryInfo(expandedPath);
                        size = dirInfo.GetFiles("*", SearchOption.TopDirectoryOnly)
                            .Take(100)
                            .Sum(f => f.Length);
                    }
                    else
                    {
                        size = new FileInfo(expandedPath).Length;
                    }

                    // Éviter les doublons
                    if (!profile.ConfigPaths.Any(cp => cp.Path.Equals(path, StringComparison.OrdinalIgnoreCase)))
                    {
                        profile.ConfigPaths.Add(new SettingsPath
                        {
                            Path = path,
                            Type = type,
                            Description = $"{description} ({FormatBytes(size)})",
                            IsDirectory = isDirectory,
                            Priority = priority,
                            IsRequired = false
                        });

                        _progress?.Report($"  ✓ Trouvé: {description}");
                    }
                }
            }
            catch
            {
                // Ignorer les erreurs d'accès
            }

            await Task.CompletedTask;
        }

        private string CleanSoftwareName(string name)
        {
            if (string.IsNullOrEmpty(name)) return "";

            // Nettoyer le nom pour les chemins de fichiers
            var cleaned = name;

            // Supprimer les parenthèses et leur contenu
            cleaned = System.Text.RegularExpressions.Regex.Replace(cleaned, @"\([^)]*\)", "").Trim();

            // Supprimer les caractères invalides pour les noms de fichiers
            var invalidChars = Path.GetInvalidFileNameChars();
            foreach (var invalid in invalidChars)
            {
                cleaned = cleaned.Replace(invalid, ' ');
            }

            // Supprimer les espaces multiples et normaliser
            cleaned = System.Text.RegularExpressions.Regex.Replace(cleaned, @"\s+", " ").Trim();

            return cleaned;
        }

        private List<string> GenerateAlternativeNames(string softwareName)
        {
            var alternatives = new List<string>();
            var cleaned = CleanSoftwareName(softwareName);

            // Ajouter des variantes courantes
            alternatives.Add(cleaned);
            alternatives.Add(cleaned.Replace(" ", ""));
            alternatives.Add(cleaned.Replace(" ", "_"));
            alternatives.Add(cleaned.Replace(" ", "-"));

            // Ajouter des mots individuels si c'est pertinent
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

        private string FormatBytes(long bytes)
        {
            if (bytes < 1024) return $"{bytes} B";
            if (bytes < 1024 * 1024) return $"{bytes / 1024:F1} KB";
            return $"{bytes / (1024 * 1024):F1} MB";
        }
    }
}