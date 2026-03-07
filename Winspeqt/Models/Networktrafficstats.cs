using System;

namespace Winspeqt.Models
{
    public class NetworkTrafficStats
    {
        public string InterfaceName { get; set; } = string.Empty;
        public long BytesSent { get; set; }
        public long BytesReceived { get; set; }
        public long PacketsSent { get; set; }
        public long PacketsReceived { get; set; }
        public DateTime LastUpdated { get; set; }

        // Rate calculations (bytes per second)
        public double SendRate { get; set; }
        public double ReceiveRate { get; set; }
    }
}