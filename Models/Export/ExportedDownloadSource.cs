namespace LazyMigrate.Models.Export
{
    public class ExportedDownloadSource
    {
        public string Type { get; set; } = "";
        public string Url { get; set; } = "";
        public bool IsOfficial { get; set; }
        public string Notes { get; set; } = "";
    }
}
