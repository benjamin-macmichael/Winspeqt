using System;
using System.Collections.Generic;
using System.Linq;
using System.Management;
using System.Threading.Tasks;
using Winspeqt.Models;

namespace Winspeqt.Services
{
    public class SecurityService
    {
        // Segoe Fluent Icons glyph codes
        private const string IconGood = "\uE73E";     // Checkmark
        private const string IconWarning = "\uE7BA";  // Warning
        private const string IconBad = "\uE711";      // Cancel/X
        private const string IconUnknown = "\uE9CE";  // Unknown
        private const string IconError = "\uE783";    // Error
        private const string IconNeutral = "\uE89A";  // Remove/dash

        public async Task<SecurityStatusInfo> GetSecurityStatusAsync()
        {
            var defenderTask = Task.Run(() => CheckWindowsDefender());
            var firewallTask = Task.Run(() => CheckFirewall());
            var updateTask = Task.Run(() => CheckWindowsUpdate());
            var bitlockerTask = Task.Run(() => CheckBitLocker());
            var driveHealthTask = Task.Run(() => CheckDriveHealth());
            var secureBootTask = Task.Run(() => CheckSecureBoot());

            await Task.WhenAll(defenderTask, firewallTask, updateTask, bitlockerTask, driveHealthTask, secureBootTask);

            var status = new SecurityStatusInfo
            {
                WindowsDefenderStatus = defenderTask.Result,
                FirewallStatus = firewallTask.Result,
                WindowsUpdateStatus = updateTask.Result,
                BitLockerStatus = bitlockerTask.Result,
                DriveHealthStatus = driveHealthTask.Result,
                SecureBootStatus = secureBootTask.Result
            };

            status.OverallSecurityScore = CalculateSecurityScore(status);
            status.OverallStatus = GetOverallStatus(status.OverallSecurityScore);

            return status;
        }

        public SecurityComponentStatus CheckWindowsDefender()
        {
            try
            {
                // Check SecurityCenter2 first for all registered AV products
                var sc2Searcher = new ManagementObjectSearcher(@"root\SecurityCenter2",
                    "SELECT * FROM AntiVirusProduct");

                var activeProducts = new List<string>();

                foreach (ManagementObject obj in sc2Searcher.Get())
                {
                    var displayName = obj["displayName"]?.ToString() ?? "Unknown";
                    var productState = Convert.ToUInt32(obj["productState"]);
                    var hex = productState.ToString("X6");

                    // productState first two hex chars indicate AV status:
                    // 06 = disabled/passive, 01 = not registered properly, 00 = unknown
                    // Rather than whitelisting known good values, we blacklist known bad ones
                    // so unknown AV products default to being treated as active
                    var statePrefix = hex.Substring(0, 2);
                    var isInactive = statePrefix == "06" || statePrefix == "01" || statePrefix == "00";
                    var isActive = !isInactive;

                    System.Diagnostics.Debug.WriteLine($"AV Product: {displayName}, State: {hex}, Active: {isActive}");

                    if (isActive)
                        activeProducts.Add(displayName);
                }

                if (activeProducts.Count > 0)
                {
                    var thirdParty = activeProducts.Where(p => !p.Contains("Windows Defender")).ToList();
                    string message;

                    if (thirdParty.Count > 0)
                        message = $"Your PC is protected by Windows Defender. The scan also found these additional security products: {string.Join(", ", thirdParty)}.";
                    else
                        message = "Windows Defender is actively protecting your computer.";

                    return new SecurityComponentStatus
                    {
                        IsEnabled = true,
                        Status = "Protected",
                        Message = message,
                        Icon = IconGood,
                        Color = "#4CAF50"
                    };
                }

                // Fall back to checking Defender directly via WMI
                var defenderSearcher = new ManagementObjectSearcher(@"root\Microsoft\Windows\Defender",
                    "SELECT * FROM MSFT_MpComputerStatus");

                foreach (ManagementObject obj in defenderSearcher.Get())
                {
                    var antivirusEnabled = Convert.ToBoolean(obj["AntivirusEnabled"]);
                    var realTimeProtectionEnabled = Convert.ToBoolean(obj["RealTimeProtectionEnabled"]);

                    // Only require real-time protection to be on — antivirus flag may be off if a third-party AV is registered
                    if (realTimeProtectionEnabled || antivirusEnabled)
                    {
                        return new SecurityComponentStatus
                        {
                            IsEnabled = true,
                            Status = "Protected",
                            Message = "Windows Defender is actively protecting your computer",
                            Icon = IconGood,
                            Color = "#4CAF50"
                        };
                    }
                    else
                    {
                        return new SecurityComponentStatus
                        {
                            IsEnabled = false,
                            Status = "At Risk",
                            Message = "No active antivirus protection detected. Enable Windows Defender or install an antivirus.",
                            Icon = IconBad,
                            Color = "#F44336"
                        };
                    }
                }

                return new SecurityComponentStatus
                {
                    IsEnabled = false,
                    Status = "Unknown",
                    Message = "Unable to check antivirus status",
                    Icon = IconUnknown,
                    Color = "#9E9E9E"
                };
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error checking antivirus: {ex.Message}");
                return new SecurityComponentStatus
                {
                    IsEnabled = false,
                    Status = "Error",
                    Message = "Could not access antivirus settings",
                    Icon = IconError,
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
                        Icon = IconGood,
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
                        Icon = IconWarning,
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
                        Icon = IconBad,
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
                    Icon = IconError,
                    Color = "#FF9800"
                };
            }
        }

