namespace LazyMigrate.Services
{
    /// <summary>
    /// Service pour scanner le registre Windows à la recherche de settings
    /// </summary>
    public class RegistryScannerService
    {
        public async Task<List<SettingsFile>> ScanRegistryForSettings(List<string> cleanNames, List<string> publisherNames)
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
    }
}