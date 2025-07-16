namespace LazyMigrate.Models
{
    public class DownloadSourceInfo
    {
        public DownloadSourceType Type { get; set; }
        public string Url { get; set; } = "";
        public bool IsOfficial { get; set; }
        public string Notes { get; set; } = "";
    }
}
