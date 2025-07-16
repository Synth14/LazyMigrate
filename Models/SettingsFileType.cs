namespace LazyMigrate.Models
{
    public enum SettingsFileType
    {
        Configuration,   // .json, .xml, .ini, .config
        UserData,       // Données utilisateur
        Cache,          // Cache (généralement exclu)
        Registry,       // Clés de registre exportées
        Database,       // .db, .sqlite
        Other
    }
}

