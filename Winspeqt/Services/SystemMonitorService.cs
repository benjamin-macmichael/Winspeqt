using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Management;
using System.Threading.Tasks;
using Winspeqt.Models;

namespace Winspeqt.Services
{
    public class SystemMonitorService
    {
        private PerformanceCounter _availableMemoryCounter;

        public SystemMonitorService()
        {
            _availableMemoryCounter = new PerformanceCounter("Memory", "Available MBytes");
        }

        public async Task<List<ProcessInfo>> GetRunningProcessesAsync()
        {
            return await Task.Run(() =>
            {
                var processes = Process.GetProcesses();
                var processInfoList = new List<ProcessInfo>();

                foreach (var process in processes)
                {
                    try
                    {
                        if (process.Id == 0) continue;

                        var processInfo = new ProcessInfo
                        {
                            ProcessId = process.Id,
                            ProcessName = process.ProcessName,
                            Description = GetFriendlyName(process.ProcessName),
                            MemoryUsageMB = process.WorkingSet64 / 1024 / 1024,
                            CpuUsagePercent = 0,
                            Status = process.Responding ? "Running" : "Not Responding",
                            FriendlyExplanation = GetFriendlyExplanation(process.ProcessName, process.WorkingSet64 / 1024 / 1024),
                            Icon = GetProcessIcon(process.ProcessName),
                            Category = GetProcessCategory(process.ProcessName),
                            StartTime = DateTime.Now // Default to now if we can't get real start time
                        };

                        // Try to get actual start time
                        try
                        {
                            processInfo.StartTime = process.StartTime;
                        }
                        catch
                        {
                            // Keep default if we can't access start time
                        }

                        processInfoList.Add(processInfo);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error reading process: {ex.Message}");
                    }
                }

                return processInfoList.OrderByDescending(p => p.MemoryUsageMB).ToList();
            });
        }

