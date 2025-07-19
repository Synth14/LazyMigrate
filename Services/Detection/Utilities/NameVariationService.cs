namespace LazyMigrate.Services.Detection.Utilities
{
    /// <summary>
    /// Service pour générer toutes les variations de noms d'un logiciel
    /// </summary>
    public class NameVariationService
    {
        public List<string> GenerateNameVariations(string name)
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
            ApplyRomanArabicConversions(variations, withoutSpecialChars);

            // Supprimer mots courants de publishers/types
            ApplyCommonWordsCleaning(variations, withoutSpecialChars);

            // Générer variations de formatage pour chaque nom de base
            ApplyFormattingVariations(variations);

            // Variations spécifiques pour certains patterns
            ApplySpecialPatterns(variations);

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

        private void ApplyRomanArabicConversions(HashSet<string> variations, string baseName)
        {
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
        }

        private void ApplyCommonWordsCleaning(HashSet<string> variations, string baseName)
        {
            var commonWords = new[] {
                "Microsoft", "Google", "LLC", "Inc", "Corporation", "Corp", "Ltd",
                "Software", "App", "Application", "Studio", "Studios", "Games", "Team",
                "Entertainment", "Interactive", "Digital", "Technologies", "Systems",
                "Battle.net", "Steam", "Epic", "Origin", "Ubisoft", "EA", "Activision",
                "Blizzard", "Valve", "Bethesda", "2K", "Rockstar"
            };

            var cleanName = baseName;
            foreach (var word in commonWords)
            {
                cleanName = Regex.Replace(cleanName, $@"\b{Regex.Escape(word)}\b", "", RegexOptions.IgnoreCase).Trim();
            }
            if (!string.IsNullOrEmpty(cleanName)) variations.Add(cleanName);
        }

        private void ApplyFormattingVariations(HashSet<string> variations)
        {
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
        }

        private void ApplySpecialPatterns(HashSet<string> variations)
        {
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
        }

        public List<string> GenerateSmartAbbreviations(string name)
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
    }
}