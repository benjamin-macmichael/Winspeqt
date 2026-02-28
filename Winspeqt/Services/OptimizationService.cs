using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Winspeqt.Models;

namespace Winspeqt.Services
{
    public class OptimizationService
    {
        public async Task<OptimizationResult> RunOptimizationAsync(
            OptimizationOptions options,
            IProgress<string> progress)
        {
            var result = new OptimizationResult();

            var tasks = new List<(bool enabled, Func<Task<OptimizationTaskResult>> action)>
            {
                (options.CleanRecycleBin,        () => CleanRecycleBinAsync(progress)),
                (options.CleanTempFiles,          () => CleanTempFilesAsync(progress)),
                (options.CleanThumbnailCache,     () => CleanThumbnailCacheAsync(progress)),
                (options.FlushDnsCache,           () => FlushDnsCacheAsync(progress)),
                (options.CleanPrefetch,           () => CleanPrefetchAsync(progress)),
                (options.CleanWindowsErrorReports,() => CleanWindowsErrorReportsAsync(progress)),
                (options.CleanCrashDumps,         () => CleanCrashDumpsAsync(progress)),
                (options.CleanWindowsUpdateCache, () => CleanWindowsUpdateCacheAsync(progress)),
                (options.CleanEventLogs,          () => CleanEventLogsAsync(progress)),
                (options.CleanBrowserCache,       () => CleanEdgeCacheAsync(progress)),
            };

            foreach (var (enabled, action) in tasks)
            {
                if (!enabled) continue;
                var taskResult = await action();
                result.TaskResults.Add(taskResult);
            }

            return result;
        }

        // ── Always-on tasks ───────────────────────────────────────────────────

        private Task<OptimizationTaskResult> CleanRecycleBinAsync(IProgress<string> progress)
        {
            return Task.Run(() =>
            {
                progress.Report("Emptying Recycle Bin...");
                long freed = 0;
                try
                {
                    foreach (var drive in DriveInfo.GetDrives())
                    {
                        if (!drive.IsReady) continue;
                        var recyclePath = Path.Combine(drive.RootDirectory.FullName, "$Recycle.Bin");
                        freed += GetDirectorySize(recyclePath);
                    }

                    // Use Shell API to empty recycle bin properly
                    NativeMethods.SHEmptyRecycleBin(IntPtr.Zero, null,
                        NativeMethods.SHERB_NOCONFIRMATION |
                        NativeMethods.SHERB_NOPROGRESSUI |
                        NativeMethods.SHERB_NOSOUND);

                    return new OptimizationTaskResult
                    {
                        TaskName = "Recycle Bin",
                        Icon = "🗑️",
                        Success = true,
                        BytesFreed = freed,
                        StatusMessage = "Emptied successfully"
                    };
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[Optimization] RecycleBin error: {ex.Message}");
                    return new OptimizationTaskResult
                    {
                        TaskName = "Recycle Bin",
                        Icon = "🗑️",
                        Success = false,
                        StatusMessage = "Could not empty Recycle Bin"
                    };
                }
            });
        }

        private Task<OptimizationTaskResult> CleanTempFilesAsync(IProgress<string> progress)
        {
            return Task.Run(() =>
            {
                progress.Report("Cleaning temporary files...");
                var paths = new[]
                {
                    Path.GetTempPath(),
                    @"C:\Windows\Temp"
                };
                var (freed, errors) = DeleteFilesInPaths(paths);
                return new OptimizationTaskResult
                {
                    TaskName = "Temp Files",
                    Icon = "📁",
                    Success = true,
                    BytesFreed = freed,
                    StatusMessage = errors > 0
                        ? $"Cleaned ({errors} files in use, skipped)"
                        : "Cleaned successfully"
                };
            });
        }

