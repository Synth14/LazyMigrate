namespace LazyMigrate.Models.Download
{
    public class SoftwareWithDownload : SoftwareInfo
    {
        public DownloadStatus DownloadStatus { get; set; } = DownloadStatus.NotSearched;
        public DownloadResult? DownloadResult { get; set; }
        public string DownloadStatusText => DownloadStatus switch
        {
            DownloadStatus.NotSearched => "⚪ Non cherché",
            DownloadStatus.Searching => "🔍 Recherche...",
            DownloadStatus.Found => $"✅ {DownloadResult?.TotalLinksFound} lien(s)",
            DownloadStatus.NotFound => "❌ Aucun lien",
            DownloadStatus.Error => "⚠️ Erreur",
            _ => "❓ Inconnu"
        };
    }
}
