namespace LazyMigrate.Services
{
    public class SettingsDetector
    {
        private readonly Action<string>? _progressCallback;
        private readonly string _logFilePath;

        public SettingsDetector(Action<string>? progressCallback = null)
        {
            _progressCallback = progressCallback;
            _logFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SettingsDetector_Debug.txt");

            // Initialiser le fichier de log
            try
            {
                var initMessage = $"=== DEBUG SETTINGS DETECTOR - {DateTime.Now:yyyy-MM-dd HH:mm:ss} ===\n";
                initMessage += $"Chemin du fichier log: {_logFilePath}\n";
                initMessage += $"Dossier de base: {AppDomain.CurrentDomain.BaseDirectory}\n\n";

                File.WriteAllText(_logFilePath, initMessage);

                // Test d'écriture immédiat
                LogDebug("✅ SettingsDetector initialisé - fichier de log créé");
            }
            catch (Exception ex)
            {
                // Essayer un chemin alternatif si le premier échoue
                try
                {
                    _logFilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "SettingsDetector_Debug.txt");
                    File.WriteAllText(_logFilePath, $"=== DEBUG SETTINGS DETECTOR (Desktop) - {DateTime.Now:yyyy-MM-dd HH:mm:ss} ===\nErreur chemin original: {ex.Message}\n\n");
                    LogDebug("✅ SettingsDetector initialisé - fichier de log créé sur Desktop");
                }
                catch
                {
                    // Si même le Desktop échoue, utiliser un chemin temp
                    _logFilePath = Path.Combine(Path.GetTempPath(), "SettingsDetector_Debug.txt");
                }
            }
        }

