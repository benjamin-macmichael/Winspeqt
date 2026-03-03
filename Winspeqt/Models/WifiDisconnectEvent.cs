using System;

namespace Winspeqt.Models
{
    public class WifiDisconnectEvent
    {
        public DateTime Timestamp { get; set; }
        public string NetworkName { get; set; } = "";
        public string Reason { get; set; } = "";

        public string TimeAgo
        {
            get
            {
                var diff = DateTime.Now - Timestamp;
                if (diff.TotalDays >= 1) return $"{(int)diff.TotalDays}d ago";
                if (diff.TotalHours >= 1) return $"{(int)diff.TotalHours}h ago";
                if (diff.TotalMinutes >= 1) return $"{(int)diff.TotalMinutes}m ago";
                return "Just now";
            }
        }

        public string DisplayTimestamp => Timestamp.ToString("MMM d, h:mm tt");
    }
}
