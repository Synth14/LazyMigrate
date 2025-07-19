using LazyMigrate.Services.Detection.PathGenerators.Interfaces;

namespace LazyMigrate.Services.Detection.PathGenerators
{
    /// <summary>
    /// Générateur de chemins pour Documents, My Games, Saved Games
    /// </summary>
    public class DocumentsPathGenerator : IPathGenerator
    {
        public List<string> GenerateAllPaths(SoftwareWithDownload software, List<string> nameVariations, List<string> publisherVariations)
        {
            var paths = new List<string>();

            foreach (var name in nameVariations)
            {
                // Documents directs
                paths.Add($@"%USERPROFILE%\Documents\{name}");
                paths.Add($@"%USERPROFILE%\Documents\My {name}");

                // My Games - dossier standard pour les sauvegardes de jeux
                paths.Add($@"%USERPROFILE%\Documents\My Games\{name}");
                paths.Add($@"%USERPROFILE%\Documents\My Games\{name}\Saves");
                paths.Add($@"%USERPROFILE%\Documents\My Games\{name}\SaveGames");
                paths.Add($@"%USERPROFILE%\Documents\My Games\{name}\Config");
                paths.Add($@"%USERPROFILE%\Documents\My Games\{name}\Settings");

                // Saved Games (dossier Windows Vista+)
                paths.Add($@"%USERPROFILE%\Saved Games\{name}");
                paths.Add($@"%USERPROFILE%\Saved Games\{name}\Saves");
                paths.Add($@"%USERPROFILE%\Saved Games\{name}\Profiles");

                // Autres patterns de sauvegardes
                paths.Add($@"%USERPROFILE%\Documents\{name}\Saves");
                paths.Add($@"%USERPROFILE%\Documents\{name}\SaveGames");
                paths.Add($@"%USERPROFILE%\Documents\{name}\Profiles");
                paths.Add($@"%USERPROFILE%\Documents\{name}\Config");
                paths.Add($@"%USERPROFILE%\Documents\{name}\Settings");
                paths.Add($@"%USERPROFILE%\Documents\{name}\Data");

                // Avec éditeur
                foreach (var publisher in publisherVariations.Take(2))
                {
                    paths.Add($@"%USERPROFILE%\Documents\{publisher}\{name}");
                    paths.Add($@"%USERPROFILE%\Documents\{publisher}");
                    paths.Add($@"%USERPROFILE%\Documents\My Games\{publisher}\{name}");
                    paths.Add($@"%USERPROFILE%\Saved Games\{publisher}\{name}");
                }
            }

            return paths;
        }
    }
}