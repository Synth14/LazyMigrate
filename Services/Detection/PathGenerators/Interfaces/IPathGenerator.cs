namespace LazyMigrate.Services.Detection.PathGenerators.Interfaces
{
    /// <summary>
    /// Interface pour les générateurs de chemins de settings
    /// </summary>
    public interface IPathGenerator
    {
        /// <summary>
        /// Génère tous les chemins possibles pour un logiciel
        /// </summary>
        List<string> GenerateAllPaths(SoftwareWithDownload software, List<string> nameVariations, List<string> publisherVariations);
    }
}