using System.Collections.Generic;
using System.Linq;

namespace Winspeqt.Models
{
    public class OptimizationTaskResult
    {
        public string TaskName { get; set; } = "";
        public string Icon { get; set; } = "";
        public bool Success { get; set; }
        public long BytesFreed { get; set; }
        public string StatusMessage { get; set; } = "";
        public bool IsOptional { get; set; }

        public string FormattedBytesFreed
        {
            get
            {
                double bytes = BytesFreed;
                if (bytes >= 1_073_741_824) return $"{bytes / 1_073_741_824:F1} GB";
                if (bytes >= 1_048_576) return $"{bytes / 1_048_576:F1} MB";
                if (bytes >= 1_024) return $"{bytes / 1_024:F1} KB";
                if (bytes > 0) return $"{bytes} B";
                return "—";
            }
        }
    }

    public class OptimizationResult
    {
        public List<OptimizationTaskResult> TaskResults { get; set; } = new();
        public long TotalBytesFreed => TaskResults.Sum(t => t.BytesFreed);
        public int TasksCompleted => TaskResults.Count(t => t.Success);
        public int TasksFailed => TaskResults.Count(t => !t.Success);

        public string FormattedBytesFreed
        {
            get
            {
                double bytes = TotalBytesFreed;
                if (bytes >= 1_073_741_824) return $"{bytes / 1_073_741_824:F1} GB";
                if (bytes >= 1_048_576) return $"{bytes / 1_048_576:F1} MB";
                if (bytes >= 1_024) return $"{bytes / 1_024:F1} KB";
                return $"{bytes} B";
            }
        }
    }

    public class OptimizationOptions
    {
        // Always-on (defaults)
        public bool CleanRecycleBin { get; set; } = true;
        public bool CleanTempFiles { get; set; } = true;
        public bool CleanThumbnailCache { get; set; } = true;
        public bool FlushDnsCache { get; set; } = true;
        public bool CleanPrefetch { get; set; } = true;
        public bool CleanWindowsErrorReports { get; set; } = true;
        public bool CleanCrashDumps { get; set; } = true;

        // Optional toggles (off by default)
        public bool CleanWindowsUpdateCache { get; set; } = false;
        public bool CleanEventLogs { get; set; } = false;
        public bool CleanBrowserCache { get; set; } = false;
    }
}