namespace LazyMigrate.Services.Detection.Utilities
{
    /// <summary>
    /// Service pour l'expansion des variables d'environnement et wildcards dans les chemins
    /// </summary>
    public class PathExpansionService
    {
        public string ExpandEnvironmentPath(string path)
        {
            var expanded = Environment.ExpandEnvironmentVariables(path);
            expanded = expanded.Replace("%PROGRAMFILES(X86)%", Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86));
            expanded = expanded.Replace("%PROGRAMFILES%", Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles));
            expanded = expanded.Replace("%APPDATA%", Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData));
            expanded = expanded.Replace("%LOCALAPPDATA%", Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData));
            expanded = expanded.Replace("%USERPROFILE%", Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
            return expanded;
        }

        public string ExpandWildcardPath(string pathPattern)
        {
            if (!pathPattern.Contains("*"))
                return ExpandEnvironmentPath(pathPattern);

            // Séparer le chemin avant et après le *
            var parts = pathPattern.Split('*');
            if (parts.Length != 2) return ExpandEnvironmentPath(pathPattern);

            var basePath = ExpandEnvironmentPath(parts[0].TrimEnd('\\'));
            var endPath = parts[1].TrimStart('\\');

            var expandedPaths = new List<string>();

            try
            {
                if (Directory.Exists(basePath))
                {
                    // Lister tous les sous-dossiers - OPTIMISATION: Limiter à 5 au lieu de tous
                    var subDirs = Directory.GetDirectories(basePath).Take(5);
                    foreach (var subDir in subDirs)
                    {
                        var fullPath = Path.Combine(subDir, endPath);
                        expandedPaths.Add(fullPath);
                    }
                }
            }
            catch { }

            return expandedPaths.FirstOrDefault() ?? ExpandEnvironmentPath(pathPattern);
        }
    }
}