using LazyMigrate.Models.Core;
using LazyMigrate.Services.Detection.PathGenerators.Interfaces;

namespace LazyMigrate.Services.Detection.PathGenerators
{
    /// <summary>
    /// Générateur de chemins pour AppData et LocalAppData
    /// </summary>
    public class AppDataPathGenerator : IPathGenerator
    {
        public List<string> GenerateAllPaths(SoftwareInfo software, List<string> nameVariations, List<string> publisherVariations)
        {
            var paths = new List<string>();

            paths.AddRange(GenerateAppDataPaths(nameVariations, publisherVariations));
            paths.AddRange(GenerateLocalAppDataPaths(nameVariations, publisherVariations));

            return paths;
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
                paths.Add($@"%APPDATA%\{name}\Save");
                paths.Add($@"%APPDATA%\{name}\SaveGames");
                paths.Add($@"%APPDATA%\{name}\Saved");

                // Patterns avec éditeurs/publishers
                foreach (var publisher in publisherNames.Take(3))
                {
                    paths.Add($@"%APPDATA%\{publisher}\{name}");
                    paths.Add($@"%APPDATA%\{publisher}\{name}\config");
                    paths.Add($@"%APPDATA%\{publisher}\{name}\Save");
                    paths.Add($@"%APPDATA%\{publisher}\{name}\saves");
                    paths.Add($@"%APPDATA%\{publisher}\{name}\Steam");
                    paths.Add($@"%APPDATA%\{publisher}\{name}\Steam\*\Save");
                    paths.Add($@"%APPDATA%\{publisher}");
                }

                // Patterns spécifiques SEGA (observé avec Persona 5 Tactica)
                paths.Add($@"%APPDATA%\SEGA\{name}");
                paths.Add($@"%APPDATA%\SEGA\{name}\Steam");
                paths.Add($@"%APPDATA%\SEGA\{name}\Steam\*\Save");

                // Patterns avec identifiants utilisateur Steam/Epic
                paths.Add($@"%APPDATA%\{name}\Steam\*\Save");
                paths.Add($@"%APPDATA%\{name}\Epic\*\Save");
                paths.Add($@"%APPDATA%\{name}\*\Save");
                paths.Add($@"%APPDATA%\{name}\*\saves");

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

                // Patterns Unreal Engine (observés avec Warhammer Boltgun, The Sinking City, Still Wakes the Deep)
                paths.Add($@"%LOCALAPPDATA%\{name}\Saved");
                paths.Add($@"%LOCALAPPDATA%\{name}\Saved\SaveGames");
                paths.Add($@"%LOCALAPPDATA%\{name}\Saved\SaveGames\*");
                paths.Add($@"%LOCALAPPDATA%\{name}Game\Saved");
                paths.Add($@"%LOCALAPPDATA%\{name}Game\Saved\SaveGames");
                paths.Add($@"%LOCALAPPDATA%\{name}Game\Saved\SaveGames\*");

                // Patterns avec suffixes de jeux variés
                var gameSuffixes = new[] { "Game", "The Game", "", "VR", "HD", "Remastered", "Edition" };
                foreach (var suffix in gameSuffixes)
                {
                    var nameWithSuffix = string.IsNullOrEmpty(suffix) ? name : $"{name}{suffix}";
                    paths.Add($@"%LOCALAPPDATA%\{nameWithSuffix}\Saved\SaveGames");
                    paths.Add($@"%LOCALAPPDATA%\{nameWithSuffix}\Saved\Config");
                    paths.Add($@"%LOCALAPPDATA%\{nameWithSuffix}\Saved\Logs");

                    // Pattern générique pour launcher saves avec user-id
                    paths.Add($@"%LOCALAPPDATA%\{nameWithSuffix}\savegames\*\*");
                }

                // Patterns navigateurs/applications modernes
                paths.Add($@"%LOCALAPPDATA%\{name}\User Data\Default");
                paths.Add($@"%LOCALAPPDATA%\{name}\Profiles");

                // Avec éditeur/publisher
                foreach (var publisher in publisherNames.Take(3))
                {
                    paths.Add($@"%LOCALAPPDATA%\{publisher}\{name}");
                    paths.Add($@"%LOCALAPPDATA%\{publisher}\{name}\Saved");
                    paths.Add($@"%LOCALAPPDATA%\{publisher}\{name}\Saved\SaveGames");
                    paths.Add($@"%LOCALAPPDATA%\{publisher}");
                }

                // Packages Windows Store
                paths.Add($@"%LOCALAPPDATA%\Packages\{name}");
                paths.Add($@"%LOCALAPPDATA%\Packages\*{name}*\LocalState");

                // Patterns avec identifiants utilisateur
                paths.Add($@"%LOCALAPPDATA%\{name}\*\SaveGames");
                paths.Add($@"%LOCALAPPDATA%\{name}\*\Save");
                paths.Add($@"%LOCALAPPDATA%\{name}\*\saves");
            }

            return paths;
        }
    }
}