namespace LazyMigrate.Models
{
    public class DownloadProgress
    {
        public string SoftwareName { get; set; } = "";
        public long DownloadedBytes { get; set; }
        public long TotalBytes { get; set; }
        public int ProgressPercent { get; set; }
        public long Speed { get; set; }
        public TimeSpan EstimatedTimeRemaining { get; set; }
        public DateTime StartTime { get; set; } = DateTime.Now;
        public string SpeedFormatted => FormatSpeed(Speed);
        public string EtaFormatted => FormatTimespan(EstimatedTimeRemaining);

        private string FormatSpeed(long bytesPerSecond)
        {
            if (bytesPerSecond < 1024) return $"{bytesPerSecond} B/s";
            if (bytesPerSecond < 1024 * 1024) return $"{bytesPerSecond / 1024:F1} KB/s";
            return $"{bytesPerSecond / (1024 * 1024):F1} MB/s";
        }

        private string FormatTimespan(TimeSpan timespan)
        {
            if (timespan.TotalSeconds < 60) return $"{timespan.Seconds}s";
            if (timespan.TotalMinutes < 60) return $"{timespan.Minutes}m {timespan.Seconds}s";
            return $"{timespan.Hours}h {timespan.Minutes}m";
        }
    }
}