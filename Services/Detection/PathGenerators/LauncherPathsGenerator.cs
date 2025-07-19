using LazyMigrate.Services.Detection.PathGenerators.Interfaces;

namespace LazyMigrate.Services.Detection.PathGenerators
{
    /// <summary>
    /// Générateur de chemins pour les patterns de launchers (Steam, Epic, Ubisoft, Origin, etc.)
    /// </summary>
    public class LauncherPathsGenerator : IPathGenerator
    {
        public List<string> GenerateAllPaths(SoftwareWithDownload software, List<string> nameVariations, List<string> publisherVariations)
        {
            var paths = new List<string>();

            foreach (var name in nameVariations.Take(5))
            {
                // Pattern générique savegames avec user-id (observé dans Steep, Still Wakes The Deep)
                paths.AddRange(GenerateGenericLauncherPaths(name));

                // Patterns spécifiques par launcher
                paths.AddRange(GenerateSteamPaths(name));
                paths.AddRange(GenerateEpicPaths(name));
                paths.AddRange(GenerateUbisoftPaths(name));
                paths.AddRange(GenerateOriginPaths(name));
                paths.AddRange(GenerateGOGPaths(name));
            }

            return paths;
        }

        private List<string> GenerateGenericLauncherPaths(string name)
        {
            return new List<string>
            {
                // Pattern générique: dossier launcher\savegames\user-id\game-id
                $@"*\savegames\*\*",
                $@"*\{name}\savegames\*\*",
                $@"*\saves\*\*",
                $@"*\{name}\saves\*\*",
                
                // Pattern user-data avec IDs
                $@"*\userdata\*\*",
                $@"*\{name}\userdata\*\*",
                
                // Pattern config avec IDs
                $@"*\config\*\*",
                $@"*\{name}\config\*\*"
            };
        }

        private List<string> GenerateSteamPaths(string name)
        {
            return new List<string>
            {
                // Steam userdata standard
                $@"%PROGRAMFILES(X86)%\Steam\userdata\*\*\local",
                $@"%PROGRAMFILES%\Steam\userdata\*\*\local",
                $@"%PROGRAMFILES(X86)%\Steam\userdata\*\*\remote",
                $@"%PROGRAMFILES%\Steam\userdata\*\*\remote",
                
                // Steam dans AppData
                $@"%APPDATA%\Steam\userdata\*\*",
                $@"%LOCALAPPDATA%\Steam\userdata\*\*",
                
                // Steam Cloud saves
                $@"*\Steam\userdata\*\*\local",
                $@"*\Steam\userdata\*\*\remote"
            };
        }

        private List<string> GenerateEpicPaths(string name)
        {
            return new List<string>
            {
                // Epic Games Store saves
                $@"%LOCALAPPDATA%\EpicGamesLauncher\Saved\SaveGames\*",
                $@"%LOCALAPPDATA%\Epic\*\SaveGames",
                $@"%LOCALAPPDATA%\Epic\*\Saved",
                
                // Epic avec user-id
                $@"*\Epic\*\SaveGames\*",
                $@"*\EpicGames\*\SaveGames\*"
            };
        }

        private List<string> GenerateUbisoftPaths(string name)
        {
            return new List<string>
            {
                // Ubisoft Connect patterns (observés dans les images)
                $@"%LOCALAPPDATA%\Ubisoft Game Launcher\savegames\*\*",
                $@"%PROGRAMFILES(X86)%\Ubisoft\Ubisoft Game Launcher\savegames\*\*",
                
                // Patterns génériques Ubisoft
                $@"*\Ubisoft\*\savegames\*\*",
                $@"*\UbisoftConnect\*\savegames\*\*",
                $@"*\Uplay\*\savegames\*\*"
            };
        }

        private List<string> GenerateOriginPaths(string name)
        {
            return new List<string>
            {
                // Origin / EA Desktop
                $@"%USERPROFILE%\Documents\EA Games\*",
                $@"%LOCALAPPDATA%\Origin\*",
                $@"%APPDATA%\Origin\*",
                
                // EA Desktop nouveau launcher
                $@"%LOCALAPPDATA%\Electronic Arts\*\Saves",
                $@"*\EA\*\Saves\*",
                $@"*\Origin\*\Saves\*"
            };
        }

        private List<string> GenerateGOGPaths(string name)
        {
            return new List<string>
            {
                // GOG Galaxy
                $@"%LOCALAPPDATA%\GOG.com\Galaxy\*",
                $@"%APPDATA%\GOG.com\Galaxy\*",
                $@"*\GOG\*\Saves\*",
                $@"*\GOGGalaxy\*\Saves\*"
            };
        }
    }
}