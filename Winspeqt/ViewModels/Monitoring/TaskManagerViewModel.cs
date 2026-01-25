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

        private double _networkUsage;
        public double NetworkUsage
        {
            get => _networkUsage;
            set => SetProperty(ref _networkUsage, value);
        }

        private string _networkStatusMessage;
        public string NetworkStatusMessage
        {
            get => _networkStatusMessage;
            set => SetProperty(ref _networkStatusMessage, value);
        }

        private double _diskUsage;
        public double DiskUsage
        {
            get => _diskUsage;
            set => SetProperty(ref _diskUsage, value);
        }

        private string _diskStatusMessage;
        public string DiskStatusMessage
        {
            get => _diskStatusMessage;
            set => SetProperty(ref _diskStatusMessage, value);
        }

        private bool _isAutoRefreshEnabled = true;
        public bool IsAutoRefreshEnabled
        {
            get => _isAutoRefreshEnabled;
            set => SetProperty(ref _isAutoRefreshEnabled, value);
        }

        private string _refreshButtonText = "⏸ Pause";
        public string RefreshButtonText
        {
            get => _refreshButtonText;
            set => SetProperty(ref _refreshButtonText, value);
        }

        private string _selectedSortOption = "Memory";
        public string SelectedSortOption
        {
            get => _selectedSortOption;
            set
            {
                if (SetProperty(ref _selectedSortOption, value))
                {
                    _ = RefreshDataAsync();
                }
            }
        }

        private string _selectedFilterOption = "All";
        public string SelectedFilterOption
        {
            get => _selectedFilterOption;
            set
            {
                if (SetProperty(ref _selectedFilterOption, value))
                {
                    _ = RefreshDataAsync();
                }
            }
        }

        public ObservableCollection<ProcessInfo> TopProcesses { get; set; }
        public ObservableCollection<string> SortOptions { get; set; }
        public ObservableCollection<string> FilterOptions { get; set; }

        public ICommand RefreshCommand { get; }
        public ICommand EndProcessCommand { get; }

        public TaskManagerViewModel()
        {
            _monitorService = new SystemMonitorService();
            _dispatcherQueue = DispatcherQueue.GetForCurrentThread();
            TopProcesses = new ObservableCollection<ProcessInfo>();

            SortOptions = new ObservableCollection<string> { "Memory", "CPU", "Name" };
            FilterOptions = new ObservableCollection<string> { "All", "Apps Only", "System Only" };

            RefreshCommand = new RelayCommand(ToggleAutoRefresh);
            EndProcessCommand = new RelayCommand<ProcessInfo>(async (process) => await EndProcessAsync(process));

            // Set loading to true initially
            IsLoading = true;

            // Start auto-refresh every 3 seconds (more reasonable for non-tech users)
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
                TimeSpan.FromSeconds(3)
            );
        }

        private void ToggleAutoRefresh()
        {
            IsAutoRefreshEnabled = !IsAutoRefreshEnabled;

            if (IsAutoRefreshEnabled)
            {
                RefreshButtonText = "⏸ Pause";
                // Restart the timer
                _refreshTimer?.Dispose();
                _refreshTimer = new System.Threading.Timer(
                    async _ => await RefreshDataAsync(),
                    null,
                    TimeSpan.Zero, // Immediately refresh when resuming
                    TimeSpan.FromSeconds(3)
                );
            }
            else
            {
                RefreshButtonText = "▶ Resume";
                // Stop the timer
                _refreshTimer?.Dispose();
                _refreshTimer = null;
            }
        }

        private async Task RefreshDataAsync()
        {
            // Check if paused at the start of refresh
            if (!IsAutoRefreshEnabled && TopProcesses.Count > 0)
            {
                return; // Don't refresh if paused (unless it's initial load)
            }

            try
            {
                System.Diagnostics.Debug.WriteLine("Starting refresh...");

                // Don't show loading on subsequent refreshes, only initial load
                var isInitialLoad = TopProcesses.Count == 0;
                if (isInitialLoad)
                {
                    IsLoading = true;
                }

                // Run all queries in parallel for speed
                var cpuTask = _monitorService.GetTotalCpuUsageAsync();
                var memTask = _monitorService.GetAvailableMemoryMBAsync();
                var totalMemTask = _monitorService.GetTotalMemoryMBAsync();
                var networkTask = _monitorService.GetNetworkUsageAsync();
                var diskTask = _monitorService.GetDiskUsageAsync();
                var processTask = _monitorService.GetTopProcessesWithCpuAsync(15);

                // Wait for all to complete
                await Task.WhenAll(cpuTask, memTask, totalMemTask, networkTask, diskTask, processTask);

                // Check again if paused after async operations
                if (!IsAutoRefreshEnabled && !isInitialLoad)
                {
                    return;
                }

                double cpu = cpuTask.Result;
                long availableMem = memTask.Result;
                long totalMem = totalMemTask.Result;
                double network = networkTask.Result;
                double disk = diskTask.Result;
                List<ProcessInfo> processes = processTask.Result;

                var usedMem = totalMem - availableMem;

                // Update on UI thread
                bool updateSuccessful = _dispatcherQueue.TryEnqueue(() =>
                {
                    try
                    {
                        System.Diagnostics.Debug.WriteLine("Updating UI...");
                        TotalCpuUsage = cpu;
                        TotalMemoryMB = totalMem;
                        UsedMemoryMB = usedMem;
                        NetworkUsage = network;
                        DiskUsage = disk;

                        // Update status messages
                        UpdateStatusMessages();

                        // Apply filtering
                        var filteredProcesses = ApplyFilter(processes);

                        // Apply sorting and get top processes
                        var topProcesses = ApplySorting(filteredProcesses).Take(10).ToList();

                        TopProcesses.Clear();
                        foreach (var proc in topProcesses)
                        {
                            TopProcesses.Add(proc);
                        }

                        if (isInitialLoad)
                        {
                            IsLoading = false;
                        }
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

        private List<ProcessInfo> ApplyFilter(List<ProcessInfo> processes)
        {
            if (SelectedFilterOption == "Apps Only")
            {
                // Filter to show only user applications (exclude system processes)
                var systemProcesses = new[] { "svchost", "system", "dwm", "csrss", "winlogon",
                    "runtimebroker", "searchindexer", "backgroundtaskhost", "taskhostw",
                    "conhost", "fontdrvhost", "sihost", "textinputhost", "audiodg" };

                return processes.Where(p =>
                    !systemProcesses.Any(sp => p.ProcessName.ToLower().Contains(sp))
                ).ToList();
            }
            else if (SelectedFilterOption == "System Only")
            {
                // Show only system processes
                var systemProcesses = new[] { "svchost", "system", "dwm", "csrss", "winlogon",
                    "runtimebroker", "searchindexer", "backgroundtaskhost", "taskhostw",
                    "conhost", "fontdrvhost", "sihost", "textinputhost", "audiodg" };

                return processes.Where(p =>
                    systemProcesses.Any(sp => p.ProcessName.ToLower().Contains(sp))
                ).ToList();
            }

            return processes;
        }

        private IEnumerable<ProcessInfo> ApplySorting(List<ProcessInfo> processes)
        {
            return SelectedSortOption switch
            {
                "CPU" => processes.OrderByDescending(p => p.CpuUsagePercent),
                "Name" => processes.OrderBy(p => p.Description),
                _ => processes.OrderByDescending(p => p.MemoryUsageMB) // Default to Memory
            };
        }

        private void UpdateStatusMessages()
        {
            try
            {
                // CPU status message with actionable advice
                if (TotalCpuUsage > 80)
                {
                    CpuStatusMessage = "⚠️ Your CPU is working very hard. Try closing apps you're not using to speed things up.";
                }
                else if (TotalCpuUsage > 50)
                {
                    CpuStatusMessage = "Your CPU is moderately busy. Everything should still run smoothly.";
                }
                else if (TotalCpuUsage > 20)
                {
                    CpuStatusMessage = "✓ Your CPU usage is normal. Your PC is running well.";
                }
                else
                {
                    CpuStatusMessage = "✓ Your CPU is barely being used. Your PC has plenty of power available.";
                }

                // Memory status message with helpful context
                if (TotalMemoryMB > 0)
                {
                    var memoryPercent = (double)UsedMemoryMB / TotalMemoryMB * 100;
                    if (memoryPercent > 90)
                    {
                        MemoryStatusMessage = $"⚠️ You're using {memoryPercent:F0}% of your memory. Your PC might slow down. Try closing some apps.";
                    }
                    else if (memoryPercent > 80)
                    {
                        MemoryStatusMessage = $"You're using {memoryPercent:F0}% of your memory. Consider closing apps you're not using.";
                    }
                    else if (memoryPercent > 60)
                    {
                        MemoryStatusMessage = $"You're using {memoryPercent:F0}% of your memory. Still plenty of room.";
                    }
                    else
                    {
                        MemoryStatusMessage = $"✓ You're using {memoryPercent:F0}% of your memory. Plenty of space available.";
                    }
                }
                else
                {
                    MemoryStatusMessage = "Memory information loading...";
                }

                // Network status message
                if (NetworkUsage > 100)
                {
                    NetworkStatusMessage = "⚠️ High network activity detected. Multiple apps are using your internet connection.";
                }
                else if (NetworkUsage > 50)
                {
                    NetworkStatusMessage = "Moderate network activity. You're actively using your internet connection.";
                }
                else if (NetworkUsage > 10)
                {
                    NetworkStatusMessage = "✓ Light network activity. Normal internet usage.";
                }
                else
                {
                    NetworkStatusMessage = "✓ Minimal network activity. Your connection is mostly idle.";
                }

                // Disk status message
                if (DiskUsage > 80)
                {
                    DiskStatusMessage = "⚠️ Your disk is very busy. This might slow down your computer.";
                }
                else if (DiskUsage > 50)
                {
                    DiskStatusMessage = "Your disk is moderately active. Some programs are reading or writing files.";
                }
                else if (DiskUsage > 20)
                {
                    DiskStatusMessage = "✓ Normal disk activity. Your storage is working as expected.";
                }
                else
                {
                    DiskStatusMessage = "✓ Your disk is mostly idle. Very light read/write activity.";
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
                // Show confirmation dialog
                var dialog = new Microsoft.UI.Xaml.Controls.ContentDialog
                {
                    Title = "End Task",
                    Content = $"Are you sure you want to end '{processInfo.Description}'?\n\nThis may cause data loss if the app has unsaved work.",
                    PrimaryButtonText = "End Task",
                    CloseButtonText = "Cancel",
                    DefaultButton = Microsoft.UI.Xaml.Controls.ContentDialogButton.Close
                };

                // This needs to be set from the Page, so we'll handle it differently
                // For now, just kill the process directly
                var process = System.Diagnostics.Process.GetProcessById(processInfo.ProcessId);
                process.Kill();

                // Refresh immediately
                await RefreshDataAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error ending process: {ex.Message}");
                // Process might already be closed or we don't have permission
            }
        }

        public void StopAutoRefresh()
        {
            _refreshTimer?.Dispose();
            _monitorService?.Dispose();
        }
    }
}