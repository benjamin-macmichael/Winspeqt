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
        private PerformanceCounter? _diskTimeCounter = new();
        private List<PerformanceCounter> _networkSentCounters = [];
        private List<PerformanceCounter> _networkReceivedCounters = [];

        public SystemMonitorService()
        {
            _availableMemoryCounter = new PerformanceCounter("Memory", "Available MBytes");
            try
            {
                _diskTimeCounter = new PerformanceCounter("PhysicalDisk", "% Disk Time", "_Total");
                _diskTimeCounter.NextValue();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error initializing disk counter: {ex.Message}");
                _diskTimeCounter = null;
            }
            _cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
            _cpuUsageCache = new Dictionary<int, (DateTime, TimeSpan)>();
            InitializeNetworkCounters();
        }

        private void InitializeNetworkCounters()
        {
            try
            {
                _networkSentCounters = new List<PerformanceCounter>();
                _networkReceivedCounters = new List<PerformanceCounter>();
                var category = new PerformanceCounterCategory("Network Interface");
                foreach (var name in category.GetInstanceNames())
                {
                    if (name.Contains("Loopback", StringComparison.OrdinalIgnoreCase) ||
                        name.Contains("Teredo", StringComparison.OrdinalIgnoreCase) ||
                        name.Contains("isatap", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    _networkSentCounters.Add(new PerformanceCounter("Network Interface", "Bytes Sent/sec", name));
                    _networkReceivedCounters.Add(new PerformanceCounter("Network Interface", "Bytes Received/sec", name));
                }

                foreach (var counter in _networkSentCounters)
                    counter.NextValue();
                foreach (var counter in _networkReceivedCounters)
                    counter.NextValue();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error initializing network counters: {ex.Message}");
                _networkSentCounters = new List<PerformanceCounter>();
                _networkReceivedCounters = new List<PerformanceCounter>();
            }
        }

        public async Task<List<ProcessInfo>> GetRunningProcessesAsync()
        {
            return await Task.Run(() =>
            {
                var processes = Process.GetProcesses();
                var processInfoList = new List<ProcessInfo>();

                // First pass: just get memory usage for sorting
                foreach (var process in processes)
                {
                    try
                    {
                        // Skip system idle process
                        if (process.Id == 0) continue;

                        var memoryMB = process.WorkingSet64 / 1024 / 1024;

                        // Only track processes using more than 10MB to reduce overhead
                        if (memoryMB < 10) continue;

                        var processInfo = new ProcessInfo
                        {
                            ProcessId = process.Id,
                            ProcessName = process.ProcessName,
                            Description = GetFriendlyName(process.ProcessName),
                            MemoryUsageMB = memoryMB,
                            CpuUsagePercent = 0, // Will calculate for top processes only
                            Status = process.Responding ? "Running" : "Not Responding",
                            FriendlyExplanation = GetFriendlyExplanation(process.ProcessName, memoryMB),
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

                // Sort by memory and return top processes
                return processInfoList.OrderByDescending(p => p.MemoryUsageMB).ToList();
            });
        }

        private double CalculateProcessCpuUsage(Process process, DateTime now)
        {
            try
            {
                var currentProcessorTime = process.TotalProcessorTime;

                if (_cpuUsageCache.TryGetValue(process.Id, out var lastSample))
                {
                    var timeDiff = (now - lastSample.timestamp).TotalMilliseconds;

                    if (timeDiff > 0)
                    {
                        var processorTimeDiff = (currentProcessorTime - lastSample.processorTime).TotalMilliseconds;
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

        public async Task<List<ProcessInfo>> GetTopProcessesWithCpuAsync(int count = 10)
        {
            return await Task.Run(() =>
            {
                var processes = Process.GetProcesses();
                var processInfoList = new List<ProcessInfo>();
                var now = DateTime.Now;

                // Quick first pass - just memory
                var quickList = new List<(Process proc, long memory)>();
                foreach (var process in processes)
                {
                    try
                    {
                        if (process.Id == 0) continue;
                        var memoryMB = process.WorkingSet64 / 1024 / 1024;
                        if (memoryMB < 10) continue;
                        quickList.Add((process, memoryMB));
                    }
                    catch { }
                }

                // Sort by memory and take top processes
                var topProcesses = quickList.OrderByDescending(x => x.memory).Take(count * 2).ToList();

                // Now calculate CPU only for top processes
                foreach (var (process, memoryMB) in topProcesses)
                {
                    try
                    {
                        double cpuUsage = CalculateProcessCpuUsage(process, now);

                        var processInfo = new ProcessInfo
                        {
                            ProcessId = process.Id,
                            ProcessName = process.ProcessName,
                            Description = GetFriendlyName(process.ProcessName),
                            MemoryUsageMB = memoryMB,
                            CpuUsagePercent = cpuUsage,
                            Status = process.Responding ? "Running" : "Not Responding",
                            FriendlyExplanation = GetFriendlyExplanation(process.ProcessName, memoryMB),
                            Icon = GetProcessIcon(process.ProcessName)
                        };

                        processInfoList.Add(processInfo);
                    }
                    catch { }
                }

                return processInfoList.OrderByDescending(p => p.MemoryUsageMB).Take(count).ToList();
            });
        }

        public async Task<double> GetTotalCpuUsageAsync()
        {
            return await Task.Run(() =>
            {
                try
                {
                    // Use the cached counter for faster results
                    var cpuUsage = _cpuCounter.NextValue();

                    // Return whatever we get immediately (don't wait for second reading on initial load)
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
                    System.Diagnostics.Debug.WriteLine("Getting available memory...");
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

                    System.Diagnostics.Debug.WriteLine("WMI returned no results; falling back to GC memory info.");
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

        public async Task<double> GetNetworkUsageAsync()
        {
            var (sent, received) = await GetNetworkThroughputMbpsAsync();
            return Math.Round(sent + received, 2);
        }

        public async Task<double> GetDiskUsageAsync()
        {
            return await GetDiskActiveTimePercentAsync();
        }

        public async Task<string> GetUsedDiscSpace()
        {
            return await Task.Run(() =>
            {
                try
                {
                    ManagementObject disk = new ManagementObject("win32_logicaldisk.deviceid=\"c:\"");
                    disk.Get();
                    string freespace = (string)disk["FreeSpace"];
                    return freespace;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error getting disk usage: {ex.Message}");
                    return "0"; // Default to 0GB if we can't detect
                }
            });
        }

        public async Task<double> GetDiskActiveTimePercentAsync()
        {
            return await Task.Run(() =>
            {
                try
                {
                    System.Diagnostics.Debug.WriteLine("Getting disk active time...");
                    if (_diskTimeCounter == null)
                        return 0;

                    return Math.Round(_diskTimeCounter.NextValue(), 1);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error reading disk active time: {ex.Message}");
                    return 0;
                }
            });
        }

        public async Task<(double SentMbps, double ReceivedMbps)> GetNetworkThroughputMbpsAsync()
        {
            return await Task.Run(() =>
            {
                try
                {
                    System.Diagnostics.Debug.WriteLine("Getting network throughput...");
                    if ((_networkSentCounters.Count == 0) &&
                        (_networkReceivedCounters.Count == 0))
                        return (0, 0);

                    double sentBytesPerSec = 0;
                    double receivedBytesPerSec = 0;

                    foreach (var counter in _networkSentCounters)
                        sentBytesPerSec += counter.NextValue();
                    foreach (var counter in _networkReceivedCounters)
                        receivedBytesPerSec += counter.NextValue();

                    const double bytesPerMegabit = 125000; // 1 Mbps = 125,000 bytes/sec
                    return (Math.Round(sentBytesPerSec / bytesPerMegabit, 2),
                        Math.Round(receivedBytesPerSec / bytesPerMegabit, 2));
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error reading network throughput: {ex.Message}");
                    return (0, 0);
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
            _diskTimeCounter?.Dispose();
            if (_networkSentCounters != null)
            {
                foreach (var counter in _networkSentCounters)
                    counter.Dispose();
            }
            if (_networkReceivedCounters != null)
            {
                foreach (var counter in _networkReceivedCounters)
                    counter.Dispose();
            }
        }
    }
}
