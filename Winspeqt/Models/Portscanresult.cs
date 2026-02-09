using System;

namespace Winspeqt.Models
{
    public class PortScanResult
    {
        public int Port { get; set; }
        public string Protocol { get; set; }
        public string State { get; set; }
        public string ServiceName { get; set; }
        public string ProcessName { get; set; }
        public int ProcessId { get; set; }
        public bool IsKnownRisky { get; set; }
        public string RiskDescription { get; set; }
        public DateTime DetectedAt { get; set; }
    }
}