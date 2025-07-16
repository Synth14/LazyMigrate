namespace LazyMigrate.Models
{
    public class SettingsFile
    {
        public string RelativePath { get; set; } = string.Empty; // Chemin relatif depuis la base
        public string FullPath { get; set; } = string.Empty; // Chemin complet sur le système
        public long Size { get; set; }
        public DateTime LastModified { get; set; }
        public bool IsDirectory { get; set; }
        public string ContentBase64 { get; set; } = string.Empty; // Contenu encodé pour le JSON
        public SettingsFileType FileType { get; set; }
        public bool IsBackedUp { get; set; } = false;

        // Propriétés calculées
        public string SizeFormatted
        {
            get
            {
                if (Size == 0) return "0 B";
                string[] sizes = { "B", "KB", "MB", "GB" };
                double len = Size;
                int order = 0;
                while (len >= 1024 && order < sizes.Length - 1)
                {
                    order++;
                    len /= 1024;
                }
                return $"{len:0.##} {sizes[order]}";
            }
        }
    }
}
