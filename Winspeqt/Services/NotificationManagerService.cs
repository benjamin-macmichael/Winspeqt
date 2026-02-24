using Microsoft.Toolkit.Uwp.Notifications;
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
        private static readonly TimeSpan CheckInterval = TimeSpan.FromSeconds(15);   // How often we check if it's time
        private static readonly TimeSpan NotifyInterval = TimeSpan.FromSeconds(30);    // Min gap between ANY notification
        //private static readonly TimeSpan CheckInterval = TimeSpan.FromHours(6);   // How often we check if it's time
        //private static readonly TimeSpan NotifyInterval = TimeSpan.FromDays(3);    // Min gap between ANY notification

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

                    System.Diagnostics.Debug.WriteLine($"[NotificationManager] Storage has LastScanTime: {c.ContainsKey("AppUpdateChecker_LastScanTime")}");
                    System.Diagnostics.Debug.WriteLine($"[NotificationManager] Storage has HealthScore: {c.ContainsKey("AppUpdateChecker_HealthScore")}");

                    if (c.ContainsKey("AppUpdateChecker_LastScanTime"))
                    {
                        var ticks = (long)c["AppUpdateChecker_LastScanTime"];
                        var lastScan = new DateTime(ticks, DateTimeKind.Local);
                        var daysSince = (DateTime.Now - lastScan).TotalDays;
                        System.Diagnostics.Debug.WriteLine($"[NotificationManager] Days since last scan: {daysSince:F1}");

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

                        System.Diagnostics.Debug.WriteLine($"[NotificationManager] Score={score}, Outdated={outdated}, Critical={critical}, Total={total}");

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

                System.Diagnostics.Debug.WriteLine("[NotificationManager] No stored data found, returning never-scanned message");
                return (50, "You haven't checked your apps yet — open Winspeqt to see if any need updates.");
            });

            _timer = new Timer(OnTimerTick, null, TimeSpan.FromSeconds(5), CheckInterval);
            System.Diagnostics.Debug.WriteLine("[NotificationManager] Timer started");
        }

        public Task TriggerCheckAsync() => CheckAndNotifyAsync();

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

                if (score < 100)
                {
                    SendNotification(featureToNotify, score, message);
                    lock (_lock)
                    {
                        _state.LastNotified[featureToNotify] = DateTime.Now;
                        _state.LastNotificationSent = DateTime.Now;
                    }
                    SaveState();
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("[NotificationManager] Score is 100, not sending notification");
                }
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
                    "SystemOptimization" => ("🧹", "Optimization"),
                    "SystemMonitoring" => ("📊", "System Health"),
                    _ => ("💡", featureKey)
                };

                var scoreBar = BuildScoreBar(score);

                string xml = $@"
                    <toast scenario='reminder'>
                        <visual>
                            <binding template='ToastGeneric'>
                                <text>{emoji} Winspeqt: {label} Health Check</text>
                                <text>{scoreBar}  {score}/100</text>
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