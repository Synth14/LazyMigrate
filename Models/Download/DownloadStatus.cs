namespace LazyMigrate.Models.Download
{
    public enum DownloadStatus
    {
        NotSearched,    // Pas encore cherché
        Searching,      // Recherche en cours
        Found,          // Lien(s) trouvé(s)
        NotFound,       // Aucun lien trouvé
        Error           // Erreur pendant la recherche
    }

}