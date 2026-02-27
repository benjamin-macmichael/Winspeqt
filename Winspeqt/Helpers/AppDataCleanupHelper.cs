using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Winspeqt.Models;

namespace Winspeqt.Helpers
{
    /// <summary>
    /// Scans AppData folders and cross-references them against the Windows registry
    /// to identify orphaned directories left behind by uninstalled applications.
    /// </summary>
    public static class AppDataCleanupHelper
    {
        // Registry paths that list installed applications
        private static readonly string[] InstalledAppRegistryKeys =
        [
            @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
            @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall",
        ];

        // Well-known system/OS folders that should never be flagged as orphaned
        private static readonly HashSet<string> SystemFolderAllowlist = new(StringComparer.OrdinalIgnoreCase)
        {
            // Windows & system
            "Microsoft", "MicrosoftEdge", "MicrosoftEdgeBackups", "Packages",
            "Windows", "WindowsApps", "WinRT", "Temp", "Temporary Internet Files",
            "Internet Explorer", "History", "Cookies", "Cache",
            // Common runtimes & infrastructure
            "NVIDIA Corporation", "Intel", "AMD", "ATI Technologies",
            "Adobe", "Java", "Oracle", "Python", "pip",
            ".NET", "dotnet", "NuGet",
            // Common launchers / stores (alive by design)
            "Steam", "EpicGamesLauncher", "GOGcom", "BattleNet", "Ubisoft Game Launcher",
            "Discord", "Slack", "Spotify", "Zoom",
            // Misc OS artefacts
            "ConnectedDevicesPlatform", "DBG", "Local Settings", "Programs",
            "CrashDumps", "DiagTrack", "PenWorkspace", "PublisherCacheFiles",
            "SquirrelTemp", "D3DSCache", "GPUCache",
        };

        // ── Public API ────────────────────────────────────────────────────────

        /// <summary>
        /// Scans all three AppData locations and returns folders that have no
        /// matching entry in the installed-applications registry.
        /// </summary>
        public static async Task<List<OrphanedAppDataEntry>> ScanAsync(
            IProgress<ScanProgress>? progress = null,
            CancellationToken cancellationToken = default)
        {
            return await Task.Run(() =>
            {
                var installedNames = GetInstalledApplicationNames();

                var results = new List<OrphanedAppDataEntry>();
                var locations = GetAppDataLocations();
                int total = locations.Count;
                int done = 0;

                foreach (var (rootPath, locationType) in locations)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    progress?.Report(new ScanProgress
                    {
                        CurrentPath = rootPath,
                        Completed = done,
                        Total = total
                    });

                    if (!Directory.Exists(rootPath))
                    {
                        done++;
                        continue;
                    }

                    try
                    {
                        var subDirs = Directory.GetDirectories(rootPath);
                        foreach (var dir in subDirs)
                        {
                            cancellationToken.ThrowIfCancellationRequested();

                            var folderName = Path.GetFileName(dir);

                            if (IsSystemFolder(folderName))
                                continue;

                            if (IsMatchedByInstalledApp(folderName, installedNames))
                                continue;

                            long size = CalculateFolderSize(dir, cancellationToken);
                            DateTime lastMod = GetLastModified(dir);

                            results.Add(new OrphanedAppDataEntry
                            {
                                FolderPath = dir,
                                FolderName = folderName,
                                Location = locationType,
                                SizeBytes = size,
                                LastModified = lastMod,
                            });
                        }
                    }
                    catch (UnauthorizedAccessException) { /* skip inaccessible roots */ }
                    catch (IOException) { /* skip locked/missing dirs */ }

                    done++;
                }

                return results.OrderByDescending(e => e.SizeBytes).ToList();
            }, cancellationToken);
        }

        /// <summary>
        /// Deletes the specified folders. Returns paths that failed with their error messages.
        /// </summary>
        public static async Task<Dictionary<string, string>> DeleteFoldersAsync(
            IEnumerable<OrphanedAppDataEntry> entries,
            IProgress<DeleteProgress>? progress = null,
            CancellationToken cancellationToken = default)
        {
            var failures = new Dictionary<string, string>();
            var list = entries.ToList();
            int total = list.Count;
            int done = 0;

            await Task.Run(() =>
            {
                foreach (var entry in list)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    progress?.Report(new DeleteProgress
                    {
                        CurrentName = entry.FolderName,
                        Completed = done,
                        Total = total
                    });

                    try
                    {
                        if (Directory.Exists(entry.FolderPath))
                            Directory.Delete(entry.FolderPath, recursive: true);
                    }
                    catch (Exception ex)
                    {
                        failures[entry.FolderPath] = ex.Message;
                    }

                    done++;
                }
            }, cancellationToken);

