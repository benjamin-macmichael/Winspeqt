using Microsoft.UI.Dispatching;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Winspeqt.Helpers;
using Winspeqt.Models;
using Winspeqt.Services;

namespace Winspeqt.ViewModels.Monitoring
{
    public class BackgroundProcessViewModel : INotifyPropertyChanged
    {
        private readonly SystemMonitorService _systemMonitor;
        private readonly DispatcherQueue _dispatcherQueue;
        private DispatcherQueueTimer? _refreshTimer;

        private bool _isLoading;
        private string _searchQuery = string.Empty;
        private bool _isRefreshing;

        public BackgroundProcessViewModel()
        {
            _systemMonitor = new SystemMonitorService();
            _dispatcherQueue = DispatcherQueue.GetForCurrentThread();

            AllProcesses = new ObservableCollection<ProcessInfo>();
            FilteredProcesses = new ObservableCollection<ProcessInfo>();
            BatteryDevices = new ObservableCollection<BatteryInfo>();

            BrowserProcesses = new ObservableCollection<ProcessInfo>();
            GamingProcesses = new ObservableCollection<ProcessInfo>();
            CloudStorageProcesses = new ObservableCollection<ProcessInfo>();
            CommunicationProcesses = new ObservableCollection<ProcessInfo>();
            SystemServicesProcesses = new ObservableCollection<ProcessInfo>();
            MediaProcesses = new ObservableCollection<ProcessInfo>();
            DevelopmentProcesses = new ObservableCollection<ProcessInfo>();
            OtherProcesses = new ObservableCollection<ProcessInfo>();

            RefreshCommand = new RelayCommand(async () => await RefreshDataAsync());

            // Delay initialization to let the UI load first
            _dispatcherQueue.TryEnqueue(async () =>
            {
                await Task.Delay(100); // Small delay to let UI render
                await InitializeAsync();
            });
        }

        public ObservableCollection<ProcessInfo> AllProcesses { get; }
        public ObservableCollection<ProcessInfo> FilteredProcesses { get; }
        public ObservableCollection<BatteryInfo> BatteryDevices { get; }

        public ObservableCollection<ProcessInfo> BrowserProcesses { get; }
        public ObservableCollection<ProcessInfo> GamingProcesses { get; }
        public ObservableCollection<ProcessInfo> CloudStorageProcesses { get; }
        public ObservableCollection<ProcessInfo> CommunicationProcesses { get; }
        public ObservableCollection<ProcessInfo> SystemServicesProcesses { get; }
        public ObservableCollection<ProcessInfo> MediaProcesses { get; }
        public ObservableCollection<ProcessInfo> DevelopmentProcesses { get; }
        public ObservableCollection<ProcessInfo> OtherProcesses { get; }

        public bool IsLoading
        {
            get => _isLoading;
            set
            {
                _isLoading = value;
                OnPropertyChanged(nameof(IsLoading));
            }
        }

        public string SearchQuery
        {
            get => _searchQuery;
            set
            {
                _searchQuery = value;
                OnPropertyChanged(nameof(SearchQuery));
                FilterProcesses();
            }
        }

        public ICommand RefreshCommand { get; }

        private async Task InitializeAsync()
        {
            try
            {
                await RefreshDataAsync();
                StartAutoRefresh();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error initializing: {ex.Message}");
            }
        }