        private Task<OptimizationTaskResult> CleanThumbnailCacheAsync(IProgress<string> progress)
        {
            return Task.Run(() =>
            {
                progress.Report("Clearing thumbnail cache...");
                var thumbDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    @"Microsoft\Windows\Explorer");

                long freed = 0;
                int errors = 0;
                try
                {
                    if (Directory.Exists(thumbDir))
                    {
                        foreach (var file in Directory.GetFiles(thumbDir, "thumbcache_*.db"))
                        {
                            try
                            {
                                freed += new FileInfo(file).Length;
                                File.Delete(file);
                            }
                            catch { errors++; }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[Optimization] ThumbnailCache error: {ex.Message}");
                }

                return new OptimizationTaskResult
                {
                    TaskName = "Thumbnail Cache",
                    Icon = "🖼️",
                    Success = true,
                    BytesFreed = freed,
                    StatusMessage = errors > 0 ? $"Cleared ({errors} files in use, skipped)" : "Cleared successfully"
                };
            });
        }

        private Task<OptimizationTaskResult> FlushDnsCacheAsync(IProgress<string> progress)
        {
            return Task.Run(() =>
            {
                progress.Report("Flushing DNS cache...");
                try
                {
                    var psi = new ProcessStartInfo("ipconfig", "/flushdns")
                    {
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        RedirectStandardOutput = true
                    };
                    using var proc = Process.Start(psi);
                    proc?.WaitForExit();

                    return new OptimizationTaskResult
                    {
                        TaskName = "DNS Cache",
                        Icon = "🌐",
                        Success = true,
                        BytesFreed = 0,
                        StatusMessage = "Flushed successfully"
                    };
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[Optimization] DNS flush error: {ex.Message}");
                    return new OptimizationTaskResult
                    {
                        TaskName = "DNS Cache",
                        Icon = "🌐",
                        Success = false,
                        StatusMessage = "Could not flush DNS cache"
                    };
                }
            });
        }

        private Task<OptimizationTaskResult> CleanPrefetchAsync(IProgress<string> progress)
        {
            return Task.Run(() =>
            {
                progress.Report("Cleaning prefetch files...");
                var path = @"C:\Windows\Prefetch";
                var (freed, _) = DeleteFilesInPaths(new[] { path }, "*.pf");
                return new OptimizationTaskResult
                {
                    TaskName = "Prefetch Files",
                    Icon = "⚡",
                    Success = true,
                    BytesFreed = freed,
                    StatusMessage = "Cleaned successfully"
                };
            });
        }

        private Task<OptimizationTaskResult> CleanWindowsErrorReportsAsync(IProgress<string> progress)
        {
            return Task.Run(() =>
            {
                progress.Report("Cleaning Windows error reports...");
                var paths = new[]
                {
                    @"C:\ProgramData\Microsoft\Windows\WER\ReportArchive",
                    @"C:\ProgramData\Microsoft\Windows\WER\ReportQueue",
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                        @"Microsoft\Windows\WER\ReportArchive"),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                        @"Microsoft\Windows\WER\ReportQueue"),
                };
                var (freed, _) = DeleteFilesInPaths(paths);
                return new OptimizationTaskResult
                {
                    TaskName = "Error Reports",
                    Icon = "📋",
                    Success = true,
                    BytesFreed = freed,
                    StatusMessage = "Cleaned successfully"
                };
            });
        }

        private Task<OptimizationTaskResult> CleanCrashDumpsAsync(IProgress<string> progress)
        {
            return Task.Run(() =>
            {
                progress.Report("Removing crash dump files...");
                var paths = new[]
                {
                    @"C:\Windows\Minidump",
                    @"C:\Windows\MEMORY.DMP"
                };
                long freed = 0;
                foreach (var p in paths)
                {
                    if (File.Exists(p))
                    {
                        try { freed += new FileInfo(p).Length; File.Delete(p); } catch { }
                    }
                    else if (Directory.Exists(p))
                    {
                        var (f, _) = DeleteFilesInPaths(new[] { p });
                        freed += f;
                    }
                }
                return new OptimizationTaskResult
                {
                    TaskName = "Crash Dumps",
                    Icon = "💥",
                    Success = true,
                    BytesFreed = freed,
                    StatusMessage = "Cleaned successfully"
                };
            });
        }

