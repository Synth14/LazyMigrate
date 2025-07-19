namespace LazyMigrate.Models.Download
{
    public class DownloadFileInfo
    {
        public string FileName { get; set; } = "";
        public string FileType { get; set; } = "";
        public long? FileSizeBytes { get; set; }
    }
}