        private async Task RefreshDataAsync()
        {
            // Prevent concurrent refreshes
            if (_isRefreshing) return;
            _isRefreshing = true;

            IsLoading = true;

            try
            {
                // Run on background thread to avoid UI freezing
                var processes = await Task.Run(async () =>
                {
                    try
                    {
                        return await _systemMonitor.GetRunningProcessesAsync();
                    }
                    catch (AccessViolationException ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"AccessViolation in GetRunningProcessesAsync: {ex.Message}");
                        return new System.Collections.Generic.List<ProcessInfo>();
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error getting processes: {ex.Message}");
                        return new System.Collections.Generic.List<ProcessInfo>();
                    }
                });

                _dispatcherQueue.TryEnqueue(() =>
                {
                    try
                    {
                        AllProcesses.Clear();
                        BatteryDevices.Clear();

                        foreach (var process in processes)
                        {
                            if (process != null)
                            {
                                AllProcesses.Add(process);
                            }
                        }

                        GroupProcessesByCategory();
                        FilterProcesses();
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error updating UI: {ex.Message}");
                    }
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error refreshing data: {ex.Message}");
            }
            finally
            {
                IsLoading = false;
                _isRefreshing = false;
            }
        }

        private void GroupProcessesByCategory()
        {
            BrowserProcesses.Clear();
            GamingProcesses.Clear();
            CloudStorageProcesses.Clear();
            CommunicationProcesses.Clear();
            SystemServicesProcesses.Clear();
            MediaProcesses.Clear();
            DevelopmentProcesses.Clear();
            OtherProcesses.Clear();

            foreach (var process in AllProcesses)
            {
                if (process == null) continue;

                switch (process.Category)
                {
                    case ProcessCategory.Browser:
                        BrowserProcesses.Add(process);
                        break;
                    case ProcessCategory.Gaming:
                        GamingProcesses.Add(process);
                        break;
                    case ProcessCategory.CloudStorage:
                        CloudStorageProcesses.Add(process);
                        break;
                    case ProcessCategory.Communication:
                        CommunicationProcesses.Add(process);
                        break;
                    case ProcessCategory.SystemServices:
                        SystemServicesProcesses.Add(process);
                        break;
                    case ProcessCategory.Development:
                        DevelopmentProcesses.Add(process);
                        break;
                    case ProcessCategory.Media:
                        MediaProcesses.Add(process);
                        break;
                    default:
                        OtherProcesses.Add(process);
                        break;
                }
            }
        }

        private void FilterProcesses()
        {
            FilteredProcesses.Clear();

            var filtered = string.IsNullOrWhiteSpace(SearchQuery)
                ? AllProcesses
                : AllProcesses.Where(p =>
                    (p.ProcessName?.Contains(SearchQuery, StringComparison.OrdinalIgnoreCase) ?? false) ||
                    (p.Description?.Contains(SearchQuery, StringComparison.OrdinalIgnoreCase) ?? false) ||
                    (p.FriendlyExplanation?.Contains(SearchQuery, StringComparison.OrdinalIgnoreCase) ?? false));

            foreach (var process in filtered)
            {
                FilteredProcesses.Add(process);
            }
        }

        private void StartAutoRefresh()
        {
            _refreshTimer = _dispatcherQueue.CreateTimer();
            _refreshTimer.Interval = TimeSpan.FromSeconds(5);
            _refreshTimer.Tick += async (s, e) =>
            {
                try
                {
                    await RefreshDataAsync();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Auto-refresh error: {ex.Message}");
                }
            };
            _refreshTimer.Start();
        }

        public async Task<bool> EndProcessAsync(ProcessInfo process)
        {
            System.Diagnostics.Process? systemProcess = null;
            try
            {
                systemProcess = System.Diagnostics.Process.GetProcessById(process.ProcessId);
                systemProcess.Kill();
                await systemProcess.WaitForExitAsync(); // Wait for process to actually end

                _dispatcherQueue.TryEnqueue(() =>
                {
                    AllProcesses.Remove(process);
                    FilteredProcesses.Remove(process);

                    switch (process.Category)
                    {
                        case ProcessCategory.Browser:
                            BrowserProcesses.Remove(process);
                            break;
                        case ProcessCategory.Gaming:
                            GamingProcesses.Remove(process);
                            break;
                        case ProcessCategory.CloudStorage:
                            CloudStorageProcesses.Remove(process);
                            break;
                        case ProcessCategory.Communication:
                            CommunicationProcesses.Remove(process);
                            break;
                        case ProcessCategory.SystemServices:
                            SystemServicesProcesses.Remove(process);
                            break;
                        case ProcessCategory.Development:
                            DevelopmentProcesses.Remove(process);
                            break;
                        case ProcessCategory.Media:
                            MediaProcesses.Remove(process);
                            break;
                        default:
                            OtherProcesses.Remove(process);
                            break;
                    }
                });

                return true;
            }
            catch (ArgumentException)
            {
                // Process already exited
                System.Diagnostics.Debug.WriteLine($"Process {process.ProcessId} already exited");
                return true;
            }
            catch (InvalidOperationException ex)
            {
                System.Diagnostics.Debug.WriteLine($"Cannot end process: {ex.Message}");
                return false;
            }
            catch (System.ComponentModel.Win32Exception ex)
            {
                // Access denied
                System.Diagnostics.Debug.WriteLine($"Access denied ending process: {ex.Message}");
                return false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error ending process: {ex.Message}");
                return false;
            }
            finally
            {
                systemProcess?.Dispose(); // Always dispose the process handle!
            }
        }

        public void StopAutoRefresh()
        {
            _refreshTimer?.Stop();
            _refreshTimer = null;
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}