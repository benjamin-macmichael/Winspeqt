using System;

namespace Winspeqt.Models
{
    public class AppUsageModel
    {
        public string AppName { get; set; }
        public string ProcessName { get; set; }
        public string IconPath { get; set; }
        public TimeSpan TotalUsageTime { get; set; }
        public DateTime LastUsed { get; set; }
        public int LaunchCount { get; set; }
        public double UsagePercentage { get; set; }
        public bool IsRunning { get; set; }

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
                else
                    return $"{(int)timeAgo.TotalDays} days ago";
            }
        }
    }

    public class AppUsageStats
    {
        public TimeSpan TotalScreenTime { get; set; }
        public int TotalAppsUsed { get; set; }
        public int ActiveApps { get; set; }
        public string MostUsedApp { get; set; }
        public DateTime TrackingStartTime { get; set; }
    }
}