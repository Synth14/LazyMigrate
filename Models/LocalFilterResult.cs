namespace QuickMigrate.Models
{
    public class LocalFilterResult
    {
        public bool ShouldInclude { get; set; }
        public string Reason { get; set; } = string.Empty;
        public double Confidence { get; set; }
        public string? SuggestedCategory { get; set; }
    }
}
