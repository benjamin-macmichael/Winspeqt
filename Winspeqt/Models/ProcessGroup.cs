using System.Collections.ObjectModel;
using System.Linq;
using Winspeqt.Helpers;

namespace Winspeqt.Models
{
    public class ProcessGroup : ObservableObject
    {
        private bool _isExpanded;

        public ProcessInfo RootProcess { get; set; } = new();
        public ObservableCollection<ProcessInfo> ChildProcesses { get; } = new();

        public bool IsExpanded
        {
            get => _isExpanded;
            set => SetProperty(ref _isExpanded, value);
        }

        public string DisplayName => RootProcess.Description;
        public string FriendlyExplanation => RootProcess.FriendlyExplanation;
        public string Icon => RootProcess.Icon;
        public bool IsProtected => RootProcess.IsProtected;
        public int ProcessCount => ChildProcesses.Count + 1;
        public bool HasChildren => ChildProcesses.Count > 0;
        public string ProcessCountDisplay => HasChildren ? $"{ProcessCount} processes" : "1 process";
        public double CpuUsagePercent => RootProcess.CpuUsagePercent + ChildProcesses.Sum(p => p.CpuUsagePercent);
        public long MemoryUsageMB => RootProcess.MemoryUsageMB + ChildProcesses.Sum(p => p.MemoryUsageMB);

        // Display formatters for table view
        public string CpuUsageDisplay => $"{CpuUsagePercent:F1}%";
        public string MemoryUsageDisplay => MemoryUsageMB >= 1024
            ? $"{MemoryUsageMB / 1024.0:F1} GB"
            : $"{MemoryUsageMB:N0} MB";
        public string RunningTimeFormatted => RootProcess.RunningTimeFormatted;

        // Heat map background colors for table view
        public string CpuHeatColor
        {
            get
            {
                if (CpuUsagePercent >= 75) return "#FFCDD2";
                if (CpuUsagePercent >= 50) return "#FFE0B2";
                if (CpuUsagePercent >= 25) return "#FFF9C4";
                return "Transparent";
            }
        }

        public string MemoryHeatColor
        {
            get
            {
                if (MemoryUsageMB >= 2000) return "#FFCDD2";
                if (MemoryUsageMB >= 1000) return "#FFE0B2";
                if (MemoryUsageMB >= 500) return "#FFF9C4";
                return "Transparent";
            }
        }

        // Foreground color for CPU text
        public string CpuUsageColor
        {
            get
            {
                if (CpuUsagePercent > 50) return "#F44336";
                if (CpuUsagePercent > 20) return "#FF9800";
                return "#4CAF50";
            }
        }
    }
}
