namespace LazyMigrate.Models
{

    public class SettingsPath
    {
        public string Path { get; set; } = string.Empty;
        public SettingsPathType Type { get; set; }
        public string Description { get; set; } = string.Empty;
        public bool IsRequired { get; set; } = true;
        public bool IsDirectory { get; set; } = false;
        public int Priority { get; set; } = 1; // 1 = haute priorité, 5 = basse priorité
    }
}
