using System;
using System.Management;
using System.Threading.Tasks;
using Winspeqt.Models;

namespace Winspeqt.Services
{
    public class SecurityService
    {
        public async Task<SecurityStatusInfo> GetSecurityStatusAsync()
        {
            return await Task.Run(() =>
            {
                var status = new SecurityStatusInfo
                {
                    WindowsDefenderStatus = CheckWindowsDefender(),
                    FirewallStatus = CheckFirewall(),
                    WindowsUpdateStatus = CheckWindowsUpdate(),
                    BitLockerStatus = CheckBitLocker()
                };

                // Calculate overall security score
                status.OverallSecurityScore = CalculateSecurityScore(status);
                status.OverallStatus = GetOverallStatus(status.OverallSecurityScore);

                return status;
            });
        }

        private SecurityComponentStatus CheckWindowsDefender()
        {
            try
            {
                var searcher = new ManagementObjectSearcher(@"root\Microsoft\Windows\Defender",
                    "SELECT * FROM MSFT_MpComputerStatus");

                foreach (ManagementObject obj in searcher.Get())
                {
                    var antivirusEnabled = Convert.ToBoolean(obj["AntivirusEnabled"]);
                    var realTimeProtectionEnabled = Convert.ToBoolean(obj["RealTimeProtectionEnabled"]);
                    var antiSpywareEnabled = Convert.ToBoolean(obj["AntispywareEnabled"]);

                    if (antivirusEnabled && realTimeProtectionEnabled && antiSpywareEnabled)
                    {
                        return new SecurityComponentStatus
                        {
                            IsEnabled = true,
                            Status = "Protected",
                            Message = "Windows Defender is actively protecting your computer",
                            Icon = "✓",
                            Color = "#4CAF50"
                        };
                    }
                    else
                    {
                        return new SecurityComponentStatus
                        {
                            IsEnabled = false,
                            Status = "At Risk",
                            Message = "Windows Defender protection is turned off or incomplete",
                            Icon = "⚠",
                            Color = "#F44336"
                        };
                    }
                }

                return new SecurityComponentStatus
                {
                    IsEnabled = false,
                    Status = "Unknown",
                    Message = "Unable to check Windows Defender status",
                    Icon = "?",
                    Color = "#9E9E9E"
                };
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error checking Defender: {ex.Message}");
                return new SecurityComponentStatus
                {
                    IsEnabled = false,
                    Status = "Error",
                    Message = "Could not access Windows Defender settings",
                    Icon = "!",
                    Color = "#FF9800"
                };
            }
        }

