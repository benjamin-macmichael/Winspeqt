using Microsoft.UI.Dispatching;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Winspeqt.Models;
using Winspeqt.Services;

namespace Winspeqt.ViewModels.Security
{
    public class NetworkSecurityViewModel : INotifyPropertyChanged
    {
        private readonly NetworkSecurityMonitor _monitor;
        private readonly DispatcherQueue _dispatcherQueue;

        private bool _isMonitoring;
        private string _statusMessage = string.Empty;
        private int _totalConnections;
        private int _listeningPorts;
        private int _riskyConnections;
        private string _totalTrafficSent = string.Empty;
        private string _totalTrafficReceived = string.Empty;

        public ObservableCollection<NetworkConnection> ActiveConnections { get; }
        public ObservableCollection<NetworkTrafficStats> TrafficStats { get; }
        public ObservableCollection<PortScanResult> OpenPorts { get; }
        public ObservableCollection<string> SecurityAlerts { get; }

        public bool IsMonitoring
        {
            get => _isMonitoring;
            set
            {
                _isMonitoring = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(MonitoringStatusText));
            }
        }

        public string StatusMessage
        {
            get => _statusMessage;
            set
            {
                _statusMessage = value;
                OnPropertyChanged();
            }
        }

        public int TotalConnections
        {
            get => _totalConnections;
            set
            {
                _totalConnections = value;
                OnPropertyChanged();
            }
        }

        public int ListeningPorts
        {
            get => _listeningPorts;
            set
            {
                _listeningPorts = value;
                OnPropertyChanged();
            }
        }

        public int RiskyConnections
        {
            get => _riskyConnections;
            set
            {
                _riskyConnections = value;
                OnPropertyChanged();
            }
        }

        public string TotalTrafficSent
        {
            get => _totalTrafficSent;
            set
            {
                _totalTrafficSent = value;
                OnPropertyChanged();
            }
        }

        public string TotalTrafficReceived
        {
            get => _totalTrafficReceived;
            set
            {
                _totalTrafficReceived = value;
                OnPropertyChanged();
            }
        }

        public string MonitoringStatusText => IsMonitoring ? "Monitoring Active" : "Monitoring Stopped";

        public ICommand StartMonitoringCommand { get; }
        public ICommand StopMonitoringCommand { get; }
        public ICommand RefreshCommand { get; }
        public ICommand ClearAlertsCommand { get; }

        public NetworkSecurityViewModel()
        {
            _dispatcherQueue = DispatcherQueue.GetForCurrentThread();
            _monitor = new NetworkSecurityMonitor();

            ActiveConnections = new ObservableCollection<NetworkConnection>();
            TrafficStats = new ObservableCollection<NetworkTrafficStats>();
            OpenPorts = new ObservableCollection<PortScanResult>();
            SecurityAlerts = new ObservableCollection<string>();

            StartMonitoringCommand = new RelayCommand(StartMonitoring);
            StopMonitoringCommand = new RelayCommand(StopMonitoring);
            RefreshCommand = new RelayCommand(async () => await RefreshDataAsync());
            ClearAlertsCommand = new RelayCommand(ClearAlerts);

            // Subscribe to monitor events
            _monitor.ConnectionsUpdated += OnConnectionsUpdated;
            _monitor.TrafficStatsUpdated += OnTrafficStatsUpdated;
            _monitor.OpenPortsDetected += OnOpenPortsDetected;
            _monitor.SecurityAlertRaised += OnSecurityAlertRaised;

            StatusMessage = "Ready to monitor network security";
        }

        private void StartMonitoring()
        {
            _monitor.StartMonitoring(3000); // Update every 3 seconds
            IsMonitoring = true;
            StatusMessage = "Network monitoring started...";
        }

        private void StopMonitoring()
        {
            _monitor.StopMonitoring();
            IsMonitoring = false;
            StatusMessage = "Network monitoring stopped";
        }

        private async System.Threading.Tasks.Task RefreshDataAsync()
        {
            StatusMessage = "Refreshing data...";

            var connections = await _monitor.GetActiveConnectionsAsync();
            var traffic = await _monitor.GetNetworkTrafficStatsAsync();
            var ports = await _monitor.ScanOpenPortsAsync();

            OnConnectionsUpdated(this, connections);
            OnTrafficStatsUpdated(this, traffic);
            OnOpenPortsDetected(this, ports);

            StatusMessage = "Data refreshed successfully";
        }

        private void ClearAlerts()
        {
            _dispatcherQueue.TryEnqueue(() =>
            {
                SecurityAlerts.Clear();
                StatusMessage = "Security alerts cleared";
            });
        }

        private void OnConnectionsUpdated(object? sender, System.Collections.Generic.List<NetworkConnection> connections)
        {
            _dispatcherQueue.TryEnqueue(() =>
            {
                ActiveConnections.Clear();
                foreach (var conn in connections)
                {
                    ActiveConnections.Add(conn);
                }

                TotalConnections = connections.Count;
                ListeningPorts = connections.Count(c => c.State == "LISTENING");
                RiskyConnections = connections.Count(c => c.RiskLevel == "High" || c.RiskLevel == "Medium");
            });
        }

        private void OnTrafficStatsUpdated(object? sender, System.Collections.Generic.List<NetworkTrafficStats> stats)
        {
            _dispatcherQueue.TryEnqueue(() =>
            {
                TrafficStats.Clear();
                foreach (var stat in stats)
                {
                    TrafficStats.Add(stat);
                }

                var totalSent = stats.Sum(s => s.BytesSent);
                var totalReceived = stats.Sum(s => s.BytesReceived);

                TotalTrafficSent = FormatBytes(totalSent);
                TotalTrafficReceived = FormatBytes(totalReceived);
            });
        }

        private void OnOpenPortsDetected(object? sender, System.Collections.Generic.List<PortScanResult> ports)
        {
            _dispatcherQueue.TryEnqueue(() =>
            {
                OpenPorts.Clear();
                foreach (var port in ports)
                {
                    OpenPorts.Add(port);
                }
            });
        }

        private void OnSecurityAlertRaised(object? sender, string alert)
        {
            _dispatcherQueue.TryEnqueue(() =>
            {
                var timestampedAlert = $"[{DateTime.Now:HH:mm:ss}] {alert}";
                SecurityAlerts.Insert(0, timestampedAlert);

                // Keep only last 50 alerts
                while (SecurityAlerts.Count > 50)
                {
                    SecurityAlerts.RemoveAt(SecurityAlerts.Count - 1);
                }
            });
        }

        private string FormatBytes(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            double len = bytes;
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len = len / 1024;
            }
            return $"{len:0.##} {sizes[order]}";
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    // Simple RelayCommand implementation
    public class RelayCommand : ICommand
    {
        private readonly Action _execute;
        private readonly Func<bool>? _canExecute;

        public RelayCommand(Action execute, Func<bool>? canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        public event EventHandler? CanExecuteChanged;

        public bool CanExecute(object? parameter) => _canExecute?.Invoke() ?? true;

        public void Execute(object? parameter) => _execute();

        public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }
}
