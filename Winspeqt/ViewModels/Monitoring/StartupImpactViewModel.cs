using Microsoft.UI.Dispatching;
using StartupInventory;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows.Input;
using Winspeqt.Helpers;
using Winspeqt.Models;

namespace Winspeqt.ViewModels.Monitoring
{
    /// <summary>
    /// View model for the "Why Is My PC Running Slow?" page.
    /// Loads startup items, exposes grouped data, and provides PC health tips.
    /// </summary>
    public class StartupImpactViewModel : ObservableObject
    {
        private readonly StartupEnumerator _startupEnumerator;
        private readonly DispatcherQueue _dispatcherQueue;
        private readonly IReadOnlyList<StartupGroupDefinition> _groupDefinitions;

        // ── Loading ──────────────────────────────────────────────────────────

        private bool _isLoading;
        public bool IsLoading
        {
            get => _isLoading;
            set => SetProperty(ref _isLoading, value);
        }

        // ── Advanced / startup section ────────────────────────────────────────

        private bool _showStartupDetail;
        /// <summary>
        /// When true the startup detail screen is shown instead of the main screen.
        /// </summary>
        public bool ShowStartupDetail
        {
            get => _showStartupDetail;
            set => SetProperty(ref _showStartupDetail, value);
        }

        // ── PC tips ───────────────────────────────────────────────────────────

        private IReadOnlyList<PcTip> _pcTips = [];
        /// <summary>
        /// The list of actionable tips shown in the "Why is my PC slow?" section.
        /// </summary>
        public IReadOnlyList<PcTip> PcTips
        {
            get => _pcTips;
            private set => SetProperty(ref _pcTips, value);
        }

        private TimeSpan _systemUptime;
        public TimeSpan SystemUptime
        {
            get => _systemUptime;
            private set
            {
                SetProperty(ref _systemUptime, value);
                OnPropertyChanged(nameof(UptimeText));
            }
        }

        public string UptimeText
        {
            get
            {
                var days = (int)_systemUptime.TotalDays;
                if (days >= 1)
                    return $"Your PC has been running for {days} day{(days == 1 ? "" : "s")} without a restart.";
                var hours = (int)_systemUptime.TotalHours;
                return hours >= 1
                    ? $"Your PC has been running for {hours} hour{(hours == 1 ? "" : "s")}."
                    : "Your PC was restarted recently.";
            }
        }

        public bool UptimeWarning => _systemUptime.TotalDays >= 7;

        // ── Startup data ──────────────────────────────────────────────────────

        private StartupApp _startupApp;
        public StartupApp StartupApp
        {
            get => _startupApp;
            private set => SetProperty(ref _startupApp, value);
        }

        private IReadOnlyList<StartupAppGroup> _startupAppGroups;
        public IReadOnlyList<StartupAppGroup> StartupAppGroups
        {
            get => _startupAppGroups;
            private set => SetProperty(ref _startupAppGroups, value);
        }

        // ── Constructor ───────────────────────────────────────────────────────

        public StartupImpactViewModel()
        {
            _startupEnumerator = new StartupEnumerator();
            _dispatcherQueue = DispatcherQueue.GetForCurrentThread();
            _groupDefinitions = BuildGroupDefinitions();
            _startupApp = new StartupApp();
            _startupAppGroups = [];
            _pcTips = [];

            ShowStartupDetail = false;
        }

        // ── Public helpers ────────────────────────────────────────────────────

        // ── Private helpers ───────────────────────────────────────────────────

        private static IReadOnlyList<StartupGroupDefinition> BuildGroupDefinitions()
        {
            var registryRunDescription = "The Registry contains information that Windows continually references during operation. The Run key makes the program run every time the user logs on. Registry keys can be viewed and edited in Registry Editor.";
            var registryRunOnceDescription = "The Registry contains information that Windows continually references during operation. The RunOnce key makes the program run one time, and then the key is deleted. Registry keys can be viewed and edited in Registry Editor.";
            var registryLink = "regedit";
            var registryButtonText = "View Registry Editor";
            var startupDescription = "The startup folder contains shortcuts to programs that run when your computer starts up. It can contain executable files (.exe), shortcuts (.lnk), or script files (ex. .bat or .cmd).";
            var startupLink = "startup";
            var startupButtonText = "View Startup Folder";
            var scheduleDescription = "Scheduled tasks are programs that run when certain criteria are met. Criteria can be specific times, system events, when your computer starts up, and more.";
            var scheduleLink = "schd";
            var scheduleButtonText = "View Task Scheduler";

            return new List<StartupGroupDefinition>
            {
                new("Registry Run",      app => app.RegistryRun,    registryRunDescription,      registryLink, registryButtonText),
                new("Registry Run Once", app => app.RegistryRunOnce, registryRunOnceDescription, registryLink, registryButtonText),
                new("Startup Folder",    app => app.StartupFolder,  startupDescription,           startupLink,  startupButtonText),
                new("Scheduled Tasks",   app => app.ScheduledTask,  scheduleDescription,          scheduleLink, scheduleButtonText),
            };
        }