        // ── Optional tasks ────────────────────────────────────────────────────

        private Task<OptimizationTaskResult> CleanWindowsUpdateCacheAsync(IProgress<string> progress)
        {
            return Task.Run(() =>
            {
                progress.Report("Cleaning Windows Update cache...");
                var path = @"C:\Windows\SoftwareDistribution\Download";
                var (freed, _) = DeleteFilesInPaths(new[] { path });
                return new OptimizationTaskResult
                {
                    TaskName = "Windows Update Cache",
                    Icon = "🔄",
                    Success = true,
                    BytesFreed = freed,
                    IsOptional = true,
                    StatusMessage = "Cleaned successfully"
                };
            });
        }

        private Task<OptimizationTaskResult> CleanEventLogsAsync(IProgress<string> progress)
        {
            return Task.Run(() =>
            {
                progress.Report("Clearing event logs...");
                long freed = 0;
                int cleared = 0;
                var logs = new[] { "Application", "System", "Setup" };

                foreach (var log in logs)
                {
                    try
                    {
                        var psi = new ProcessStartInfo("wevtutil", $"cl {log}")
                        {
                            UseShellExecute = false,
                            CreateNoWindow = true,
                            Verb = "runas"
                        };
                        using var proc = Process.Start(psi);
                        proc?.WaitForExit();
                        cleared++;
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[Optimization] EventLog {log} error: {ex.Message}");
                    }
                }

                return new OptimizationTaskResult
                {
                    TaskName = "Event Logs",
                    Icon = "📝",
                    Success = cleared > 0,
                    BytesFreed = freed,
                    IsOptional = true,
                    StatusMessage = cleared > 0 ? $"Cleared {cleared} logs" : "Could not clear event logs"
                };
            });
        }

        private Task<OptimizationTaskResult> CleanEdgeCacheAsync(IProgress<string> progress)
        {
            return Task.Run(() =>
            {
                progress.Report("Clearing Microsoft Edge cache...");
                var edgeCachePath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    @"Microsoft\Edge\User Data\Default\Cache\Cache_Data");

                var (freed, _) = DeleteFilesInPaths(new[] { edgeCachePath });
                return new OptimizationTaskResult
                {
                    TaskName = "Edge Cache",
                    Icon = "🌍",
                    Success = true,
                    BytesFreed = freed,
                    IsOptional = true,
                    StatusMessage = "Cleared successfully"
                };
            });
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private (long bytesFreed, int errors) DeleteFilesInPaths(string[] paths, string pattern = "*")
        {
            long freed = 0;
            int errors = 0;

            foreach (var dir in paths)
            {
                if (!Directory.Exists(dir)) continue;
                try
                {
                    foreach (var file in Directory.EnumerateFiles(dir, pattern, SearchOption.AllDirectories))
                    {
                        try
                        {
                            freed += new FileInfo(file).Length;
                            File.Delete(file);
                        }
                        catch { errors++; }
                    }
                }
                catch { errors++; }
            }

            return (freed, errors);
        }

        private long GetDirectorySize(string path)
        {
            long size = 0;
            if (!Directory.Exists(path)) return 0;
            try
            {
                foreach (var file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
                {
                    try { size += new FileInfo(file).Length; } catch { }
                }
            }
            catch { }
            return size;
        }
    }

    // P/Invoke for SHEmptyRecycleBin
    internal static class NativeMethods
    {
        public const uint SHERB_NOCONFIRMATION = 0x00000001;
        public const uint SHERB_NOPROGRESSUI = 0x00000002;
        public const uint SHERB_NOSOUND = 0x00000004;

        [System.Runtime.InteropServices.DllImport("Shell32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
        public static extern uint SHEmptyRecycleBin(IntPtr hwnd, string? pszRootPath, uint dwFlags);
    }
}