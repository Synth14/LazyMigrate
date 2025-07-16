namespace LazyMigrate.Services
{
    public class DownloadSource
    {
        public string Name { get; set; } = "";
        public string Url { get; set; } = "";
        public DownloadSourceType Type { get; set; }
        public bool IsOfficial { get; set; }
        public string Notes { get; set; } = "";
        public string SourcePage { get; set; } = "";
        public int Score { get; set; }
        public DateTime DiscoveredAt { get; set; } = DateTime.Now;
        public long? ExpectedFileSize { get; set; }
        public string? ExpectedFileName { get; set; }
        public Dictionary<string, string> Metadata { get; set; } = new();
        public string TypeDescription => Type switch
        {
            DownloadSourceType.Official => "Site officiel",
            DownloadSourceType.GitHub => "GitHub Release",
            DownloadSourceType.WebScraping => "Détection automatique",
            DownloadSourceType.Registry => "Registre Windows",
            DownloadSourceType.Aggregator => "Site de téléchargement",
            _ => "Inconnu"
        };

        public string TrustLevel => IsOfficial ? "🛡️ Fiable" : "⚠️ À vérifier";
    }
}