        public async Task<List<ProcessInfo>> GetBackgroundProcessesAsync()
        {
            return await Task.Run(() =>
            {
                var processes = Process.GetProcesses();
                var backgroundProcesses = new List<ProcessInfo>();

                foreach (var process in processes)
                {
                    try
                    {
                        if (process.Id == 0) continue;

                        bool isBackground = IsBackgroundProcess(process);

                        if (isBackground && process.WorkingSet64 / 1024 / 1024 > 50)
                        {
                            var processInfo = new ProcessInfo
                            {
                                ProcessId = process.Id,
                                ProcessName = process.ProcessName,
                                Description = GetFriendlyName(process.ProcessName),
                                MemoryUsageMB = process.WorkingSet64 / 1024 / 1024,
                                CpuUsagePercent = 0,
                                Status = process.Responding ? "Running in background" : "Not Responding",
                                FriendlyExplanation = GetBackgroundExplanation(process.ProcessName, process.WorkingSet64 / 1024 / 1024),
                                Icon = GetProcessIcon(process.ProcessName),
                                Category = GetProcessCategory(process.ProcessName),
                                StartTime = process.StartTime
                            };

                            backgroundProcesses.Add(processInfo);
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error reading process: {ex.Message}");
                    }
                }

                return backgroundProcesses.OrderByDescending(p => p.MemoryUsageMB).ToList();
            });
        }

        public async Task<List<BatteryInfo>> GetBatteryInfoAsync()
        {
            return await Task.Run(() =>
            {
                var batteries = new List<BatteryInfo>();

                try
                {
                    // Get system battery
                    var query = new ManagementObjectSearcher("SELECT * FROM Win32_Battery");
                    foreach (ManagementObject obj in query.Get())
                    {
                        var battery = new BatteryInfo
                        {
                            DeviceName = "Laptop Battery",
                            BatteryLevel = Convert.ToInt32(obj["EstimatedChargeRemaining"]),
                            IsCharging = Convert.ToUInt16(obj["BatteryStatus"]) == 2,
                            Status = GetBatteryStatus(Convert.ToUInt16(obj["BatteryStatus"])),
                            Icon = "&#xE83F;" // Battery icon
                        };
                        batteries.Add(battery);
                    }

                    // Get Bluetooth device batteries
                    var btQuery = new ManagementObjectSearcher("SELECT * FROM Win32_PnPEntity WHERE Name LIKE '%Bluetooth%'");
                    foreach (ManagementObject obj in btQuery.Get())
                    {
                        var name = obj["Name"]?.ToString() ?? "Unknown Device";
                        if (name.Contains("Mouse") || name.Contains("Keyboard") || name.Contains("Headset"))
                        {
                            // Note: Getting actual battery level for Bluetooth devices requires additional APIs
                            // This is a simplified version
                            var btDevice = new BatteryInfo
                            {
                                DeviceName = name,
                                BatteryLevel = 100, // Placeholder - would need Windows.Devices.Bluetooth API
                                IsCharging = false,
                                Status = "Connected",
                                Icon = GetBluetoothDeviceIcon(name)
                            };
                            batteries.Add(btDevice);
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error reading battery info: {ex.Message}");
                }

                return batteries;
            });
        }

        public async Task<bool> EndProcessAsync(int processId)
        {
            return await Task.Run(() =>
            {
                try
                {
                    var process = Process.GetProcessById(processId);
                    process.Kill();
                    return true;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error ending process: {ex.Message}");
                    return false;
                }
            });
        }

        private ProcessCategory GetProcessCategory(string processName)
        {
            var lower = processName.ToLower();

            // Browsers
            if (lower.Contains("chrome") || lower.Contains("firefox") || lower.Contains("msedge") ||
                lower.Contains("edge") || lower.Contains("browser") || lower.Contains("safari") ||
                lower.Contains("opera") || lower.Contains("brave"))
                return ProcessCategory.Browser;

            // Gaming
            if (lower.Contains("steam") || lower.Contains("epic") || lower.Contains("origin") ||
                lower.Contains("uplay") || lower.Contains("battlenet") || lower.Contains("riot"))
                return ProcessCategory.Gaming;

            // Cloud Storage
            if (lower.Contains("onedrive") || lower.Contains("dropbox") || lower.Contains("googledrive") ||
                lower.Contains("box") || lower.Contains("sync"))
                return ProcessCategory.CloudStorage;

            // Communication
            if (lower.Contains("discord") || lower.Contains("slack") || lower.Contains("teams") ||
                lower.Contains("zoom") || lower.Contains("skype") || lower.Contains("telegram"))
                return ProcessCategory.Communication;

            // Development
            if (lower.Contains("code") || lower.Contains("devenv") || lower.Contains("git") ||
                lower.Contains("docker") || lower.Contains("node"))
                return ProcessCategory.Development;

            // Media
            if (lower.Contains("spotify") || lower.Contains("music") || lower.Contains("itunes") ||
                lower.Contains("vlc") || lower.Contains("media"))
                return ProcessCategory.Media;

            // System Services
            if (lower.Contains("svchost") || lower.Contains("system") || lower.Contains("service") ||
                lower.Contains("nvidia") || lower.Contains("amd") || lower.Contains("intel") ||
                lower.Contains("windows") || lower.Contains("defender"))
                return ProcessCategory.SystemServices;

            return ProcessCategory.Other;
        }

        private string GetBatteryStatus(ushort status)
        {
            return status switch
            {
                1 => "Discharging",
                2 => "Charging",
                3 => "Fully Charged",
                4 => "Low Battery",
                5 => "Critical",
                _ => "Unknown"
            };
        }

        private string GetBluetoothDeviceIcon(string name)
        {
            var lower = name.ToLower();
            if (lower.Contains("mouse")) return "&#xE962;"; // Mouse
            if (lower.Contains("keyboard")) return "&#xE92E;"; // Keyboard
            if (lower.Contains("headset") || lower.Contains("headphone")) return "&#xE95B;"; // Headphones
            return "&#xE702;"; // Bluetooth
        }

        private bool IsBackgroundProcess(Process process)
        {
            try
            {
                if (process.MainWindowHandle == IntPtr.Zero)
                {
                    var lowerName = process.ProcessName.ToLower();

                    if (lowerName == "system" || lowerName == "idle" || lowerName == "registry" ||
                        lowerName == "smss" || lowerName == "csrss" || lowerName == "wininit" ||
                        lowerName == "services" || lowerName == "lsass")
                    {
                        return false;
                    }

                    return true;
                }

                return false;
            }
            catch
            {
                return false;
            }
        }

        private string GetBackgroundExplanation(string processName, long memoryMB)
        {
            var lower = processName.ToLower();

            if (lower.Contains("onedrive")) return "Syncing your files with OneDrive cloud storage";
            if (lower.Contains("dropbox")) return "Syncing your files with Dropbox cloud storage";
            if (lower.Contains("googledrivesync") || lower.Contains("googledrive")) return "Syncing your files with Google Drive";
            if (lower.Contains("spotify")) return "Playing music or running in the background";
            if (lower.Contains("discord")) return "Messaging app running in the background for notifications";
            if (lower.Contains("slack")) return "Work messaging app running for notifications";
            if (lower.Contains("teams")) return "Microsoft Teams running in background for calls and messages";
            if (lower.Contains("skype")) return "Skype running in background for calls and messages";
            if (lower.Contains("zoom")) return "Zoom running in background";
            if (lower.Contains("steam")) return "Steam client running for game updates and downloads";
            if (lower.Contains("epic") || lower.Contains("epicgames")) return "Epic Games Launcher running in background";
            if (lower.Contains("nvidia") || lower.Contains("nvcontainer")) return "NVIDIA graphics driver helper running in background";
            if (lower.Contains("adobe")) return "Adobe background service for Creative Cloud apps";
            if (lower.Contains("antimalware") || lower.Contains("defender")) return "Windows Security protection running in background";
            if (lower.Contains("svchost")) return "Windows service host - runs essential Windows services";
            if (lower.Contains("runtime") || lower.Contains("runtimebroker")) return "Windows process that manages permissions for apps";
            if (lower.Contains("explorer")) return "Windows Explorer - manages desktop and file browsing";

            if (memoryMB > 500) return "This app is using significant memory while running in the background";
            if (memoryMB > 200) return "Running in the background with moderate resource usage";
            return "Running quietly in the background";
        }

        private string GetFriendlyExplanation(string processName, long memoryMB)
        {
            var lower = processName.ToLower();

            if (lower.Contains("chrome") || lower.Contains("firefox") || lower.Contains("msedge"))
            {
                if (memoryMB > 500)
                    return "Your web browser is using a lot of memory, probably because you have many tabs open";
                return "Your web browser is running normally";
            }

            if (lower.Contains("explorer"))
                return "File Explorer helps you browse files and folders on your PC";

            if (lower.Contains("svchost"))
                return "A Windows background service that helps your PC run smoothly";

            if (lower.Contains("system"))
                return "Core Windows system process - this is normal and necessary";

            if (lower.Contains("dwm"))
                return "Manages visual effects and window animations in Windows";

            if (lower.Contains("spotify") || lower.Contains("music"))
                return "Music streaming application";

            if (lower.Contains("discord") || lower.Contains("teams") || lower.Contains("slack"))
                return "Communication app for messaging and calls";

            if (lower.Contains("code") || lower.Contains("devenv"))
                return "Code editor or development environment";

            if (memoryMB > 1000)
                return "This app is using a lot of memory - you might want to close it if you're not using it";

            return "Application running in the background";
        }

        public async Task<double> GetTotalCpuUsageAsync()
        {
            return await Task.Run(() =>
            {
                try
                {
                    var searcher = new ManagementObjectSearcher("SELECT LoadPercentage FROM Win32_Processor");
                    var cpuObjects = searcher.Get();
                    double totalLoad = 0;
                    int count = 0;

                    foreach (ManagementObject obj in cpuObjects)
                    {
                        totalLoad += Convert.ToDouble(obj["LoadPercentage"]);
                        count++;
                    }

                    return count > 0 ? Math.Round(totalLoad / count, 1) : 0;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error reading CPU: {ex.Message}");
                    return 0;
                }
            });
        }

        public async Task<long> GetAvailableMemoryMBAsync()
        {
            return await Task.Run(() =>
            {
                try
                {
                    return (long)_availableMemoryCounter.NextValue();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error reading memory: {ex.Message}");
                    return 0;
                }
            });
        }

        public async Task<long> GetTotalMemoryMBAsync()
        {
            return await Task.Run(() =>
            {
                try
                {
                    var gcMemoryInfo = GC.GetGCMemoryInfo();
                    return gcMemoryInfo.TotalAvailableMemoryBytes / 1024 / 1024;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error getting total memory: {ex.Message}");
                    return 8192;
                }
            });
        }

        private string GetFriendlyName(string processName)
        {
            var cleanName = processName.ToLower();
            if (cleanName.EndsWith(".exe"))
                cleanName = cleanName.Substring(0, cleanName.Length - 4);

            var friendlyNames = new Dictionary<string, string>
            {
                { "chrome", "Google Chrome" }, { "firefox", "Mozilla Firefox" }, { "msedge", "Microsoft Edge" },
                { "explorer", "File Explorer" }, { "code", "Visual Studio Code" }, { "devenv", "Visual Studio" },
                { "spotify", "Spotify" }, { "discord", "Discord" }, { "teams", "Microsoft Teams" },
                { "slack", "Slack" }, { "outlook", "Microsoft Outlook" }, { "excel", "Microsoft Excel" },
                { "word", "Microsoft Word" }, { "powerpnt", "Microsoft PowerPoint" },
                { "svchost", "Windows Service Host" }, { "system", "Windows System" },
                { "audiodg", "Windows Audio" }, { "dwm", "Desktop Window Manager" },
                { "onedrive", "OneDrive" }, { "dropbox", "Dropbox" }, { "steam", "Steam" },
                { "nvidia", "NVIDIA Services" }
            };

            if (friendlyNames.ContainsKey(cleanName))
                return friendlyNames[cleanName];

            try
            {
                var process = Process.GetProcessesByName(processName).FirstOrDefault();
                if (process != null && !string.IsNullOrEmpty(process.MainModule?.FileVersionInfo?.FileDescription))
                    return process.MainModule.FileVersionInfo.FileDescription;
            }
            catch { }

            if (cleanName.Length > 0)
                return char.ToUpper(cleanName[0]) + cleanName.Substring(1);

            return processName;
        }

        private string GetProcessIcon(string processName)
        {
            var lower = processName.ToLower();

            if (lower.Contains("chrome") || lower.Contains("firefox") || lower.Contains("edge"))
                return "&#xE774;";
            if (lower.Contains("explorer")) return "&#xE8B7;";
            if (lower.Contains("code") || lower.Contains("devenv")) return "&#xE943;";
            if (lower.Contains("spotify") || lower.Contains("music")) return "&#xE8D6;";
            if (lower.Contains("discord") || lower.Contains("teams") || lower.Contains("slack")) return "&#xE8F2;";
            if (lower.Contains("onedrive") || lower.Contains("dropbox") || lower.Contains("drive")) return "&#xE753;";
            if (lower.Contains("steam") || lower.Contains("epic")) return "&#xE7FC;";
            if (lower.Contains("nvidia") || lower.Contains("amd")) return "&#xE7F8;";

            return "&#xE7C4;";
        }
    }
}