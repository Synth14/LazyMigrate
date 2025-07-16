namespace LazyMigrate.Models
{
    public class MatchResult
    {
        public string SoftwareName { get; set; } = string.Empty;
        public string ProfileName { get; set; } = string.Empty;
        public bool IsMatch { get; set; }
        public string MatchReason { get; set; } = string.Empty;
        public double Confidence { get; set; } = 0.0;
    }

}
