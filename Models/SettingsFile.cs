namespace LazyMigrate.Models
{
    public class SettingsFile
    {
        public string RelativePath { get; set; } = "";
        public string FullPath { get; set; } = "";
        public long Size { get; set; }
        public DateTime LastModified { get; set; }
        public bool IsDirectory { get; set; }
        public SettingsFileType FileType { get; set; }

        public string SizeFormatted
        {
            get
            {
                if (Size < 1024) return $"{Size} B";
                if (Size < 1024 * 1024) return $"{Size / 1024:F1} KB";
                return $"{Size / (1024 * 1024):F1} MB";
            }
        }
    }
}