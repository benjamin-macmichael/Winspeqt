using System;

namespace Winspeqt.Models
{
    public class NetworkTrafficStats
    {
        public string InterfaceName { get; set; }
        public long BytesSent { get; set; }
        public long BytesReceived { get; set; }
        public long PacketsSent { get; set; }
        public long PacketsReceived { get; set; }
        public DateTime LastUpdated { get; set; }

        // Rate calculations (bytes per second)
        public double SendRate { get; set; }
        public double ReceiveRate { get; set; }

        public string FormattedBytesSent => FormatBytes(BytesSent);
        public string FormattedBytesReceived => FormatBytes(BytesReceived);
        public string FormattedSendRate => $"{FormatBytes((long)SendRate)}/s";
        public string FormattedReceiveRate => $"{FormatBytes((long)ReceiveRate)}/s";

        private string FormatBytes(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            double len = bytes;
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len = len / 1024;
            }
            return $"{len:0.##} {sizes[order]}";
        }
    }
}