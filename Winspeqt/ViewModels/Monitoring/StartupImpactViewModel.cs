using Microsoft.UI.Dispatching;
using StartupInventory;
using System;
using System.Collections.Generic;
using System.Management;
using System.Threading.Tasks;
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
        /// The list of actionable tips shown in the "Analyzing Your PC" section.
        /// </summary>
        public IReadOnlyList<PcTip> PcTips
        {
            get => _pcTips;
            private set => SetProperty(ref _pcTips, value);
        }

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

        /// <summary>
        /// Formats a TimeSpan into a human-readable uptime string down to the minute.
        /// e.g. "3 days, 4 hours, 12 minutes" or "45 minutes"
        /// </summary>
        public static string FormatUptime(TimeSpan uptime)
        {
            var days = (int)uptime.TotalDays;
            var hours = uptime.Hours;
            var minutes = uptime.Minutes;

            if (days >= 1)
            {
                var parts = new List<string> { $"{days} day{(days == 1 ? "" : "s")}" };
                if (hours > 0) parts.Add($"{hours} hour{(hours == 1 ? "" : "s")}");
                if (minutes > 0) parts.Add($"{minutes} minute{(minutes == 1 ? "" : "s")}");
                return $"Your PC has been running for {string.Join(", ", parts)} without a restart.";
            }
            if (hours >= 1)
            {
                var parts = new List<string> { $"{hours} hour{(hours == 1 ? "" : "s")}" };
                if (minutes > 0) parts.Add($"{minutes} minute{(minutes == 1 ? "" : "s")}");
                return $"Your PC has been running for {string.Join(", ", parts)}.";
            }
            if (minutes >= 1)
                return $"Your PC has been running for {minutes} minute{(minutes == 1 ? "" : "s")}.";

            return "Your PC was restarted very recently.";
        }

        /// <summary>
        /// Attempts to get the BIOS release date via WMI. Returns null on failure.
        /// </summary>
        private static DateTime? GetBiosDate()
        {
            try
            {
                using var searcher = new ManagementObjectSearcher("SELECT ReleaseDate FROM Win32_BIOS");
                foreach (ManagementObject obj in searcher.Get())
                {
                    var raw = obj["ReleaseDate"]?.ToString();
                    if (!string.IsNullOrEmpty(raw))
                    {
                        // WMI datetime format: yyyyMMddHHmmss.mmmmmm+UTC
                        var dateStr = raw[..8]; // "20181204"
                        if (DateTime.TryParseExact(dateStr, "yyyyMMdd",
                            System.Globalization.CultureInfo.InvariantCulture,
                            System.Globalization.DateTimeStyles.None, out var biosDate))
                            return biosDate;
                    }
                }
            }
            catch { /* WMI unavailable */ }
            return null;
        }

        /// <summary>
        /// Gets the Windows OS install date from the registry.
        /// </summary>
        private static DateTime? GetOsInstallDate()
        {
            try
            {
                using var key = Microsoft.Win32.Registry.LocalMachine
                    .OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion");
                if (key?.GetValue("InstallDate") is int ts)
                    return DateTimeOffset.FromUnixTimeSeconds(ts).LocalDateTime;
            }
            catch { }
            return null;
        }

        private static double GetRamUsagePercent()
        {
            try
            {
                using var searcher = new ManagementObjectSearcher("SELECT TotalVisibleMemorySize, FreePhysicalMemory FROM Win32_OperatingSystem");
                foreach (ManagementObject obj in searcher.Get())
                {
                    var total = Convert.ToDouble(obj["TotalVisibleMemorySize"]);
                    var free = Convert.ToDouble(obj["FreePhysicalMemory"]);
                    if (total > 0)
                        return (total - free) / total * 100.0;
                }
            }
            catch { }
            return -1;
        }

        private static (bool isHdd, string driveName) GetSystemDriveType()
        {
            try
            {
                var systemRoot = System.IO.Path.GetPathRoot(Environment.GetFolderPath(Environment.SpecialFolder.System));
                using var partSearcher = new ManagementObjectSearcher(
                    $"ASSOCIATORS OF {{Win32_LogicalDisk.DeviceID='{systemRoot?.TrimEnd('\\')}'}}" +
                    " WHERE AssocClass=Win32_LogicalDiskToPartition");
                foreach (ManagementObject part in partSearcher.Get())
                {
                    using var diskSearcher = new ManagementObjectSearcher(
                        $"ASSOCIATORS OF {{Win32_DiskPartition.DeviceID='{part["DeviceID"]}'}}" +
                        " WHERE AssocClass=Win32_DiskDriveToDiskPartition");
                    foreach (ManagementObject disk in diskSearcher.Get())
                    {
                        var mediaType = disk["MediaType"]?.ToString() ?? "";
                        var model = disk["Model"]?.ToString() ?? "your system drive";
                        // MediaType contains "Fixed hard disk" for HDD, SSD usually says "SSD" in model or has different media type
                        var isSsd = mediaType.Contains("SSD", StringComparison.OrdinalIgnoreCase)
                                 || (disk["Model"]?.ToString() ?? "").Contains("SSD", StringComparison.OrdinalIgnoreCase)
                                 || (disk["Model"]?.ToString() ?? "").Contains("NVMe", StringComparison.OrdinalIgnoreCase)
                                 || (disk["Model"]?.ToString() ?? "").Contains("Solid", StringComparison.OrdinalIgnoreCase);
                        return (!isSsd, model);
                    }
                }
            }
            catch { }
            return (false, "");
        }

        private static double GetTempFolderSizeGb()
        {
            try
            {
                var tempPath = System.IO.Path.GetTempPath();
                var dir = new System.IO.DirectoryInfo(tempPath);
                if (!dir.Exists) return 0;
                long bytes = 0;
                foreach (var file in dir.EnumerateFiles("*", System.IO.SearchOption.AllDirectories))
                {
                    try { bytes += file.Length; } catch { }
                }
                return bytes / (1024.0 * 1024 * 1024);
            }
            catch { }
            return 0;
        }

        private static bool IsHighPerformancePowerPlan()
        {
            try
            {
                using var searcher = new ManagementObjectSearcher("SELECT ElementName, IsActive FROM Win32_PowerPlan");
                foreach (ManagementObject obj in searcher.Get())
                {
                    if (obj["IsActive"] is true)
                    {
                        var name = obj["ElementName"]?.ToString() ?? "";
                        return name.Contains("High performance", StringComparison.OrdinalIgnoreCase)
                            || name.Contains("Ultimate", StringComparison.OrdinalIgnoreCase);
                    }
                }
            }
            catch { }
            return false;
        }

        private static IReadOnlyList<PcTip> BuildTips(TimeSpan uptime, StartupApp apps)
        {
            var tips = new List<PcTip>();

            // ── Uptime ────────────────────────────────────────────────────────
            var uptimeDays = (int)uptime.TotalDays;
            var uptimeFormatted = FormatUptime(uptime);

            if (uptimeDays >= 7)
                tips.Add(new PcTip(
                    PcTipSeverity.Warning,
                    "Your PC hasn't been restarted in over a week",
                    $"{uptimeFormatted} A restart is one of the quickest fixes you can try.",
                    "Restart Now",
                    "restart"));
            else if (uptimeDays >= 3)
                tips.Add(new PcTip(
                    PcTipSeverity.Info,
                    "Consider restarting your PC",
                    $"{uptimeFormatted} If something is behaving strangely or feeling sluggish, this is always the first thing worth trying.",
                    "Restart Now",
                    "restart"));
            else
                tips.Add(new PcTip(
                    PcTipSeverity.Info,
                    "Restarting is always a good first troubleshooting step",
                    $"{uptimeFormatted} Some hard-to-find background processes can only be cleared with a reboot. If your PC feels off, a restart is the quickest thing to try.",
                    "Restart Now",
                    "restart"));

            // ── PC Age ────────────────────────────────────────────────────────
            var biosDate = GetBiosDate();
            var osDate = GetOsInstallDate();

            if (biosDate.HasValue)
            {
                var ageYears = (DateTime.Now - biosDate.Value).TotalDays / 365.25;
                var ageDesc = ageYears >= 1
                    ? $"{(int)ageYears} year{((int)ageYears == 1 ? "" : "s")} old (BIOS date: {biosDate.Value:MMMM yyyy})"
                    : $"less than a year old (BIOS date: {biosDate.Value:MMMM yyyy})";

                if (ageYears >= 7)
                    tips.Add(new PcTip(
                        PcTipSeverity.Warning,
                        $"Your PC hardware is approximately {ageDesc}",
                        "Older hardware naturally becomes slower over time as software demands grow. You may want to consider a hardware upgrade or replacing the PC, especially if performance has been degrading gradually.",
                        null, null));
                else if (ageYears >= 4)
                    tips.Add(new PcTip(
                        PcTipSeverity.Info,
                        $"Your PC hardware is approximately {ageDesc}",
                        "Your PC is getting on in age. It should still run modern software well, but you may start noticing slowdowns with more demanding applications.",
                        null, null));
                else
                    tips.Add(new PcTip(
                        PcTipSeverity.Info,
                        $"Your PC hardware is relatively recent — {ageDesc}",
                        "Hardware age is unlikely to be the cause of any slowdowns you're experiencing.",
                        null, null));
            }
            else if (osDate.HasValue)
            {
                // Fallback: OS install date as a proxy
                var ageYears = (DateTime.Now - osDate.Value).TotalDays / 365.25;
                if (ageYears >= 5)
                    tips.Add(new PcTip(
                        PcTipSeverity.Info,
                        "Your Windows installation is over 5 years old",
                        $"Your current Windows installation dates back to around {osDate.Value:MMMM yyyy}. Older installations can accumulate software cruft over time. A clean reinstall can sometimes restore snappiness — though this is a big step.",
                        null, null));
            }

            // ── File Explorer ─────────────────────────────────────────────────
            tips.Add(new PcTip(
                PcTipSeverity.Info,
                "Restart File Explorer if your Windows UI feels sluggish",
                "File Explorer (explorer.exe) handles your taskbar, desktop, and file browsing. Restarting it can fix UI freezes, unresponsive taskbars, and general sluggishness — without a full reboot.",
                "Restart File Explorer",
                "restart-explorer"));

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

            // ── RAM usage ─────────────────────────────────────────────────────
            var ramPct = GetRamUsagePercent();
            if (ramPct >= 90)
                tips.Add(new PcTip(
                    PcTipSeverity.Warning,
                    $"Your RAM is almost full ({ramPct:F0}% in use)",
                    "With very little free memory, Windows is likely using your disk as overflow (virtual memory), which is significantly slower. Close apps you aren't using or consider upgrading your RAM.",
                    "Open Task Manager",
                    "taskmgr"));
            else if (ramPct >= 80)
                tips.Add(new PcTip(
                    PcTipSeverity.Info,
                    $"Your RAM usage is high ({ramPct:F0}% in use)",
                    "High memory usage can cause slowdowns, especially when switching between apps. Check Task Manager to see what's using the most memory.",
                    "Open Task Manager",
                    "taskmgr"));

            // ── Windows Update ────────────────────────────────────────────────
            tips.Add(new PcTip(
                PcTipSeverity.Info,
                "Run Windows Update and check for driver updates",
                "Outdated drivers (especially graphics and chipset) can cause slowdowns, stuttering, and instability. Keeping Windows and drivers up to date is one of the simplest performance maintenance steps.",
                "Open Windows Update",
                "windows-update"));

            // ── Network ───────────────────────────────────────────────────────
            tips.Add(new PcTip(
                PcTipSeverity.Info,
                "A slow internet connection can make your PC feel slow",
                "If apps are taking a long time to load, streaming is choppy, or pages are sluggish, your network could be the bottleneck rather than your PC itself. Anything under 25 Mbps download may cause noticeable slowdowns for everyday tasks.",
                "Run a Speed Test",
                "speedtest"));

            // ── Temp files ────────────────────────────────────────────────────
            var tempGb = GetTempFolderSizeGb();
            if (tempGb >= 2)
                tips.Add(new PcTip(
                    PcTipSeverity.Info,
                    $"Your temp folder is using {tempGb:F1} GB of disk space",
                    "Temporary files accumulate over time and can take up significant space on your system drive. Clearing them is safe and can free up space Windows needs to operate efficiently.",
                    "Open Storage Sense",
                    "storage"));

            // ── Disk space ────────────────────────────────────────────────────
            try
            {
                var drive = new System.IO.DriveInfo(
                    System.IO.Path.GetPathRoot(Environment.GetFolderPath(Environment.SpecialFolder.System)) ?? "C:\\");
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

            // ── Power plan ────────────────────────────────────────────────────
            if (!IsHighPerformancePowerPlan())
                tips.Add(new PcTip(
                    PcTipSeverity.Info,
                    "Your power plan may be limiting performance",
                    "Windows can throttle your CPU speed to save power when on the Balanced or Power Saver plan. Switching to High Performance ensures your hardware runs at full speed.",
                    "Open Power Settings",
                    "power"));

            // ── Browser extensions ────────────────────────────────────────────
            tips.Add(new PcTip(
                PcTipSeverity.Info,
                "Too many browser extensions can slow things down",
                "Each browser extension runs in the background and consumes memory and CPU. If your browser feels sluggish or your PC slows down when browsing, try disabling extensions you don't actively use.",
                null, null));

            // ── HDD vs SSD ────────────────────────────────────────────────────
            var (isHdd, driveName) = GetSystemDriveType();
            if (isHdd)
                tips.Add(new PcTip(
                    PcTipSeverity.Info,
                    "Your PC is running on a traditional hard drive (HDD)",
                    $"{(string.IsNullOrEmpty(driveName) ? "Your system drive" : driveName)} is a spinning hard disk, which is significantly slower than a modern SSD for everyday tasks like booting, opening apps, and loading files. Upgrading to an SSD is one of the single biggest performance improvements you can make on an older PC.",
                    null, null));

            // ── Malware scan ──────────────────────────────────────────────────
            tips.Add(new PcTip(
                PcTipSeverity.Info,
                "Run a malware scan",
                "Malware and unwanted background programs are a common but easy-to-miss cause of slowdowns. Running a Windows Defender scan is quick and free.",
                "Open Windows Security",
                "security"));

            // ── SFC scan ──────────────────────────────────────────────────────
            tips.Add(new PcTip(
                PcTipSeverity.Info,
                "Scan for corrupted system files",
                "Windows includes a built-in tool (System File Checker) that scans for and repairs corrupted or missing system files, which can cause slowdowns and instability. This will open an elevated terminal window and may take a few minutes to complete — leave it open until it finishes.",
                "Run System File Checker",
                "sfc"));

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