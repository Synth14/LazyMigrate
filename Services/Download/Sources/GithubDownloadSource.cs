namespace LazyMigrate.Services.Download.Sources
{
    public class GitHubDownloadSource : IDownloadSource
    {
        public string SourceName => "GitHub";
        public int Priority => 2;

        private readonly Action<string>? _progressCallback;
        private readonly HttpClient _httpClient;

        public GitHubDownloadSource(Action<string>? progressCallback = null)
        {
            _progressCallback = progressCallback;
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "LazyMigrate/1.0");
        }

        public bool CanHandle(SoftwareWithDownload software)
        {
            // Prioriser les logiciels qui semblent être open source
            var name = software.Name.ToLowerInvariant();
            var openSourceIndicators = new[] { "code", "git", "notepad", "obs", "vlc", "audacity", "gimp", "blender" };
            return openSourceIndicators.Any(indicator => name.Contains(indicator));
        }

        public async Task<List<DownloadSource>> FindDownloadLinksAsync(SoftwareWithDownload software)
        {
            var results = new List<DownloadSource>();

            _progressCallback?.Invoke($"    🔍 Recherche sur GitHub...");

            try
            {
                // Recherche directe pour des repos connus
                var knownRepos = GetKnownGitHubRepos();
                var softwareLower = software.Name.ToLowerInvariant();

                foreach (var repo in knownRepos)
                {
                    if (softwareLower.Contains(repo.Key))
                    {
                        // Simuler un lien GitHub Release (en attendant l'API)
                        results.Add(new DownloadSource
                        {
                            SourceType = "GitHub",
                            DownloadUrl = $"https://github.com/{repo.Value}/releases/latest",
                            PageUrl = $"https://github.com/{repo.Value}",
                            FileName = "GitHub Release",
                            FileType = "exe",
                            IsValid = true,
                            Confidence = 0.85,
                            FoundAt = DateTime.Now
                        });

                        _progressCallback?.Invoke($"    ✅ GitHub repo trouvé: {repo.Value}");
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                _progressCallback?.Invoke($"    ⚠️ Erreur GitHub: {ex.Message}");
            }

            return results;
        }

        private Dictionary<string, string> GetKnownGitHubRepos()
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["notepad++"] = "notepad-plus-plus/notepad-plus-plus",
                ["obs"] = "obsproject/obs-studio",
                ["vlc"] = "videolan/vlc",
                ["audacity"] = "audacity/audacity",
                ["gimp"] = "GNOME/gimp",
                ["blender"] = "blender/blender",
                ["git"] = "git-for-windows/git",
                ["putty"] = "putty-tools/putty",
                ["wireshark"] = "wireshark/wireshark",
                ["handbrake"] = "HandBrake/HandBrake",
                ["krita"] = "KDE/krita",
                ["inkscape"] = "inkscape/inkscape"
            };
        }

        public void Dispose()
        {
            _httpClient?.Dispose();
        }
    }
}