using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading.Tasks;
using System.Timers;
using Winspeqt.Models;

namespace Winspeqt.Services
{
    public class AppUsageService
    {
        private Dictionary<string, AppUsageData> _usageData;
        private Timer _trackingTimer;
        private Timer _saveTimer;
        private string _currentActiveProcess;
        private DateTime _lastCheckTime;
        private readonly string _dataFilePath;
        private bool _isTracking;

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern int GetWindowThreadProcessId(IntPtr hWnd, out int processId);

        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern IntPtr GetWindow(IntPtr hWnd, uint uCmd);

        [DllImport("user32.dll")]
        private static extern IntPtr GetWindowLong(IntPtr hWnd, int nIndex);

        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_TOOLWINDOW = 0x00000080;
        private const uint GW_OWNER = 4;

        public AppUsageService()
        {
            _usageData = new Dictionary<string, AppUsageData>();
            _lastCheckTime = DateTime.Now;

            // Set up data file path in AppData
            string appDataFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Winspeqt");

            if (!Directory.Exists(appDataFolder))
                Directory.CreateDirectory(appDataFolder);

            _dataFilePath = Path.Combine(appDataFolder, "app_usage_data.json");

            // Load existing data
            LoadPersistedData();

            InitializeTracking();
        }

        private void InitializeTracking()
        {
            // Tracking timer - check active window every second
            _trackingTimer = new Timer(1000);
            _trackingTimer.Elapsed += TrackingTimer_Elapsed;
            _trackingTimer.Start();
            _isTracking = true;

            // Save timer - persist data every 5 minutes
            _saveTimer = new Timer(300000); // 5 minutes
            _saveTimer.Elapsed += (s, e) => SaveData();
            _saveTimer.Start();
        }

        private void TrackingTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            if (!_isTracking) return;

            try
            {
                var activeProcess = GetActiveProcessName();
                if (!string.IsNullOrEmpty(activeProcess))
                {
                    if (_currentActiveProcess != activeProcess)
                    {
                        _currentActiveProcess = activeProcess;
                    }

                    var elapsed = DateTime.Now - _lastCheckTime;
                    TrackUsage(activeProcess, elapsed);
                    _lastCheckTime = DateTime.Now;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error tracking usage: {ex.Message}");
            }
        }

        private string GetActiveProcessName()
        {
            try
            {
                IntPtr hwnd = GetForegroundWindow();

                // Only track if it's a visible, non-tool window
                if (!IsWindowVisible(hwnd))
                    return null;

                // Check if it's a tool window (skip those)
                IntPtr exStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
                if ((exStyle.ToInt32() & WS_EX_TOOLWINDOW) != 0)
                    return null;

                // Check if it has an owner (skip owned windows)
                IntPtr owner = GetWindow(hwnd, GW_OWNER);
                if (owner != IntPtr.Zero)
                    return null;

                GetWindowThreadProcessId(hwnd, out int processId);
                Process process = Process.GetProcessById(processId);

                // Skip system processes
                string processName = process.ProcessName.ToLower();
                if (processName == "explorer" || processName == "textinputhost" ||
                    processName == "searchapp" || processName == "startmenuexperiencehost" ||
                    processName == "shellexperiencehost" || processName == "applicationframehost")
                    return null;

                return process.ProcessName;
            }
            catch
            {
                return null;
            }
        }

        private void TrackUsage(string processName, TimeSpan elapsed)
        {
            if (!_usageData.ContainsKey(processName))
            {
                _usageData[processName] = new AppUsageData
                {
                    ProcessName = processName,
                    TotalUsageTime = TimeSpan.Zero,
                    LaunchCount = 1,
                    FirstUsed = DateTime.Now
                };
            }

            var data = _usageData[processName];
            data.TotalUsageTime += elapsed;
            data.LastUsed = DateTime.Now;
        }

        public async Task<List<AppUsageModel>> GetAppUsageDataAsync()
        {
            return await Task.Run(() =>
            {
                var runningProcesses = Process.GetProcesses()
                    .Select(p => p.ProcessName)
                    .ToHashSet();

                var totalUsageTime = _usageData.Values.Sum(d => d.TotalUsageTime.TotalSeconds);

                return _usageData.Values
                    .OrderByDescending(d => d.TotalUsageTime)
                    .Select(d => new AppUsageModel
                    {
                        AppName = GetFriendlyAppName(d.ProcessName),
                        ProcessName = d.ProcessName,
                        TotalUsageTime = d.TotalUsageTime,
                        LastUsed = d.LastUsed,
                        LaunchCount = d.LaunchCount,
                        UsagePercentage = totalUsageTime > 0 ? (d.TotalUsageTime.TotalSeconds / totalUsageTime) * 100 : 0,
                        IsRunning = runningProcesses.Contains(d.ProcessName)
                    })
                    .ToList();
            });
        }