        private static IReadOnlyList<StartupAppGroup> BuildGroups(
            StartupApp apps,
            IReadOnlyList<StartupGroupDefinition> definitions)
        {
            var groups = new List<StartupAppGroup>();
            foreach (var def in definitions)
            {
                var items = def.ItemsSelector(apps);
                if (items == null || items.Count == 0) continue;
                groups.Add(new StartupAppGroup(def.Title, def.Description, def.Link, def.ButtonText, items));
            }
            return groups;
        }

        private static TimeSpan GetSystemUptime()
        {
            try { return TimeSpan.FromMilliseconds(Environment.TickCount64); }
            catch { return TimeSpan.Zero; }
        }

        private static IReadOnlyList<PcTip> BuildTips(TimeSpan uptime, StartupApp apps)
        {
            var tips = new List<PcTip>();

            // ── Uptime ────────────────────────────────────────────────────────
            var uptimeDays = (int)uptime.TotalDays;
            if (uptimeDays >= 14)
                tips.Add(new PcTip(
                    PcTipSeverity.Warning,
                    "Your PC hasn't been restarted in over 2 weeks",
                    $"Your PC has been running for {uptimeDays} days. Restarting clears memory, installs pending updates, and can noticeably speed things up.",
                    "Restart Now",
                    "restart"));
            else if (uptimeDays >= 7)
                tips.Add(new PcTip(
                    PcTipSeverity.Info,
                    "Consider restarting your PC",
                    $"Your PC has been on for {uptimeDays} days. A restart can clear out background processes and free up memory.",
                    "Restart Now",
                    "restart"));

            // ── Startup apps ──────────────────────────────────────────────────
            var totalStartup = apps.RegistryRun.Count + apps.StartupFolder.Count;
            if (totalStartup >= 10)
                tips.Add(new PcTip(
                    PcTipSeverity.Warning,
                    $"You have {totalStartup} apps launching at startup",
                    "A large number of startup apps can significantly slow down how long it takes to reach your desktop and affect early performance. Consider disabling ones you don't need immediately.",
                    "Review Startup Apps",
                    "startup-section"));
            else if (totalStartup >= 1)
                tips.Add(new PcTip(
                    PcTipSeverity.Info,
                    $"You have {totalStartup} app{(totalStartup == 1 ? "" : "s")} launching at startup",
                    "Startup apps can add time to your boot and consume memory in the background. Review them to see if any can be disabled.",
                    "Review Startup Apps",
                    "startup-section"));

            // ── File Explorer ─────────────────────────────────────────────────
            tips.Add(new PcTip(
                PcTipSeverity.Info,
                "Restart File Explorer if your Windows UI feels sluggish",
                "File Explorer (explorer.exe) handles your taskbar, desktop, and file browsing. Restarting it can fix UI freezes, unresponsive taskbars, and general sluggishness — without a full reboot.",
                "Restart File Explorer",
                "restart-explorer"));

            // ── Network ───────────────────────────────────────────────────────
            tips.Add(new PcTip(
                PcTipSeverity.Info,
                "A slow internet connection can make your PC feel slow",
                "If apps are taking a long time to load, streaming is choppy, or pages are sluggish, your network could be the bottleneck rather than your PC itself. Anything under 25 Mbps download may cause noticeable slowdowns for everyday tasks.",
                "Run a Speed Test",
                "speedtest"));

            // ── Disk space ────────────────────────────────────────────────────
            try
            {
                var drive = new System.IO.DriveInfo(System.IO.Path.GetPathRoot(Environment.GetFolderPath(Environment.SpecialFolder.System)) ?? "C:\\");
                var freeGb = drive.AvailableFreeSpace / (1024.0 * 1024 * 1024);
                var totalGb = drive.TotalSize / (1024.0 * 1024 * 1024);
                var freePct = freeGb / totalGb * 100;

                if (freePct < 5)
                    tips.Add(new PcTip(
                        PcTipSeverity.Warning,
                        $"Your system drive is almost full ({freeGb:F1} GB free)",
                        "Windows needs free space on your system drive for virtual memory, temporary files, and updates. When it runs out, performance can degrade sharply. Try removing unused apps or large files.",
                        "Open Storage Settings",
                        "storage"));
                else if (freePct < 15)
                    tips.Add(new PcTip(
                        PcTipSeverity.Info,
                        $"Your system drive is getting full ({freeGb:F1} GB free, {freePct:F0}% free)",
                        "Low disk space can cause slowdowns. Consider clearing temporary files or uninstalling apps you no longer use.",
                        "Open Storage Settings",
                        "storage"));
            }
            catch { /* skip if drive info unavailable */ }

            // ── Hardware age (heuristic via OS install date) ──────────────────
            try
            {
                using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion");
                if (key?.GetValue("InstallDate") is int installTimestamp)
                {
                    var installDate = DateTimeOffset.FromUnixTimeSeconds(installTimestamp).LocalDateTime;
                    var ageYears = (DateTime.Now - installDate).TotalDays / 365.25;
                    if (ageYears >= 5)
                        tips.Add(new PcTip(
                            PcTipSeverity.Info,
                            "Your Windows installation is over 5 years old",
                            $"Your current Windows installation dates back to around {installDate:MMMM yyyy}. Older installations accumulate software cruft over time. A clean reinstall can sometimes restore snappiness — though this is a big step.",
                            null, null));
                }
            }
            catch { /* skip */ }

            // ── Always-present general tip ────────────────────────────────────
            tips.Add(new PcTip(
                PcTipSeverity.Info,
                "Run Windows Update and check for driver updates",
                "Outdated drivers (especially graphics and chipset) can cause slowdowns, stuttering, and instability. Keeping Windows and drivers up to date is one of the simplest performance maintenance steps.",
                "Open Windows Update",
                "windows-update"));

            return tips;
        }

