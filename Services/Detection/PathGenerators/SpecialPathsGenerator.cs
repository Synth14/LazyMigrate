using LazyMigrate.Models.Core;
using LazyMigrate.Services.Detection.PathGenerators.Interfaces;

namespace LazyMigrate.Services.Detection.PathGenerators
{
    /// <summary>
    /// Générateur de chemins spécialisés (publishers connus, profiles, program files)
    /// </summary>
    public class SpecialPathsGenerator : IPathGenerator
    {
        public List<string> GenerateAllPaths(SoftwareInfo software, List<string> nameVariations, List<string> publisherVariations)
        {
            var paths = new List<string>();

            paths.AddRange(GenerateKnownPublisherPaths(nameVariations, software));
            paths.AddRange(GenerateUserProfilePaths(nameVariations));
            paths.AddRange(GenerateProgramFilesPaths(software.InstallPath, nameVariations));
            paths.AddRange(GenerateSpecializedPaths(software, nameVariations));

            return paths;
        }

        private List<string> GenerateKnownPublisherPaths(List<string> cleanNames, SoftwareInfo software)
        {
            var paths = new List<string>();

            // Publishers connus et leurs patterns typiques
            var knownPublishers = new Dictionary<string, string[]>
            {
                ["SEGA"] = new[] { "SEGA" },
                ["Ubisoft"] = new[] { "Ubisoft", "Ubisoft Connect" },
                ["Epic Games"] = new[] { "Epic", "Epic Games", "EpicGames" },
                ["Electronic Arts"] = new[] { "EA", "Electronic Arts", "EA Games" },
                ["Activision"] = new[] { "Activision", "Activision Blizzard" },
                ["Bethesda"] = new[] { "Bethesda", "Bethesda Softworks" },
                ["2K"] = new[] { "2K", "2K Games" },
                ["Rockstar"] = new[] { "Rockstar", "Rockstar Games" },
                ["Square Enix"] = new[] { "Square Enix", "SquareEnix" },
                ["Capcom"] = new[] { "Capcom" },
                ["Konami"] = new[] { "Konami" },
                ["Bandai Namco"] = new[] { "Bandai", "Namco", "Bandai Namco" }
            };

            // Déterminer le publisher probable
            var softwarePublisher = software.Publisher?.ToLowerInvariant() ?? "";
            var softwareName = software.Name?.ToLowerInvariant() ?? "";

            var detectedPublishers = new List<string>();

            foreach (var kvp in knownPublishers)
            {
                var publisherKey = kvp.Key.ToLowerInvariant();
                var variants = kvp.Value;

                // Vérifier si le publisher est mentionné dans le logiciel
                if (softwarePublisher.Contains(publisherKey) ||
                    softwareName.Contains(publisherKey) ||
                    variants.Any(v => softwarePublisher.Contains(v.ToLowerInvariant()) || softwareName.Contains(v.ToLowerInvariant())))
                {
                    detectedPublishers.AddRange(variants);
                }
            }

            // Si aucun publisher détecté, ajouter les plus courants
            if (!detectedPublishers.Any())
            {
                detectedPublishers.AddRange(new[] { "SEGA", "Ubisoft", "EA", "Epic Games" });
            }

            // Générer les patterns pour chaque publisher détecté
            foreach (var publisher in detectedPublishers.Take(3))
            {
                foreach (var name in cleanNames.Take(3))
                {
                    // AppData patterns avec publishers
                    paths.Add($@"%APPDATA%\{publisher}\{name}");
                    paths.Add($@"%APPDATA%\{publisher}\{name}\Steam");
                    paths.Add($@"%APPDATA%\{publisher}\{name}\Steam\*\Save");
                    paths.Add($@"%APPDATA%\{publisher}\{name}\Epic");
                    paths.Add($@"%APPDATA%\{publisher}\{name}\Epic\*\Save");

                    // LocalAppData patterns avec publishers
                    paths.Add($@"%LOCALAPPDATA%\{publisher}\{name}");
                    paths.Add($@"%LOCALAPPDATA%\{publisher}\{name}\Saved");
                    paths.Add($@"%LOCALAPPDATA%\{publisher}\{name}\Saved\SaveGames");

                    // Patterns Ubisoft Connect spécifiques
                    if (publisher.Contains("Ubisoft"))
                    {
                        paths.Add($@"%LOCALAPPDATA%\Ubisoft Game Launcher\savegames\*\*");
                        paths.Add($@"%PROGRAMFILES(X86)%\Ubisoft\Ubisoft Game Launcher\savegames\*\*");
                        // Patterns génériques pour tous les launchers avec user-id
                        paths.Add($@"*\savegames\*\*");
                        paths.Add($@"*\{name}\savegames\*\*");
                    }

                    // Patterns Steam avec publishers
                    paths.Add($@"%APPDATA%\{publisher}\{name}\Steam\*");
                    paths.Add($@"%LOCALAPPDATA%\{publisher}\{name}\Steam\*");
                }
            }

            return paths;
        }

