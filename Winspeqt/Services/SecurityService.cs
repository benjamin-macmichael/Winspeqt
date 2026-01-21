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
            // Run all security checks in parallel for speed
            var defenderTask = Task.Run(() => CheckWindowsDefender());
            var firewallTask = Task.Run(() => CheckFirewall());
            var updateTask = Task.Run(() => CheckWindowsUpdate());
            var bitlockerTask = Task.Run(() => CheckBitLocker());

            // Wait for all to complete
            await Task.WhenAll(defenderTask, firewallTask, updateTask, bitlockerTask);

            var status = new SecurityStatusInfo
            {
                WindowsDefenderStatus = defenderTask.Result,
                FirewallStatus = firewallTask.Result,
                WindowsUpdateStatus = updateTask.Result,
                BitLockerStatus = bitlockerTask.Result
            };

            // Calculate overall security score
            status.OverallSecurityScore = CalculateSecurityScore(status);
            status.OverallStatus = GetOverallStatus(status.OverallSecurityScore);

            return status;
        }

        public SecurityComponentStatus CheckWindowsDefender()
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

        public SecurityComponentStatus CheckFirewall()
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

        public SecurityComponentStatus CheckWindowsUpdate()
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

        public SecurityComponentStatus CheckBitLocker()
        {
            try
            {
                // First try the full BitLocker WMI namespace (Pro/Enterprise/Education)
                ManagementScope scope = new ManagementScope(@"root\CIMV2\Security\MicrosoftVolumeEncryption");

                try
                {
                    scope.Connect();
                    return CheckBitLockerFull(scope);
                }
                catch (ManagementException)
                {
                    // Full BitLocker not available, try Device Encryption (Windows Home)
                    return CheckDeviceEncryption();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error checking BitLocker: {ex.GetType().Name} - {ex.Message}");

                return new SecurityComponentStatus
                {
                    IsEnabled = false,
                    Status = "Not Available",
                    Message = "Encryption status could not be determined",
                    Icon = "–",
                    Color = "#9E9E9E"
                };
            }
        }

        private SecurityComponentStatus CheckBitLockerFull(ManagementScope scope)
        {
            try
            {
                var searcher = new ManagementObjectSearcher(scope,
                    new ObjectQuery("SELECT * FROM Win32_EncryptableVolume"));

                bool hasEncryptedVolume = false;
                int totalVolumes = 0;
                int encryptedVolumes = 0;

                foreach (ManagementObject obj in searcher.Get())
                {
                    totalVolumes++;

                    try
                    {
                        var protectionStatus = Convert.ToInt32(obj["ProtectionStatus"]);

                        // 1 = Protected, 0 = Unprotected
                        if (protectionStatus == 1)
                        {
                            hasEncryptedVolume = true;
                            encryptedVolumes++;
                        }
                    }
                    catch
                    {
                        continue;
                    }
                }

                if (totalVolumes == 0)
                {
                    return new SecurityComponentStatus
                    {
                        IsEnabled = false,
                        Status = "Not Available",
                        Message = "No encryptable drives found",
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
                        Message = $"All {totalVolumes} drive(s) are encrypted with BitLocker. Make sure you've backed up your recovery key!",
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
                        Message = $"{encryptedVolumes} of {totalVolumes} drives encrypted with BitLocker",
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
                        Message = "BitLocker is available but not enabled on your drives",
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
                    Message = "Administrator privileges required to check encryption status",
                    Icon = "🔒",
                    Color = "#FF9800"
                };
            }
        }

        private SecurityComponentStatus CheckDeviceEncryption()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("Checking Device Encryption...");

                // Check Windows Device Encryption (available on some Windows Home devices)
                var searcher = new ManagementObjectSearcher(@"root\CIMV2\Security\MicrosoftTpm",
                    "SELECT * FROM Win32_Tpm");

                bool tpmPresent = false;
                bool tpmEnabled = false;
                bool tpmActivated = false;

                try
                {
                    foreach (ManagementObject obj in searcher.Get())
                    {
                        tpmPresent = true;
                        tpmEnabled = Convert.ToBoolean(obj["IsEnabled_InitialValue"]);
                        tpmActivated = Convert.ToBoolean(obj["IsActivated_InitialValue"]);
                        System.Diagnostics.Debug.WriteLine($"TPM Found - Enabled: {tpmEnabled}, Activated: {tpmActivated}");
                    }
                }
                catch (Exception tpmEx)
                {
                    System.Diagnostics.Debug.WriteLine($"TPM query error: {tpmEx.Message}");
                }

                // Try to check if device encryption is actually on by checking registry
                bool hasEncryptionKeys = false;
                try
                {
                    using (var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\BitLocker"))
                    {
                        if (key != null)
                        {
                            hasEncryptionKeys = true;
                            System.Diagnostics.Debug.WriteLine("BitLocker registry keys found!");
                        }
                        else
                        {
                            System.Diagnostics.Debug.WriteLine("No BitLocker registry keys found");
                        }
                    }
                }
                catch (Exception regEx)
                {
                    System.Diagnostics.Debug.WriteLine($"Registry check error: {regEx.Message}");
                }

                // If we found encryption keys in registry, it's encrypted
                if (hasEncryptionKeys)
                {
                    return new SecurityComponentStatus
                    {
                        IsEnabled = true,
                        Status = "Encrypted",
                        Message = "Device encryption is enabled and protecting your drives. Remember to back up your recovery key to a safe place!",
                        Icon = "✓",
                        Color = "#4CAF50"
                    };
                }

                // No encryption keys but TPM present
                if (tpmPresent)
                {
                    if (tpmEnabled && tpmActivated)
                    {
                        return new SecurityComponentStatus
                        {
                            IsEnabled = false,
                            Status = "Available",
                            Message = "Your device supports encryption but it may not be enabled. Check Settings > Privacy & Security > Device Encryption",
                            Icon = "⚠",
                            Color = "#FF9800"
                        };
                    }
                    else
                    {
                        return new SecurityComponentStatus
                        {
                            IsEnabled = false,
                            Status = "TPM Not Ready",
                            Message = "Your device has TPM hardware but it needs to be enabled in BIOS/UEFI",
                            Icon = "⚠",
                            Color = "#FF9800"
                        };
                    }
                }

                // No TPM found
                return new SecurityComponentStatus
                {
                    IsEnabled = false,
                    Status = "Not Supported",
                    Message = "This device doesn't have TPM hardware required for encryption",
                    Icon = "–",
                    Color = "#9E9E9E"
                };
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Device Encryption check failed: {ex.GetType().Name} - {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");

                // Provide a helpful fallback message
                return new SecurityComponentStatus
                {
                    IsEnabled = false,
                    Status = "Unknown",
                    Message = "Could not determine encryption status. BitLocker/Device Encryption may not be available on Windows Home edition.",
                    Icon = "?",
                    Color = "#9E9E9E"
                };
            }
        }

        public int CalculateSecurityScore(SecurityStatusInfo status)
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

        public string GetOverallStatus(int score)
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