namespace Winspeqt.Services
{
    public sealed class SystemStatistics
    {
        public System.Threading.Tasks.Task<double> CpuUsage(SystemMonitorService monitorService)
        {
            System.Diagnostics.Debug.WriteLine("Getting CPU...");
            return monitorService.GetTotalCpuUsageAsync();
        }

        public System.Threading.Tasks.Task<long> AvailableMemory(SystemMonitorService monitorService)
        {
            System.Diagnostics.Debug.WriteLine("Getting available memory...");
            return monitorService.GetAvailableMemoryMBAsync();
        }

        public System.Threading.Tasks.Task<long> TotalMemory(SystemMonitorService monitorService)
        {
            System.Diagnostics.Debug.WriteLine("Getting total memory...");
            return monitorService.GetTotalMemoryMBAsync();
        }

        public System.Threading.Tasks.Task<double> DiskActiveTime(SystemMonitorService monitorService)
        {
            System.Diagnostics.Debug.WriteLine("Getting disk active time...");
            return monitorService.GetDiskActiveTimePercentAsync();
        }

        public System.Threading.Tasks.Task<(double SentMbps, double ReceivedMbps)> NetworkThroughput(SystemMonitorService monitorService)
        {
            System.Diagnostics.Debug.WriteLine("Getting network throughput...");
            return monitorService.GetNetworkThroughputMbpsAsync();
        }
    }
}
