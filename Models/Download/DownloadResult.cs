namespace LazyMigrate.Models.Download
{
    /// <summary>
    /// Résultat de recherche de téléchargement pour un logiciel
    /// </summary>
    public class DownloadResult
    {
        public string SoftwareName { get; set; } = "";
        public string Publisher { get; set; } = "";
        public DateTime SearchStarted { get; set; }
        public DateTime SearchCompleted { get; set; }
        public bool IsSuccess { get; set; }
        public int TotalLinksFound { get; set; }
        public List<DownloadSource> Sources { get; set; } = new();
        public TimeSpan SearchDuration => SearchCompleted - SearchStarted;

        /// <summary>
        /// Meilleur lien trouvé (priorité : site officiel > GitHub > autres)
        /// </summary>
        public DownloadSource? BestDownloadLink =>
            Sources.Where(s => s.IsValid)
                   .OrderBy(s => GetSourcePriority(s.SourceType))
                   .ThenByDescending(s => s.Confidence)
                   .FirstOrDefault();

        private int GetSourcePriority(string sourceType) => sourceType switch
        {
            "Official Site" => 1,
            "Known Sites" => 2,
            "GitHub" => 3,
            "Winget" => 4,
            "Web Search" => 5,
            _ => 99
        };
    }
}