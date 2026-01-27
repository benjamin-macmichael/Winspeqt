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
using System.Threading;

namespace Winspeqt.ViewModels.Monitoring
{
    public class TaskManagerViewModel : ObservableObject
    {
        private readonly SystemMonitorService _monitorService;
        private readonly DispatcherQueue _dispatcherQueue;
        private System.Threading.Timer _refreshTimer;
        private Microsoft.UI.Xaml.XamlRoot _xamlRoot;
        private bool _isRefreshing;
        private readonly SemaphoreSlim _refreshLock = new SemaphoreSlim(1, 1);

        public void SetXamlRoot(Microsoft.UI.Xaml.XamlRoot xamlRoot)
        {
            _xamlRoot = xamlRoot;
        }

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
        public ICommand RestartProcessCommand { get; }

        public TaskManagerViewModel()
        {
            _monitorService = new SystemMonitorService();
            _dispatcherQueue = DispatcherQueue.GetForCurrentThread();
            TopProcesses = new ObservableCollection<ProcessInfo>();

            SortOptions = new ObservableCollection<string> { "Memory", "CPU", "Name" };
            FilterOptions = new ObservableCollection<string> { "All", "Apps Only", "System Only" };

            RefreshCommand = new RelayCommand(ToggleAutoRefresh);
            EndProcessCommand = new RelayCommand<ProcessInfo>(async (process) => await EndProcessAsync(process));
            RestartProcessCommand = new RelayCommand<ProcessInfo>(async (process) => await RestartProcessAsync(process));

            IsLoading = true;
            StartAutoRefresh();
            _ = RefreshDataAsync();
        }

        private void StartAutoRefresh()
        {
            _refreshTimer = new System.Threading.Timer(
                _ => _ = RefreshDataAsync(),
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
                StartAutoRefresh();
            }
            else
            {
                RefreshButtonText = "▶ Resume";
                _refreshTimer?.Dispose();
                _refreshTimer = null;
            }
        }

