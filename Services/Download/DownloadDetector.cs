namespace LazyMigrate.Services.Download
{
    /// <summary>
    /// Service principal de détection des liens de téléchargement avec fallback en cascade
    /// </summary>
    public class DownloadDetector
    {
        private readonly List<IDownloadSource> _downloadSources;
        private readonly Action<string>? _progressCallback;

        public DownloadDetector(Action<string>? progressCallback = null)
        {
            _progressCallback = progressCallback;

            // Ordre de priorité des sources (stratégie 1 en premier, puis fallbacks)
            _downloadSources = new List<IDownloadSource>
            {
                new WebSearchDownloadSource(progressCallback),      // Stratégie 1 : Recherche web
                new KnownSitesDownloadSource(progressCallback),     // Fallback 1 : Sites connus  
                new GitHubDownloadSource(progressCallback),         // Fallback 2 : GitHub API
                new WingetDownloadSource(progressCallback)          // Fallback 3 : Winget API
            };
        }

        public async Task<DownloadResult> FindDownloadLinksAsync(SoftwareInfo software)
        {
            _progressCallback?.Invoke($"🔍 Recherche téléchargement pour {software.Name}...");

            var result = new DownloadResult
            {
                SoftwareName = software.Name,
                Publisher = software.Publisher,
                SearchStarted = DateTime.Now,
                Sources = new List<DownloadSource>()
            };

            // Essayer chaque source dans l'ordre de priorité
            foreach (var source in _downloadSources)
            {
                try
                {
                    _progressCallback?.Invoke($"  📡 Tentative: {source.SourceName}");

                    var sourceResults = await source.FindDownloadLinksAsync(software);
                    result.Sources.AddRange(sourceResults);

                    // Si on a trouvé des liens valides, on peut s'arrêter ou continuer selon la stratégie
                    if (sourceResults.Any(s => s.IsValid))
                    {
                        _progressCallback?.Invoke($"  ✅ Trouvé via {source.SourceName}");

                        // Stratégie : s'arrêter au premier succès pour être rapide
                        // Ou continuer pour avoir plus d'options (à configurer)
                        if (ShouldStopOnFirstSuccess(source))
                        {
                            break;
                        }
                    }
                }
                catch (Exception ex)
                {
                    _progressCallback?.Invoke($"  ❌ Erreur {source.SourceName}: {ex.Message}");

                    // Ajouter l'erreur mais continuer avec la source suivante
                    result.Sources.Add(new DownloadSource
                    {
                        SourceType = source.SourceName,
                        IsValid = false,
                        ErrorMessage = ex.Message
                    });
                }
            }

            result.SearchCompleted = DateTime.Now;
            result.IsSuccess = result.Sources.Any(s => s.IsValid);
            result.TotalLinksFound = result.Sources.Count(s => s.IsValid);

            var summary = result.IsSuccess
                ? $"✅ {result.TotalLinksFound} lien(s) trouvé(s)"
                : "❌ Aucun lien trouvé";

            _progressCallback?.Invoke($"  {summary}");

            return result;
        }

        private bool ShouldStopOnFirstSuccess(IDownloadSource source)
        {
            // Stratégie : s'arrêter si on trouve via recherche web ou sites connus
            // Continuer pour GitHub/Winget pour avoir plus d'options
            return source is WebSearchDownloadSource or KnownSitesDownloadSource;
        }

        public async Task<List<DownloadResult>> FindDownloadLinksForMultipleSoftwareAsync(List<SoftwareInfo> softwareList)
        {
            var results = new List<DownloadResult>();
            var total = softwareList.Count;

            _progressCallback?.Invoke($"🚀 Recherche téléchargements pour {total} logiciels...");

            for (int i = 0; i < softwareList.Count; i++)
            {
                var software = softwareList[i];
                _progressCallback?.Invoke($"📦 [{i + 1}/{total}] {software.Name}");

                var result = await FindDownloadLinksAsync(software);
                results.Add(result);

                // Petite pause pour éviter le rate limiting
                if (i < softwareList.Count - 1)
                {
                    await Task.Delay(500);
                }
            }

            var successCount = results.Count(r => r.IsSuccess);
            _progressCallback?.Invoke($"🎯 Terminé: {successCount}/{total} logiciels avec liens trouvés");

            return results;
        }
    }
}