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
        private PerformanceCounter _cpuCounter;
        private Dictionary<int, (DateTime timestamp, TimeSpan processorTime)> _cpuUsageCache;

        public SystemMonitorService()
        {
            _availableMemoryCounter = new PerformanceCounter("Memory", "Available MBytes");
            _cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
            _cpuUsageCache = new Dictionary<int, (DateTime, TimeSpan)>();
        }

        public async Task<List<ProcessInfo>> GetRunningProcessesAsync()
        {
            return await Task.Run(() =>
            {
                var processes = Process.GetProcesses();
                var processInfoList = new List<ProcessInfo>();
                var now = DateTime.Now;

                foreach (var process in processes)
                {
                    try
                    {
                        // Skip system idle process
                        if (process.Id == 0) continue;

                        // Calculate CPU usage per process
                        double cpuUsage = CalculateProcessCpuUsage(process, now);

                        var processInfo = new ProcessInfo
                        {
                            ProcessId = process.Id,
                            ProcessName = process.ProcessName,
                            Description = GetFriendlyName(process.ProcessName),
                            MemoryUsageMB = process.WorkingSet64 / 1024 / 1024,
                            CpuUsagePercent = cpuUsage,
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

        private double CalculateProcessCpuUsage(Process process, DateTime now)
        {
            try
            {
                var currentProcessorTime = process.TotalProcessorTime;

                if (_cpuUsageCache.ContainsKey(process.Id))
                {
                    var (lastTime, lastProcessorTime) = _cpuUsageCache[process.Id];
                    var timeDiff = (now - lastTime).TotalMilliseconds;

                    if (timeDiff > 0)
                    {
                        var processorTimeDiff = (currentProcessorTime - lastProcessorTime).TotalMilliseconds;
                        var cpuUsage = (processorTimeDiff / (timeDiff * Environment.ProcessorCount)) * 100;
                        _cpuUsageCache[process.Id] = (now, currentProcessorTime);
                        return Math.Round(Math.Min(cpuUsage, 100), 1);
                    }
                }

                _cpuUsageCache[process.Id] = (now, currentProcessorTime);
                return 0;
            }
            catch
            {
                return 0;
            }
        }

        public async Task<double> GetTotalCpuUsageAsync()
        {
            return await Task.Run(() =>
            {
                try
                {
                    // Get current CPU reading
                    var cpuUsage = _cpuCounter.NextValue();

                    // If first reading is 0, wait a bit and try again
                    if (cpuUsage == 0)
                    {
                        System.Threading.Thread.Sleep(100);
                        cpuUsage = _cpuCounter.NextValue();
                    }

                    return Math.Round(cpuUsage, 1);
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
                    // Use WMI to get accurate total physical memory
                    var searcher = new ManagementObjectSearcher("SELECT TotalPhysicalMemory FROM Win32_ComputerSystem");
                    foreach (ManagementObject obj in searcher.Get())
                    {
                        var totalBytes = Convert.ToUInt64(obj["TotalPhysicalMemory"]);
                        return (long)(totalBytes / 1024 / 1024);
                    }

                    return 8192; // Fallback
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
            // Clean the process name first - remove .exe and convert to lowercase
            var cleanName = processName.ToLower();
            if (cleanName.EndsWith(".exe"))
                cleanName = cleanName.Substring(0, cleanName.Length - 4);

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
                { "dwm", "Desktop Window Manager" },
                { "searchindexer", "Windows Search" },
                { "runtimebroker", "Runtime Broker" },
                { "backgroundtaskhost", "Background Tasks" },
                { "csrss", "Windows Client Server" },
                { "winlogon", "Windows Logon" },
                { "taskhostw", "Task Host Window" },
                { "conhost", "Console Window Host" },
                { "fontdrvhost", "Font Driver Host" },
                { "sihost", "Shell Infrastructure Host" },
                { "startmenuexperiencehost", "Start Menu" },
                { "searchapp", "Windows Search App" },
                { "textinputhost", "Text Input Host" }
            };

            // Check manual dictionary with cleaned name
            if (friendlyNames.ContainsKey(cleanName))
                return friendlyNames[cleanName];

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
            // Capitalize first letter
            if (cleanName.Length > 0)
            {
                return char.ToUpper(cleanName[0]) + cleanName.Substring(1);
            }

            return processName;
        }

        private string GetFriendlyExplanation(string processName, long memoryMB)
        {
            var lower = processName.ToLower();

            if (lower.Contains("chrome") || lower.Contains("firefox") || lower.Contains("msedge"))
            {
                if (memoryMB > 500)
                    return "Your web browser is using a lot of memory, probably because you have many tabs open. Consider closing tabs you're not using.";
                return "Your web browser is running normally";
            }

            if (lower.Contains("explorer"))
                return "File Explorer helps you browse files and folders on your PC";

            if (lower.Contains("svchost"))
                return "A Windows background service that helps your PC run smoothly. It's normal to see several of these.";

            if (lower.Contains("system"))
                return "Core Windows system process - this is normal and necessary";

            if (lower.Contains("dwm"))
                return "Manages visual effects and window animations in Windows";

            if (lower.Contains("runtimebroker"))
                return "Manages permissions for apps from the Microsoft Store";

            if (lower.Contains("searchindexer") || lower.Contains("searchapp"))
                return "Helps Windows search find files quickly on your computer";

            if (lower.Contains("spotify") || lower.Contains("music"))
                return "Music streaming application";

            if (lower.Contains("discord") || lower.Contains("teams") || lower.Contains("slack"))
                return "Communication app for messaging and calls";

            if (lower.Contains("code") || lower.Contains("devenv"))
                return "Code editor or development environment";

            if (lower.Contains("csrss") || lower.Contains("winlogon") || lower.Contains("taskhostw"))
                return "Essential Windows system process - should not be closed";

            if (lower.Contains("backgroundtaskhost") || lower.Contains("sihost"))
                return "Windows background service for system tasks";

            if (lower.Contains("startmenu"))
                return "Powers the Windows Start Menu";

            if (memoryMB > 1000)
                return "This app is using a lot of memory. If you're not using it, you might want to close it to free up resources.";

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

            if (lower.Contains("search"))
                return "&#xE721;"; // Search icon

            return "&#xE7C4;"; // Default app icon
        }

        public void Dispose()
        {
            _availableMemoryCounter?.Dispose();
            _cpuCounter?.Dispose();
        }
    }
}