        private async Task RefreshDataAsync()
        {
            // Use semaphore to prevent concurrent refreshes
            if (!await _refreshLock.WaitAsync(0))
            {
                System.Diagnostics.Debug.WriteLine("Refresh already in progress, skipping...");
                return;
            }

            try
            {
                // Check if paused (but allow initial load)
                if (!IsAutoRefreshEnabled && TopProcesses.Count > 0)
                {
                    return;
                }

                System.Diagnostics.Debug.WriteLine("Starting refresh...");

                var isInitialLoad = TopProcesses.Count == 0;
                if (isInitialLoad)
                {
                    await DispatchAsync(() => IsLoading = true);
                }

                // Run all queries in parallel
                var cpuTask = _monitorService.GetTotalCpuUsageAsync();
                var memTask = _monitorService.GetAvailableMemoryMBAsync();
                var totalMemTask = _monitorService.GetTotalMemoryMBAsync();
                var networkTask = _monitorService.GetNetworkUsageAsync();
                var diskTask = _monitorService.GetDiskUsageAsync();
                var processTask = _monitorService.GetTopProcessesWithCpuAsync(15);

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

                // Apply filtering and sorting
                var filteredProcesses = ApplyFilter(processes);
                var topProcesses = ApplySorting(filteredProcesses).Take(10).ToList();

                // Update UI on dispatcher queue
                await DispatchAsync(() =>
                {
                    System.Diagnostics.Debug.WriteLine("Updating UI...");

                    TotalCpuUsage = cpu;
                    TotalMemoryMB = totalMem;
                    UsedMemoryMB = usedMem;
                    NetworkUsage = network;
                    DiskUsage = disk;

                    UpdateStatusMessages();

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
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ERROR in RefreshDataAsync: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");

                await DispatchAsync(() =>
                {
                    IsLoading = false;
                    if (string.IsNullOrEmpty(CpuStatusMessage))
                    {
                        CpuStatusMessage = $"Error loading data: {ex.Message}";
                    }
                });
            }
            finally
            {
                _refreshLock.Release();
            }
        }

        private async Task DispatchAsync(Action action)
        {
            var tcs = new TaskCompletionSource<bool>();

            bool enqueued = _dispatcherQueue.TryEnqueue(() =>
            {
                try
                {
                    action();
                    tcs.SetResult(true);
                }
                catch (Exception ex)
                {
                    tcs.SetException(ex);
                }
            });

            if (!enqueued)
            {
                System.Diagnostics.Debug.WriteLine("Failed to enqueue UI update");
                tcs.SetResult(false);
            }

            await tcs.Task;
        }

        private List<ProcessInfo> ApplyFilter(List<ProcessInfo> processes)
        {
            if (SelectedFilterOption == "Apps Only")
            {
                var systemProcesses = new[] { "svchost", "system", "dwm", "csrss", "winlogon",
                    "runtimebroker", "searchindexer", "backgroundtaskhost", "taskhostw",
                    "conhost", "fontdrvhost", "sihost", "textinputhost", "audiodg" };

                return processes.Where(p =>
                    !systemProcesses.Any(sp => p.ProcessName.ToLower().Contains(sp))
                ).ToList();
            }
            else if (SelectedFilterOption == "System Only")
            {
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
                _ => processes.OrderByDescending(p => p.MemoryUsageMB)
            };
        }

        private void UpdateStatusMessages()
        {
            try
            {
                // CPU status message
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

                // Memory status message
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
                var process = System.Diagnostics.Process.GetProcessById(processInfo.ProcessId);

                if (IsCriticalProcess(process, processInfo))
                {
                    await ShowCriticalProcessWarning(processInfo.Description);
                    return;
                }

                if (processInfo.ProcessName.ToLower().Contains("svchost"))
                {
                    bool shouldContinue = await ShowSvchostWarning(processInfo.Description);
                    if (!shouldContinue) return;
                }
                else
                {
                    bool shouldContinue = await ShowEndTaskConfirmation(processInfo.Description);
                    if (!shouldContinue) return;
                }

                process.Kill();
                await Task.Delay(500);
                await RefreshDataAsync();
            }
            catch (ArgumentException)
            {
                System.Diagnostics.Debug.WriteLine($"Process {processInfo.ProcessId} no longer exists");
                await RefreshDataAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error ending process: {ex.Message}");
            }
        }

        private async Task RestartProcessAsync(ProcessInfo processInfo)
        {
            if (processInfo == null) return;

            try
            {
                var process = System.Diagnostics.Process.GetProcessById(processInfo.ProcessId);

                if (IsCriticalProcess(process, processInfo))
                {
                    await ShowCriticalProcessWarning(processInfo.Description);
                    return;
                }

                if (processInfo.ProcessName.ToLower().Contains("svchost"))
                {
                    bool shouldContinue = await ShowSvchostWarning(processInfo.Description);
                    if (!shouldContinue) return;
                }
                else
                {
                    bool shouldContinue = await ShowRestartConfirmation(processInfo.Description);
                    if (!shouldContinue) return;
                }

                string executablePath = null;

                try
                {
                    executablePath = process.MainModule?.FileName;
                }
                catch
                {
                    System.Diagnostics.Debug.WriteLine($"Cannot restart {processInfo.Description} - unable to access executable path");
                    return;
                }

                if (string.IsNullOrEmpty(executablePath))
                {
                    System.Diagnostics.Debug.WriteLine($"Cannot restart {processInfo.Description} - no executable path found");
                    return;
                }

                process.Kill();
                await Task.Delay(500);

                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = executablePath,
                    UseShellExecute = true
                });

                await Task.Delay(1000);
                await RefreshDataAsync();
            }
            catch (ArgumentException)
            {
                System.Diagnostics.Debug.WriteLine($"Process {processInfo.ProcessId} no longer exists");
                await RefreshDataAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error restarting process: {ex.Message}");
            }
        }