        public SecurityComponentStatus CheckWindowsUpdate()
        {
            try
            {
                // Use Microsoft.Update.Session to check for pending updates
                var updateSession = new WUApiLib.UpdateSession();
                var updateSearcher = updateSession.CreateUpdateSearcher();

                // Only search for software updates that are not installed and not hidden
                var searchResult = updateSearcher.Search("IsInstalled=0 AND IsHidden=0 AND Type='Software'");

                int pendingCount = searchResult.Updates.Count;

                if (pendingCount == 0)
                {
                    return new SecurityComponentStatus
                    {
                        IsEnabled = true,
                        Status = "Up to Date",
                        Message = "Windows has checked for updates and your system is fully up to date",
                        Icon = IconGood,
                        Color = "#4CAF50"
                    };
                }
                else if (pendingCount <= 5)
                {
                    return new SecurityComponentStatus
                    {
                        IsEnabled = false,
                        Status = "Updates Available",
                        Message = $"{pendingCount} update(s) are available but not yet installed. Open Windows Update to install them.",
                        Icon = IconWarning,
                        Color = "#FF9800"
                    };
                }
                else
                {
                    return new SecurityComponentStatus
                    {
                        IsEnabled = false,
                        Status = "Outdated",
                        Message = $"{pendingCount} updates are waiting to be installed. Your system may be missing important security patches.",
                        Icon = IconBad,
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
                    Icon = IconError,
                    Color = "#FF9800"
                };
            }
        }

        public SecurityComponentStatus CheckBitLocker()
        {
            try
            {
                ManagementScope scope = new ManagementScope(@"root\CIMV2\Security\MicrosoftVolumeEncryption");

                try
                {
                    scope.Connect();
                    return CheckBitLockerFull(scope);
                }
                catch (ManagementException)
                {
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
                    Icon = IconNeutral,
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
                        if (protectionStatus == 1)
                        {
                            hasEncryptedVolume = true;
                            encryptedVolumes++;
                        }
                    }
                    catch { continue; }
                }

                if (totalVolumes == 0)
                {
                    return new SecurityComponentStatus
                    {
                        IsEnabled = false,
                        Status = "Not Available",
                        Message = "No encryptable drives found",
                        Icon = IconNeutral,
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
                        Icon = IconGood,
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
                        Icon = IconWarning,
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
                        Icon = IconBad,
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
                    Icon = IconWarning,
                    Color = "#FF9800"
                };
            }
        }

        private SecurityComponentStatus CheckDeviceEncryption()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("Checking Device Encryption...");

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

                if (hasEncryptionKeys)
                {
                    return new SecurityComponentStatus
                    {
                        IsEnabled = true,
                        Status = "Encrypted",
                        Message = "Device encryption is enabled and protecting your drives. Remember to back up your recovery key to a safe place!",
                        Icon = IconGood,
                        Color = "#4CAF50"
                    };
                }

                if (tpmPresent)
                {
                    if (tpmEnabled && tpmActivated)
                    {
                        return new SecurityComponentStatus
                        {
                            IsEnabled = false,
                            Status = "Available",
                            Message = "Your device supports encryption but it may not be enabled. Check Settings > Privacy & Security > Device Encryption",
                            Icon = IconWarning,
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
                            Icon = IconWarning,
                            Color = "#FF9800"
                        };
                    }
                }

                return new SecurityComponentStatus
                {
                    IsEnabled = false,
                    Status = "Not Supported",
                    Message = "This device doesn't have TPM hardware required for encryption",
                    Icon = IconNeutral,
                    Color = "#9E9E9E"
                };
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Device Encryption check failed: {ex.GetType().Name} - {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");

                return new SecurityComponentStatus
                {
                    IsEnabled = false,
                    Status = "Unknown",
                    Message = "Could not determine encryption status. BitLocker/Device Encryption may not be available on Windows Home edition.",
                    Icon = IconUnknown,
                    Color = "#9E9E9E"
                };
            }
        }

