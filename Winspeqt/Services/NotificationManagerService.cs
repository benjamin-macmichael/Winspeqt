using System;
using System.Collections.Generic;
using System.IO;
using System.Security;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Windows.Data.Xml.Dom;
using Windows.UI.Notifications;

namespace Winspeqt.Services
{
    public class NotificationManagerService : IDisposable
    {
        // --- Singleton ---
        private static NotificationManagerService? _instance;
        private static readonly object _lock = new();
        public static NotificationManagerService Instance
        {
            get
            {
                lock (_lock)
                {
                    _instance ??= new NotificationManagerService();
                    return _instance;
                }
            }
        }

        // --- Config ---
        //private static readonly TimeSpan CheckInterval = TimeSpan.FromSeconds(10);   // How often we check if it's time
        //private static readonly TimeSpan NotifyInterval = TimeSpan.FromSeconds(5);    // Min gap between ANY notification
        private static readonly TimeSpan CheckInterval = TimeSpan.FromHours(6);   // How often we check if it's time
        private static readonly TimeSpan NotifyInterval = TimeSpan.FromDays(6);    // Min gap between ANY notification

        private readonly string _stateFilePath;
        private readonly Dictionary<string, Func<Task<(int score, string message)>>> _features = new();
        private readonly List<string> _featureOrder = new();
        private NotificationState _state = new();
        private Timer? _timer;
        private bool _disposed;
        private bool _notificationsAvailable = false;

        private NotificationManagerService()
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var dir = Path.Combine(appData, "Winspeqt");
            Directory.CreateDirectory(dir);
            _stateFilePath = Path.Combine(dir, "notification_state.json");
            LoadState();
        }

        public void RegisterFeature(string featureKey, Func<Task<(int score, string message)>> scoreProvider)
        {
            lock (_lock)
            {
                _features[featureKey] = scoreProvider;
                if (!_featureOrder.Contains(featureKey))
                    _featureOrder.Add(featureKey);
                if (!_state.LastNotified.ContainsKey(featureKey))
                    _state.LastNotified[featureKey] = DateTime.MinValue;
            }
            System.Diagnostics.Debug.WriteLine($"[NotificationManager] Registered feature: {featureKey}");
        }