        public async Task<AppUsageStats> GetUsageStatsAsync()
        {
            return await Task.Run(() =>
            {
                // Count only apps we're actually tracking (user apps)
                var trackedAppsCount = _usageData.Count;
                var mostUsed = _usageData.Values.OrderByDescending(d => d.TotalUsageTime).FirstOrDefault();

                return new AppUsageStats
                {
                    TotalScreenTime = TimeSpan.FromSeconds(_usageData.Values.Sum(d => d.TotalUsageTime.TotalSeconds)),
                    TotalAppsUsed = trackedAppsCount,
                    ActiveApps = trackedAppsCount, // Just show how many apps we've tracked
                    MostUsedApp = mostUsed != null ? GetFriendlyAppName(mostUsed.ProcessName) : "N/A",
                    TrackingStartTime = _usageData.Values.OrderBy(d => d.FirstUsed).FirstOrDefault()?.FirstUsed ?? DateTime.Now
                };
            });
        }

        private string GetFriendlyAppName(string processName)
        {
            var nameMap = new Dictionary<string, string>
            {
                { "chrome", "Google Chrome" },
                { "firefox", "Firefox" },
                { "msedge", "Microsoft Edge" },
                { "Code", "Visual Studio Code" },
                { "devenv", "Visual Studio" },
                { "explorer", "File Explorer" },
                { "notepad", "Notepad" },
                { "Spotify", "Spotify" },
                { "Discord", "Discord" },
                { "Teams", "Microsoft Teams" },
                { "Slack", "Slack" },
                { "OUTLOOK", "Outlook" },
                { "EXCEL", "Excel" },
                { "WINWORD", "Word" },
                { "POWERPNT", "PowerPoint" }
            };

            return nameMap.ContainsKey(processName) ? nameMap[processName] : processName;
        }

        public void ResetTracking()
        {
            _usageData.Clear();
            _lastCheckTime = DateTime.Now;
            SaveData();
        }

        public void StopTracking()
        {
            _isTracking = false;
            _trackingTimer?.Stop();
            SaveData(); // Save before stopping
        }

        public void StartTracking()
        {
            _isTracking = true;
            _lastCheckTime = DateTime.Now;
            _trackingTimer?.Start();
        }

        public void SaveData()
        {
            try
            {
                var dataToSave = new PersistedUsageData
                {
                    LastSaved = DateTime.Now,
                    UsageData = _usageData.Values.ToList()
                };

                var options = new JsonSerializerOptions
                {
                    WriteIndented = true
                };

                string json = JsonSerializer.Serialize(dataToSave, options);
                File.WriteAllText(_dataFilePath, json);

                Debug.WriteLine($"App usage data saved to: {_dataFilePath}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error saving app usage data: {ex.Message}");
            }
        }

        private void LoadPersistedData()
        {
            try
            {
                if (File.Exists(_dataFilePath))
                {
                    string json = File.ReadAllText(_dataFilePath);
                    var loadedData = JsonSerializer.Deserialize<PersistedUsageData>(json);

                    if (loadedData?.UsageData != null)
                    {
                        _usageData = loadedData.UsageData.ToDictionary(d => d.ProcessName);
                        Debug.WriteLine($"Loaded {_usageData.Count} app usage entries from storage");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading app usage data: {ex.Message}");
                _usageData = new Dictionary<string, AppUsageData>();
            }
        }

        public void Dispose()
        {
            SaveData(); // Final save before disposal
            _trackingTimer?.Stop();
            _trackingTimer?.Dispose();
            _saveTimer?.Stop();
            _saveTimer?.Dispose();
        }

        public async Task<List<InstalledAppModel>> GetInstalledAppsAsync()
        {
            return await Task.Run(() =>
            {
                // Just convert our tracked apps to InstalledAppModel format
                return _usageData.Values
                    .Select(tracked => new InstalledAppModel
                    {
                        AppName = GetFriendlyAppName(tracked.ProcessName),
                        Publisher = "Unknown", // We don't track this
                        Version = "Unknown", // We don't track this
                        InstallDate = tracked.FirstUsed,
                        LastUsed = tracked.LastUsed,
                        SizeInBytes = 0, // We don't track this
                        UninstallString = null
                    })
                    .OrderByDescending(a => a.LastUsed)
                    .ToList();
            });
        }
    }

    internal class AppUsageData
    {
        public string ProcessName { get; set; }
        public TimeSpan TotalUsageTime { get; set; }
        public DateTime LastUsed { get; set; }
        public int LaunchCount { get; set; }
        public DateTime FirstUsed { get; set; }
    }

    internal class PersistedUsageData
    {
        public DateTime LastSaved { get; set; }
        public List<AppUsageData> UsageData { get; set; }
    }
}