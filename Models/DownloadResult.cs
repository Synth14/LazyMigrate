namespace LazyMigrate.Services
{
    public class DownloadResult
    {
        public SoftwareInfo Software { get; set; } = new();
        public DownloadStatus Status { get; set; }
        public string FilePath { get; set; } = "";
        public long FileSize { get; set; }
        public DownloadSource? Source { get; set; }
        public string ErrorMessage { get; set; } = "";
        public bool IsValidExecutable { get; set; }
        public DateTime DownloadTime { get; set; } = DateTime.Now;
        public TimeSpan DownloadDuration { get; set; }
        public string FileName => !string.IsNullOrEmpty(FilePath) ? System.IO.Path.GetFileName(FilePath) : "";
        public string FileSizeFormatted => FormatBytes(FileSize);

        private string FormatBytes(long bytes)
        {
            if (bytes < 1024) return $"{bytes} B";
            if (bytes < 1024 * 1024) return $"{bytes / 1024:F1} KB";
            if (bytes < 1024 * 1024 * 1024) return $"{bytes / (1024 * 1024):F1} MB";
            return $"{bytes / (1024L * 1024 * 1024):F1} GB";
        }
    }
}