        public SecurityComponentStatus CheckDriveHealth()
        {
            try
            {
                var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_DiskDrive");
                var criticalIssues = new List<string>();
                var warnings = new List<string>();
                int totalDrives = 0;

                foreach (ManagementObject obj in searcher.Get())
                {
                    totalDrives++;
                    var model = obj["Model"]?.ToString() ?? "Unknown Drive";
                    var status = obj["Status"]?.ToString() ?? "Unknown";

                    switch (status)
                    {
                        case "OK":
                            break;
                        case "Pred Fail":
                            criticalIssues.Add($"{model}: its built-in diagnostics are predicting an imminent failure. " +
                                               "SMART does not provide a specific timeline — treat this as urgent.");
                            break;
                        case "Error":
                            criticalIssues.Add($"{model}: a hardware error has been detected on this drive.");
                            break;
                        case "Degraded":
                            warnings.Add($"{model}: is reporting degraded performance, which may indicate early signs of wear.");
                            break;
                        case "Unknown":
                            warnings.Add($"{model}: is reporting an unknown status. This may be a driver or compatibility issue.");
                            break;
                        default:
                            warnings.Add($"{model}: is reporting an unexpected status ({status}).");
                            break;
                    }
                }

                if (totalDrives == 0)
                {
                    return new SecurityComponentStatus
                    {
                        IsEnabled = false,
                        Status = "No Drives Found",
                        Message = "No drives could be detected. This may indicate a driver issue.",
                        Icon = IconUnknown,
                        Color = "#9E9E9E"
                    };
                }

                if (criticalIssues.Count == 0 && warnings.Count == 0)
                {
                    return new SecurityComponentStatus
                    {
                        IsEnabled = true,
                        Status = "Healthy",
                        Message = $"All {totalDrives} drive(s) are reporting healthy status. No issues detected.",
                        Icon = IconGood,
                        Color = "#4CAF50"
                    };
                }
                else if (criticalIssues.Count > 0)
                {
                    var details = string.Join("\n\n", criticalIssues);
                    return new SecurityComponentStatus
                    {
                        IsEnabled = false,
                        Status = "At Risk",
                        Message = $"Critical drive issue(s) detected:\n\n{details}\n\nBack up your important files immediately and replace the affected drive as soon as possible.",
                        Icon = IconBad,
                        Color = "#F44336"
                    };
                }
                else
                {
                    var details = string.Join("\n\n", warnings);
                    return new SecurityComponentStatus
                    {
                        IsEnabled = false,
                        Status = "Warning",
                        Message = $"Drive warning(s) detected:\n\n{details}\n\nConsider backing up your data and monitoring these drives closely.",
                        Icon = IconWarning,
                        Color = "#FF9800"
                    };
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error checking drive health: {ex.Message}");
                return new SecurityComponentStatus
                {
                    IsEnabled = false,
                    Status = "Error",
                    Message = "Could not check drive health status. Try running Winspeqt as administrator.",
                    Icon = IconError,
                    Color = "#FF9800"
                };
            }
        }

        public SecurityComponentStatus CheckSecureBoot()
        {
            try
            {
                using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\SecureBoot\State");

                if (key == null)
                {
                    return new SecurityComponentStatus
                    {
                        IsEnabled = false,
                        Status = "Not Supported",
                        Message = "Your system uses legacy BIOS and does not support Secure Boot",
                        Icon = IconNeutral,
                        Color = "#9E9E9E"
                    };
                }

                var value = key.GetValue("UEFISecureBootEnabled");
                if (value == null)
                {
                    return new SecurityComponentStatus
                    {
                        IsEnabled = false,
                        Status = "Unknown",
                        Message = "Could not determine Secure Boot state",
                        Icon = IconUnknown,
                        Color = "#9E9E9E"
                    };
                }

                bool isEnabled = Convert.ToInt32(value) == 1;

                if (isEnabled)
                {
                    return new SecurityComponentStatus
                    {
                        IsEnabled = true,
                        Status = "Enabled",
                        Message = "Secure Boot is active, protecting your PC from unauthorized software at startup",
                        Icon = IconGood,
                        Color = "#4CAF50"
                    };
                }
                else
                {
                    return new SecurityComponentStatus
                    {
                        IsEnabled = false,
                        Status = "Disabled",
                        Message = "Secure Boot is off. Your PC may be vulnerable to bootkit malware. Enable it in your BIOS/UEFI settings.",
                        Icon = IconWarning,
                        Color = "#FF9800"
                    };
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error checking Secure Boot: {ex.Message}");
                return new SecurityComponentStatus
                {
                    IsEnabled = false,
                    Status = "Error",
                    Message = "Could not check Secure Boot status",
                    Icon = IconError,
                    Color = "#FF9800"
                };
            }
        }

        public int CalculateSecurityScore(SecurityStatusInfo status)
        {
            int score = 0;

            // Windows Defender (25 points)
            if (status.WindowsDefenderStatus.IsEnabled && status.WindowsDefenderStatus.Status == "Protected")
                score += 25;
            else if (status.WindowsDefenderStatus.Status == "Error" || status.WindowsDefenderStatus.Status == "Unknown")
                score += 12;

            // Firewall (25 points)
            if (status.FirewallStatus.IsEnabled && status.FirewallStatus.Status == "Active")
                score += 25;
            else if (status.FirewallStatus.Status == "Partial")
                score += 15;

            // Windows Update (20 points)
            if (status.WindowsUpdateStatus.Status == "Up to Date")
                score += 20;
            else if (status.WindowsUpdateStatus.Status == "Updates Available")
                score += 10;

            // BitLocker (10 points) — less critical, not all systems support it
            if (status.BitLockerStatus.Status == "Encrypted")
                score += 10;
            else if (status.BitLockerStatus.Status == "Partial")
                score += 5;
            else if (status.BitLockerStatus.Status == "Not Available" || status.BitLockerStatus.Status == "Not Supported")
                score += 10; // don't penalize unsupported hardware

            // Drive Health (10 points)
            if (status.DriveHealthStatus.Status == "Healthy")
                score += 10;
            else if (status.DriveHealthStatus.Status == "Warning")
                score += 5;
            else if (status.DriveHealthStatus.Status == "Error" || status.DriveHealthStatus.Status == "No Drives Found")
                score += 5; // don't fully penalize for check failures

            // Secure Boot (10 points)
            if (status.SecureBootStatus.Status == "Enabled")
                score += 10;
            else if (status.SecureBootStatus.Status == "Not Supported" || status.SecureBootStatus.Status == "Error" || status.SecureBootStatus.Status == "Unknown")
                score += 10; // don't penalize legacy/unsupported systems

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