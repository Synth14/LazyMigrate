namespace LazyMigrate.Models.Download
{
    public class DownloadSource
    {
        public string SourceType { get; set; } = ""; // "Official Site", "GitHub", "Winget", etc.
        public string DownloadUrl { get; set; } = "";
        public string PageUrl { get; set; } = ""; // Page où le lien a été trouvé
        public string FileName { get; set; } = "";
        public string Version { get; set; } = "";
        public long? FileSizeBytes { get; set; }
        public string FileType { get; set; } = ""; // "exe", "msi", "zip", etc.
        public bool IsValid { get; set; }
        public double Confidence { get; set; } // 0.0 à 1.0
        public string? ErrorMessage { get; set; }
        public DateTime FoundAt { get; set; } = DateTime.Now;

        public string FileSizeFormatted => FileSizeBytes.HasValue
            ? FormatFileSize(FileSizeBytes.Value)
            : "Taille inconnue";

        private string FormatFileSize(long bytes)
        {
            if (bytes < 1024) return $"{bytes} B";
            if (bytes < 1024 * 1024) return $"{bytes / 1024:F1} KB";
            if (bytes < 1024 * 1024 * 1024) return $"{bytes / (1024 * 1024):F1} MB";
            return $"{bytes / (1024L * 1024 * 1024):F1} GB";
        }
    }
}