using System;
using System.Collections.Generic;

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
        public string DataSource { get; set; } = string.Empty;

        // Package manager ID
        public string WinGetId { get; set; } = string.Empty;

        // Package name as found by the API (for display)
        public string WinGetPackageName { get; set; } = string.Empty;

        // Multiple installs detection
        public bool HasMultipleInstalls { get; set; } = false;
        public int InstallCount { get; set; } = 1;

        // The display names of the other detected versions of this app
        public List<string> OtherVersions { get; set; } = new();

        // Human-readable summary of other versions for display e.g. "3.10.0, 3.11.2, 3.12.1"
        public string OtherVersionsSummary => OtherVersions.Count > 0
            ? string.Join(", ", OtherVersions)
            : string.Empty;

        // Full warning message shown on the card
        public string MultipleInstallsMessage
        {
            get
            {
                if (!HasMultipleInstalls) return string.Empty;
                var versionList = OtherVersions.Count > 0
                    ? $" Found: {OtherVersionsSummary}."
                    : string.Empty;
                return $"{InstallCount} versions installed.{versionList} Multiple versions can waste disk space and cause conflicts. Uninstall versions you no longer need via Windows Settings > Apps > Installed apps.";
            }
        }

        public bool IsOutdated => Status == SecurityStatus.Outdated || Status == SecurityStatus.Critical;
        public bool IsUnknown => Status == SecurityStatus.Unknown;
        public bool IsUpToDate => Status == SecurityStatus.UpToDate;

        // Icon glyph (Unicode character for FontIcon)
        public string StatusIcon => Status switch
        {
            SecurityStatus.UpToDate => "\uE73E",      // Checkmark
            SecurityStatus.Outdated => "\uE7BA",      // Warning
            SecurityStatus.Unknown => "\uE9CE",       // Help/Question
            SecurityStatus.Critical => "\uE7BA",      // Error
            _ => "\uE91F"                             // Dot
        };

        // Icon color
        public string StatusIconColor => Status switch
        {
            SecurityStatus.UpToDate => "#4CAF50",     // Green
            SecurityStatus.Outdated => "#FF9800",     // Orange
            SecurityStatus.Unknown => "#9E9E9E",      // Gray
            SecurityStatus.Critical => "#F44336",     // Red
            _ => "#9E9E9E"
        };

        // Keep StatusColor for backward compatibility
        public string StatusColor => StatusIconColor;

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