            return failures;
        }

        // ── Registry scanning ─────────────────────────────────────────────────

        /// <summary>
        /// Reads all installed application display names and install locations
        /// from both HKLM and HKCU uninstall keys.
        /// </summary>
        private static HashSet<string> GetInstalledApplicationNames()
        {
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            void ReadKey(RegistryHive hive, string keyPath)
            {
                try
                {
                    using var baseKey = RegistryKey.OpenBaseKey(hive, RegistryView.Default);
                    using var key = baseKey.OpenSubKey(keyPath);
                    if (key is null) return;

                    foreach (var subName in key.GetSubKeyNames())
                    {
                        try
                        {
                            using var sub = key.OpenSubKey(subName);
                            if (sub is null) continue;

                            // Collect the DisplayName
                            if (sub.GetValue("DisplayName") is string displayName && !string.IsNullOrWhiteSpace(displayName))
                                names.Add(displayName.Trim());

                            // Collect the install location folder name
                            if (sub.GetValue("InstallLocation") is string installLoc && !string.IsNullOrWhiteSpace(installLoc))
                            {
                                var folderName = Path.GetFileName(installLoc.TrimEnd('\\', '/'));
                                if (!string.IsNullOrWhiteSpace(folderName))
                                    names.Add(folderName);
                            }

                            // The subkey name itself is often the publisher/app identifier
                            names.Add(subName);
                        }
                        catch { /* skip inaccessible subkey */ }
                    }
                }
                catch { /* skip inaccessible hive */ }
            }

            foreach (var regPath in InstalledAppRegistryKeys)
            {
                ReadKey(RegistryHive.LocalMachine, regPath);
                ReadKey(RegistryHive.CurrentUser, regPath);
            }

            return names;
        }

        /// <summary>
        /// Checks whether a folder name is "close enough" to a known installed app name.
        /// Uses exact match, contains, and starts-with heuristics.
        /// </summary>
        private static bool IsMatchedByInstalledApp(string folderName, HashSet<string> installedNames)
        {
            if (installedNames.Contains(folderName))
                return true;

            // Check if any installed app name starts with or contains the folder name (and vice versa)
            foreach (var name in installedNames)
            {
                if (name.Length < 3 || folderName.Length < 3) continue;

                if (name.StartsWith(folderName, StringComparison.OrdinalIgnoreCase) ||
                    folderName.StartsWith(name, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        private static bool IsSystemFolder(string folderName)
            => SystemFolderAllowlist.Contains(folderName)
            || folderName.StartsWith("com.", StringComparison.OrdinalIgnoreCase)
            || folderName.StartsWith("net.", StringComparison.OrdinalIgnoreCase)
            || (folderName.StartsWith("{") && folderName.EndsWith("}")) // GUIDs
            || folderName.StartsWith(".");

        // ── Filesystem helpers ────────────────────────────────────────────────

        private static List<(string Path, AppDataLocation Location)> GetAppDataLocations()
        {
            var roaming = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var localLow = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "AppData", "LocalLow");

            return
            [
                (roaming,  AppDataLocation.Roaming),
                (local,    AppDataLocation.Local),
                (localLow, AppDataLocation.LocalLow),
            ];
        }

        private static long CalculateFolderSize(string path, CancellationToken ct)
        {
            long total = 0;
            try
            {
                foreach (var file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
                {
                    ct.ThrowIfCancellationRequested();
                    try { total += new FileInfo(file).Length; }
                    catch { /* skip locked files */ }
                }
            }
            catch (OperationCanceledException) { throw; }
            catch { /* skip inaccessible dirs */ }
            return total;
        }

        private static DateTime GetLastModified(string path)
        {
            try { return Directory.GetLastWriteTime(path); }
            catch { return DateTime.MinValue; }
        }
    }

    // ── Progress report types ─────────────────────────────────────────────────

    public record ScanProgress
    {
        public string CurrentPath { get; init; } = string.Empty;
        public int Completed { get; init; }
        public int Total { get; init; }
        public double Percent => Total > 0 ? (double)Completed / Total * 100 : 0;
    }

    public record DeleteProgress
    {
        public string CurrentName { get; init; } = string.Empty;
        public int Completed { get; init; }
        public int Total { get; init; }
        public double Percent => Total > 0 ? (double)Completed / Total * 100 : 0;
    }
}
