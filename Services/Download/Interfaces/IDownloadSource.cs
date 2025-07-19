namespace LazyMigrate.Services.Download.Interfaces
{
    public interface IDownloadSource
    {
        /// <summary>
        /// Nom de la source (pour affichage et logs)
        /// </summary>
        string SourceName { get; }

        /// <summary>
        /// Priorité de la source (1 = plus haute priorité)
        /// </summary>
        int Priority { get; }

        /// <summary>
        /// Rechercher les liens de téléchargement pour un logiciel
        /// </summary>
        Task<List<DownloadSource>> FindDownloadLinksAsync(SoftwareWithDownload software);

        /// <summary>
        /// Vérifier si cette source peut traiter ce type de logiciel
        /// </summary>
        bool CanHandle(SoftwareWithDownload software);
    }
}
