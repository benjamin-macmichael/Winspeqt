using System;

namespace Winspeqt.Models
{
    public class AppUsageModel
    {
        public string AppName { get; set; } = string.Empty;
        public string ProcessName { get; set; } = string.Empty;
        public string IconPath { get; set; } = string.Empty;
        public TimeSpan TotalUsageTime { get; set; }
        public DateTime LastUsed { get; set; }
        public int LaunchCount { get; set; }
        public double UsagePercentage { get; set; }
        public bool IsRunning { get; set; }

        public string FormattedUsagePercentage
        {
            get
            {
                if (UsagePercentage < 1)
                    return UsagePercentage.ToString("F2");
                return ((int)UsagePercentage).ToString();
            }
        }

        public string FormattedUsageTime
        {
            get
            {
                if (TotalUsageTime.TotalHours >= 1)
                    return $"{(int)TotalUsageTime.TotalHours}h {TotalUsageTime.Minutes}m";
                else if (TotalUsageTime.TotalMinutes >= 1)
                    return $"{(int)TotalUsageTime.TotalMinutes}m {TotalUsageTime.Seconds}s";
                else
                    return $"{TotalUsageTime.Seconds}s";
            }
        }

        public string FormattedLastUsed
        {
            get
            {
                var timeAgo = DateTime.Now - LastUsed;
                if (timeAgo.TotalMinutes < 1)
                    return "Just now";
                else if (timeAgo.TotalHours < 1)
                    return $"{(int)timeAgo.TotalMinutes} min ago";
                else if (timeAgo.TotalDays < 1)
                    return $"{(int)timeAgo.TotalHours} hours ago";
                else if (timeAgo.TotalDays < 30)
                    return $"{(int)timeAgo.TotalDays} days ago";
                else if (timeAgo.TotalDays < 365)
                    return $"{(int)(timeAgo.TotalDays / 30)} months ago";
                else
                    return $"{(int)(timeAgo.TotalDays / 365)} years ago";
            }
        }
    }

    public class InstalledAppModel
    {
        public string AppName { get; set; } = string.Empty;
        public string Publisher { get; set; } = string.Empty;
        public string Version { get; set; } = string.Empty;
        public DateTime? InstallDate { get; set; }
        public DateTime? LastUsed { get; set; }
        public long SizeInBytes { get; set; }
        public string? UninstallString { get; set; }

        public string FormattedSize
        {
            get
            {
                if (SizeInBytes == 0) return "Unknown";
                if (SizeInBytes < 1024) return $"{SizeInBytes} B";
                if (SizeInBytes < 1024 * 1024) return $"{SizeInBytes / 1024} KB";
                if (SizeInBytes < 1024 * 1024 * 1024) return $"{SizeInBytes / (1024 * 1024)} MB";
                return $"{SizeInBytes / (1024 * 1024 * 1024)} GB";
            }
        }

        public string FormattedLastUsed
        {
            get
            {
                if (!LastUsed.HasValue) return "Not tracked";

                var timeAgo = DateTime.Now - LastUsed.Value;
                if (timeAgo.TotalDays < 1)
                    return "Today";
                else if (timeAgo.TotalDays < 7)
                    return $"{(int)timeAgo.TotalDays} days ago";
                else if (timeAgo.TotalDays < 30)
                    return $"{(int)timeAgo.TotalDays} days ago";
                else if (timeAgo.TotalDays < 365)
                    return $"{(int)(timeAgo.TotalDays / 30)} months ago";
                else
                    return $"{(int)(timeAgo.TotalDays / 365)} years ago";
            }
        }

        public string FormattedInstallDate
        {
            get
            {
                if (!InstallDate.HasValue) return "Unknown";
                return InstallDate.Value.ToString("MMM dd, yyyy");
            }
        }

        public bool IsUnused
        {
            get
            {
                if (!LastUsed.HasValue) return false;
                var daysSinceUse = (DateTime.Now - LastUsed.Value).TotalDays;
                return daysSinceUse > 90;
            }
        }
    }

    public class AppUsageStats
    {
        public TimeSpan TotalScreenTime { get; set; }
        public int TotalAppsUsed { get; set; }
        public int ActiveApps { get; set; }
        public string MostUsedApp { get; set; } = string.Empty;
        public DateTime TrackingStartTime { get; set; }
    }
}