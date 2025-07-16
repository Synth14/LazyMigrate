namespace LazyMigrate.Models
{
    public class ExportedDownloadSource
    {
        public string Type { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
        public bool IsOfficial { get; set; }
        public string Notes { get; set; } = string.Empty;
    }
}