        public void Start()
        {
            System.Diagnostics.Debug.WriteLine("[NotificationManager] Start() called");
            _notificationsAvailable = true;

            RegisterFeature("AppUpdateChecker", async () =>
            {
                System.Diagnostics.Debug.WriteLine("[NotificationManager] AppUpdateChecker delegate called");
                try
                {
                    var c = Windows.Storage.ApplicationData.Current.LocalSettings.Values;

                    if (c.ContainsKey("AppUpdateChecker_LastScanTime"))
                    {
                        var ticks = (long)c["AppUpdateChecker_LastScanTime"];
                        var lastScan = new DateTime(ticks, DateTimeKind.Local);
                        var daysSince = (DateTime.Now - lastScan).TotalDays;

                        if (daysSince > 14)
                            return (50, $"Your last app scan was {(int)daysSince} days ago — open Winspeqt to get a fresh result.");
                    }

                    if (c.ContainsKey("AppUpdateChecker_HealthScore"))
                    {
                        var score = (int)c["AppUpdateChecker_HealthScore"];
                        var outdated = c.ContainsKey("AppUpdateChecker_OutdatedApps") ? (int)c["AppUpdateChecker_OutdatedApps"] : 0;
                        var critical = c.ContainsKey("AppUpdateChecker_CriticalApps") ? (int)c["AppUpdateChecker_CriticalApps"] : 0;
                        var total = c.ContainsKey("AppUpdateChecker_TotalApps") ? (int)c["AppUpdateChecker_TotalApps"] : 0;
                        var upToDate = c.ContainsKey("AppUpdateChecker_UpToDateApps") ? (int)c["AppUpdateChecker_UpToDateApps"] : 0;

                        string msg = score switch
                        {
                            >= 90 => $"Your apps are in great shape! {upToDate} of {total} are up to date.",
                            >= 70 => $"{outdated} app{(outdated == 1 ? "" : "s")} could use an update. Open Winspeqt to see which ones.",
                            >= 50 => $"{outdated + critical} apps need attention. Keep your software up to date for best security.",
                            _ => $"Your app health is low — {critical} critical and {outdated} outdated apps found. Update them soon!"
                        };

                        return (score, msg);
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[NotificationManager] Error reading storage: {ex.Message}");
                }

                return (50, "You haven't checked your apps yet — open Winspeqt to see if any need updates.");
            });

            RegisterFeature("SecurityStatus", async () =>
            {
                System.Diagnostics.Debug.WriteLine("[NotificationManager] SecurityStatus delegate called");
                try
                {
                    var c = Windows.Storage.ApplicationData.Current.LocalSettings.Values;

                    if (c.ContainsKey("SecurityStatus_HealthScore"))
                    {
                        var score = (int)c["SecurityStatus_HealthScore"];

                        string msg = score switch
                        {
                            100 => "Your security is perfect! Have you checked your app updates lately?",
                            >= 90 => $"Your security score is {score}/100 — almost perfect! Open Winspeqt to see what's holding you back.",
                            >= 70 => $"Your security score is {score}/100. A few things could be improved — open Winspeqt to see what needs attention.",
                            >= 50 => $"Your security score is {score}/100. Some issues need your attention — open Winspeqt for details.",
                            _ => $"Your security score is {score}/100 — your PC needs attention. Open Winspeqt to see what's at risk."
                        };

                        return (score, msg);
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[NotificationManager] Error reading SecurityStatus storage: {ex.Message}");
                }

                return (50, "You haven't checked your security status yet — open Winspeqt to run a scan.");
            });

            RegisterFeature("Optimization", async () =>
            {
                System.Diagnostics.Debug.WriteLine("[NotificationManager] Optimization delegate called");
                try
                {
                    var c = Windows.Storage.ApplicationData.Current.LocalSettings.Values;

                    if (!c.ContainsKey("Optimization_LastRunTime"))
                        return (50, "You haven't run an optimization yet — open Winspeqt to free up space on your PC.");

                    var ticks = (long)c["Optimization_LastRunTime"];
                    var lastRun = new DateTime(ticks, DateTimeKind.Local);
                    var daysSince = (DateTime.Now - lastRun).TotalDays;
                    var bytesFreed = c.ContainsKey("Optimization_LastBytesFreed") ? (long)c["Optimization_LastBytesFreed"] : 0;
                    var mb = bytesFreed / 1_048_576.0;
                    var freed = mb >= 1024 ? $"{mb / 1024:F1} GB" : $"{mb:F0} MB";

                    string msg = daysSince > 14
                        ? $"It's been {(int)daysSince} days since your last cleanup — junk files may be building up. Open Winspeqt to free up space."
                        : $"Last cleanup freed {freed} of junk files. Run it again to keep your PC running smoothly.";

                    return (-1, msg);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[NotificationManager] Error reading Optimization storage: {ex.Message}");
                }

                return (-1, "You haven't run an optimization yet — open Winspeqt to free up space on your PC.");
            });

            RegisterFeature("LargeFileFinder", async () =>
            {
                System.Diagnostics.Debug.WriteLine("[NotificationManager] LargeFileFinder delegate called");
                try
                {
                    var c = Windows.Storage.ApplicationData.Current.LocalSettings.Values;

                    if (c.ContainsKey("LargeFileFinder_Zone"))
                    {
                        var zone = (string)c["LargeFileFinder_Zone"];
                        var usedPct = c.ContainsKey("LargeFileFinder_UsedPercent") ? (int)c["LargeFileFinder_UsedPercent"] : -1;
                        var avail = c.ContainsKey("LargeFileFinder_AvailableBytes") ? (long)c["LargeFileFinder_AvailableBytes"] : 0;

                        string availLabel = FormatBytes(avail);

                        string msg = zone switch
                        {
                            "Green" => $"Your drive is in great shape — only {usedPct}% used with {availLabel} free.",
                            "Orange" => $"Your drive is {usedPct}% full with {availLabel} remaining. Consider cleaning up large files.",
                            "Red" => $"Your drive is {usedPct}% full — only {availLabel} left! Open Winspeqt to free up space.",
                            _ => $"Open Winspeqt's Large File Finder to check how much space you have left."
                        };

                        return (-1, msg);
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[NotificationManager] Error reading LargeFileFinder storage: {ex.Message}");
                }

                return (-1, "Open Winspeqt's Large File Finder to check how much space you have left on your drive.");
            });

            _timer = new Timer(OnTimerTick, null, TimeSpan.FromSeconds(30), CheckInterval);
            System.Diagnostics.Debug.WriteLine("[NotificationManager] Timer started");
        }

        public Task TriggerCheckAsync() => CheckAndNotifyAsync();

        // Tracked in-memory so we only toast once per network name per session
        private readonly HashSet<string> _notifiedUnsecuredNetworks = new(StringComparer.OrdinalIgnoreCase);

        // Throttle: don't re-notify about Quick Assist within 5 minutes
        private DateTime _lastQuickAssistNotification = DateTime.MinValue;

        public void SendUnsecuredNetworkNotification(string networkName)
        {
            if (!_notificationsAvailable) return;
            if (!_notifiedUnsecuredNetworks.Add(networkName)) return; // already notified this session

            try
            {
                var escapedName = System.Security.SecurityElement.Escape(networkName);
                string xml = $@"
                    <toast scenario='reminder'>
                        <visual>
                            <binding template='ToastGeneric'>
                                <text>⚠️ Winspeqt: Unsecured Network Detected</text>
                                <text>You are connected to ""{escapedName}"" — this network has no password or encryption.</text>
                                <text>Your passwords and personal data may be visible to others nearby. Consider disconnecting or using a VPN.</text>
                            </binding>
                        </visual>
                        <audio src='ms-winsoundevent:Notification.Looping.Alarm2' loop='false'/>
                        <actions>
                            <action content='Open Network Security' arguments='action=open&amp;feature=NetworkSecurity' activationType='foreground'/>
                            <action content='Dismiss' arguments='dismiss' activationType='system' hint-buttonStyle='Success'/>
                        </actions>
                    </toast>";

                var doc = new Windows.Data.Xml.Dom.XmlDocument();
                doc.LoadXml(xml);
                var toast = new ToastNotification(doc);
                ToastNotificationManager.CreateToastNotifier().Show(toast);

                System.Diagnostics.Debug.WriteLine($"[NotificationManager] Unsecured network toast shown for: {networkName}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[NotificationManager] Failed to send unsecured network toast: {ex.Message}");
            }
        }

        public void SendQuickAssistLaunchedNotification()
        {
            if (!_notificationsAvailable) return;
            if (DateTime.Now - _lastQuickAssistNotification < TimeSpan.FromMinutes(5)) return;
            _lastQuickAssistNotification = DateTime.Now;

            try
            {
                string xml = @"
                    <toast scenario='reminder'>
                        <visual>
                            <binding template='ToastGeneric'>
                                <text>⚠️ Winspeqt: Quick Assist Session Started</text>
                                <text>Someone now has remote access to your screen and files.</text>
                                <text>Only continue if YOU made this call. If something feels off — close the session immediately.</text>
                            </binding>
                        </visual>
                        <audio src='ms-winsoundevent:Notification.Looping.Alarm2' loop='false'/>
                        <actions>
                            <action content='Close Quick Assist' arguments='action=closeQuickAssist' activationType='foreground'/>
                            <action content='Dismiss' arguments='dismiss' activationType='system' hint-buttonStyle='Success'/>
                        </actions>
                    </toast>";

                var doc = new XmlDocument();
                doc.LoadXml(xml);
                var toast = new ToastNotification(doc);
                ToastNotificationManager.CreateToastNotifier().Show(toast);

                System.Diagnostics.Debug.WriteLine("[NotificationManager] Quick Assist launched toast shown");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[NotificationManager] Failed to send Quick Assist toast: {ex.Message}");
            }
        }

        private void OnTimerTick(object? state)
        {
            System.Diagnostics.Debug.WriteLine("[NotificationManager] Timer tick fired");
            _ = CheckAndNotifyAsync();
        }

        private async Task CheckAndNotifyAsync()
        {
            System.Diagnostics.Debug.WriteLine($"[NotificationManager] CheckAndNotifyAsync — LastSent={_state.LastNotificationSent}, Gap={DateTime.Now - _state.LastNotificationSent}");

            if (DateTime.Now - _state.LastNotificationSent < NotifyInterval)
            {
                System.Diagnostics.Debug.WriteLine("[NotificationManager] Cooldown active, skipping");
                return;
            }

            string? featureToNotify = null;
            Func<Task<(int, string)>>? provider = null;

            lock (_lock)
            {
                if (_featureOrder.Count == 0)
                {
                    System.Diagnostics.Debug.WriteLine("[NotificationManager] No features registered, skipping");
                    return;
                }

                DateTime oldestTime = DateTime.MaxValue;
                foreach (var key in _featureOrder)
                {
                    var last = _state.LastNotified.TryGetValue(key, out var t) ? t : DateTime.MinValue;
                    if (last < oldestTime)
                    {
                        oldestTime = last;
                        featureToNotify = key;
                    }
                }

                if (featureToNotify != null)
                    _features.TryGetValue(featureToNotify, out provider);
            }

            System.Diagnostics.Debug.WriteLine($"[NotificationManager] Feature selected: {featureToNotify}");

            if (featureToNotify == null || provider == null) return;

            try
            {
                var (score, message) = await provider();
                System.Diagnostics.Debug.WriteLine($"[NotificationManager] Provider returned score={score}, message={message}");

                SendNotification(featureToNotify, score, message);
                lock (_lock)
                {
                    _state.LastNotified[featureToNotify] = DateTime.Now;
                    _state.LastNotificationSent = DateTime.Now;
                }
                SaveState();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[NotificationManager] Error: {ex.Message}");
            }
        }

        private static void SendNotification(string featureKey, int score, string message)
        {
            if (!Instance._notificationsAvailable)
            {
                System.Diagnostics.Debug.WriteLine("[NotificationManager] Notifications not available, skipping");
                return;
            }

            try
            {
                var (emoji, label) = featureKey switch
                {
                    "AppUpdateChecker" => ("🔄", "App Updates"),
                    "SecurityStatus" => ("🛡️", "Security"),
                    "Optimization" => ("🧹", "Optimization"),
                    "LargeFileFinder" => ("💾", "Drive Storage"),
                    "SystemMonitoring" => ("📊", "System Health"),
                    _ => ("💡", featureKey)
                };

                var scoreBar = BuildScoreBar(score);
                var scoreLine = score >= 0
                    ? $"<text>{scoreBar}  {score}/100</text>"
                    : string.Empty;

                string xml = $@"
                    <toast scenario='reminder'>
                        <visual>
                            <binding template='ToastGeneric'>
                                <text>{emoji} Winspeqt: {label} Health Check</text>
                                {scoreLine}
                                <text>{SecurityElement.Escape(message)}</text>
                            </binding>
                        </visual>
                        <audio src='ms-winsoundevent:Notification.Default'/>
                        <actions>
                            <action content='Open Winspeqt' arguments='action=open&amp;feature={featureKey}' activationType='foreground'/>
                            <action content='Dismiss' arguments='dismiss' activationType='system' hint-buttonStyle='Success'/>
                        </actions>
                    </toast>";

                var doc = new XmlDocument();
                doc.LoadXml(xml);

                var toast = new ToastNotification(doc);
                ToastNotificationManager.CreateToastNotifier().Show(toast);

                System.Diagnostics.Debug.WriteLine($"[NotificationManager] Toast shown for {featureKey}, score={score}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[NotificationManager] Failed to send toast: {ex.Message}");
            }
        }

        private static string BuildScoreBar(int score)
        {
            int filled = score / 10;
            int empty = 10 - filled;
            return new string('█', filled) + new string('░', empty);
        }

        /// <summary>
        /// Formats a byte count into a human-readable GB or MB string.
        /// </summary>
        private static string FormatBytes(long bytes)
        {
            if (bytes <= 0) return "unknown space";
            double gb = bytes / 1_073_741_824.0;
            if (gb >= 1.0) return $"{gb:F1} GB";
            double mb = bytes / 1_048_576.0;
            return $"{mb:F0} MB";
        }

        private void LoadState()
        {
            try
            {
                if (File.Exists(_stateFilePath))
                {
                    var json = File.ReadAllText(_stateFilePath);
                    _state = JsonSerializer.Deserialize<NotificationState>(json) ?? new NotificationState();
                }
            }
            catch
            {
                _state = new NotificationState();
            }
        }

        private void SaveState()
        {
            try
            {
                var json = JsonSerializer.Serialize(_state, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_stateFilePath, json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[NotificationManager] Failed to save state: {ex.Message}");
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _timer?.Dispose();
        }

        private class NotificationState
        {
            public Dictionary<string, DateTime> LastNotified { get; set; } = new();
            public DateTime LastNotificationSent { get; set; } = DateTime.MinValue;
        }
    }
}