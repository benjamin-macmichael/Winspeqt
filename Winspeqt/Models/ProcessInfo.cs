using System;
using System.Diagnostics;

namespace Winspeqt.Models
{
    public sealed class ProcessInfo
    {
        public int Id { get; init; }
        public string Name { get; init; } = "";
        
        // Task Manager-like: "Memory (Active Private Working Set)" in bytes
        public long ActivePrivateWorkingSetBytes { get; init; }

        // Optional convenience for display
        public double ActivePrivateWorkingSetMiB => ActivePrivateWorkingSetBytes / 1024d / 1024d;
        
        public double CpuUsage { get; init; }
        
        public double WorkingSet64Bytes { get; init; }
        
        public double WorkingSetMiB => WorkingSet64Bytes / 1024d / 1024d;
    }
}