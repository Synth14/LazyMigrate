namespace LazyMigrate.Models
{
    public class DownloadSource
    {
        public string Url { get; set; } = string.Empty;
        public SourceType Type { get; set; }
        public bool IsOfficial { get; set; }
        public string Notes { get; set; } = string.Empty;
    }
}
