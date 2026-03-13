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
    }
}
