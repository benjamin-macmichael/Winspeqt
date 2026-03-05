using Microsoft.UI.Dispatching;
using System;
using System.Collections.Generic;
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

        // Backing lists for all data (unfiltered)
        private readonly List<NetworkConnection> _allNotableConnections = new();
        private readonly List<PortScanResult> _allRiskyPorts = new();

        // Expand/collapse state
        private bool _showAllConnections = false;
        private bool _showAllRiskyPorts = false;

        // Public collections bound to the UI
        public ObservableCollection<NetworkConnection> DisplayedConnections { get; } = new();
        public ObservableCollection<PortScanResult> DisplayedRiskyPorts { get; } = new();
        public ObservableCollection<ConnectedDevice> ConnectedDevices { get; } = new();
        public ObservableCollection<WifiDisconnectEvent> WifiEvents { get; } = new();
        public ObservableCollection<string> SecurityAlerts { get; } = new();

        // Keep full collections for stats
        public ObservableCollection<NetworkConnection> ActiveConnections { get; } = new();
        public ObservableCollection<PortScanResult> OpenPorts { get; } = new();

        public bool IsMonitoring
        {
            get => _isMonitoring;
            set { _isMonitoring = value; OnPropertyChanged(); OnPropertyChanged(nameof(MonitoringStatusText)); }
        }

        public string StatusMessage
        {
            get => _statusMessage;
            set { _statusMessage = value; OnPropertyChanged(); }
        }

        public int TotalConnections
        {
            get => _totalConnections;
            set { _totalConnections = value; OnPropertyChanged(); }
        }

        public int ListeningPorts
        {
            get => _listeningPorts;
            set { _listeningPorts = value; OnPropertyChanged(); }
        }

        public int RiskyConnections
        {
            get => _riskyConnections;
            set { _riskyConnections = value; OnPropertyChanged(); }
        }

        public string MonitoringStatusText => IsMonitoring ? "Monitoring Active" : "Monitoring Stopped";

        // ── Connections expand/collapse ──────────────────────────────────────
        public bool HasMoreConnections { get; private set; }

        public string ConnectionsToggleText =>
            _showAllConnections
                ? "Show less"
                : $"Show {_allNotableConnections.Count - 10} more";

        // ── Risky ports expand/collapse ──────────────────────────────────────
        public bool HasMoreRiskyPorts { get; private set; }
        public bool HasNoRiskyPorts { get; private set; }

        public string RiskyPortsToggleText =>
            _showAllRiskyPorts
                ? "Show less"
                : $"Show {_allRiskyPorts.Count - 10} more";

        // ── Commands ─────────────────────────────────────────────────────────
        public ICommand StartMonitoringCommand { get; }
        public ICommand StopMonitoringCommand { get; }
        public ICommand RefreshCommand { get; }
        public ICommand ClearAlertsCommand { get; }
        public ICommand ToggleConnectionsCommand { get; }
        public ICommand ToggleRiskyPortsCommand { get; }
        public ICommand DisconnectDeviceCommand { get; }

        public NetworkSecurityViewModel()
        {
            _dispatcherQueue = DispatcherQueue.GetForCurrentThread();
            _monitor = new NetworkSecurityMonitor();

            StartMonitoringCommand = new RelayCommand(StartMonitoring);
            StopMonitoringCommand = new RelayCommand(StopMonitoring);
            RefreshCommand = new RelayCommand(async () => await RefreshDataAsync());
            ClearAlertsCommand = new RelayCommand(ClearAlerts);
            ToggleConnectionsCommand = new RelayCommand(ToggleConnections);
            ToggleRiskyPortsCommand = new RelayCommand(ToggleRiskyPorts);
            DisconnectDeviceCommand = new RelayCommand<ConnectedDevice>(async d => await DisconnectDeviceAsync(d));

            _monitor.ConnectionsUpdated += OnConnectionsUpdated;
            _monitor.OpenPortsDetected += OnOpenPortsDetected;
            _monitor.ConnectedDevicesUpdated += OnConnectedDevicesUpdated;
            _monitor.SecurityAlertRaised += OnSecurityAlertRaised;

            StatusMessage = "Ready to monitor network security";

            // Load WiFi disconnect history on startup
            _ = LoadWifiEventsAsync();
        }

        private void StartMonitoring()
        {
            _monitor.StartMonitoring(3000);
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
            var ports = await _monitor.ScanOpenPortsAsync();
            var devices = await _monitor.GetConnectedDevicesAsync();

            OnConnectionsUpdated(this, connections);
            OnOpenPortsDetected(this, ports);
            OnConnectedDevicesUpdated(this, devices);

            await LoadWifiEventsAsync();

            StatusMessage = "Data refreshed";
        }

        private void ClearAlerts()
        {
            _dispatcherQueue.TryEnqueue(() =>
            {
                SecurityAlerts.Clear();
                StatusMessage = "Security alerts cleared";
            });
        }

        private void ToggleConnections()
        {
            _showAllConnections = !_showAllConnections;
            _dispatcherQueue.TryEnqueue(() => RefreshDisplayedConnections());
        }

        private void ToggleRiskyPorts()
        {
            _showAllRiskyPorts = !_showAllRiskyPorts;
            _dispatcherQueue.TryEnqueue(() => RefreshDisplayedRiskyPorts());
        }

        private async System.Threading.Tasks.Task DisconnectDeviceAsync(ConnectedDevice? device)
        {
            if (device == null || !device.CanDisconnect) return;
            StatusMessage = $"Disconnecting {device.Name}...";
            await _monitor.DisconnectWifiAsync(device.InterfaceName);
            StatusMessage = $"Disconnect command sent to {device.Name}";
        }

        private async System.Threading.Tasks.Task LoadWifiEventsAsync()
        {
            var events = await _monitor.GetWifiDisconnectEventsAsync();
            _dispatcherQueue.TryEnqueue(() =>
            {
                WifiEvents.Clear();
                foreach (var ev in events)
                    WifiEvents.Add(ev);
            });
        }

        // ── Event handlers ────────────────────────────────────────────────────

        private void OnConnectionsUpdated(object? sender, List<NetworkConnection> connections)
        {
            _dispatcherQueue.TryEnqueue(() =>
            {
                ActiveConnections.Clear();
                foreach (var c in connections) ActiveConnections.Add(c);

                TotalConnections = connections.Count;
                ListeningPorts = connections.Count(c => c.State == "LISTENING");
                RiskyConnections = connections.Count(c => c.RiskLevel == "High" || c.RiskLevel == "Medium");

                // Notable = active (not listening), with a real remote address, non-loopback
                _allNotableConnections.Clear();
                _allNotableConnections.AddRange(connections
                    .Where(c => c.State != "LISTENING"
                             && !string.IsNullOrEmpty(c.RemoteAddress)
                             && c.RemoteAddress != "0.0.0.0"
                             && c.RemoteAddress != "::"
                             && !c.RemoteAddress.StartsWith("127.")
                             && c.RemoteAddress != "::1")
                    .OrderByDescending(c => c.RiskLevel == "High" ? 2 : c.RiskLevel == "Medium" ? 1 : 0));

                RefreshDisplayedConnections();
            });
        }

        private void OnOpenPortsDetected(object? sender, List<PortScanResult> ports)
        {
            _dispatcherQueue.TryEnqueue(() =>
            {
                OpenPorts.Clear();
                foreach (var p in ports) OpenPorts.Add(p);

                _allRiskyPorts.Clear();
                _allRiskyPorts.AddRange(ports.Where(p => p.IsKnownRisky).OrderBy(p => p.Port));

                RefreshDisplayedRiskyPorts();
            });
        }

        private void OnConnectedDevicesUpdated(object? sender, List<ConnectedDevice> devices)
        {
            _dispatcherQueue.TryEnqueue(() =>
            {
                ConnectedDevices.Clear();
                foreach (var d in devices) ConnectedDevices.Add(d);
            });
        }

        private void OnSecurityAlertRaised(object? sender, string alert)
        {
            _dispatcherQueue.TryEnqueue(() =>
            {
                var timestamped = $"[{DateTime.Now:HH:mm:ss}] {alert}";
                SecurityAlerts.Insert(0, timestamped);

                while (SecurityAlerts.Count > 50)
                    SecurityAlerts.RemoveAt(SecurityAlerts.Count - 1);
            });
        }

        // ── Display refresh helpers ───────────────────────────────────────────

        private void RefreshDisplayedConnections()
        {
            DisplayedConnections.Clear();
            var toShow = _showAllConnections
                ? _allNotableConnections
                : _allNotableConnections.Take(10).ToList();

            foreach (var c in toShow) DisplayedConnections.Add(c);

            HasMoreConnections = _allNotableConnections.Count > 10;
            OnPropertyChanged(nameof(HasMoreConnections));
            OnPropertyChanged(nameof(ConnectionsToggleText));
        }

        private void RefreshDisplayedRiskyPorts()
        {
            DisplayedRiskyPorts.Clear();
            var toShow = _showAllRiskyPorts
                ? _allRiskyPorts
                : _allRiskyPorts.Take(10).ToList();

            foreach (var p in toShow) DisplayedRiskyPorts.Add(p);

            HasMoreRiskyPorts = _allRiskyPorts.Count > 10;
            HasNoRiskyPorts = _allRiskyPorts.Count == 0;
            OnPropertyChanged(nameof(HasMoreRiskyPorts));
            OnPropertyChanged(nameof(HasNoRiskyPorts));
            OnPropertyChanged(nameof(RiskyPortsToggleText));
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

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

    public class RelayCommand<T> : ICommand
    {
        private readonly Action<T?> _execute;
        private readonly Func<T?, bool>? _canExecute;

        public RelayCommand(Action<T?> execute, Func<T?, bool>? canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        public event EventHandler? CanExecuteChanged;
        public bool CanExecute(object? parameter) => _canExecute?.Invoke((T?)parameter) ?? true;
        public void Execute(object? parameter) => _execute((T?)parameter);
        public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }
}
