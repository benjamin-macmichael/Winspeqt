using System;
using System.Collections.Generic;

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
    }
}