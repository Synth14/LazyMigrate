using LazyMigrate.Services.Download.Interfaces;

namespace LazyMigrate.Services.Download.Sources
{
    /// <summary>
    /// Source de téléchargement via recherche web intelligente (Stratégie 1 prioritaire)
    /// </summary>
    public class WebSearchDownloadSource : IDownloadSource
    {
        public string SourceName => "Web Search";
        public int Priority => 1;

        private readonly HttpClient _httpClient;
        private readonly Action<string>? _progressCallback;

        public WebSearchDownloadSource(Action<string>? progressCallback = null)
        {
            _progressCallback = progressCallback;
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Add("User-Agent",
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
        }

        public bool CanHandle(SoftwareWithDownload software)
        {
            // Peut traiter tous les logiciels
            return true;
        }

        public async Task<List<DownloadSource>> FindDownloadLinksAsync(SoftwareWithDownload software)
        {
            var results = new List<DownloadSource>();

            try
            {
                // 1. Construire les requêtes de recherche intelligentes
                var searchQueries = BuildSearchQueries(software);

                // 2. Essayer chaque requête
                foreach (var query in searchQueries.Take(3)) // Limiter pour éviter la lenteur
                {
                    var queryResults = await SearchAndParseResults(query, software);
                    results.AddRange(queryResults);

                    // Si on trouve des résultats de haute confiance, on peut s'arrêter
                    if (queryResults.Any(r => r.Confidence > 0.8))
                    {
                        break;
                    }
                }

                return results.DistinctBy(r => r.DownloadUrl).ToList();
            }
            catch (Exception ex)
            {
                _progressCallback?.Invoke($"    ❌ Erreur recherche web: {ex.Message}");
                return results;
            }
        }

        private List<string> BuildSearchQueries(SoftwareWithDownload software)
        {
            var queries = new List<string>();
            var name = software.Name;
            var publisher = software.Publisher ?? "";

            // Requêtes par ordre de priorité (plus spécifique = plus efficace)
            queries.Add($"\"{name}\" \"{publisher}\" download official site");
            queries.Add($"\"{name}\" download official");
            queries.Add($"{name} {publisher} download");
            queries.Add($"{name} download site:github.com");
            queries.Add($"{name} download exe msi");

            return queries;
        }

        private async Task<List<DownloadSource>> SearchAndParseResults(string query, SoftwareWithDownload software)
        {
            var results = new List<DownloadSource>();

            try
            {
                // Utiliser DuckDuckGo ou Bing (plus permissifs que Google)
                var searchUrl = $"https://duckduckgo.com/html/?q={Uri.EscapeDataString(query)}";

                _progressCallback?.Invoke($"    🔎 Recherche: {query}");

                var response = await _httpClient.GetStringAsync(searchUrl);

                // Parser les résultats et extraire les liens potentiels
                var links = ExtractDownloadLinksFromSearchResults(response, software);
                results.AddRange(links);

            }
            catch (Exception ex)
            {
                _progressCallback?.Invoke($"    ⚠️ Erreur requête: {ex.Message}");
            }

            return results;
        }

        private List<DownloadSource> ExtractDownloadLinksFromSearchResults(string html, SoftwareWithDownload software)
        {
            var results = new List<DownloadSource>();

            try
            {
                // Patterns regex pour trouver les liens de téléchargement
                var downloadPatterns = new[]
                {
                    @"href=[""']([^""']*download[^""']*\.(?:exe|msi|zip|dmg))[""']",
                    @"href=[""']([^""']*\.(?:exe|msi))[""']",
                    @"href=[""']([^""']*github\.com[^""']*releases[^""']*)[""']",
                };

                foreach (var pattern in downloadPatterns)
                {
                    var matches = Regex.Matches(html, pattern, RegexOptions.IgnoreCase);

                    foreach (Match match in matches)
                    {
                        var url = match.Groups[1].Value;

                        if (IsValidDownloadUrl(url, software))
                        {
                            results.Add(new DownloadSource
                            {
                                SourceType = "Web Search",
                                DownloadUrl = url,
                                PageUrl = "", // URL de la page de recherche
                                FileName = Path.GetFileName(url),
                                FileType = Path.GetExtension(url).TrimStart('.'),
                                IsValid = true,
                                Confidence = CalculateConfidence(url, software),
                                FoundAt = DateTime.Now
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _progressCallback?.Invoke($"    ⚠️ Erreur parsing: {ex.Message}");
            }

            return results;
        }

        private bool IsValidDownloadUrl(string url, SoftwareWithDownload software)
        {
            if (string.IsNullOrEmpty(url) || !Uri.IsWellFormedUriString(url, UriKind.Absolute))
                return false;

            // Vérifier que l'URL contient des éléments liés au logiciel
            var urlLower = url.ToLowerInvariant();
            var nameLower = software.Name.ToLowerInvariant();

            // Doit contenir au moins une partie du nom ou être sur un site fiable
            var trustedDomains = new[] { "github.com", "sourceforge.net", "microsoft.com", "adobe.com" };

            return trustedDomains.Any(domain => urlLower.Contains(domain)) ||
                   urlLower.Contains(nameLower.Replace(" ", "")) ||
                   urlLower.Contains(nameLower.Replace(" ", "-"));
        }

        private double CalculateConfidence(string url, SoftwareWithDownload software)
        {
            double confidence = 0.5; // Base
            var urlLower = url.ToLowerInvariant();
            var nameLower = software.Name.ToLowerInvariant();

            // Bonus pour sites officiels/fiables
            if (urlLower.Contains("github.com")) confidence += 0.2;
            if (urlLower.Contains("sourceforge.net")) confidence += 0.2;
            if (urlLower.Contains("microsoft.com")) confidence += 0.3;
            if (urlLower.Contains("adobe.com")) confidence += 0.3;

            // Bonus pour correspondance nom
            if (urlLower.Contains(nameLower.Replace(" ", ""))) confidence += 0.2;
            if (urlLower.Contains("download")) confidence += 0.1;

            // Bonus pour extensions fiables
            if (urlLower.EndsWith(".exe") || urlLower.EndsWith(".msi")) confidence += 0.1;

            return Math.Min(confidence, 1.0);
        }

        public void Dispose()
        {
            _httpClient?.Dispose();
        }
    }
}