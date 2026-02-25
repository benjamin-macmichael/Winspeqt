using System;

namespace Winspeqt.Models
{
    public class ProcessInfo
    {
        // Basic process info (for resource trends view)
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public long Memory { get; set; } = 0;

        // Extended info for Task Manager view
        public int ProcessId { get; set; }
        public string ProcessName { get; set; } = "";
        public string Description { get; set; } = "";
        public double CpuUsagePercent { get; set; }
        public long MemoryUsageMB { get; set; }
        public string Status { get; set; } = "";
        public string FriendlyExplanation { get; set; } = "";
        public string Icon { get; set; } = "";

        // Category for grouping processes
        public ProcessCategory Category { get; set; } = ProcessCategory.Other;

        // Running time tracking
        public DateTime StartTime { get; set; } = DateTime.Now;
        public TimeSpan RunningTime => DateTime.Now - StartTime;
        public string RunningTimeFormatted
        {
            get
            {
                var time = RunningTime;
                if (time.TotalDays >= 1)
                    return $"{(int)time.TotalDays}d {time.Hours}h";
                if (time.TotalHours >= 1)
                    return $"{(int)time.TotalHours}h {time.Minutes}m";
                return $"{time.Minutes}m {time.Seconds}s";
            }
        }

        // Formatted strings for display
        public string CpuUsageDisplay => $"{CpuUsagePercent:F1}%";
        public string MemoryUsageDisplay => $"{MemoryUsageMB:N0} MB";

        // Color coding for visual feedback
        public string CpuUsageColor
        {
            get
            {
                if (CpuUsagePercent > 50) return "#F44336"; // Red
                if (CpuUsagePercent > 20) return "#FF9800"; // Orange
                return "#4CAF50"; // Green
            }
        }

        public string MemoryUsageColor
        {
            get
            {
                if (MemoryUsageMB > 1000) return "#F44336"; // Red (>1GB)
                if (MemoryUsageMB > 500) return "#FF9800"; // Orange (>500MB)
                return "#4CAF50"; // Green
            }
        }
    }

    public enum ProcessCategory
    {
        Browser,
        Gaming,
        CloudStorage,
        Communication,
        SystemServices,
        Development,
        Media,
        Other
    }
}