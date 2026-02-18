using System;

namespace Winspeqt.Models
{
    public class NetworkConnection
    {
        public string Protocol { get; set; }
        public string LocalAddress { get; set; }
        public int LocalPort { get; set; }
        public string RemoteAddress { get; set; }
        public int RemotePort { get; set; }
        public string State { get; set; }
        public int ProcessId { get; set; }
        public string ProcessName { get; set; }
        public DateTime DetectedAt { get; set; }

        public string DisplayLocalEndpoint => $"{LocalAddress}:{LocalPort}";
        public string DisplayRemoteEndpoint =>
            string.IsNullOrEmpty(RemoteAddress) ? "N/A" : $"{RemoteAddress}:{RemotePort}";

        public string SecurityRisk { get; set; }
        public string RiskLevel { get; set; } // Low, Medium, High, Critical
    }
}