        private SecurityComponentStatus CheckFirewall()
        {
            try
            {
                var searcher = new ManagementObjectSearcher(@"root\StandardCimv2",
                    "SELECT * FROM MSFT_NetFirewallProfile");

                bool allProfilesEnabled = true;
                int enabledCount = 0;
                int totalProfiles = 0;

                foreach (ManagementObject obj in searcher.Get())
                {
                    totalProfiles++;
                    var enabled = Convert.ToBoolean(obj["Enabled"]);
                    if (enabled)
                        enabledCount++;
                    else
                        allProfilesEnabled = false;
                }

                if (allProfilesEnabled && totalProfiles > 0)
                {
                    return new SecurityComponentStatus
                    {
                        IsEnabled = true,
                        Status = "Active",
                        Message = "Windows Firewall is protecting all network connections",
                        Icon = "✓",
                        Color = "#4CAF50"
                    };
                }
                else if (enabledCount > 0)
                {
                    return new SecurityComponentStatus
                    {
                        IsEnabled = true,
                        Status = "Partial",
                        Message = $"Firewall is active on {enabledCount} of {totalProfiles} network profiles",
                        Icon = "⚠",
                        Color = "#FF9800"
                    };
                }
                else
                {
                    return new SecurityComponentStatus
                    {
                        IsEnabled = false,
                        Status = "Disabled",
                        Message = "Windows Firewall is turned off - your PC is vulnerable",
                        Icon = "✗",
                        Color = "#F44336"
                    };
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error checking Firewall: {ex.Message}");
                return new SecurityComponentStatus
                {
                    IsEnabled = false,
                    Status = "Error",
                    Message = "Could not check Windows Firewall status",
                    Icon = "!",
                    Color = "#FF9800"
                };
            }
        }

        private SecurityComponentStatus CheckWindowsUpdate()
        {
            try
            {
                var searcher = new ManagementObjectSearcher(
                    "SELECT * FROM Win32_QuickFixEngineering");

                DateTime lastUpdate = DateTime.MinValue;
                int updateCount = 0;

                foreach (ManagementObject obj in searcher.Get())
                {
                    updateCount++;
                    try
                    {
                        var installedOn = obj["InstalledOn"]?.ToString();
                        if (!string.IsNullOrEmpty(installedOn))
                        {
                            if (DateTime.TryParse(installedOn, out DateTime installDate))
                            {
                                if (installDate > lastUpdate)
                                    lastUpdate = installDate;
                            }
                        }
                    }
                    catch { }
                }

                var daysSinceUpdate = (DateTime.Now - lastUpdate).Days;

                if (lastUpdate == DateTime.MinValue)
                {
                    return new SecurityComponentStatus
                    {
                        IsEnabled = true,
                        Status = "Unknown",
                        Message = "Unable to determine last update date",
                        Icon = "?",
                        Color = "#9E9E9E"
                    };
                }
                else if (daysSinceUpdate <= 30)
                {
                    return new SecurityComponentStatus
                    {
                        IsEnabled = true,
                        Status = "Up to Date",
                        Message = $"Last update was {daysSinceUpdate} days ago - your system is current",
                        Icon = "✓",
                        Color = "#4CAF50"
                    };
                }
                else if (daysSinceUpdate <= 60)
                {
                    return new SecurityComponentStatus
                    {
                        IsEnabled = true,
                        Status = "Check for Updates",
                        Message = $"Last update was {daysSinceUpdate} days ago - consider checking for updates",
                        Icon = "⚠",
                        Color = "#FF9800"
                    };
                }
                else
                {
                    return new SecurityComponentStatus
                    {
                        IsEnabled = false,
                        Status = "Outdated",
                        Message = $"Last update was {daysSinceUpdate} days ago - updates needed!",
                        Icon = "✗",
                        Color = "#F44336"
                    };
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error checking Windows Update: {ex.Message}");
                return new SecurityComponentStatus
                {
                    IsEnabled = false,
                    Status = "Error",
                    Message = "Could not check Windows Update status",
                    Icon = "!",
                    Color = "#FF9800"
                };
            }
        }

        private SecurityComponentStatus CheckBitLocker()
        {
            try
            {
                var searcher = new ManagementObjectSearcher(@"root\CIMV2\Security\MicrosoftVolumeEncryption",
                    "SELECT * FROM Win32_EncryptableVolume");

                bool hasEncryptedVolume = false;
                int totalVolumes = 0;
                int encryptedVolumes = 0;

                foreach (ManagementObject obj in searcher.Get())
                {
                    totalVolumes++;
                    var protectionStatus = Convert.ToInt32(obj["ProtectionStatus"]);

                    // 1 = Protected, 0 = Unprotected
                    if (protectionStatus == 1)
                    {
                        hasEncryptedVolume = true;
                        encryptedVolumes++;
                    }
                }

                if (totalVolumes == 0)
                {
                    return new SecurityComponentStatus
                    {
                        IsEnabled = false,
                        Status = "Not Available",
                        Message = "BitLocker is not available on this system",
                        Icon = "–",
                        Color = "#9E9E9E"
                    };
                }
                else if (encryptedVolumes == totalVolumes)
                {
                    return new SecurityComponentStatus
                    {
                        IsEnabled = true,
                        Status = "Encrypted",
                        Message = $"All {totalVolumes} drive(s) are encrypted and protected",
                        Icon = "✓",
                        Color = "#4CAF50"
                    };
                }
                else if (hasEncryptedVolume)
                {
                    return new SecurityComponentStatus
                    {
                        IsEnabled = true,
                        Status = "Partial",
                        Message = $"{encryptedVolumes} of {totalVolumes} drives are encrypted",
                        Icon = "⚠",
                        Color = "#FF9800"
                    };
                }
                else
                {
                    return new SecurityComponentStatus
                    {
                        IsEnabled = false,
                        Status = "Not Encrypted",
                        Message = "Your drives are not encrypted - data could be at risk if device is stolen",
                        Icon = "✗",
                        Color = "#FF9800"
                    };
                }
            }
            catch (UnauthorizedAccessException)
            {
                return new SecurityComponentStatus
                {
                    IsEnabled = false,
                    Status = "Access Denied",
                    Message = "Run Winspeqt as Administrator to check BitLocker status",
                    Icon = "🔒",
                    Color = "#FF9800"
                };
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error checking BitLocker: {ex.Message}");
                return new SecurityComponentStatus
                {
                    IsEnabled = false,
                    Status = "Error",
                    Message = "Could not check BitLocker status",
                    Icon = "!",
                    Color = "#FF9800"
                };
            }
        }

        private int CalculateSecurityScore(SecurityStatusInfo status)
        {
            int score = 0;

            // Windows Defender (30 points)
            if (status.WindowsDefenderStatus.IsEnabled && status.WindowsDefenderStatus.Status == "Protected")
                score += 30;
            else if (status.WindowsDefenderStatus.Status == "Error" || status.WindowsDefenderStatus.Status == "Unknown")
                score += 15;

            // Firewall (30 points)
            if (status.FirewallStatus.IsEnabled && status.FirewallStatus.Status == "Active")
                score += 30;
            else if (status.FirewallStatus.Status == "Partial")
                score += 20;

            // Windows Update (25 points)
            if (status.WindowsUpdateStatus.Status == "Up to Date")
                score += 25;
            else if (status.WindowsUpdateStatus.Status == "Check for Updates")
                score += 15;

            // BitLocker (15 points) - less critical as not all systems support it
            if (status.BitLockerStatus.Status == "Encrypted")
                score += 15;
            else if (status.BitLockerStatus.Status == "Partial")
                score += 10;
            else if (status.BitLockerStatus.Status == "Not Available")
                score += 15; // Don't penalize if not available

            return score;
        }

        private string GetOverallStatus(int score)
        {
            if (score >= 85)
                return "Excellent - Your PC is well protected";
            else if (score >= 70)
                return "Good - Your PC has decent protection";
            else if (score >= 50)
                return "Fair - Some security improvements needed";
            else
                return "At Risk - Your PC needs immediate attention";
        }
    }
}