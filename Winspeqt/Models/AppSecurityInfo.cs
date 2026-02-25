using System;

namespace Winspeqt.Models
{
    public class AppSecurityInfo
    {
        public string AppName { get; set; } = string.Empty;
        public string InstalledVersion { get; set; } = string.Empty;
        public string LatestVersion { get; set; } = string.Empty;
        public string Publisher { get; set; } = string.Empty;
        public string InstallLocation { get; set; } = string.Empty;
        public DateTime? InstallDate { get; set; }
        public SecurityStatus Status { get; set; }
        public string StatusMessage { get; set; } = string.Empty;
        public string UpdateInstructions { get; set; } = string.Empty;

        // Confidence scoring
        public int ConfidenceScore { get; set; } // 0-100
        public string ConfidenceLevel => ConfidenceScore switch
        {
            >= 90 => "High",
            >= 70 => "Medium",
            >= 50 => "Low",
            _ => "Unknown"
        };
        public string ConfidenceColor => ConfidenceScore switch
        {
            >= 90 => "#4CAF50",
            >= 70 => "#FF9800",
            >= 50 => "#F44336",
            _ => "#9E9E9E"
        };
        public string DataSource { get; set; } = string.Empty; // "Direct Match", "Search Result", etc.

        // Package manager ID
        public string WinGetId { get; set; } = string.Empty;

        // Package name as found by the API (for display)
        public string WinGetPackageName { get; set; } = string.Empty;

        public bool IsOutdated => Status == SecurityStatus.Outdated || Status == SecurityStatus.Critical;
        public bool IsUnknown => Status == SecurityStatus.Unknown;
        public bool IsUpToDate => Status == SecurityStatus.UpToDate;

        public string StatusIcon => Status switch
        {
            SecurityStatus.UpToDate => "✓",
            SecurityStatus.Outdated => "⚠️",
            SecurityStatus.Unknown => "❓",
            SecurityStatus.Critical => "🔴",
            _ => "•"
        };

        public string StatusColor => Status switch
        {
            SecurityStatus.UpToDate => "#4CAF50",
            SecurityStatus.Outdated => "#FF9800",
            SecurityStatus.Unknown => "#9E9E9E",
            SecurityStatus.Critical => "#F44336",
            _ => "#9E9E9E"
        };

        public string FormattedInstallDate => InstallDate?.ToString("MMM dd, yyyy") ?? "Unknown";
    }

    public enum SecurityStatus
    {
        UpToDate,
        Outdated,
        Unknown,
        Critical
    }
}