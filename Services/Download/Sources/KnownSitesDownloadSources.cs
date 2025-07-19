namespace LazyMigrate.Services.Download.Sources
{
    public class KnownSitesDownloadSource : IDownloadSource
    {
        public string SourceName => "Known Sites";
        public int Priority => 1;

        private readonly Action<string>? _progressCallback;
        private readonly SoftwareDatabase _database;

        public KnownSitesDownloadSource(Action<string>? progressCallback = null)
        {
            _progressCallback = progressCallback;
            _database = new SoftwareDatabase();
        }

        public bool CanHandle(SoftwareWithDownload software)
        {
            return true;
        }

        public async Task<List<DownloadSource>> FindDownloadLinksAsync(SoftwareWithDownload software)
        {
            await Task.CompletedTask;
            var results = new List<DownloadSource>();

            _progressCallback?.Invoke($"    🔍 Recherche dans base de données...");

            // Recherche optimisée avec scoring
            var matches = _database.FindMatches(software.Name, software.Publisher);

            foreach (var match in matches.Take(3)) // Top 3 matches
            {
                results.Add(new DownloadSource
                {
                    SourceType = "Known Sites",
                    DownloadUrl = match.DownloadUrl,
                    PageUrl = match.OfficialSite,
                    FileName = match.FileInfo.FileName,
                    FileType = match.FileInfo.FileType,
                    IsValid = true,
                    Confidence = match.Confidence,
                    FoundAt = DateTime.Now
                });

                _progressCallback?.Invoke($"    ✅ Match trouvé: {match.Name} (confiance: {match.Confidence:P0})");
            }

            return results;
        }
    }
}