        public async Task RefreshDataAsync()
        {
            if (IsLoading) return;

            IsLoading = true;
            try
            {
                var (fullStartupApp, uptime) = await Task.Run(() =>
                    (_startupEnumerator.GetStartupItems(), GetSystemUptime()));

                _dispatcherQueue.TryEnqueue(() =>
                {
                    SystemUptime = uptime;
                    StartupApp = fullStartupApp;
                    StartupAppGroups = BuildGroups(StartupApp, _groupDefinitions);
                    PcTips = BuildTips(uptime, fullStartupApp);
                });
            }
            catch
            {
                // Keep existing data if refresh fails.
            }
            finally
            {
                IsLoading = false;
            }
        }

        // ── Nested display models ─────────────────────────────────────────────

        public sealed class StartupAppGroup
        {
            public StartupAppGroup(string title, string description, string link, string buttonText, IReadOnlyList<StartupItem> items)
            {
                Title = title;
                Description = description;
                Link = link;
                ButtonText = buttonText;
                Items = items;
            }

            public string Title { get; }
            public string Description { get; }
            public string Link { get; }
            public string ButtonText { get; }
            public IReadOnlyList<StartupItem> Items { get; }
        }
    }

    // ── PC Tip models ─────────────────────────────────────────────────────────

    public enum PcTipSeverity { Info, Warning }

    public sealed class PcTip
    {
        public PcTip(PcTipSeverity severity, string title, string detail, string? actionLabel, string? actionKey)
        {
            Severity = severity;
            Title = title;
            Detail = detail;
            ActionLabel = actionLabel;
            ActionKey = actionKey;
            HasAction = actionLabel != null && actionKey != null;
        }

        public PcTipSeverity Severity { get; }
        public string Title { get; }
        public string Detail { get; }
        public string? ActionLabel { get; }
        public string? ActionKey { get; }
        public bool HasAction { get; }
        public bool IsWarning => Severity == PcTipSeverity.Warning;
    }
}