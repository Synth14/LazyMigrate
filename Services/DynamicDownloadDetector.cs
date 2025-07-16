
namespace LazyMigrate.Services
{
    public class DynamicDownloadDetector
    {
        private readonly HttpClient _httpClient;
        private readonly Action<string>? _progressCallback;

        public DynamicDownloadDetector(Action<string>? progressCallback = null)
        {
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Add("User-Agent",
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
            _progressCallback = progressCallback;
        }

        public async Task<List<DownloadSource>> FindDownloadLinksAsync(SoftwareInfo software)
        {
            var sources = new List<DownloadSource>();
            var softwareName = software.Name;
            var publisher = software.Publisher;

            ReportProgress($"🔍 Recherche dynamique pour {softwareName}...");

            try
            {
                // 1. Recherche Google intelligente
                var googleResults = await SearchGoogleAsync(softwareName, publisher);
                sources.AddRange(googleResults);

                // 2. Recherche GitHub automatique
                var githubResults = await SearchGitHubDynamicAsync(softwareName, publisher);
                sources.AddRange(githubResults);

                // 3. Recherche sur les sites de téléchargement populaires
                var aggregatorResults = await SearchDownloadAggregatorsAsync(softwareName);
                sources.AddRange(aggregatorResults);

                // 4. Extraction depuis les infos du registre
                var registryResults = ExtractFromSoftwareInfo(software);
                sources.AddRange(registryResults);

                // 5. Nettoyage et scoring des résultats
                sources = CleanAndScoreResults(sources, softwareName, publisher);

                ReportProgress($"  ✅ {sources.Count} sources trouvées pour {softwareName}");
            }
            catch (Exception ex)
            {
                ReportProgress($"  ❌ Erreur: {ex.Message}");
            }

            return sources.Take(5).ToList(); // Garder les 5 meilleures
        }

        private async Task<List<DownloadSource>> SearchGoogleAsync(string softwareName, string publisher)
        {
            var sources = new List<DownloadSource>();

            try
            {
                // Construire plusieurs requêtes Google intelligentes
                var queries = GenerateSearchQueries(softwareName, publisher);

                foreach (var query in queries.Take(3)) // Limiter à 3 recherches
                {
                    var googleUrl = $"https://www.google.com/search?q={Uri.EscapeDataString(query)}";
                    var response = await _httpClient.GetStringAsync(googleUrl);

                    // Extraire les URLs des résultats Google
                    var urls = ExtractUrlsFromGoogleResults(response);

                    foreach (var url in urls.Take(10))
                    {
                        // Analyser chaque page pour trouver des liens de téléchargement
                        var pageResults = await AnalyzePageForDownloads(url, softwareName);
                        sources.AddRange(pageResults);
                    }

                    await Task.Delay(1000); // Respecter Google
                }
            }
            catch (Exception ex)
            {
                ReportProgress($"    Erreur recherche Google: {ex.Message}");
            }

            return sources;
        }

        private List<string> GenerateSearchQueries(string softwareName, string publisher)
        {
            var queries = new List<string>();
            var cleanName = CleanSearchTerm(softwareName);
            var cleanPublisher = CleanSearchTerm(publisher);

            // Requêtes orientées téléchargement
            queries.Add($"{cleanName} download official");
            queries.Add($"{cleanName} download site:github.com");
            queries.Add($"\"{cleanName}\" download windows exe");

            if (!string.IsNullOrEmpty(cleanPublisher))
            {
                queries.Add($"{cleanPublisher} {cleanName} download");
                queries.Add($"site:{GuessOfficialDomain(cleanPublisher)} {cleanName} download");
            }

            // Requêtes spécifiques
            queries.Add($"{cleanName} installer download");
            queries.Add($"{cleanName} setup exe download");

            return queries;
        }

        private string GuessOfficialDomain(string publisher)
        {
            // Deviner le domaine officiel basé sur l'éditeur
            var cleaned = publisher.ToLowerInvariant()
                                 .Replace(" ", "")
                                 .Replace("inc", "")
                                 .Replace("llc", "")
                                 .Replace("corporation", "")
                                 .Replace("corp", "")
                                 .Replace(".", "");

            return $"{cleaned}.com";
        }

        private List<string> ExtractUrlsFromGoogleResults(string html)
        {
            var urls = new List<string>();

            // Pattern pour extraire les URLs des résultats Google
            var pattern = @"<a[^>]+href=""([^""]*)"">.*?</a>";
            var matches = Regex.Matches(html, pattern, RegexOptions.IgnoreCase);

            foreach (Match match in matches)
            {
                var url = match.Groups[1].Value;

                // Filtrer les URLs Google internes
                if (!url.Contains("google.com") &&
                    !url.Contains("youtube.com") &&
                    !url.StartsWith("/") &&
                    Uri.IsWellFormedUriString(url, UriKind.Absolute))
                {
                    urls.Add(url);
                }
            }

            return urls.Distinct().ToList();
        }

        private async Task<List<DownloadSource>> AnalyzePageForDownloads(string pageUrl, string softwareName)
        {
            var sources = new List<DownloadSource>();

            try
            {
                var html = await _httpClient.GetStringAsync(pageUrl);

                // Patterns pour détecter les liens de téléchargement
                var downloadPatterns = new[]
                {
                    @"href\s*=\s*[""']([^""']*\.exe)[""']",
                    @"href\s*=\s*[""']([^""']*\.msi)[""']",
                    @"href\s*=\s*[""']([^""']*download[^""']*)[""']",
                    @"data-download-url\s*=\s*[""']([^""']*)[""']",
                    @"<a[^>]*download[^>]*href\s*=\s*[""']([^""']*)[""']"
                };

                foreach (var pattern in downloadPatterns)
                {
                    var matches = Regex.Matches(html, pattern, RegexOptions.IgnoreCase);

                    foreach (Match match in matches)
                    {
                        var downloadUrl = match.Groups[1].Value;

                        if (IsValidDownloadUrl(downloadUrl, softwareName))
                        {
                            sources.Add(new DownloadSource
                            {
                                Name = $"{softwareName} (Auto-détecté)",
                                Url = NormalizeUrl(downloadUrl, pageUrl),
                                Type = DownloadSourceType.WebScraping,
                                IsOfficial = IsOfficialDomain(pageUrl, softwareName),
                                Notes = $"Trouvé sur {new Uri(pageUrl).Host}",
                                SourcePage = pageUrl
                            });
                        }
                    }
                }

                // Chercher aussi dans le texte des boutons/liens
                var buttonPatterns = new[]
                {
                    @"<button[^>]*>[^<]*download[^<]*</button>",
                    @"<a[^>]*>[^<]*download[^<]*</a>",
                    @"<a[^>]*>[^<]*télécharger[^<]*</a>"
                };

                foreach (var pattern in buttonPatterns)
                {
                    var matches = Regex.Matches(html, pattern, RegexOptions.IgnoreCase);
                    // Analyser le contexte autour des boutons pour trouver les vraies URLs
                }
            }
            catch (Exception ex)
            {
                // Page inaccessible, continuer
            }

            return sources;
        }

        private async Task<List<DownloadSource>> SearchGitHubDynamicAsync(string softwareName, string publisher)
        {
            var sources = new List<DownloadSource>();

            try
            {
                // Recherche GitHub via leur API de recherche
                var searchTerms = GenerateGitHubSearchTerms(softwareName, publisher);

                foreach (var term in searchTerms.Take(3))
                {
                    var searchUrl = $"https://api.github.com/search/repositories?q={Uri.EscapeDataString(term)}&sort=stars&order=desc";
                    var response = await _httpClient.GetStringAsync(searchUrl);
                    var searchResult = JsonSerializer.Deserialize<GitHubSearchResult>(response);

                    if (searchResult?.items != null)
                    {
                        foreach (var repo in searchResult.items.Take(5))
                        {
                            // Vérifier si ce repo correspond vraiment au logiciel
                            if (IsRelevantRepo(repo, softwareName))
                            {
                                var releaseSource = await GetLatestReleaseFromRepo(repo.full_name, softwareName);
                                if (releaseSource != null)
                                {
                                    sources.Add(releaseSource);
                                }
                            }
                        }
                    }

                    await Task.Delay(1000); // Respecter les limites GitHub
                }
            }
            catch (Exception ex)
            {
                ReportProgress($"    Erreur recherche GitHub: {ex.Message}");
            }

            return sources;
        }

        private List<string> GenerateGitHubSearchTerms(string softwareName, string publisher)
        {
            var terms = new List<string>();
            var cleanName = CleanSearchTerm(softwareName);
            var cleanPublisher = CleanSearchTerm(publisher);

            terms.Add(cleanName);
            terms.Add($"{cleanName} windows");
            terms.Add($"{cleanName} installer");

            if (!string.IsNullOrEmpty(cleanPublisher))
            {
                terms.Add($"{cleanPublisher} {cleanName}");
            }

            // Termes spécifiques pour des types de logiciels
            if (cleanName.Contains("code") || cleanName.Contains("editor"))
                terms.Add($"{cleanName} editor");
            if (cleanName.Contains("browser"))
                terms.Add($"{cleanName} browser");

            return terms;
        }

        private bool IsRelevantRepo(GitHubRepository repo, string softwareName)
        {
            var cleanSoftware = CleanSearchTerm(softwareName).ToLowerInvariant();
            var repoName = repo.name.ToLowerInvariant();
            var repoDesc = (repo.description ?? "").ToLowerInvariant();

            // Vérifications de pertinence
            return repoName.Contains(cleanSoftware) ||
                   repoDesc.Contains(cleanSoftware) ||
                   CalculateSimilarity(cleanSoftware, repoName) > 0.6;
        }

        private double CalculateSimilarity(string text1, string text2)
        {
            // Algorithme simple de similarité (Levenshtein simplifié)
            var words1 = text1.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var words2 = text2.Split(' ', StringSplitOptions.RemoveEmptyEntries);

            var commonWords = words1.Intersect(words2, StringComparer.OrdinalIgnoreCase).Count();
            var totalWords = Math.Max(words1.Length, words2.Length);

            return totalWords > 0 ? (double)commonWords / totalWords : 0;
        }

        private async Task<DownloadSource?> GetLatestReleaseFromRepo(string repoFullName, string softwareName)
        {
            try
            {
                var releaseUrl = $"https://api.github.com/repos/{repoFullName}/releases/latest";
                var response = await _httpClient.GetStringAsync(releaseUrl);
                var release = JsonSerializer.Deserialize<GitHubRelease>(response);

                if (release?.assets != null)
                {
                    var windowsAsset = release.assets.FirstOrDefault(a =>
                        (a.name.Contains(".exe", StringComparison.OrdinalIgnoreCase) ||
                         a.name.Contains(".msi", StringComparison.OrdinalIgnoreCase)) &&
                        (a.name.Contains("win", StringComparison.OrdinalIgnoreCase) ||
                         a.name.Contains("windows", StringComparison.OrdinalIgnoreCase) ||
                         !a.name.Contains("linux", StringComparison.OrdinalIgnoreCase) &&
                         !a.name.Contains("mac", StringComparison.OrdinalIgnoreCase)));

                    if (windowsAsset != null)
                    {
                        return new DownloadSource
                        {
                            Name = $"{softwareName} (GitHub Release)",
                            Url = windowsAsset.browser_download_url,
                            Type = DownloadSourceType.GitHub,
                            IsOfficial = true,
                            Notes = $"Release {release.tag_name} depuis {repoFullName}",
                            SourcePage = $"https://github.com/{repoFullName}/releases"
                        };
                    }
                }
            }
            catch
            {
                // Release non trouvée
            }

            return null;
        }

        private async Task<List<DownloadSource>> SearchDownloadAggregatorsAsync(string softwareName)
        {
            var sources = new List<DownloadSource>();

            // Sites d'agrégation populaires (sans avoir leurs APIs en dur)
            var aggregatorDomains = new[]
            {
                "sourceforge.net",
                "filehippo.com",
                "softonic.com",
                "download.com",
                "majorgeeks.com"
            };

            foreach (var domain in aggregatorDomains.Take(2)) // Limiter pour éviter la lenteur
            {
                try
                {
                    var searchUrl = $"https://www.google.com/search?q=site:{domain} {Uri.EscapeDataString(softwareName)} download";
                    var html = await _httpClient.GetStringAsync(searchUrl);
                    var urls = ExtractUrlsFromGoogleResults(html);

                    foreach (var url in urls.Take(3))
                    {
                        if (url.Contains(domain))
                        {
                            var pageResults = await AnalyzePageForDownloads(url, softwareName);
                            sources.AddRange(pageResults);
                        }
                    }

                    await Task.Delay(1000);
                }
                catch
                {
                    // Continuer avec le prochain site
                }
            }

            return sources;
        }

        private List<DownloadSource> ExtractFromSoftwareInfo(SoftwareInfo software)
        {
            var sources = new List<DownloadSource>();

            // Extraire des URLs depuis les infos du logiciel
            var textToAnalyze = new[]
            {
                software.UninstallString,
                software.InstallPath,
                software.Publisher
            };

            foreach (var text in textToAnalyze.Where(t => !string.IsNullOrEmpty(t)))
            {
                var urls = ExtractUrlsFromText(text!);
                foreach (var url in urls)
                {
                    if (CouldBeDownloadUrl(url))
                    {
                        sources.Add(new DownloadSource
                        {
                            Name = $"{software.Name} (Registry)",
                            Url = url,
                            Type = DownloadSourceType.Registry,
                            IsOfficial = false,
                            Notes = "URL extraite du registre Windows"
                        });
                    }
                }
            }

            return sources;
        }

        private List<DownloadSource> CleanAndScoreResults(List<DownloadSource> sources, string softwareName, string publisher)
        {
            // Nettoyer les doublons et scorer les résultats
            var uniqueSources = sources
                .GroupBy(s => s.Url)
                .Select(g => g.First())
                .ToList();

            // Scorer chaque source
            foreach (var source in uniqueSources)
            {
                source.Score = CalculateSourceScore(source, softwareName, publisher);
            }

            // Retourner les meilleures sources triées par score
            return uniqueSources
                .Where(s => s.Score > 30) // Seuil minimum
                .OrderByDescending(s => s.Score)
                .ToList();
        }

        private int CalculateSourceScore(DownloadSource source, string softwareName, string publisher)
        {
            var score = 0;

            // Bonus pour source officielle
            if (source.IsOfficial) score += 50;

            // Bonus selon le type
            score += source.Type switch
            {
                DownloadSourceType.GitHub => 40,
                DownloadSourceType.Official => 60,
                DownloadSourceType.WebScraping => 20,
                DownloadSourceType.Registry => 10,
                _ => 0
            };

            // Vérifier la pertinence de l'URL
            var urlLower = source.Url.ToLowerInvariant();
            var nameLower = softwareName.ToLowerInvariant();

            if (urlLower.Contains(nameLower.Replace(" ", ""))) score += 30;
            if (urlLower.Contains("download")) score += 20;
            if (urlLower.Contains(".exe") || urlLower.Contains(".msi")) score += 25;
            if (urlLower.Contains("official")) score += 15;

            // Pénalités
            if (urlLower.Contains("ad") || urlLower.Contains("spam")) score -= 30;
            if (urlLower.Contains("mirror")) score -= 10;

            return Math.Max(0, score);
        }

        // Méthodes utilitaires
        private string CleanSearchTerm(string term)
        {
            if (string.IsNullOrEmpty(term)) return "";

            return Regex.Replace(term, @"\([^)]*\)", "")
                       .Replace("Microsoft", "")
                       .Replace("Google", "")
                       .Replace("Inc", "")
                       .Replace("LLC", "")
                       .Trim();
        }

        private bool IsValidDownloadUrl(string url, string softwareName)
        {
            if (string.IsNullOrEmpty(url)) return false;

            var urlLower = url.ToLowerInvariant();
            var nameLower = softwareName.ToLowerInvariant();

            // Vérifications basiques
            return (urlLower.EndsWith(".exe") || urlLower.EndsWith(".msi") || urlLower.Contains("download")) &&
                   !urlLower.Contains("ad") &&
                   !urlLower.Contains("malware") &&
                   Uri.IsWellFormedUriString(url, UriKind.Absolute);
        }

        private bool IsOfficialDomain(string pageUrl, string softwareName)
        {
            try
            {
                var domain = new Uri(pageUrl).Host.ToLowerInvariant();
                var softwareLower = softwareName.ToLowerInvariant();

                // Vérifications heuristiques pour domaine officiel
                return domain.Contains(softwareLower.Replace(" ", "")) ||
                       domain.EndsWith(".org") ||
                       domain.EndsWith(".edu") ||
                       (domain.Contains("github") && !domain.Contains("gist"));
            }
            catch
            {
                return false;
            }
        }

        private string NormalizeUrl(string url, string baseUrl)
        {
            if (Uri.IsWellFormedUriString(url, UriKind.Absolute))
                return url;

            try
            {
                var baseUri = new Uri(baseUrl);
                var fullUrl = new Uri(baseUri, url);
                return fullUrl.ToString();
            }
            catch
            {
                return url;
            }
        }

        private List<string> ExtractUrlsFromText(string text)
        {
            var urls = new List<string>();
            var pattern = @"https?://[^\s\)""']+";
            var matches = Regex.Matches(text, pattern);

            foreach (Match match in matches)
            {
                urls.Add(match.Value);
            }

            return urls;
        }

        private bool CouldBeDownloadUrl(string url)
        {
            var urlLower = url.ToLowerInvariant();
            return urlLower.Contains("download") ||
                   urlLower.Contains("setup") ||
                   urlLower.Contains("installer") ||
                   urlLower.EndsWith(".exe") ||
                   urlLower.EndsWith(".msi");
        }

        private void ReportProgress(string message)
        {
            _progressCallback?.Invoke(message);
        }

        public void Dispose()
        {
            _httpClient?.Dispose();
        }
    }
}