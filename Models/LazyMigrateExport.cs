
namespace LazyMigrate.Models
{
    public class LazyMigrateExport
    {
        public DateTime ExportDate { get; set; }
        public string ComputerName { get; set; } = "";
        public string UserName { get; set; } = "";
        public string OperatingSystem { get; set; } = "";
        public int TotalSoftware { get; set; }
        public List<ExportedSoftware> Software { get; set; } = new();
    }
}
