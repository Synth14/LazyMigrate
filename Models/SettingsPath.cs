namespace QuickMigrate.Models
{

    public class SettingsPath
    {
        public string Path { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public SettingsPathType Type { get; set; }
        public bool IsRequired { get; set; } = true;
    }
}
