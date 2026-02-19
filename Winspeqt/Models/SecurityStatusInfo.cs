namespace Winspeqt.Models
{
    public class SecurityStatusInfo
    {
        public SecurityComponentStatus WindowsDefenderStatus { get; set; }
        public SecurityComponentStatus FirewallStatus { get; set; }
        public SecurityComponentStatus WindowsUpdateStatus { get; set; }
        public SecurityComponentStatus BitLockerStatus { get; set; }
        public SecurityComponentStatus DriveHealthStatus { get; set; }
        public SecurityComponentStatus SecureBootStatus { get; set; }
        public int OverallSecurityScore { get; set; }
        public string OverallStatus { get; set; }
    }

    public class SecurityComponentStatus
    {
        public bool IsEnabled { get; set; }
        public string Status { get; set; }
        public string Message { get; set; }
        public string Icon { get; set; }
        public string Color { get; set; }
    }
}