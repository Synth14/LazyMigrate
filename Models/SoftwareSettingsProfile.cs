namespace QuickMigrate.Models
{
    public class SoftwareSettingsProfile
    {
        public string SoftwareName { get; set; } = string.Empty;
        public List<string> AlternativeNames { get; set; } = new(); // "VS Code", "Visual Studio Code", etc.
        public List<SettingsPath> ConfigPaths { get; set; } = new();
        public List<string> ExcludePatterns { get; set; } = new(); // *.log, cache/, temp/
        public RestoreStrategy Strategy { get; set; } = RestoreStrategy.OverwriteExisting;
        public bool RequiresElevation { get; set; } = false;
        public bool BackupBeforeRestore { get; set; } = true;
        public string Notes { get; set; } = string.Empty;

        // Méthodes utilitaires
        public bool MatchesSoftware(string softwareName)
        {
            if (string.Equals(SoftwareName, softwareName, StringComparison.OrdinalIgnoreCase))
                return true;

            return AlternativeNames.Any(alt =>
                softwareName.Contains(alt, StringComparison.OrdinalIgnoreCase) ||
                alt.Contains(softwareName, StringComparison.OrdinalIgnoreCase));
        }
    }
}
