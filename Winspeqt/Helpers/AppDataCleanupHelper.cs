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
    public static class AppDataCleanupHelper
    {
        private static readonly string[] InstalledAppRegistryKeys =
        [
            @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
            @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall",
        ];

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
                    catch (UnauthorizedAccessException) { }
                    catch (IOException) { }

                    done++;
                }

                return results.OrderByDescending(e => e.SizeBytes).ToList();
            }, cancellationToken);
        }

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

                            if (sub.GetValue("DisplayName") is string displayName && !string.IsNullOrWhiteSpace(displayName))
                                names.Add(displayName.Trim());

                            if (sub.GetValue("InstallLocation") is string installLoc && !string.IsNullOrWhiteSpace(installLoc))
                            {
                                var folderName = Path.GetFileName(installLoc.TrimEnd('\\', '/'));
                                if (!string.IsNullOrWhiteSpace(folderName))
                                    names.Add(folderName);
                            }
                        }
                        catch { }
                    }
                }
                catch { }
            }

            foreach (var regPath in InstalledAppRegistryKeys)
            {
                ReadKey(RegistryHive.LocalMachine, regPath);
                ReadKey(RegistryHive.CurrentUser, regPath);
            }

            return names;
        }

        /// <summary>
        /// Only skips a folder if it's an exact match or the folder name is fully
        /// contained within a known installed app name (not the other way around).
        /// This prevents "Google" from matching "Google Chrome" and being skipped.
        /// </summary>
        private static bool IsMatchedByInstalledApp(string folderName, HashSet<string> installedNames)
        {
            // Exact match
            if (installedNames.Contains(folderName))
                return true;

            // Only match if an installed app name fully starts with the folder name
            // AND the folder name is at least 5 chars (avoids short false positives)
            if (folderName.Length >= 5)
            {
                foreach (var name in installedNames)
                {
                    if (name.Length < 3) continue;

                    // Folder name exactly matches start of app name (e.g. "Mozilla" matches "Mozilla Firefox")
                    if (name.StartsWith(folderName, StringComparison.OrdinalIgnoreCase))
                        return true;
                }
            }

            return false;
        }

        private static bool IsSystemFolder(string folderName)
            => SystemFolderAllowlist.Contains(folderName)
            || folderName.StartsWith("com.", StringComparison.OrdinalIgnoreCase)
            || folderName.StartsWith("net.", StringComparison.OrdinalIgnoreCase)
            || (folderName.StartsWith("{") && folderName.EndsWith("}"))
            || folderName.StartsWith(".");

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
                    catch { }
                }
            }
            catch (OperationCanceledException) { throw; }
            catch { }
            return total;
        }

        private static DateTime GetLastModified(string path)
        {
            try { return Directory.GetLastWriteTime(path); }
            catch { return DateTime.MinValue; }
        }
    }

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