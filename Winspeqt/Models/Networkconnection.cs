using System;

namespace Winspeqt.Models
{
    public class NetworkConnection
    {
        public string Protocol { get; set; } = string.Empty;
        public string LocalAddress { get; set; } = string.Empty;
        public int LocalPort { get; set; }
        public string RemoteAddress { get; set; } = string.Empty;
        public int RemotePort { get; set; }
        public string State { get; set; } = string.Empty;
        public int ProcessId { get; set; }
        public string ProcessName { get; set; } = string.Empty;
        public DateTime DetectedAt { get; set; }

        public string DisplayLocalEndpoint => $"{LocalAddress}:{LocalPort}";
        public string DisplayRemoteEndpoint =>
            string.IsNullOrEmpty(RemoteAddress) ? "N/A" : $"{RemoteAddress}:{RemotePort}";

        public string SecurityRisk { get; set; } = string.Empty;
        public string RiskLevel { get; set; } = string.Empty; // Low, Medium, High, Critical
    }
}