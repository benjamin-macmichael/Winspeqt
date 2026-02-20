namespace Winspeqt.Models
{
    public class SecurityStatusInfo
    {
        public SecurityComponentStatus WindowsDefenderStatus { get; set; } = new SecurityComponentStatus();
        public SecurityComponentStatus FirewallStatus { get; set; } = new SecurityComponentStatus();
        public SecurityComponentStatus WindowsUpdateStatus { get; set; } = new SecurityComponentStatus();
        public SecurityComponentStatus BitLockerStatus { get; set; } = new SecurityComponentStatus();
        public int OverallSecurityScore { get; set; }
        public string OverallStatus { get; set; } = "";
    }

    public class SecurityComponentStatus
    {
        public bool IsEnabled { get; set; } = false;
        public string Status { get; set; } = "";
        public string Message { get; set; } = "";
        public string Icon { get; set; } = "";
        public string Color { get; set; } = "";
    }
}