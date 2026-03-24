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
        public string RemoteServiceName { get; set; } = string.Empty;
        public DateTime DetectedAt { get; set; }

        public string SecurityRisk { get; set; } = string.Empty;
        public string RiskLevel { get; set; } = string.Empty; // Low, Medium, High

        // ── Computed display properties ───────────────────────────────────────

        public string DisplayLocalEndpoint => $"{LocalAddress}:{LocalPort}";

        public string DisplayRemoteEndpoint =>
            string.IsNullOrEmpty(RemoteAddress) ? "N/A" : $"{RemoteAddress}:{RemotePort}";

        /// <summary>Remote address with optional service name annotation.</summary>
        public string RemoteDisplay
        {
            get
            {
                if (string.IsNullOrEmpty(RemoteAddress)
                    || RemoteAddress == "0.0.0.0"
                    || RemoteAddress == "::")
                    return "—";

                var svc = !string.IsNullOrEmpty(RemoteServiceName)
                          && !RemoteServiceName.StartsWith("Port ", StringComparison.Ordinal)
                    ? $"  ({RemoteServiceName})" : "";

                return $"{RemoteAddress}:{RemotePort}{svc}";
            }
        }

        /// <summary>Process name + PID formatted for display.</summary>
        public string ProcessDisplay =>
            !string.IsNullOrEmpty(ProcessName)
                ? ProcessId > 0 ? $"{ProcessName}  ·  PID {ProcessId}" : ProcessName
                : ProcessId > 0 ? $"PID {ProcessId}" : "Unknown Process";

        /// <summary>Human-readable connection state.</summary>
        public string StateDisplay => State switch
        {
            "Established" or "ESTABLISHED" => "ESTABLISHED",
            "CloseWait" or "CLOSE_WAIT" => "CLOSE WAIT",
            "TimeWait" or "TIME_WAIT" => "TIME WAIT",
            "SynSent" or "SYN_SENT" => "CONNECTING",
            "SynReceived" or "SYN_RECEIVED" => "INCOMING",
            "FinWait1" or "FIN_WAIT_1" => "FIN WAIT 1",
            "FinWait2" or "FIN_WAIT_2" => "FIN WAIT 2",
            "LISTENING" => "LISTENING",
            _ => State.ToUpperInvariant()
        };

        /// <summary>Hex color representing risk level — use with StringToBrushConverter.</summary>
        public string RiskColor => RiskLevel switch
        {
            "High" => "#D13438",
            "Medium" => "#FF9800",
            "Low" => "#7eb900",
            _ => "#555555"
        };

        /// <summary>Hex color for state badge.</summary>
        public string StateColor => State switch
        {
            "Established" or "ESTABLISHED" => "#2196F3",
            "LISTENING" => "#7eb900",
            _ => "#888888"
        };
    }
}
