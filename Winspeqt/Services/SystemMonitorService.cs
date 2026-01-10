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
                        // Skip system idle process
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
                            Icon = GetProcessIcon(process.ProcessName)
                        };

                        processInfoList.Add(processInfo);
                    }
                    catch (Exception ex)
                    {
                        // Skip processes we can't access
                        System.Diagnostics.Debug.WriteLine($"Error reading process: {ex.Message}");
                    }
                }

                // Sort by memory usage
                return processInfoList.OrderByDescending(p => p.MemoryUsageMB).ToList();
            });
        }

        public async Task<double> GetTotalCpuUsageAsync()
        {
            return await Task.Run(() =>
            {
                try
                {
                    // Use WMI to get accurate CPU usage
                    var searcher = new ManagementObjectSearcher("SELECT LoadPercentage FROM Win32_Processor");
                    var cpuObjects = searcher.Get();

                    double totalLoad = 0;
                    int count = 0;

                    foreach (ManagementObject obj in cpuObjects)
                    {
                        totalLoad += Convert.ToDouble(obj["LoadPercentage"]);
                        count++;
                    }

                    if (count > 0)
                    {
                        return Math.Round(totalLoad / count, 1);
                    }

                    return 0;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error reading CPU via WMI: {ex.Message}");
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
                    // Use GC.GetGCMemoryInfo which gives us total physical memory
                    var gcMemoryInfo = GC.GetGCMemoryInfo();
                    return gcMemoryInfo.TotalAvailableMemoryBytes / 1024 / 1024; // Convert bytes to MB
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error getting total memory: {ex.Message}");
                    return 8192; // Default to 8GB if we can't detect
                }
            });
        }

        private string GetFriendlyName(string processName)
        {
            // Manual overrides for common apps
            var friendlyNames = new Dictionary<string, string>
            {
                { "chrome", "Google Chrome" },
                { "firefox", "Mozilla Firefox" },
                { "msedge", "Microsoft Edge" },
                { "explorer", "File Explorer" },
                { "code", "Visual Studio Code" },
                { "devenv", "Visual Studio" },
                { "spotify", "Spotify" },
                { "discord", "Discord" },
                { "teams", "Microsoft Teams" },
                { "slack", "Slack" },
                { "outlook", "Microsoft Outlook" },
                { "excel", "Microsoft Excel" },
                { "word", "Microsoft Word" },
                { "powerpnt", "Microsoft PowerPoint" },
                { "svchost", "Windows Service Host" },
                { "system", "Windows System" },
                { "audiodg", "Windows Audio" },
                { "dwm", "Desktop Window Manager" }
            };

            var lower = processName.ToLower();

            // Check manual dictionary first
            if (friendlyNames.ContainsKey(lower))
                return friendlyNames[lower];

            // Try to get the file description from the process
            try
            {
                var process = Process.GetProcessesByName(processName).FirstOrDefault();
                if (process != null && !string.IsNullOrEmpty(process.MainModule?.FileVersionInfo?.FileDescription))
                {
                    return process.MainModule.FileVersionInfo.FileDescription;
                }
            }
            catch
            {
                // Can't access process info, continue to fallback
            }

            // Fallback: Clean up the process name
            // Remove .exe if present
            if (lower.EndsWith(".exe"))
                processName = processName.Substring(0, processName.Length - 4);

            // Capitalize first letter
            if (processName.Length > 0)
            {
                return char.ToUpper(processName[0]) + processName.Substring(1);
            }

            return processName;
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

        private string GetProcessIcon(string processName)
        {
            var lower = processName.ToLower();

            if (lower.Contains("chrome") || lower.Contains("firefox") || lower.Contains("edge"))
                return "&#xE774;"; // Globe icon

            if (lower.Contains("explorer"))
                return "&#xE8B7;"; // Folder icon

            if (lower.Contains("code") || lower.Contains("devenv"))
                return "&#xE943;"; // Code icon

            if (lower.Contains("spotify") || lower.Contains("music"))
                return "&#xE8D6;"; // Music icon

            if (lower.Contains("discord") || lower.Contains("teams") || lower.Contains("slack"))
                return "&#xE8F2;"; // Chat icon

            return "&#xE7C4;"; // Default app icon
        }
    }
}