        private List<string> GenerateUserProfilePaths(List<string> cleanNames)
        {
            var paths = new List<string>();

            foreach (var name in cleanNames)
            {
                var lowerName = name.ToLowerInvariant();

                // Dotfiles Unix-style
                paths.Add($@"%USERPROFILE%\.{lowerName}");
                paths.Add($@"%USERPROFILE%\.{lowerName}rc");
                paths.Add($@"%USERPROFILE%\.config\{lowerName}");

                // Fichiers de config dans le profil
                var configExts = new[] { ".conf", ".config", ".ini", ".json", ".xml" };
                foreach (var ext in configExts)
                {
                    paths.Add($@"%USERPROFILE%\{lowerName}{ext}");
                    paths.Add($@"%USERPROFILE%\.{lowerName}{ext}");
                }

                // Dossiers dans user profile
                paths.Add($@"%USERPROFILE%\{name}");
                paths.Add($@"%USERPROFILE%\.{name}");
            }

            return paths;
        }

        private List<string> GenerateProgramFilesPaths(string installPath, List<string> cleanNames)
        {
            var paths = new List<string>();

            if (!string.IsNullOrEmpty(installPath) && Directory.Exists(installPath))
            {
                // Config près de l'exe (applications portables)
                paths.Add(Path.Combine(installPath, "config"));
                paths.Add(Path.Combine(installPath, "settings"));
                paths.Add(Path.Combine(installPath, "data"));
                paths.Add(Path.Combine(installPath, "user"));

                // Fichiers ini/config dans le dossier d'installation
                foreach (var name in cleanNames.Take(3))
                {
                    var configExts = new[] { ".ini", ".config", ".conf", ".cfg", ".json", ".xml" };
                    foreach (var ext in configExts)
                    {
                        paths.Add(Path.Combine(installPath, $"{name}{ext}"));
                    }
                }
            }

            return paths;
        }

