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
    }
}