namespace LazyMigrate.Services
{
    public class PreciseSettingsDetector
    {
        private readonly Action<string>? _progressCallback;

        public PreciseSettingsDetector(Action<string>? progressCallback = null)
        {
            _progressCallback = progressCallback;
        }

        public async Task<List<SettingsFile>> DetectSettingsAsync(SoftwareInfo software)
        {
            var settingsFiles = new List<SettingsFile>();

            _progressCallback?.Invoke($"🎯 Analyse des settings pour {software.Name}...");

            try
            {
                // Simulation d'une recherche de settings
                await Task.Delay(100); // Petite pause pour simuler le travail

                // Logique simplifiée de détection
                var possiblePaths = new[]
                {
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), software.Name),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), software.Name),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), software.Name)
                };

                foreach (var path in possiblePaths)
                {
                    if (Directory.Exists(path))
                    {
                        var files = Directory.GetFiles(path, "*", SearchOption.TopDirectoryOnly)
                                           .Where(f => IsSettingsFile(f))
                                           .Take(10);

                        foreach (var file in files)
                        {
                            var fileInfo = new FileInfo(file);
                            settingsFiles.Add(new SettingsFile
                            {
                                RelativePath = Path.GetFileName(file),
                                FullPath = file,
                                Size = fileInfo.Length,
                                LastModified = fileInfo.LastWriteTime,
                                IsDirectory = false,
                                FileType = GetFileType(file)
                            });
                        }

                        if (settingsFiles.Any())
                        {
                            _progressCallback?.Invoke($"  ✅ Trouvé: {settingsFiles.Count} fichiers de settings");
                            break; // Arrêter à la première trouvaille
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _progressCallback?.Invoke($"  ❌ Erreur: {ex.Message}");
            }

            return settingsFiles;
        }

        private bool IsSettingsFile(string filePath)
        {
            var extension = Path.GetExtension(filePath).ToLowerInvariant();
            var fileName = Path.GetFileName(filePath).ToLowerInvariant();

            var configExtensions = new[] { ".json", ".xml", ".ini", ".conf", ".config", ".cfg" };
            var configNames = new[] { "settings", "config", "preferences", "options" };

            return configExtensions.Contains(extension) ||
                   configNames.Any(name => fileName.Contains(name));
        }

        private SettingsFileType GetFileType(string filePath)
        {
            var extension = Path.GetExtension(filePath).ToLowerInvariant();
            return extension switch
            {
                ".json" or ".xml" or ".ini" or ".config" or ".conf" or ".cfg" => SettingsFileType.Configuration,
                ".db" or ".sqlite" => SettingsFileType.Database,
                ".reg" => SettingsFileType.Registry,
                _ => SettingsFileType.UserData
            };
        }
    }
}