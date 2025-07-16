using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LazyMigrate.Models
{
    public class LazyMigrateExport
    {
        public DateTime ExportDate { get; set; }
        public string ComputerName { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public string OperatingSystem { get; set; } = string.Empty;
        public int TotalSoftware { get; set; }
        public List<ExportedSoftware> Software { get; set; } = new();
    }
}
