namespace LazyMigrate.Models.Download
{
    public enum DownloadSourceType
    {
        Official,      // Site officiel du logiciel
        GitHub,        // Release GitHub
        WebScraping,   // Détecté par web scraping
        Registry,      // Extrait du registre Windows
        Aggregator,    // Site d'agrégation (SourceForge, etc.)
        Mirror,        // Site miroir
        Portable,      // Version portable
        Package        // Package manager (Chocolatey, etc.)
    }
}