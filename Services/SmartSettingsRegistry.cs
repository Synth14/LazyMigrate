namespace LazyMigrate.Services
{
    public class SmartSettingsDetector
    {
        private readonly Action<string>? _progressCallback;

        public SmartSettingsDetector(Action<string>? progressCallback = null)
        {
            _progressCallback = progressCallback;
        }

        public async Task<List<SettingsFile>> DetectSettingsAsync(SoftwareInfo software)
        {
            var settingsFiles = new List<SettingsFile>();
            var softwareName = software.Name;
            var publisher = software.Publisher;

            ReportProgress($"🔍 Analyse des settings pour {softwareName}...");

            // 1. Nettoyer le nom pour les chemins
            var cleanNames = GenerateCleanNames(softwareName);
            var publisherNames = GenerateCleanNames(publisher);

            // 2. Tester tous les patterns possibles
            var allPaths = new List<string>();

            // AppData Roaming patterns
            allPaths.AddRange(GenerateAppDataPaths(cleanNames, publisherNames));

            // LocalAppData patterns  
            allPaths.AddRange(GenerateLocalAppDataPaths(cleanNames, publisherNames));

            // User Profile patterns (dotfiles, configs)
            allPaths.AddRange(GenerateUserProfilePaths(cleanNames));

            // Documents patterns
            allPaths.AddRange(GenerateDocumentsPaths(cleanNames, publisherNames));

            // Program Files patterns (portable configs)
            allPaths.AddRange(GenerateProgramFilesPaths(software.InstallPath, cleanNames));

            // Registry patterns
            allPaths.AddRange(GenerateRegistryPaths(cleanNames, publisherNames));

            // 3. Tester chaque chemin
            var foundPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var path in allPaths.Distinct())
            {
                try
                {
                    var expandedPath = ExpandEnvironmentPath(path);

                    if (Directory.Exists(expandedPath))
                    {
                        var dirFiles = await ScanDirectoryForSettings(expandedPath);
                        if (dirFiles.Any())
                        {
                            settingsFiles.AddRange(dirFiles);
                            foundPaths.Add(path);
                            ReportProgress($"  ✅ Dossier: {Path.GetFileName(expandedPath)} ({dirFiles.Count} fichiers)");
                        }
                    }
                    else if (File.Exists(expandedPath))
                    {
                        var fileInfo = new FileInfo(expandedPath);
                        if (IsSettingsFile(expandedPath))
                        {
                            settingsFiles.Add(new SettingsFile
                            {
                                RelativePath = Path.GetFileName(expandedPath),
                                FullPath = expandedPath,
                                Size = fileInfo.Length,
                                LastModified = fileInfo.LastWriteTime,
                                IsDirectory = false,
                                FileType = GetFileType(expandedPath)
                            });
                            foundPaths.Add(path);
                            ReportProgress($"  ✅ Fichier: {Path.GetFileName(expandedPath)}");
                        }
                    }
                }
                catch
                {
                    // Ignorer les erreurs d'accès
                }
            }

            ReportProgress($"🎯 {softwareName}: {settingsFiles.Count} settings trouvés");
            return settingsFiles;
        }

        private List<string> GenerateCleanNames(string name)
        {
            if (string.IsNullOrEmpty(name)) return new List<string>();

            var variations = new List<string>();
            var original = name;

            // Nom original
            variations.Add(original);

            // Supprimer les parenthèses et contenu
            var withoutParens = System.Text.RegularExpressions.Regex.Replace(original, @"\([^)]*\)", "").Trim();
            if (withoutParens != original) variations.Add(withoutParens);

            // Supprimer les versions
            var withoutVersion = System.Text.RegularExpressions.Regex.Replace(withoutParens, @"\s+\d+(\.\d+)*", "").Trim();
            if (withoutVersion != withoutParens) variations.Add(withoutVersion);

            // Supprimer les mots courants
            var commonWords = new[] { "Microsoft", "Google", "LLC", "Inc", "Corporation", "Corp", "Ltd", "Software", "App", "Application" };
            foreach (var word in commonWords)
            {
                var withoutWord = withoutVersion.Replace(word, "").Trim();
                if (withoutWord != withoutVersion && !string.IsNullOrEmpty(withoutWord))
                    variations.Add(withoutWord);
            }

            // Variations de formatage
            foreach (var variation in variations.ToList())
            {
                // Sans espaces
                variations.Add(variation.Replace(" ", ""));
                // Avec underscores
                variations.Add(variation.Replace(" ", "_"));
                // Avec tirets
                variations.Add(variation.Replace(" ", "-"));
                // Lowercase
                variations.Add(variation.ToLowerInvariant());
                // Premier mot seulement
                var firstWord = variation.Split(' ').First();
                if (firstWord.Length > 3) variations.Add(firstWord);
            }

            return variations.Where(v => !string.IsNullOrEmpty(v) && v.Length > 1)
                           .Distinct(StringComparer.OrdinalIgnoreCase)
                           .ToList();
        }

        private List<string> GenerateAppDataPaths(List<string> cleanNames, List<string> publisherNames)
        {
            var paths = new List<string>();

            foreach (var name in cleanNames)
            {
                // Patterns directs
                paths.Add($"%APPDATA%\\{name}");
                paths.Add($"%APPDATA%\\{name}\\config");
                paths.Add($"%APPDATA%\\{name}\\settings");
                paths.Add($"%APPDATA%\\{name}\\user");
                paths.Add($"%APPDATA%\\{name}\\preferences");

                // Avec éditeur
                foreach (var publisher in publisherNames)
                {
                    paths.Add($"%APPDATA%\\{publisher}\\{name}");
                    paths.Add($"%APPDATA%\\{publisher}\\{name}\\config");
                    paths.Add($"%APPDATA%\\{publisher}");
                }

                // Fichiers de config directs
                paths.Add($"%APPDATA%\\{name}.conf");
                paths.Add($"%APPDATA%\\{name}.config");
                paths.Add($"%APPDATA%\\{name}.ini");
                paths.Add($"%APPDATA%\\{name}.json");
                paths.Add($"%APPDATA%\\{name}.xml");
            }

            return paths;
        }

        private List<string> GenerateLocalAppDataPaths(List<string> cleanNames, List<string> publisherNames)
        {
            var paths = new List<string>();

            foreach (var name in cleanNames)
            {
                // Patterns directs
                paths.Add($"%LOCALAPPDATA%\\{name}");
                paths.Add($"%LOCALAPPDATA%\\{name}\\User Data");
                paths.Add($"%LOCALAPPDATA%\\{name}\\config");
                paths.Add($"%LOCALAPPDATA%\\{name}\\settings");

                // Avec éditeur
                foreach (var publisher in publisherNames)
                {
                    paths.Add($"%LOCALAPPDATA%\\{publisher}\\{name}");
                    paths.Add($"%LOCALAPPDATA%\\{publisher}\\{name}\\User Data");
                    paths.Add($"%LOCALAPPDATA%\\{publisher}");
                }
            }

            return paths;
        }

        private List<string> GenerateUserProfilePaths(List<string> cleanNames)
        {
            var paths = new List<string>();

            foreach (var name in cleanNames)
            {
                // Dotfiles Unix-style
                paths.Add($"%USERPROFILE%\\.{name.ToLowerInvariant()}");
                paths.Add($"%USERPROFILE%\\.{name.ToLowerInvariant()}rc");
                paths.Add($"%USERPROFILE%\\.config\\{name.ToLowerInvariant()}");

                // Fichiers de config
                paths.Add($"%USERPROFILE%\\{name.ToLowerInvariant()}.conf");
                paths.Add($"%USERPROFILE%\\{name.ToLowerInvariant()}.config");
                paths.Add($"%USERPROFILE%\\{name.ToLowerInvariant()}.ini");

                // Dossiers dans user profile
                paths.Add($"%USERPROFILE%\\{name}");
                paths.Add($"%USERPROFILE%\\.{name}");
            }

            return paths;
        }

        private List<string> GenerateDocumentsPaths(List<string> cleanNames, List<string> publisherNames)
        {
            var paths = new List<string>();

            foreach (var name in cleanNames)
            {
                // Documents directs
                paths.Add($"%USERPROFILE%\\Documents\\{name}");
                paths.Add($"%USERPROFILE%\\Documents\\My {name}");

                // Avec éditeur
                foreach (var publisher in publisherNames)
                {
                    paths.Add($"%USERPROFILE%\\Documents\\{publisher}\\{name}");
                    paths.Add($"%USERPROFILE%\\Documents\\{publisher}");
                }
            }

            return paths;
        }

        private List<string> GenerateProgramFilesPaths(string installPath, List<string> cleanNames)
        {
            var paths = new List<string>();

            if (!string.IsNullOrEmpty(installPath) && Directory.Exists(installPath))
            {
                // Config près de l'exe
                paths.Add(Path.Combine(installPath, "config"));
                paths.Add(Path.Combine(installPath, "settings"));
                paths.Add(Path.Combine(installPath, "data"));
                paths.Add(Path.Combine(installPath, "user"));

                // Fichiers ini/config dans le dossier d'install
                foreach (var name in cleanNames)
                {
                    paths.Add(Path.Combine(installPath, $"{name}.ini"));
                    paths.Add(Path.Combine(installPath, $"{name}.config"));
                    paths.Add(Path.Combine(installPath, $"{name}.conf"));
                }
            }

            return paths;
        }

        private List<string> GenerateRegistryPaths(List<string> cleanNames, List<string> publisherNames)
        {
            // TODO: Implémenter la lecture du registre pour les settings
            // HKEY_CURRENT_USER\Software\{Publisher}\{Software}
            return new List<string>();
        }

        private async Task<List<SettingsFile>> ScanDirectoryForSettings(string directoryPath)
        {
            var settingsFiles = new List<SettingsFile>();

            try
            {
                await Task.Run(() =>
                {
                    // Scanner les fichiers de settings dans le dossier
                    var files = Directory.GetFiles(directoryPath, "*", SearchOption.TopDirectoryOnly)
                                       .Where(f => IsSettingsFile(f))
                                       .Take(50); // Limiter pour éviter la lenteur

                    foreach (var file in files)
                    {
                        try
                        {
                            var fileInfo = new FileInfo(file);
                            if (fileInfo.Length < 100 * 1024 * 1024) // Max 100MB
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

                    // Scanner aussi les sous-dossiers importants
                    var importantSubdirs = new[] { "User", "config", "settings", "preferences" };
                    var subdirs = Directory.GetDirectories(directoryPath)
                                          .Where(d => importantSubdirs.Any(sub =>
                                              Path.GetFileName(d).Contains(sub, StringComparison.OrdinalIgnoreCase)))
                                          .Take(5);

                    foreach (var subdir in subdirs)
                    {
                        var subdirFiles = Directory.GetFiles(subdir, "*", SearchOption.AllDirectories)
                                                  .Where(f => IsSettingsFile(f))
                                                  .Take(20);

                        foreach (var file in subdirFiles)
                        {
                            try
                            {
                                var fileInfo = new FileInfo(file);
                                if (fileInfo.Length < 50 * 1024 * 1024) // Max 50MB pour sous-dossiers
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
                                // Ignorer les erreurs
                            }
                        }
                    }
                });
            }
            catch
            {
                // Ignorer les erreurs d'accès au dossier
            }

            return settingsFiles;
        }

        private bool IsSettingsFile(string filePath)
        {
            var fileName = Path.GetFileName(filePath).ToLowerInvariant();
            var extension = Path.GetExtension(filePath).ToLowerInvariant();

            // Extensions de fichiers de configuration
            var configExtensions = new[] { ".json", ".xml", ".ini", ".conf", ".config", ".cfg", ".yaml", ".yml", ".toml", ".plist", ".reg" };
            if (configExtensions.Contains(extension)) return true;

            // Noms de fichiers typiques
            var configNames = new[] { "settings", "preferences", "config", "options", "user", "profile", "bookmarks" };
            if (configNames.Any(name => fileName.Contains(name))) return true;

            // Fichiers sans extension mais avec des noms typiques
            if (string.IsNullOrEmpty(extension))
            {
                var noExtNames = new[] { "config", "settings", "preferences", "user" };
                if (noExtNames.Contains(fileName)) return true;
            }

            return false;
        }

        private SettingsFileType GetFileType(string filePath)
        {
            var extension = Path.GetExtension(filePath).ToLowerInvariant();
            return extension switch
            {
                ".json" or ".xml" or ".ini" or ".config" or ".conf" or ".cfg" or ".yaml" or ".yml" or ".toml" => SettingsFileType.Configuration,
                ".db" or ".sqlite" or ".sqlite3" => SettingsFileType.Database,
                ".reg" => SettingsFileType.Registry,
                ".plist" => SettingsFileType.Configuration,
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
    }
}