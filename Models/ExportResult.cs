namespace QuickMigrate.Models
{    public class ExportResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public List<string> CopiedFiles { get; set; } = new();
        public List<string> Errors { get; set; } = new();
    }
}
