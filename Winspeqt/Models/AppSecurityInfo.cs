using System;

namespace Winspeqt.Models
{
    public class AppSecurityInfo
    {
        public string AppName { get; set; }
        public string InstalledVersion { get; set; }
        public string LatestVersion { get; set; }
        public string Publisher { get; set; }
        public string InstallLocation { get; set; }
        public DateTime? InstallDate { get; set; }

        public SecurityStatus Status { get; set; }
        public string StatusMessage { get; set; }
        public string UpdateInstructions { get; set; }

        public bool IsOutdated => Status == SecurityStatus.Outdated;
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