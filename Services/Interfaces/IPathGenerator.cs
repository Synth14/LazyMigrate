namespace LazyMigrate.Services.Interfaces
{
    /// <summary>
    /// Interface pour les générateurs de chemins de settings
    /// </summary>
    public interface IPathGenerator
    {
        /// <summary>
        /// Génère tous les chemins possibles pour un logiciel
        /// </summary>
        List<string> GenerateAllPaths(SoftwareInfo software, List<string> nameVariations, List<string> publisherVariations);
    }
}