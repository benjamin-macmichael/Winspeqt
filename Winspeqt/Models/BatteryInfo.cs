namespace Winspeqt.Models
{
    public class BatteryInfo
    {
        public string DeviceName { get; set; } = "";
        public int BatteryLevel { get; set; }
        public bool IsCharging { get; set; }
        public string Status { get; set; } = "";
        public string Icon { get; set; } = "";

        public string BatteryLevelFormatted => $"{BatteryLevel}%";

        public string BatteryColor
        {
            get
            {
                if (BatteryLevel <= 20) return "#F44336";
                if (BatteryLevel <= 50) return "#FF9800";
                return "#4CAF50";
            }
        }
    }
}