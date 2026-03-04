using Microsoft.UI.Dispatching;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows.Input;
using Winspeqt.Helpers;
using Winspeqt.Models;
using Winspeqt.Services;

namespace Winspeqt.ViewModels.Optimization
{
    public class OptimizationViewModel : ObservableObject
    {
        private readonly OptimizationService _service;
        private readonly DispatcherQueue _dispatcherQueue;
        private Dictionary<string, long> _scannedSizes = new();

        // ── Scan state ────────────────────────────────────────────────────────

        private bool _isScanning = true;
        public bool IsScanning
        {
            get => _isScanning;
            set => SetProperty(ref _isScanning, value);
        }

        private string _estimatedTotal = "Calculating...";
        public string EstimatedTotal
        {
            get => _estimatedTotal;
            set => SetProperty(ref _estimatedTotal, value);
        }

        private string _sizeRecycleBin = "...";
        public string SizeRecycleBin { get => _sizeRecycleBin; set => SetProperty(ref _sizeRecycleBin, value); }

        private string _sizeTempFiles = "...";
        public string SizeTempFiles { get => _sizeTempFiles; set => SetProperty(ref _sizeTempFiles, value); }

        private string _sizeThumbnailCache = "...";
        public string SizeThumbnailCache { get => _sizeThumbnailCache; set => SetProperty(ref _sizeThumbnailCache, value); }

        private string _sizePrefetch = "...";
        public string SizePrefetch { get => _sizePrefetch; set => SetProperty(ref _sizePrefetch, value); }

        private string _sizeErrorReports = "...";
        public string SizeErrorReports { get => _sizeErrorReports; set => SetProperty(ref _sizeErrorReports, value); }

        private string _sizeCrashDumps = "...";
        public string SizeCrashDumps { get => _sizeCrashDumps; set => SetProperty(ref _sizeCrashDumps, value); }

        private string _sizeUpdateCache = "...";
        public string SizeUpdateCache { get => _sizeUpdateCache; set => SetProperty(ref _sizeUpdateCache, value); }

        private string _sizeEdgeCache = "...";
        public string SizeEdgeCache { get => _sizeEdgeCache; set => SetProperty(ref _sizeEdgeCache, value); }

        // ── Run state ─────────────────────────────────────────────────────────

        private bool _isRunning;
        public bool IsRunning
        {
            get => _isRunning;
            set => SetProperty(ref _isRunning, value);
        }

        private bool _hasCompleted;
        public bool HasCompleted
        {
            get => _hasCompleted;
            set => SetProperty(ref _hasCompleted, value);
        }

        private string _currentTaskLabel = "";
        public string CurrentTaskLabel
        {
            get => _currentTaskLabel;
            set => SetProperty(ref _currentTaskLabel, value);
        }

        private OptimizationResult? _result;
        public OptimizationResult? Result
        {
            get => _result;
            set => SetProperty(ref _result, value);
        }

        // ── Options ───────────────────────────────────────────────────────────

        public OptimizationOptions Options { get; } = new();

        private bool _cleanRecycleBin = true;
        public bool CleanRecycleBin
        {
            get => _cleanRecycleBin;
            set { SetProperty(ref _cleanRecycleBin, value); Options.CleanRecycleBin = value; RecalculateEstimate(); }
        }

        private bool _cleanTempFiles = true;
        public bool CleanTempFiles
        {
            get => _cleanTempFiles;
            set { SetProperty(ref _cleanTempFiles, value); Options.CleanTempFiles = value; RecalculateEstimate(); }
        }

        private bool _cleanThumbnailCache = true;
        public bool CleanThumbnailCache
        {
            get => _cleanThumbnailCache;
            set { SetProperty(ref _cleanThumbnailCache, value); Options.CleanThumbnailCache = value; RecalculateEstimate(); }
        }

        private bool _flushDnsCache = true;
        public bool FlushDnsCache
        {
            get => _flushDnsCache;
            set { SetProperty(ref _flushDnsCache, value); Options.FlushDnsCache = value; RecalculateEstimate(); }
        }

        private bool _cleanPrefetch = true;
        public bool CleanPrefetch
        {
            get => _cleanPrefetch;
            set { SetProperty(ref _cleanPrefetch, value); Options.CleanPrefetch = value; RecalculateEstimate(); }
        }

        private bool _cleanWindowsErrorReports = true;
        public bool CleanWindowsErrorReports
        {
            get => _cleanWindowsErrorReports;
            set { SetProperty(ref _cleanWindowsErrorReports, value); Options.CleanWindowsErrorReports = value; RecalculateEstimate(); }
        }

        private bool _cleanCrashDumps = true;
        public bool CleanCrashDumps
        {
            get => _cleanCrashDumps;
            set { SetProperty(ref _cleanCrashDumps, value); Options.CleanCrashDumps = value; RecalculateEstimate(); }
        }

        private bool _cleanUpdateCache = true;
        public bool CleanUpdateCache
        {
            get => _cleanUpdateCache;
            set { SetProperty(ref _cleanUpdateCache, value); Options.CleanWindowsUpdateCache = value; RecalculateEstimate(); }
        }

        private bool _cleanEventLogs = true;
        public bool CleanEventLogs
        {
            get => _cleanEventLogs;
            set { SetProperty(ref _cleanEventLogs, value); Options.CleanEventLogs = value; RecalculateEstimate(); }
        }

