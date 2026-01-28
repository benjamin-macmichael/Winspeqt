namespace Winspeqt.Models
{
    public class NetworkInfo
    {
        public string NetworkName { get; set; } = "";
        public string SecurityType { get; set; } = ""; // WPA2, WPA3, etc.
        public string NetworkBand { get; set; } = ""; // 2.4GHz, 5GHz
        public int SignalStrength { get; set; } // 0-100
        public string SignalQuality { get; set; } = ""; // Excellent, Good, Fair, Poor
        public bool IsSecure { get; set; }
        public int ConnectedDevicesCount { get; set; }
        public string IpAddress { get; set; } = "";
        public string MacAddress { get; set; } = "";

        public string SignalStrengthFormatted => $"{SignalStrength}%";

        public string SecurityIcon => IsSecure ? "&#xE72E;" : "&#xE7BA;"; // Shield or Warning

        public string SignalIcon
        {
            get
            {
                if (SignalStrength >= 75) return "&#xEC3B;"; // Full signal
                if (SignalStrength >= 50) return "&#xEC3A;"; // Good signal
                if (SignalStrength >= 25) return "&#xEC39;"; // Medium signal
                return "&#xEC38;"; // Weak signal
            }
        }
    }

    public class OpenPort
    {
        public int PortNumber { get; set; }
        public string Protocol { get; set; } = ""; // TCP, UDP
        public string Service { get; set; } = ""; // HTTP, HTTPS, FTP, etc.
        public PortRiskLevel RiskLevel { get; set; }
        public string Description { get; set; } = "";

        public string RiskLevelText => RiskLevel.ToString();

        public string RiskColor
        {
            get
            {
                return RiskLevel switch
                {
                    PortRiskLevel.Critical => "#F44336", // Red
                    PortRiskLevel.High => "#FF9800", // Orange
                    PortRiskLevel.Medium => "#FFC107", // Yellow
                    PortRiskLevel.Low => "#4CAF50", // Green
                    _ => "#9E9E9E" // Gray
                };
            }
        }

        public string RiskIcon
        {
            get
            {
                return RiskLevel switch
                {
                    PortRiskLevel.Critical => "&#xE7BA;", // Warning
                    PortRiskLevel.High => "&#xE7BA;",
                    PortRiskLevel.Medium => "&#xE946;", // Info
                    PortRiskLevel.Low => "&#xE73E;", // Checkmark
                    _ => "&#xE946;"
                };
            }
        }
    }

    public enum PortRiskLevel
    {
        Low,
        Medium,
        High,
        Critical
    }

    public class SecurityVulnerability
    {
        public string Title { get; set; } = "";
        public string Description { get; set; } = "";
        public string Severity { get; set; } = ""; // Critical, High, Medium, Low
        public string RecommendedAction { get; set; } = "";

        public string SeverityColor
        {
            get
            {
                return Severity.ToLower() switch
                {
                    "critical" => "#F44336",
                    "high" => "#FF9800",
                    "medium" => "#FFC107",
                    "low" => "#4CAF50",
                    _ => "#9E9E9E"
                };
            }
        }
    }
}