        private List<string> GenerateSpecializedPaths(SoftwareInfo software, List<string> cleanNames)
        {
            var paths = new List<string>();
            var category = software.Category?.ToLowerInvariant() ?? "";
            var name = software.Name.ToLowerInvariant();

            // Jeux Steam
            if (name.Contains("steam") || category.Contains("jeu"))
            {
                paths.Add(@"%PROGRAMFILES(X86)%\Steam\userdata");
                paths.Add(@"%PROGRAMFILES%\Steam\userdata");
            }

            // Détection heuristique de jeux (sans dépendre de la catégorie)
            var gameIndicators = new[] { "game", "games", "play", "studio", "entertainment", "interactive" };
            var isLikelyGame = gameIndicators.Any(indicator => name.Contains(indicator)) ||
                             category.Contains("jeu") || category.Contains("game");

            if (isLikelyGame)
            {
                foreach (var cleanName in cleanNames.Take(3))
                {
                    // Emplacements courants pour les jeux
                    paths.Add($@"%USERPROFILE%\Documents\My Games\{cleanName}");
                    paths.Add($@"%USERPROFILE%\Saved Games\{cleanName}");
                    paths.Add($@"%APPDATA%\{cleanName}\Saves");
                    paths.Add($@"%LOCALAPPDATA%\{cleanName}\Saved Games");

                    // Epic Games Store
                    paths.Add($@"%LOCALAPPDATA%\EpicGamesLauncher\Saved\SaveGames");

                    // Origin
                    paths.Add($@"%USERPROFILE%\Documents\EA Games\{cleanName}");

                    // Ubisoft Connect
                    paths.Add($@"%USERPROFILE%\Documents\My Games\{cleanName}");
                }
            }

            // Navigateurs
            if (name.Contains("chrome") || name.Contains("firefox") || name.Contains("edge") || name.Contains("browser"))
            {
                foreach (var cleanName in cleanNames.Take(2))
                {
                    paths.Add($@"%LOCALAPPDATA%\{cleanName}\User Data\Default");
                    paths.Add($@"%APPDATA%\{cleanName}\Profiles");
                }
            }

            // Développement (IDE, éditeurs de code)
            var devIndicators = new[] { "visual studio", "code", "ide", "studio", "intellij", "eclipse", "atom", "sublime" };
            if (devIndicators.Any(indicator => name.Contains(indicator)) || category.Contains("développement"))
            {
                foreach (var cleanName in cleanNames.Take(2))
                {
                    paths.Add($@"%APPDATA%\{cleanName}\User");
                    paths.Add($@"%USERPROFILE%\.{cleanName.ToLowerInvariant()}");
                    paths.Add($@"%USERPROFILE%\.config\{cleanName.ToLowerInvariant()}");
                }
            }

            // Communication (Discord, Teams, Slack, etc.)
            var commIndicators = new[] { "discord", "teams", "slack", "skype", "zoom", "telegram", "whatsapp" };
            if (commIndicators.Any(indicator => name.Contains(indicator)) || category.Contains("communication"))
            {
                foreach (var cleanName in cleanNames.Take(2))
                {
                    paths.Add($@"%APPDATA%\{cleanName}");
                    paths.Add($@"%LOCALAPPDATA%\{cleanName}");
                }
            }

            // Adobe spécifique
            var adobeIndicators = new[] { "adobe", "photoshop", "premiere", "after effects", "illustrator", "indesign", "acrobat" };
            if (adobeIndicators.Any(indicator => name.Contains(indicator)) || category.Contains("adobe"))
            {
                foreach (var cleanName in cleanNames.Take(2))
                {
                    paths.Add($@"%APPDATA%\Adobe\{cleanName}");
                    paths.Add($@"%USERPROFILE%\Documents\Adobe\{cleanName}");
                    paths.Add($@"%LOCALAPPDATA%\Adobe\{cleanName}");
                }
            }

            // Autres multimédia (OBS, VLC, etc.)
            var mediaIndicators = new[] { "obs", "vlc", "media", "player", "studio" };
            if (mediaIndicators.Any(indicator => name.Contains(indicator)) || category.Contains("multimédia"))
            {
                foreach (var cleanName in cleanNames.Take(2))
                {
                    paths.Add($@"%APPDATA%\{cleanName}");
                    paths.Add($@"%LOCALAPPDATA%\{cleanName}");
                    paths.Add($@"%USERPROFILE%\Documents\{cleanName}");

                    // Patterns spécifiques multimédia
                    if (name.Contains("obs"))
                    {
                        paths.Add($@"%APPDATA%\obs-studio");
                        paths.Add($@"%APPDATA%\obs-studio\basic\profiles\*");
                    }
                    if (name.Contains("vlc"))
                    {
                        paths.Add($@"%APPDATA%\vlc");
                        paths.Add($@"%LOCALAPPDATA%\VLC media player");
                    }
                }
            }

            return paths;
        }
    }
}