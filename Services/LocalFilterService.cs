namespace LazyMigrate.Services
{
    public class LocalFilterService
    {
        private readonly HashSet<string> _excludePatterns;
        private readonly Dictionary<string, string> _knownUserSoftware;
        private readonly HashSet<string> _excludePublishers;

        public LocalFilterService()
        {
            _excludePatterns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            _knownUserSoftware = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            _excludePublishers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            LoadDataFromResources();
        }

        private void LoadDataFromResources()
        {
            try
            {
                var resourceManager = new ResourceManager("LazyMigrate.SoftwareData", Assembly.GetExecutingAssembly());

                // Charger les patterns d'exclusion
                var excludePatterns = resourceManager.GetString("ExcludePatterns");
                if (!string.IsNullOrEmpty(excludePatterns))
                {
                    foreach (var pattern in excludePatterns.Split(';'))
                    {
                        if (!string.IsNullOrWhiteSpace(pattern))
                        {
                            _excludePatterns.Add(pattern.Trim());
                        }
                    }
                }

                // Charger les logiciels connus
                var knownSoftware = resourceManager.GetString("KnownSoftware");
                if (!string.IsNullOrEmpty(knownSoftware))
                {
                    foreach (var entry in knownSoftware.Split(';'))
                    {
                        var parts = entry.Split(':');
                        if (parts.Length == 2 && !string.IsNullOrWhiteSpace(parts[0]) && !string.IsNullOrWhiteSpace(parts[1]))
                        {
                            _knownUserSoftware[parts[0].Trim()] = parts[1].Trim();
                        }
                    }
                }

                // Charger les éditeurs à exclure
                var excludePublishers = resourceManager.GetString("ExcludePublishers");
                if (!string.IsNullOrEmpty(excludePublishers))
                {
                    foreach (var publisher in excludePublishers.Split(';'))
                    {
                        if (!string.IsNullOrWhiteSpace(publisher))
                        {
                            _excludePublishers.Add(publisher.Trim());
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // Si les ressources ne se chargent pas, utiliser des valeurs par défaut
                Console.WriteLine($"Erreur chargement ressources: {ex.Message}");
                LoadDefaultData();
            }
        }

        private void LoadDefaultData()
        {
            // Données de fallback si les ressources ne fonctionnent pas
            var defaultExcludes = new[] {
                "Security Update", "Hotfix", "Update for", "KB", "Microsoft Visual C++ 20",
                "Microsoft .NET Framework", "Windows SDK", "Redistributable", "Runtime"
            };

            var defaultKnownSoftware = new Dictionary<string, string>
            {
                ["Visual Studio Code"] = "Développement",
                ["Google Chrome"] = "Navigateurs",
                ["Mozilla Firefox"] = "Navigateurs",
                ["Steam"] = "Jeux",
                ["Discord"] = "Communication",
                ["7-Zip"] = "Utilitaires"
            };

            var defaultExcludePublishers = new[] {
                "Intel Corporation", "NVIDIA Corporation", "Realtek"
            };

            foreach (var pattern in defaultExcludes)
                _excludePatterns.Add(pattern);

            foreach (var kvp in defaultKnownSoftware)
                _knownUserSoftware[kvp.Key] = kvp.Value;

            foreach (var publisher in defaultExcludePublishers)
                _excludePublishers.Add(publisher);
        }

        public Task<LocalFilterResult> ShouldIncludeSoftwareAsync(SoftwareInfo software)
        {
            var result = EvaluateSoftware(software);
            return Task.FromResult(result);
        }

        private LocalFilterResult EvaluateSoftware(SoftwareInfo software)
        {
            // 1. Vérifier les patterns d'exclusion
            foreach (var pattern in _excludePatterns)
            {
                if (software.Name.Contains(pattern, StringComparison.OrdinalIgnoreCase))
                {
                    return new LocalFilterResult
                    {
                        ShouldInclude = false,
                        Reason = $"Pattern exclu: {pattern}",
                        Confidence = 0.9
                    };
                }
            }

            // 2. Vérifier si c'est un logiciel utilisateur connu
            foreach (var knownSoft in _knownUserSoftware)
            {
                if (software.Name.Contains(knownSoft.Key, StringComparison.OrdinalIgnoreCase))
                {
                    return new LocalFilterResult
                    {
                        ShouldInclude = true,
                        Reason = $"Logiciel utilisateur connu: {knownSoft.Key}",
                        Confidence = 0.95,
                        SuggestedCategory = knownSoft.Value
                    };
                }
            }

            // 3. Vérifier les éditeurs à exclure
            foreach (var publisher in _excludePublishers)
            {
                if (software.Publisher.Contains(publisher, StringComparison.OrdinalIgnoreCase))
                {
                    return new LocalFilterResult
                    {
                        ShouldInclude = false,
                        Reason = $"Éditeur exclu: {publisher}",
                        Confidence = 0.8
                    };
                }
            }

            // 4. Règle spéciale pour Microsoft Corporation
            if (software.Publisher.Contains("Microsoft Corporation", StringComparison.OrdinalIgnoreCase))
            {
                return new LocalFilterResult
                {
                    ShouldInclude = false,
                    Reason = "Microsoft Corporation - composant système non reconnu",
                    Confidence = 0.7
                };
            }

            // 5. Heuristiques basées sur la taille et autres critères
            return ApplyHeuristics(software);
        }

        private LocalFilterResult ApplyHeuristics(SoftwareInfo software)
        {
            // Trop petit = probablement un composant
            if (software.EstimatedSize > 0 && software.EstimatedSize < 1024 * 1024)
            {
                return new LocalFilterResult
                {
                    ShouldInclude = false,
                    Reason = "Taille trop petite (< 1MB)",
                    Confidence = 0.6
                };
            }

            // Pas de chemin d'installation = suspect
            if (string.IsNullOrEmpty(software.InstallPath))
            {
                return new LocalFilterResult
                {
                    ShouldInclude = false,
                    Reason = "Pas de chemin d'installation",
                    Confidence = 0.5
                };
            }

            // Installation récente = plus probablement utilisateur
            if (software.InstallDate > DateTime.Now.AddYears(-2))
            {
                return new LocalFilterResult
                {
                    ShouldInclude = true,
                    Reason = "Installation récente (< 2 ans)",
                    Confidence = 0.6
                };
            }

            // Taille raisonnable = probablement utilisateur
            if (software.EstimatedSize > 5 * 1024 * 1024)
            {
                return new LocalFilterResult
                {
                    ShouldInclude = true,
                    Reason = "Taille raisonnable (> 5MB)",
                    Confidence = 0.5
                };
            }

            // Par défaut : inclure avec faible confiance
            return new LocalFilterResult
            {
                ShouldInclude = true,
                Reason = "Aucun critère d'exclusion trouvé",
                Confidence = 0.3
            };
        }

        // Méthodes pour gérer les règles de filtrage
        public void AddKnownSoftware(string name, string category)
        {
            _knownUserSoftware[name] = category;
        }

        public void AddExcludePattern(string pattern)
        {
            _excludePatterns.Add(pattern);
        }

        public void RemoveKnownSoftware(string name)
        {
            _knownUserSoftware.Remove(name);
        }

        public void RemoveExcludePattern(string pattern)
        {
            _excludePatterns.Remove(pattern);
        }

        public IReadOnlyDictionary<string, string> GetKnownSoftware()
        {
            return _knownUserSoftware;
        }

        public IReadOnlyCollection<string> GetExcludePatterns()
        {
            return _excludePatterns;
        }
    }

}