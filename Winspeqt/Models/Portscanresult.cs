using System;

namespace Winspeqt.Models
{
    public class PortScanResult
    {
        public int Port { get; set; }
        public string Protocol { get; set; } = string.Empty;
        public string State { get; set; } = string.Empty;
        public string ServiceName { get; set; } = string.Empty;
        public string ProcessName { get; set; } = string.Empty;
        public int ProcessId { get; set; }
        public bool IsKnownRisky { get; set; }
        public string RiskDescription { get; set; } = string.Empty;
        public DateTime DetectedAt { get; set; }
    }
}