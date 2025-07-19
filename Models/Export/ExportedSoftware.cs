namespace LazyMigrate.Models.Export
{
    public class ExportedSoftware
    {
        public string Name { get; set; } = string.Empty;
        public string Publisher { get; set; } = string.Empty;
        public string Version { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string InstallPath { get; set; } = string.Empty;
        public string UninstallString { get; set; } = string.Empty;
        public long EstimatedSize { get; set; }
        public DateTime InstallDate { get; set; }
        public bool IncludeSettings { get; set; }
        public List<string> SettingsPaths { get; set; } = new();
        public List<string> ExecutablePaths { get; set; } = new();
        public List<ExportedDownloadSource> DownloadSources { get; set; } = new();
    }
}
