namespace LazyMigrate.Models
{
    public enum DownloadStatus
    {
        Success,          // Téléchargement réussi
        Error,            // Erreur lors du téléchargement
        NoSourceFound,    // Aucune source de téléchargement trouvée
        AlreadyExists,    // Fichier déjà présent
        Cancelled,        // Téléchargement annulé
        InvalidFile,      // Fichier téléchargé invalide
        NetworkError,     // Erreur réseau
        AccessDenied      // Accès refusé au fichier/dossier
    }
}