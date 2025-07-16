namespace LazyMigrate.Services
{
    public class SoftwareDownloader
    {
        private readonly string _downloadFolder;
        private readonly Action<string>? _progressCallback;
        private readonly Action<DownloadProgress>? _downloadProgressCallback;
        private readonly HttpClient _httpClient;

        public SoftwareDownloader(string downloadFolder,
            Action<string>? progressCallback = null,
            Action<DownloadProgress>? downloadProgressCallback = null)
        {
            _downloadFolder = downloadFolder;
            _progressCallback = progressCallback;
            _downloadProgressCallback = downloadProgressCallback;

            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "LazyMigrate/1.0");

            Directory.CreateDirectory(_downloadFolder);
        }

        public async Task<List<DownloadResult>> DownloadSoftwareAsync(
            List<SoftwareInfo> softwareList,
            CancellationToken cancellationToken = default)
        {
            var results = new List<DownloadResult>();

            _progressCallback?.Invoke($"🚀 Début du téléchargement de {softwareList.Count} logiciels...");

            for (int i = 0; i < softwareList.Count; i++)
            {
                if (cancellationToken.IsCancellationRequested)
                    break;

                var software = softwareList[i];
                _progressCallback?.Invoke($"📦 [{i + 1}/{softwareList.Count}] {software.Name}");

                try
                {
                    // Pour l'instant, simuler une recherche de téléchargement
                    await Task.Delay(1000, cancellationToken); // Simulation

                    // Résultat simulé - à remplacer par la vraie logique plus tard
                    results.Add(new DownloadResult
                    {
                        Software = software,
                        Status = DownloadStatus.NoSourceFound,
                        ErrorMessage = "Fonction de téléchargement en cours d'implémentation"
                    });
                }
                catch (Exception ex)
                {
                    results.Add(new DownloadResult
                    {
                        Software = software,
                        Status = DownloadStatus.Error,
                        ErrorMessage = ex.Message
                    });
                }
            }

            _progressCallback?.Invoke($"✅ Recherche terminée: {results.Count} logiciels traités");
            return results;
        }

        public void Dispose()
        {
            _httpClient?.Dispose();
        }
    }
}