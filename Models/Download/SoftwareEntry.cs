
namespace LazyMigrate.Models.Download
{
    public class SoftwareEntry
    {
        public string Name { get; set; } = "";
        public string Publisher { get; set; } = "";
        public string OfficialSite { get; set; } = "";
        public string DownloadUrl { get; set; } = "";
        public string[] AlternativeNames { get; set; } = Array.Empty<string>();
        public string[] Keywords { get; set; } = Array.Empty<string>();
        public DownloadFileInfo FileInfo { get; set; } = new();
        public double Confidence { get; set; } = 0.8;

        public override bool Equals(object? obj) => obj is SoftwareEntry other && Name.Equals(other.Name, StringComparison.OrdinalIgnoreCase);
        public override int GetHashCode() => Name.ToLowerInvariant().GetHashCode();
    }
}