        private bool IsCriticalProcess(System.Diagnostics.Process process, ProcessInfo processInfo)
        {
            try
            {
                var criticalProcessNames = new[]
                {
                    "system", "idle", "registry", "memory compression",
                    "csrss", "winlogon", "wininit", "smss", "services",
                    "lsass", "fontdrvhost", "dwm", "ntoskrnl"
                };

                string processNameLower = processInfo.ProcessName.ToLower();
                if (criticalProcessNames.Any(p => processNameLower == p || processNameLower == p + ".exe"))
                {
                    return true;
                }

                if (process.Id == 0 || process.Id == 4)
                {
                    return true;
                }

                try
                {
                    string executablePath = process.MainModule?.FileName?.ToLower();
                    if (!string.IsNullOrEmpty(executablePath))
                    {
                        if ((executablePath.Contains(@"\system32\") || executablePath.Contains(@"\syswow64\")) &&
                            (executablePath.Contains("csrss") || executablePath.Contains("winlogon") ||
                             executablePath.Contains("smss") || executablePath.Contains("wininit") ||
                             executablePath.Contains("lsass") || executablePath.Contains("services")))
                        {
                            return true;
                        }
                    }
                }
                catch
                {
                    if (processNameLower.Contains("system") || processNameLower.Contains("csrss") ||
                        processNameLower.Contains("winlogon") || processNameLower.Contains("lsass"))
                    {
                        return true;
                    }
                }

                return false;
            }
            catch
            {
                return true;
            }
        }

        private async Task ShowCriticalProcessWarning(string processName)
        {
            if (_xamlRoot == null) return;

            await DispatchAsync(async () =>
            {
                var dialog = new Microsoft.UI.Xaml.Controls.ContentDialog
                {
                    Title = "⚠️ Cannot End This Process",
                    Content = $"'{processName}' is a critical Windows system process.\n\n" +
                              "Ending it would crash your computer and you would lose any unsaved work.\n\n" +
                              "This process is essential for Windows to run properly.",
                    CloseButtonText = "OK",
                    XamlRoot = _xamlRoot
                };

                await dialog.ShowAsync();
            });
        }

        private async Task<bool> ShowSvchostWarning(string processName)
        {
            if (_xamlRoot == null) return false;

            bool result = false;
            var tcs = new TaskCompletionSource<bool>();

            await DispatchAsync(async () =>
            {
                var dialog = new Microsoft.UI.Xaml.Controls.ContentDialog
                {
                    Title = "⚠️ Warning: Service Host Process",
                    Content = $"'{processName}' hosts important Windows services.\n\n" +
                              "Ending this process might:\n" +
                              "• Stop network connectivity\n" +
                              "• Disable audio\n" +
                              "• Break other Windows features\n\n" +
                              "These services usually restart automatically, but you may need to restart your computer.\n\n" +
                              "Are you sure you want to continue?",
                    PrimaryButtonText = "End Process Anyway",
                    CloseButtonText = "Cancel",
                    DefaultButton = Microsoft.UI.Xaml.Controls.ContentDialogButton.Close,
                    XamlRoot = _xamlRoot
                };

                var dialogResult = await dialog.ShowAsync();
                result = (dialogResult == Microsoft.UI.Xaml.Controls.ContentDialogResult.Primary);
                tcs.SetResult(result);
            });

            return await tcs.Task;
        }

        private async Task<bool> ShowEndTaskConfirmation(string processName)
        {
            System.Diagnostics.Debug.WriteLine($"ShowEndTaskConfirmation called for: {processName}");

            if (_xamlRoot == null)
            {
                System.Diagnostics.Debug.WriteLine("ERROR: XamlRoot is null!");
                return false;
            }

            bool result = false;
            var tcs = new TaskCompletionSource<bool>();

            try
            {
                await DispatchAsync(async () =>
                {
                    System.Diagnostics.Debug.WriteLine("Creating dialog...");
                    var dialog = new Microsoft.UI.Xaml.Controls.ContentDialog
                    {
                        Title = "End Task",
                        Content = $"Are you sure you want to end '{processName}'?\n\n" +
                                  "Any unsaved work in this app will be lost.",
                        PrimaryButtonText = "End Task",
                        CloseButtonText = "Cancel",
                        DefaultButton = Microsoft.UI.Xaml.Controls.ContentDialogButton.Close,
                        XamlRoot = _xamlRoot
                    };

                    System.Diagnostics.Debug.WriteLine("Showing dialog...");
                    var dialogResult = await dialog.ShowAsync();
                    System.Diagnostics.Debug.WriteLine($"Dialog result: {dialogResult}");
                    result = (dialogResult == Microsoft.UI.Xaml.Controls.ContentDialogResult.Primary);
                    tcs.SetResult(result);
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ERROR showing dialog: {ex.Message}");
                tcs.SetResult(false);
            }

            return await tcs.Task;
        }

        private async Task<bool> ShowRestartConfirmation(string processName)
        {
            if (_xamlRoot == null) return false;

            bool result = false;
            var tcs = new TaskCompletionSource<bool>();

            await DispatchAsync(async () =>
            {
                var dialog = new Microsoft.UI.Xaml.Controls.ContentDialog
                {
                    Title = "Restart Process",
                    Content = $"Are you sure you want to restart '{processName}'?\n\n" +
                              "The app will close and reopen. Any unsaved work will be lost.",
                    PrimaryButtonText = "Restart",
                    CloseButtonText = "Cancel",
                    DefaultButton = Microsoft.UI.Xaml.Controls.ContentDialogButton.Close,
                    XamlRoot = _xamlRoot
                };

                var dialogResult = await dialog.ShowAsync();
                result = (dialogResult == Microsoft.UI.Xaml.Controls.ContentDialogResult.Primary);
                tcs.SetResult(result);
            });

            return await tcs.Task;
        }

        public void StopAutoRefresh()
        {
            _refreshTimer?.Dispose();
            _refreshTimer = null;
            _monitorService?.Dispose();
            _refreshLock?.Dispose();
        }
    }
}