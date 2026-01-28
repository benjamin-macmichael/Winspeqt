using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Microsoft.UI.Dispatching;
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

        public BackgroundProcessViewModel()
        {
            _systemMonitor = new SystemMonitorService();
            _dispatcherQueue = DispatcherQueue.GetForCurrentThread();

            AllProcesses = new ObservableCollection<ProcessInfo>();
            FilteredProcesses = new ObservableCollection<ProcessInfo>();
            BatteryDevices = new ObservableCollection<BatteryInfo>();

            // Category collections
            BrowserProcesses = new ObservableCollection<ProcessInfo>();
            GamingProcesses = new ObservableCollection<ProcessInfo>();
            CloudStorageProcesses = new ObservableCollection<ProcessInfo>();
            CommunicationProcesses = new ObservableCollection<ProcessInfo>();
            SystemServicesProcesses = new ObservableCollection<ProcessInfo>();
            MediaProcesses = new ObservableCollection<ProcessInfo>();
            DevelopmentProcesses = new ObservableCollection<ProcessInfo>();
            OtherProcesses = new ObservableCollection<ProcessInfo>();

            RefreshCommand = new RelayCommand(async () => await RefreshDataAsync());

            InitializeAsync();
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

        private async void InitializeAsync()
        {
            await RefreshDataAsync();
            StartAutoRefresh();
        }

        private async Task RefreshDataAsync()
        {
            IsLoading = true;

            try
            {
                var processesTask = _systemMonitor.GetRunningProcessesAsync();
                var batteryTask = _systemMonitor.GetBatteryInfoAsync();

                await Task.WhenAll(processesTask, batteryTask);

                _dispatcherQueue.TryEnqueue(() =>
                {
                    AllProcesses.Clear();
                    BatteryDevices.Clear();

                    // Add all processes
                    foreach (var process in processesTask.Result)
                    {
                        AllProcesses.Add(process);
                    }

                    // Add battery devices
                    foreach (var battery in batteryTask.Result)
                    {
                        BatteryDevices.Add(battery);
                    }

                    GroupProcessesByCategory();
                    FilterProcesses();
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error refreshing data: {ex.Message}");
            }
            finally
            {
                IsLoading = false;
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
                    p.ProcessName.Contains(SearchQuery, StringComparison.OrdinalIgnoreCase) ||
                    p.Description.Contains(SearchQuery, StringComparison.OrdinalIgnoreCase) ||
                    p.FriendlyExplanation.Contains(SearchQuery, StringComparison.OrdinalIgnoreCase));

            foreach (var process in filtered)
            {
                FilteredProcesses.Add(process);
            }
        }

        private void StartAutoRefresh()
        {
            _refreshTimer = _dispatcherQueue.CreateTimer();
            _refreshTimer.Interval = TimeSpan.FromSeconds(5);
            _refreshTimer.Tick += async (s, e) => await RefreshDataAsync();
            _refreshTimer.Start();
        }

        public async Task<bool> EndProcessAsync(ProcessInfo process)
        {
            try
            {
                var systemProcess = System.Diagnostics.Process.GetProcessById(process.ProcessId);
                systemProcess.Kill();

                // Remove from collections
                _dispatcherQueue.TryEnqueue(() =>
                {
                    AllProcesses.Remove(process);
                    FilteredProcesses.Remove(process);

                    // Remove from category collection
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
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error ending process: {ex.Message}");
                return false;
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}