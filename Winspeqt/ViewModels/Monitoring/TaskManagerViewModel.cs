using Microsoft.UI.Dispatching;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows.Input;
using Winspeqt.Helpers;
using Winspeqt.Models;
using Winspeqt.Services;
using System.Linq;

namespace Winspeqt.ViewModels.Monitoring
{
    public class TaskManagerViewModel : ObservableObject
    {
        private readonly SystemMonitorService _monitorService;
        private readonly DispatcherQueue _dispatcherQueue;
        private System.Threading.Timer _refreshTimer;

        private bool _isLoading;
        public bool IsLoading
        {
            get => _isLoading;
            set => SetProperty(ref _isLoading, value);
        }

        private double _totalCpuUsage;
        public double TotalCpuUsage
        {
            get => _totalCpuUsage;
            set => SetProperty(ref _totalCpuUsage, value);
        }

        private long _usedMemoryMB;
        public long UsedMemoryMB
        {
            get => _usedMemoryMB;
            set => SetProperty(ref _usedMemoryMB, value);
        }

        private long _totalMemoryMB;
        public long TotalMemoryMB
        {
            get => _totalMemoryMB;
            set => SetProperty(ref _totalMemoryMB, value);
        }

        private string _cpuStatusMessage;
        public string CpuStatusMessage
        {
            get => _cpuStatusMessage;
            set => SetProperty(ref _cpuStatusMessage, value);
        }

        private string _memoryStatusMessage;
        public string MemoryStatusMessage
        {
            get => _memoryStatusMessage;
            set => SetProperty(ref _memoryStatusMessage, value);
        }

        public ObservableCollection<ProcessInfo> TopProcesses { get; set; }

        public ICommand RefreshCommand { get; }
        public ICommand EndProcessCommand { get; }

        public TaskManagerViewModel()
        {
            _monitorService = new SystemMonitorService();
            _dispatcherQueue = DispatcherQueue.GetForCurrentThread();
            TopProcesses = new ObservableCollection<ProcessInfo>();

            RefreshCommand = new RelayCommand(async () => await RefreshDataAsync());
            EndProcessCommand = new RelayCommand<ProcessInfo>(async (process) => await EndProcessAsync(process));

            // Set loading to true initially
            IsLoading = true;

            // Start auto-refresh every 2 seconds
            StartAutoRefresh();

            // Initial load
            _ = RefreshDataAsync();
        }

        private void StartAutoRefresh()
        {
            _refreshTimer = new System.Threading.Timer(
                async _ => await RefreshDataAsync(),
                null,
                TimeSpan.Zero,
                TimeSpan.FromSeconds(2)
            );
        }

        private async Task RefreshDataAsync()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("Starting refresh...");

                // Ensure loading shows for at least 500ms so users see it
                var loadingTask = Task.Delay(500);

                // Get system metrics with individual error handling
                double cpu = 0;
                long availableMem = 0;
                long totalMem = 8192; // Default fallback
                List<ProcessInfo> processes = new List<ProcessInfo>();

                try
                {
                    System.Diagnostics.Debug.WriteLine("Getting CPU...");
                    cpu = await _monitorService.GetTotalCpuUsageAsync();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"CPU error: {ex.Message}");
                }

                try
                {
                    System.Diagnostics.Debug.WriteLine("Getting memory...");
                    availableMem = await _monitorService.GetAvailableMemoryMBAsync();
                    totalMem = await _monitorService.GetTotalMemoryMBAsync();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Memory error: {ex.Message}");
                }

                try
                {
                    System.Diagnostics.Debug.WriteLine("Getting processes...");
                    processes = await _monitorService.GetRunningProcessesAsync();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Process error: {ex.Message}");
                }

                var usedMem = totalMem - availableMem;

                // Wait for minimum loading time
                await loadingTask;

                // Update on UI thread
                bool updateSuccessful = _dispatcherQueue.TryEnqueue(() =>
                {
                    try
                    {
                        System.Diagnostics.Debug.WriteLine("Updating UI...");
                        TotalCpuUsage = cpu;
                        TotalMemoryMB = totalMem;
                        UsedMemoryMB = usedMem;

                        // Update status messages
                        UpdateStatusMessages();

                        // Get top 5 by memory
                        var topProcesses = processes.OrderByDescending(p => p.MemoryUsageMB).Take(5).ToList();
                        TopProcesses.Clear();
                        foreach (var proc in topProcesses)
                        {
                            TopProcesses.Add(proc);
                        }

                        IsLoading = false;
                        System.Diagnostics.Debug.WriteLine("Refresh complete!");
                    }
                    catch (Exception uiEx)
                    {
                        System.Diagnostics.Debug.WriteLine($"UI update error: {uiEx.Message}");
                        IsLoading = false;
                    }
                });

                if (!updateSuccessful)
                {
                    System.Diagnostics.Debug.WriteLine("Failed to enqueue UI update");
                    IsLoading = false;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ERROR in RefreshDataAsync: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");

                // Try to update error message on UI thread
                try
                {
                    _dispatcherQueue.TryEnqueue(() =>
                    {
                        IsLoading = false;
                        // Don't overwrite good data with error message if we already have it
                        if (string.IsNullOrEmpty(CpuStatusMessage))
                        {
                            CpuStatusMessage = $"Error loading data: {ex.Message}";
                        }
                    });
                }
                catch
                {
                    // Failed to even show error - just silently fail this update cycle
                }
            }
        }

        private void UpdateStatusMessages()
        {
            try
            {
                // CPU status message
                if (TotalCpuUsage > 80)
                    CpuStatusMessage = "⚠️ Your CPU is working very hard right now. Close some apps to speed things up.";
                else if (TotalCpuUsage > 50)
                    CpuStatusMessage = "Your CPU is moderately busy. Everything should still run smoothly.";
                else
                    CpuStatusMessage = "✓ Your CPU usage is normal. Your PC is running well.";

                // Memory status message
                if (TotalMemoryMB > 0)
                {
                    var memoryPercent = (double)UsedMemoryMB / TotalMemoryMB * 100;
                    if (memoryPercent > 90)
                        MemoryStatusMessage = $"⚠️ You're using {memoryPercent:F0}% of your memory. Close some apps to free up space.";
                    else if (memoryPercent > 70)
                        MemoryStatusMessage = $"You're using {memoryPercent:F0}% of your memory. Still some room left.";
                    else
                        MemoryStatusMessage = $"✓ You're using {memoryPercent:F0}% of your memory. Plenty of space available.";
                }
                else
                {
                    MemoryStatusMessage = "Memory information loading...";
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error updating status messages: {ex.Message}");
                CpuStatusMessage = "System information loaded";
                MemoryStatusMessage = "System information loaded";
            }
        }

        private async Task EndProcessAsync(ProcessInfo processInfo)
        {
            if (processInfo == null) return;

            try
            {
                var process = System.Diagnostics.Process.GetProcessById(processInfo.ProcessId);
                process.Kill();
                await RefreshDataAsync();
            }
            catch
            {
                // Process might already be closed or we don't have permission
            }
        }

        public void StopAutoRefresh()
        {
            _refreshTimer?.Dispose();
        }
    }
}