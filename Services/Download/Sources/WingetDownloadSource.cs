namespace LazyMigrate.Services.Download.Sources
{
    public class WingetDownloadSource : IDownloadSource
    {
        public string SourceName => "Winget";
        public int Priority => 4;

        private readonly Action<string>? _progressCallback;

        public WingetDownloadSource(Action<string>? progressCallback = null)
        {
            _progressCallback = progressCallback;
        }

        public bool CanHandle(SoftwareWithDownload software)
        {
            return true;
        }

        public async Task<List<DownloadSource>> FindDownloadLinksAsync(SoftwareWithDownload software)
        {
            await Task.CompletedTask;

            // TODO: Implémenter l'API Winget
            _progressCallback?.Invoke($"    🔍 Recherche via Winget...");

            return new List<DownloadSource>();
        }
    }
}