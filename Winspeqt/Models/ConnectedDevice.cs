namespace Winspeqt.Models
{
    public class ConnectedDevice
    {
        public string Name { get; set; } = "";
        public string Description { get; set; } = "";
        public string DeviceType { get; set; } = ""; // "WiFi", "Bluetooth", "Ethernet", "Network"
        public string Status { get; set; } = "Connected";
        public string InterfaceName { get; set; } = ""; // netsh interface name (WiFi only)

        public string Icon => DeviceType switch
        {
            "WiFi" => "📶",
            "Bluetooth" => "🔷",
            "Ethernet" => "🔌",
            _ => "🌐"
        };

        // Only WiFi interfaces can be disconnected via netsh
        public bool CanDisconnect => DeviceType == "WiFi";
    }
}