        private void LogDebug(string message)
        {
            try
            {
                var timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
                var logMessage = $"[{timestamp}] {message}\n";
                File.AppendAllText(_logFilePath, logMessage);

                // Aussi afficher dans l'interface si possible
                _progressCallback?.Invoke(message);
            }
            catch { }
        }
        private string ExpandWildcardPath(string pathPattern)
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
                    // Lister tous les sous-dossiers
                    var subDirs = Directory.GetDirectories(basePath);
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
        public async Task<List<SettingsFile>> DetectSettingsAsync(SoftwareInfo software)
        {
            var settingsFiles = new List<SettingsFile>();
            var softwareName = software.Name;
            var publisher = software.Publisher;

            _progressCallback?.Invoke($"🔍 Détection avancée pour {softwareName}...");

            try
            {
                // 1. Générer toutes les variations possibles du nom
                var cleanNames = GenerateNameVariations(softwareName);
                var publisherNames = GenerateNameVariations(publisher);

                // DEBUG: Afficher les variations générées pour Persona 5 Tactica
                if (softwareName.Contains("Persona", StringComparison.OrdinalIgnoreCase))
                {
                    _progressCallback?.Invoke($"  🔧 DEBUG Variations pour {softwareName}: {string.Join(", ", cleanNames.Take(10))}");
                }

                // 2. Construire tous les chemins possibles à tester
                var searchPaths = new List<string>();

                searchPaths.AddRange(GenerateAppDataPaths(cleanNames, publisherNames));
                searchPaths.AddRange(GenerateLocalAppDataPaths(cleanNames, publisherNames));
                searchPaths.AddRange(GenerateUserProfilePaths(cleanNames));
                searchPaths.AddRange(GenerateDocumentsPaths(cleanNames, publisherNames));
                searchPaths.AddRange(GenerateProgramFilesPaths(software.InstallPath, cleanNames));
                searchPaths.AddRange(GenerateSpecializedPaths(software, cleanNames));
                searchPaths.AddRange(GenerateKnownPublisherPaths(cleanNames, software));

                // DEBUG: Afficher quelques chemins SEGA/P5T pour Persona
                if (softwareName.Contains("Persona", StringComparison.OrdinalIgnoreCase))
                {
                    var segaPaths = searchPaths.Where(p => p.Contains("SEGA") || p.Contains("P5T")).Take(5);
                    _progressCallback?.Invoke($"  🔧 DEBUG Chemins SEGA: {string.Join(" | ", segaPaths)}");
                }

                // 3. Tester tous les chemins
                var foundPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                foreach (var pathPattern in searchPaths.Distinct())
                {
                    try
                    {
                        if(softwareName.Contains("Persona"))
                        File.AppendAllText(_logFilePath, pathPattern);

                        //var expandedPath = ExpandEnvironmentPath(pathPattern);
                        var expandedPath = ExpandWildcardPath(pathPattern); // Au lieu de ExpandEnvironmentPath
                        // DEBUG: Tester spécifiquement les chemins SEGA pour Persona
                        if (softwareName.Contains("Persona", StringComparison.OrdinalIgnoreCase) &&
                            (pathPattern.Contains("SEGA") || pathPattern.Contains("P5T")))
                        {
                            _progressCallback?.Invoke($"  🔧 DEBUG Test: {expandedPath} - Existe: {Directory.Exists(expandedPath)}");
                        }

                        if (Directory.Exists(expandedPath))
                        {
                            var dirFiles = await ScanDirectoryForSettings(expandedPath, softwareName);
                            if (dirFiles.Any())
                            {
                                settingsFiles.AddRange(dirFiles);
                                foundPaths.Add(pathPattern);
                            }
                        }
                        else if (File.Exists(expandedPath) && IsSettingsFile(expandedPath, softwareName))
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
                            foundPaths.Add(pathPattern);
                        }
                    }
                    catch
                    {
                        // Ignorer les erreurs d'accès
                    }
                }

                // 4. Scanner le registre Windows
                var registrySettings = await ScanRegistryForSettings(cleanNames, publisherNames);
                settingsFiles.AddRange(registrySettings);

                // 5. Recherche fuzzy globale dans les dossiers communs
                var fuzzySettings = await ScanFuzzyGlobal(cleanNames, softwareName);
                settingsFiles.AddRange(fuzzySettings);

                // 6. Filtrer et prioriser les résultats
                var filteredSettings = FilterAndPrioritizeSettings(settingsFiles, software);

                var summary = GetSettingsSummary(filteredSettings);
                _progressCallback?.Invoke($"  ✅ {summary}");

                return filteredSettings;
            }
            catch (Exception ex)
            {
                _progressCallback?.Invoke($"  ❌ Erreur: {ex.Message}");
                return new List<SettingsFile>();
            }
        }

        private List<string> GenerateNameVariations(string name)
        {
            if (string.IsNullOrEmpty(name)) return new List<string>();

            var variations = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // Nom original
            variations.Add(name);

            // Supprimer parenthèses et contenu
            var withoutParens = Regex.Replace(name, @"\([^)]*\)", "").Trim();
            if (!string.IsNullOrEmpty(withoutParens)) variations.Add(withoutParens);

            // Supprimer caractères spéciaux (®, ™, ©, etc.)
            var withoutSpecialChars = Regex.Replace(withoutParens, @"[®™©→←↑↓•◆■□▲▼★☆♠♣♥♦]", "").Trim();
            if (!string.IsNullOrEmpty(withoutSpecialChars)) variations.Add(withoutSpecialChars);

            // Supprimer versions et numéros
            var withoutVersion = Regex.Replace(withoutSpecialChars, @"\s+\d+(\.\d+)*", "").Trim();
            if (!string.IsNullOrEmpty(withoutVersion)) variations.Add(withoutVersion);

            // Convertir chiffres romains en arabes et vice versa
            var romanToArabic = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { " III", " 3" }, { " II", " 2" }, { " IV", " 4" }, { " V", " 5" },
                { " VI", " 6" }, { " VII", " 7" }, { " VIII", " 8" }, { " IX", " 9" }, { " X", " 10" }
            };

            var arabicToRoman = romanToArabic.ToDictionary(kvp => kvp.Value, kvp => kvp.Key, StringComparer.OrdinalIgnoreCase);

            var baseVariations = variations.ToList();
            foreach (var variation in baseVariations)
            {
                // Convertir romain → arabe
                foreach (var conversion in romanToArabic)
                {
                    if (variation.Contains(conversion.Key, StringComparison.OrdinalIgnoreCase))
                    {
                        variations.Add(variation.Replace(conversion.Key, conversion.Value, StringComparison.OrdinalIgnoreCase));
                    }
                }

                // Convertir arabe → romain
                foreach (var conversion in arabicToRoman)
                {
                    if (variation.Contains(conversion.Key, StringComparison.OrdinalIgnoreCase))
                    {
                        variations.Add(variation.Replace(conversion.Key, conversion.Value, StringComparison.OrdinalIgnoreCase));
                    }
                }
            }

            // Supprimer mots courants de publishers/types
            var commonWords = new[] {
                "Microsoft", "Google", "LLC", "Inc", "Corporation", "Corp", "Ltd",
                "Software", "App", "Application", "Studio", "Studios", "Games", "Team",
                "Entertainment", "Interactive", "Digital", "Technologies", "Systems",
                "Battle.net", "Steam", "Epic", "Origin", "Ubisoft", "EA", "Activision",
                "Blizzard", "Valve", "Bethesda", "2K", "Rockstar"
            };

            var cleanName = withoutSpecialChars;
            foreach (var word in commonWords)
            {
                cleanName = Regex.Replace(cleanName, $@"\b{Regex.Escape(word)}\b", "", RegexOptions.IgnoreCase).Trim();
            }
            if (!string.IsNullOrEmpty(cleanName)) variations.Add(cleanName);

            // Générer variations de formatage pour chaque nom de base
            var finalBaseNames = variations.ToList();
            foreach (var baseName in finalBaseNames)
            {
                if (string.IsNullOrEmpty(baseName) || baseName.Length <= 1) continue;

                // Sans espaces
                variations.Add(baseName.Replace(" ", ""));
                // Avec underscores
                variations.Add(baseName.Replace(" ", "_"));
                // Avec tirets
                variations.Add(baseName.Replace(" ", "-"));
                // Lowercase
                variations.Add(baseName.ToLowerInvariant());
                // Uppercase
                variations.Add(baseName.ToUpperInvariant());

                // Premier mot seulement
                var firstWord = baseName.Split(' ').First().Trim();
                if (firstWord.Length > 2) variations.Add(firstWord);

                // Dernier mot si composé
                var words = baseName.Split(' ');
                if (words.Length > 1)
                {
                    var lastWord = words.Last().Trim();
                    if (lastWord.Length > 2) variations.Add(lastWord);
                }

                // Combiner premiers et derniers mots pour les noms longs
                if (words.Length > 2)
                {
                    variations.Add($"{words.First()} {words.Last()}");
                }
            }

            // Variations spécifiques pour certains patterns
            var allVariations = variations.ToList();
            foreach (var variation in allVariations)
            {
                // Supprimer "The " au début
                if (variation.StartsWith("The ", StringComparison.OrdinalIgnoreCase))
                {
                    variations.Add(variation.Substring(4));
                }

                // Ajouter "The " au début s'il n'y est pas
                if (!variation.StartsWith("The ", StringComparison.OrdinalIgnoreCase) && variation.Length > 3)
                {
                    variations.Add($"The {variation}");
                }
            }

            // Générer des abréviations intelligentes
            var abbreviations = GenerateSmartAbbreviations(withoutSpecialChars);
            foreach (var abbrev in abbreviations)
            {
                variations.Add(abbrev);
            }

            return variations.Where(v => !string.IsNullOrEmpty(v) && v.Length > 1)
                           .Distinct(StringComparer.OrdinalIgnoreCase)
                           .ToList();
        }

        private List<string> GenerateSmartAbbreviations(string name)
        {
            var abbreviations = new List<string>();
            if (string.IsNullOrEmpty(name)) return abbreviations;

            var words = name.Split(new[] { ' ', '-', '_', '.' }, StringSplitOptions.RemoveEmptyEntries)
                           .Where(w => w.Length > 0)
                           .ToList();

            if (words.Count < 2) return abbreviations;

            // 1. Initiales simples (première lettre de chaque mot)
            var initials = string.Join("", words.Select(w => w[0]));
            if (initials.Length >= 2 && initials.Length <= 6)
            {
                abbreviations.Add(initials.ToUpperInvariant());
                abbreviations.Add(initials.ToLowerInvariant());
            }

            // 2. Abréviations avec chiffres préservés
            var initialsWithNumbers = "";
            foreach (var word in words)
            {
                if (word.All(char.IsDigit))
                {
                    // Si c'est un nombre, le garder entier
                    initialsWithNumbers += word;
                }
                else
                {
                    // Sinon, prendre la première lettre
                    initialsWithNumbers += word[0];
                }
            }
            if (initialsWithNumbers != initials && initialsWithNumbers.Length >= 2)
            {
                abbreviations.Add(initialsWithNumbers.ToUpperInvariant());
                abbreviations.Add(initialsWithNumbers.ToLowerInvariant());
            }

            // 3. Patterns spécifiques pour les jeux
            // Exemple: "Persona 5 Tactica" → "P5T"
            if (words.Count >= 3)
            {
                var specialPattern = "";
                for (int i = 0; i < words.Count; i++)
                {
                    var word = words[i];
                    if (word.All(char.IsDigit))
                    {
                        specialPattern += word; // Garder les chiffres
                    }
                    else if (word.All(char.IsLetter))
                    {
                        specialPattern += word[0]; // Première lettre
                    }
                }
                if (specialPattern.Length >= 2 && specialPattern != initials && specialPattern != initialsWithNumbers)
                {
                    abbreviations.Add(specialPattern.ToUpperInvariant());
                    abbreviations.Add(specialPattern.ToLowerInvariant());
                }
            }

            // 4. Abréviations courantes de mots
            var commonAbbreviations = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Persona"] = "P",
                ["Final Fantasy"] = "FF",
                ["Grand Theft Auto"] = "GTA",
                ["Call of Duty"] = "COD",
                ["Battlefield"] = "BF",
                ["Counter Strike"] = "CS",
                ["World of Warcraft"] = "WOW",
                ["League of Legends"] = "LoL",
                ["Defense of the Ancients"] = "DOTA",
                ["Player Unknown"] = "PU",
                ["Battlegrounds"] = "BG",
                ["Total War"] = "TW",
                ["Command and Conquer"] = "CNC",
                ["Age of Empires"] = "AOE",
                ["Civilization"] = "CIV",
                ["Street Fighter"] = "SF",
                ["Mortal Kombat"] = "MK",
                ["Assassin's Creed"] = "AC",
                ["Mass Effect"] = "ME",
                ["Elder Scrolls"] = "ES",
                ["Fallout New Vegas"] = "FNV",
                ["Red Dead Redemption"] = "RDR",
                ["Grand Strategy"] = "GS"
            };

            var nameUpper = name.ToUpperInvariant();
            foreach (var abbrev in commonAbbreviations)
            {
                if (nameUpper.Contains(abbrev.Key.ToUpperInvariant()))
                {
                    var abbreviated = nameUpper.Replace(abbrev.Key.ToUpperInvariant(), abbrev.Value);
                    // Nettoyer les espaces multiples
                    abbreviated = Regex.Replace(abbreviated, @"\s+", " ").Trim();
                    if (abbreviated.Length >= 2)
                    {
                        abbreviations.Add(abbreviated);
                        abbreviations.Add(abbreviated.ToLowerInvariant());
                    }
                }
            }

            // 5. Combinaisons première lettre + nombre + dernière lettre
            if (words.Count >= 3)
            {
                var hasNumber = words.Any(w => w.Any(char.IsDigit));
                if (hasNumber)
                {
                    var pattern = "";
                    pattern += words[0][0]; // Première lettre du premier mot

                    // Ajouter tous les chiffres trouvés
                    foreach (var word in words)
                    {
                        if (word.All(char.IsDigit))
                        {
                            pattern += word;
                        }
                        else
                        {
                            var digits = new string(word.Where(char.IsDigit).ToArray());
                            if (digits.Length > 0) pattern += digits;
                        }
                    }

                    pattern += words.Last()[0]; // Première lettre du dernier mot

                    if (pattern.Length >= 2)
                    {
                        abbreviations.Add(pattern.ToUpperInvariant());
                        abbreviations.Add(pattern.ToLowerInvariant());
                    }
                }
            }

            return abbreviations.Distinct().ToList();
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
                    }

                    // Patterns Steam avec publishers
                    paths.Add($@"%APPDATA%\{publisher}\{name}\Steam\*");
                    paths.Add($@"%LOCALAPPDATA%\{publisher}\{name}\Steam\*");
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

                // Patterns Unreal Engine (observés avec Warhammer Boltgun, The Sinking City)
                paths.Add($@"%LOCALAPPDATA%\{name}\Saved");
                paths.Add($@"%LOCALAPPDATA%\{name}\Saved\SaveGames");
                paths.Add($@"%LOCALAPPDATA%\{name}\Saved\SaveGames\*");
                paths.Add($@"%LOCALAPPDATA%\{name}Game\Saved");
                paths.Add($@"%LOCALAPPDATA%\{name}Game\Saved\SaveGames");
                paths.Add($@"%LOCALAPPDATA%\{name}Game\Saved\SaveGames\*");

                // Patterns avec suffixes de jeux
                var gameSuffixes = new[] { "Game", "The Game", "" };
                foreach (var suffix in gameSuffixes)
                {
                    var nameWithSuffix = string.IsNullOrEmpty(suffix) ? name : $"{name}{suffix}";
                    paths.Add($@"%LOCALAPPDATA%\{nameWithSuffix}\Saved\SaveGames");
                    paths.Add($@"%LOCALAPPDATA%\{nameWithSuffix}\Saved\Config");
                    paths.Add($@"%LOCALAPPDATA%\{nameWithSuffix}\Saved\Logs");
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

        private List<string> GenerateDocumentsPaths(List<string> cleanNames, List<string> publisherNames)
        {
            var paths = new List<string>();

            foreach (var name in cleanNames)
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
                foreach (var publisher in publisherNames.Take(2))
                {
                    paths.Add($@"%USERPROFILE%\Documents\{publisher}\{name}");
                    paths.Add($@"%USERPROFILE%\Documents\{publisher}");
                    paths.Add($@"%USERPROFILE%\Documents\My Games\{publisher}\{name}");
                    paths.Add($@"%USERPROFILE%\Saved Games\{publisher}\{name}");
                }
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

            // Multimédia (Adobe, OBS, VLC, etc.)
            var mediaIndicators = new[] { "adobe", "photoshop", "premiere", "obs", "vlc", "media", "player", "studio" };
            if (mediaIndicators.Any(indicator => name.Contains(indicator)) || category.Contains("multimédia"))
            {
                foreach (var cleanName in cleanNames.Take(2))
                {
                    paths.Add($@"%APPDATA%\Adobe\{cleanName}");
                    paths.Add($@"%USERPROFILE%\Documents\Adobe\{cleanName}");
                    paths.Add($@"%APPDATA%\{cleanName}");
                }
            }

            return paths;
        }

        private async Task<List<SettingsFile>> ScanDirectoryForSettings(string directoryPath, string softwareName)
        {
            var settingsFiles = new List<SettingsFile>();

            try
            {
                await Task.Run(() =>
                {
                    // Profondeur augmentée pour les jeux avec des structures complexes
                    var maxDepth = directoryPath.ToLowerInvariant().Contains("save") ? 4 : 2;
                    ScanDirectoryRecursive(directoryPath, settingsFiles, softwareName, 0, maxDepth);
                });
            }
            catch
            {
                // Ignorer les erreurs d'accès
            }

            return settingsFiles;
        }

        private void ScanDirectoryRecursive(string directoryPath, List<SettingsFile> settingsFiles, string softwareName, int depth, int maxDepth)
        {
            if (depth > maxDepth || settingsFiles.Count > 50) return;

            try
            {
                // Scanner les fichiers du dossier courant
                var files = Directory.GetFiles(directoryPath)
                                   .Where(f => IsSettingsFile(f, softwareName))
                                   .Take(20);

                foreach (var file in files)
                {
                    try
                    {
                        var fileInfo = new FileInfo(file);
                        if (fileInfo.Length < 50 * 1024 * 1024) // Max 50MB
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

                // Scanner les sous-dossiers pertinents
                if (depth < maxDepth)
                {
                    var subdirs = Directory.GetDirectories(directoryPath)
                                          .Where(d => ShouldScanSubdirectory(d, softwareName))
                                          .Take(10); // Augmenté pour capturer plus de sous-dossiers

                    foreach (var subdir in subdirs)
                    {
                        ScanDirectoryRecursive(subdir, settingsFiles, softwareName, depth + 1, maxDepth);
                    }
                }
            }
            catch
            {
                // Ignorer les erreurs d'accès aux dossiers
            }
        }

        private bool ShouldScanSubdirectory(string directoryPath, string softwareName)
        {
            var dirName = Path.GetFileName(directoryPath).ToLowerInvariant();
            var softwareLower = softwareName.ToLowerInvariant();
            var parentPath = Path.GetDirectoryName(directoryPath)?.ToLowerInvariant() ?? "";

            // Dossiers à scanner
            var importantDirs = new[] {
                "config", "settings", "user", "data", "saves", "save", "savegames",
                "profiles", "preferences", "default", "saved", "steam", "epic"
            };
            if (importantDirs.Any(important => dirName.Contains(important))) return true;

            // Scanner si le nom du dossier ressemble au logiciel
            if (dirName.Contains(softwareLower.Replace(" ", ""))) return true;

            // Scanner les dossiers qui ressemblent à des IDs utilisateur (Steam, Epic, etc.)
            if (IsUserIdDirectory(dirName)) return true;

            // Scanner les dossiers numériques dans les dossiers de sauvegarde (ex: 100, 200, 300)
            if (parentPath.Contains("save") || parentPath.Contains("savegame"))
            {
                if (dirName.All(char.IsDigit) && dirName.Length >= 1 && dirName.Length <= 5)
                {
                    return true;
                }
            }

            // Scanner les dossiers avec des noms courts dans les contextes de jeux
            if (dirName.Length <= 4 && dirName.All(c => char.IsLetterOrDigit(c)))
            {
                // Dans des contextes de jeux (SEGA, Steam, etc.)
                if (parentPath.Contains("sega") || parentPath.Contains("steam") ||
                    parentPath.Contains("epic") || parentPath.Contains("save"))
                {
                    return true;
                }
            }

            // Éviter les dossiers volumineux
            var avoidDirs = new[] { "cache", "temp", "log", "crash", "backup", "update", "installer" };
            if (avoidDirs.Any(avoid => dirName.Contains(avoid))) return false;

            return true;
        }

        private bool IsUserIdDirectory(string directoryName)
        {
            // Vérifier si c'est un ID utilisateur (Steam = 17 chiffres, Epic = format spécifique)
            if (directoryName.All(char.IsDigit))
            {
                // Steam ID (17 chiffres), ou autres IDs numériques courts
                return directoryName.Length >= 8 && directoryName.Length <= 20;
            }

            // GUID ou ID alphanumériques
            if (directoryName.Length >= 8 && directoryName.Length <= 40)
            {
                var alphaNumericCount = directoryName.Count(c => char.IsLetterOrDigit(c) || c == '-');
                return alphaNumericCount == directoryName.Length;
            }

            return false;
        }

        private async Task<List<SettingsFile>> ScanRegistryForSettings(List<string> cleanNames, List<string> publisherNames)
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

        private async Task<List<SettingsFile>> ScanFuzzyGlobal(List<string> cleanNames, string originalSoftwareName)
        {
            var settingsFiles = new List<SettingsFile>();

            try
            {
                // Dossiers racines à scanner avec recherche fuzzy
                var basePaths = new[]
                {
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "My Games"),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Saved Games"),
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)
                }.Where(Directory.Exists);

                foreach (var basePath in basePaths)
                {
                    try
                    {
                        var directories = Directory.GetDirectories(basePath);

                        foreach (var directory in directories.Take(100)) // Limiter pour éviter la lenteur
                        {
                            var dirName = Path.GetFileName(directory);

                            // Recherche fuzzy : ce dossier correspond-il au logiciel ?
                            if (IsFuzzyMatch(dirName, cleanNames, originalSoftwareName))
                            {
                                var dirFiles = await ScanDirectoryForSettings(directory, originalSoftwareName);
                                if (dirFiles.Any())
                                {
                                    settingsFiles.AddRange(dirFiles);
                                    _progressCallback?.Invoke($"  🔍 Correspondance: {Path.GetFileName(basePath)}\\{dirName}");
                                }
                            }
                        }
                    }
                    catch
                    {
                        // Ignorer les erreurs d'accès aux dossiers
                    }
                }
            }
            catch
            {
                // Ignorer les erreurs
            }

            return settingsFiles;
        }

        private bool IsFuzzyMatch(string directoryName, List<string> cleanNames, string originalSoftwareName)
        {
            var dirNameLower = directoryName.ToLowerInvariant();
            var originalLower = originalSoftwareName.ToLowerInvariant();

            // 1. Correspondance exacte avec une des variations (déjà testée normalement)
            foreach (var cleanName in cleanNames)
            {
                if (dirNameLower.Equals(cleanName.ToLowerInvariant(), StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            // 2. Le nom du dossier contient une partie significative du logiciel
            var significantWords = ExtractSignificantWords(originalLower);

            foreach (var word in significantWords)
            {
                if (dirNameLower.Contains(word) && word.Length > 3)
                    return true;
            }

            // 3. Le nom du logiciel contient le nom du dossier (ou vice versa)
            var dirWords = ExtractSignificantWords(dirNameLower);
            foreach (var dirWord in dirWords)
            {
                if (dirWord.Length > 3 && originalLower.Contains(dirWord))
                    return true;
            }

            // 4. Recherche par similarité textuelle pour les noms courts/similaires
            foreach (var cleanName in cleanNames.Take(3))
            {
                if (CalculateLevenshteinSimilarity(dirNameLower, cleanName.ToLowerInvariant()) > 0.75)
                    return true;
            }

            // 5. Patterns spéciaux (initiales, abréviations courantes)
            if (IsAbbreviationMatch(dirNameLower, originalLower))
                return true;

            return false;
        }

        private List<string> ExtractSignificantWords(string text)
        {
            // Mots à ignorer car trop génériques
            var commonWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "game", "games", "software", "app", "application", "program", "tool", "tools",
                "studio", "studios", "edition", "version", "the", "and", "or", "for", "with",
                "inc", "llc", "corp", "corporation", "ltd", "limited", "co", "company"
            };

            return text.Split(new[] { ' ', '-', '_', '.', '(', ')', '[', ']' }, StringSplitOptions.RemoveEmptyEntries)
                      .Where(word => word.Length > 2 && !commonWords.Contains(word))
                      .Select(word => word.ToLowerInvariant())
                      .Distinct()
                      .ToList();
        }

        private double CalculateLevenshteinSimilarity(string text1, string text2)
        {
            if (string.IsNullOrEmpty(text1) || string.IsNullOrEmpty(text2))
                return 0;

            var maxLength = Math.Max(text1.Length, text2.Length);
            if (maxLength == 0) return 1;

            var distance = CalculateLevenshteinDistance(text1, text2);
            return 1.0 - (double)distance / maxLength;
        }

        private int CalculateLevenshteinDistance(string text1, string text2)
        {
            var matrix = new int[text1.Length + 1, text2.Length + 1];

            for (int i = 0; i <= text1.Length; i++)
                matrix[i, 0] = i;

            for (int j = 0; j <= text2.Length; j++)
                matrix[0, j] = j;

            for (int i = 1; i <= text1.Length; i++)
            {
                for (int j = 1; j <= text2.Length; j++)
                {
                    var cost = text1[i - 1] == text2[j - 1] ? 0 : 1;
                    matrix[i, j] = Math.Min(
                        Math.Min(matrix[i - 1, j] + 1, matrix[i, j - 1] + 1),
                        matrix[i - 1, j - 1] + cost);
                }
            }

            return matrix[text1.Length, text2.Length];
        }

        private bool IsAbbreviationMatch(string directoryName, string softwareName)
        {
            // Vérifier si le nom du dossier pourrait être une abréviation
            if (directoryName.Length < softwareName.Length / 3) // Le dossier est beaucoup plus court
            {
                var softwareWords = ExtractSignificantWords(softwareName);
                var initials = string.Join("", softwareWords.Select(w => w.FirstOrDefault()));

                if (directoryName.Equals(initials, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        private bool IsSettingsFile(string filePath, string softwareName)
        {
            var fileName = Path.GetFileName(filePath).ToLowerInvariant();
            var extension = Path.GetExtension(filePath).ToLowerInvariant();
            var softwareLower = softwareName.ToLowerInvariant();

            // Extensions de configuration connues
            var configExtensions = new[] {
                ".json", ".xml", ".ini", ".conf", ".config", ".cfg", ".yaml", ".yml",
                ".toml", ".plist", ".reg", ".dat", ".db", ".sqlite", ".sqlite3",
                ".properties", ".settings", ".prefs"
            };

            if (configExtensions.Contains(extension)) return true;

            // Extensions de sauvegarde de jeux
            var saveExtensions = new[] {
                ".sav", ".save", ".dat", ".bin", ".gam", ".sg", ".slot", ".usr", ".pro", ".profile"
            };

            if (saveExtensions.Contains(extension)) return true;

            // Noms de fichiers typiques
            var configNames = new[] {
                "settings", "preferences", "config", "options", "user", "profile",
                "bookmarks", "history", "state", "session", "workspace", "save", "data"
            };

            if (configNames.Any(name => fileName.Contains(name))) return true;

            // Fichiers de sauvegarde typiques (sans extension ou avec extensions inhabituelles)
            var saveNames = new[] {
                "save", "savegame", "savedata", "gamesave", "slot", "checkpoint", "progress", "game"
            };

            if (saveNames.Any(name => fileName.Contains(name))) return true;

            // Fichiers spécifiques au logiciel
            if (fileName.Contains(softwareLower.Replace(" ", ""))) return true;

            // Fichiers dans des dossiers de sauvegarde (même sans extension évidente)
            var directoryPath = Path.GetDirectoryName(filePath)?.ToLowerInvariant() ?? "";
            if (directoryPath.Contains("save") || directoryPath.Contains("savegame") || directoryPath.Contains("saves"))
            {
                // Dans un dossier de sauvegarde, accepter plus de types de fichiers
                if (extension == "" || // Fichiers sans extension
                    extension == ".tmp" || // Fichiers temporaires de sauvegarde
                    fileName.All(c => char.IsDigit(c) || c == '.')) // Fichiers avec noms numériques
                {
                    return true;
                }
            }

            // Fichiers avec des noms numériques dans des dossiers de sauvegarde (ex: 100, 200, 300)
            if (fileName.All(char.IsDigit) && fileName.Length >= 1 && fileName.Length <= 5)
            {
                return true;
            }

            return false;
        }

        private List<SettingsFile> FilterAndPrioritizeSettings(List<SettingsFile> settingsFiles, SoftwareInfo software)
        {
            // Filtrer les fichiers non pertinents
            var filtered = settingsFiles.Where(sf => ShouldIncludeSettingsFile(sf)).ToList();

            // Prioriser et limiter
            return filtered.OrderBy(sf => GetFilePriority(sf, software))
                          .ThenByDescending(sf => sf.LastModified)
                          .Take(15) // Max 15 fichiers par logiciel
                          .ToList();
        }

        private bool ShouldIncludeSettingsFile(SettingsFile settingsFile)
        {
            var fileName = Path.GetFileName(settingsFile.FullPath).ToLowerInvariant();

            // Exclure les fichiers temporaires et logs
            var excludeNames = new[] { "debug.log", "error.log", "crash.log", "temp.dat", "cache.dat" };
            if (excludeNames.Contains(fileName)) return false;

            // Exclure les extensions temporaires
            var excludeExts = new[] { ".tmp", ".temp", ".log", ".bak", ".old" };
            if (excludeExts.Contains(Path.GetExtension(fileName))) return false;

            // Limiter la taille
            if (settingsFile.Size > 20 * 1024 * 1024 || settingsFile.Size < 5) return false;

            return true;
        }

        private int GetFilePriority(SettingsFile settingsFile, SoftwareInfo software)
        {
            var fileName = Path.GetFileName(settingsFile.FullPath).ToLowerInvariant();

            // Priorité 1 = le plus important
            var importantFiles = new[] { "settings.json", "config.json", "preferences.json", "user.config" };
            if (importantFiles.Contains(fileName)) return 1;

            if (settingsFile.FileType == SettingsFileType.Configuration) return 2;
            if (settingsFile.FileType == SettingsFileType.UserData) return 3;
            if (settingsFile.FileType == SettingsFileType.Database) return 4;
            if (settingsFile.FileType == SettingsFileType.Registry) return 5;

            return 6;
        }

        private SettingsFileType GetFileType(string filePath)
        {
            var extension = Path.GetExtension(filePath).ToLowerInvariant();
            return extension switch
            {
                ".json" or ".xml" or ".ini" or ".config" or ".conf" or ".cfg" or ".yaml" or ".yml" => SettingsFileType.Configuration,
                ".db" or ".sqlite" or ".sqlite3" => SettingsFileType.Database,
                ".reg" => SettingsFileType.Registry,
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

        private string GetSettingsSummary(List<SettingsFile> settingsFiles)
        {
            if (!settingsFiles.Any()) return "❌ Aucun setting";

            var totalSize = settingsFiles.Sum(sf => sf.Size);
            var configCount = settingsFiles.Count(sf => sf.FileType == SettingsFileType.Configuration);
            var dataCount = settingsFiles.Count(sf => sf.FileType == SettingsFileType.UserData);
            var dbCount = settingsFiles.Count(sf => sf.FileType == SettingsFileType.Database);
            var regCount = settingsFiles.Count(sf => sf.FileType == SettingsFileType.Registry);

            var summary = $"✅ {settingsFiles.Count} fichiers";

            var parts = new List<string>();
            if (configCount > 0) parts.Add($"{configCount} config");
            if (dataCount > 0) parts.Add($"{dataCount} data");
            if (dbCount > 0) parts.Add($"{dbCount} db");
            if (regCount > 0) parts.Add($"{regCount} reg");

            if (parts.Any()) summary += $" ({string.Join(", ", parts)})";
            summary += $" • {FormatFileSize(totalSize)}";

            return summary;
        }

        private string FormatFileSize(long bytes)
        {
            if (bytes < 1024) return $"{bytes} B";
            if (bytes < 1024 * 1024) return $"{bytes / 1024:F1} KB";
            if (bytes < 1024 * 1024 * 1024) return $"{bytes / (1024 * 1024):F1} MB";
            return $"{bytes / (1024L * 1024 * 1024):F1} GB";
        }
    }
}