        private bool _cleanBrowserCache = true;
        public bool CleanBrowserCache
        {
            get => _cleanBrowserCache;
            set { SetProperty(ref _cleanBrowserCache, value); Options.CleanBrowserCache = value; RecalculateEstimate(); }
        }

        // ── Command ───────────────────────────────────────────────────────────

        public ICommand RunOptimizationCommand { get; }

        public OptimizationViewModel()
        {
            _service = new OptimizationService();
            _dispatcherQueue = DispatcherQueue.GetForCurrentThread();
            RunOptimizationCommand = new RelayCommand(async () => await RunOptimizationAsync(), () => !IsRunning);
            _ = ScanSizesAsync();
        }

        // ── Scan ──────────────────────────────────────────────────────────────

        private async Task ScanSizesAsync()
        {
            _dispatcherQueue.TryEnqueue(() =>
            {
                IsScanning = true;
                EstimatedTotal = "Calculating...";
                SizeRecycleBin = "...";
                SizeTempFiles = "...";
                SizeThumbnailCache = "...";
                SizePrefetch = "...";
                SizeErrorReports = "...";
                SizeCrashDumps = "...";
                SizeUpdateCache = "...";
                SizeEdgeCache = "...";
            });

            _scannedSizes = await _service.ScanSizesAsync();

            _dispatcherQueue.TryEnqueue(() =>
            {
                SizeRecycleBin = FormatBytes(_scannedSizes.GetValueOrDefault("RecycleBin"));
                SizeTempFiles = FormatBytes(_scannedSizes.GetValueOrDefault("TempFiles"));
                SizeThumbnailCache = FormatBytes(_scannedSizes.GetValueOrDefault("ThumbnailCache"));
                SizePrefetch = FormatBytes(_scannedSizes.GetValueOrDefault("Prefetch"));
                SizeErrorReports = FormatBytes(_scannedSizes.GetValueOrDefault("ErrorReports"));
                SizeCrashDumps = FormatBytes(_scannedSizes.GetValueOrDefault("CrashDumps"));
                SizeUpdateCache = FormatBytes(_scannedSizes.GetValueOrDefault("UpdateCache"));
                SizeEdgeCache = FormatBytes(_scannedSizes.GetValueOrDefault("EdgeCache"));
                IsScanning = false;
                RecalculateEstimate();
            });
        }

        private void RecalculateEstimate()
        {
            if (_scannedSizes.Count == 0) return;

            long total = 0;
            if (Options.CleanRecycleBin) total += _scannedSizes.GetValueOrDefault("RecycleBin");
            if (Options.CleanTempFiles) total += _scannedSizes.GetValueOrDefault("TempFiles");
            if (Options.CleanThumbnailCache) total += _scannedSizes.GetValueOrDefault("ThumbnailCache");
            if (Options.CleanPrefetch) total += _scannedSizes.GetValueOrDefault("Prefetch");
            if (Options.CleanWindowsErrorReports) total += _scannedSizes.GetValueOrDefault("ErrorReports");
            if (Options.CleanCrashDumps) total += _scannedSizes.GetValueOrDefault("CrashDumps");
            if (Options.CleanWindowsUpdateCache) total += _scannedSizes.GetValueOrDefault("UpdateCache");
            if (Options.CleanBrowserCache) total += _scannedSizes.GetValueOrDefault("EdgeCache");

            EstimatedTotal = FormatBytes(total);
        }

        // ── Run ───────────────────────────────────────────────────────────────

        private async Task RunOptimizationAsync()
        {
            IsRunning = true;
            HasCompleted = false;
            Result = null;
            CurrentTaskLabel = "Starting optimization...";

            var progress = new Progress<string>(msg =>
                _dispatcherQueue.TryEnqueue(() => CurrentTaskLabel = msg));

            try
            {
                var result = await _service.RunOptimizationAsync(Options, progress);

                _dispatcherQueue.TryEnqueue(() =>
                {
                    Result = result;
                    IsRunning = false;
                    HasCompleted = true;
                    CurrentTaskLabel = "Optimization complete!";

                    try
                    {
                        var settings = Windows.Storage.ApplicationData.Current.LocalSettings.Values;
                        settings["Optimization_LastRunTime"] = DateTime.Now.Ticks;
                        settings["Optimization_LastBytesFreed"] = result.TotalBytesFreed;
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[OptimizationViewModel] LocalSettings error: {ex.Message}");
                    }
                });

                // Rescan after run so estimates are fresh
                await ScanSizesAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[OptimizationViewModel] Error: {ex.Message}");
                _dispatcherQueue.TryEnqueue(() =>
                {
                    IsRunning = false;
                    CurrentTaskLabel = "An error occurred during optimization.";
                });
            }
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private string FormatBytes(long bytes)
        {
            if (bytes >= 1_073_741_824) return $"{bytes / 1_073_741_824.0:F1} GB";
            if (bytes >= 1_048_576) return $"{bytes / 1_048_576.0:F1} MB";
            if (bytes >= 1_024) return $"{bytes / 1_024.0:F1} KB";
            if (bytes > 0) return $"{bytes} B";
            return "0 KB";
        }
    }
}