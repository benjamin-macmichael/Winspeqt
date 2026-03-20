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
        private readonly NetworkSecurityMonitorService _monitor;
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
        private bool _isConnectionsExpanded = false;

        // Unsecured network state
        private bool _isOnUnsecuredNetwork;
        private string _unsecuredNetworkName = "";
        private string _unsecuredNetworkInterface = "";
        private readonly HashSet<string> _popupShownNetworks = new();

        // Quick Assist acknowledgement
        private bool _isQuickAssistAcknowledged;
        private string _quickAssistConfirmText = "";

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

        public bool IsConnectionsExpanded
        {
            get => _isConnectionsExpanded;
            set
            {
                _isConnectionsExpanded = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ConnectionsChevronGlyph));
                OnPropertyChanged(nameof(ShowConnectionsMoreButton));
            }
        }

        public string ConnectionsChevronGlyph => _isConnectionsExpanded ? "\uE70E" : "\uE70D";

        public bool ShowConnectionsMoreButton => _isConnectionsExpanded && HasMoreConnections;

        public bool IsOnUnsecuredNetwork
        {
            get => _isOnUnsecuredNetwork;
            set { _isOnUnsecuredNetwork = value; OnPropertyChanged(); }
        }

        public string UnsecuredNetworkName
        {
            get => _unsecuredNetworkName;
            set { _unsecuredNetworkName = value; OnPropertyChanged(); OnPropertyChanged(nameof(UnsecuredNetworkWarningMessage)); }
        }

        public string UnsecuredNetworkWarningMessage =>
            string.IsNullOrEmpty(_unsecuredNetworkName)
                ? "You are connected to an open WiFi network. Your data is not encrypted — anyone nearby could intercept it."
                : $"You are connected to \"{_unsecuredNetworkName}\", which has no password or encryption. Anyone nearby could intercept your traffic.";

        // Fired once per unique network per session — page subscribes to show the popup
        public event Action<string, string>? UnsecuredNetworkDetected;

        // ── Quick Assist ─────────────────────────────────────────────────────
        public bool IsQuickAssistAcknowledged
        {
            get => _isQuickAssistAcknowledged;
            set
            {
                _isQuickAssistAcknowledged = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CanLaunchQuickAssist));
                (_launchQuickAssistCommand as RelayCommand)?.RaiseCanExecuteChanged();
            }
        }

        public string QuickAssistConfirmText
        {
            get => _quickAssistConfirmText;
            set
            {
                _quickAssistConfirmText = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CanLaunchQuickAssist));
                (_launchQuickAssistCommand as RelayCommand)?.RaiseCanExecuteChanged();
            }
        }

        public bool CanLaunchQuickAssist =>
            _isQuickAssistAcknowledged &&
            _quickAssistConfirmText.Trim().Equals("I understand", StringComparison.OrdinalIgnoreCase);

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
        public ICommand ToggleConnectionsExpandedCommand { get; }
        public ICommand ToggleRiskyPortsCommand { get; }
        public ICommand DisconnectUnsecuredNetworkCommand { get; }

        private readonly ICommand _launchQuickAssistCommand;
        public ICommand LaunchQuickAssistCommand => _launchQuickAssistCommand;
        public ICommand DisconnectDeviceCommand { get; }

        public NetworkSecurityViewModel()
        {
            _dispatcherQueue = DispatcherQueue.GetForCurrentThread();
            _monitor = new NetworkSecurityMonitorService();

            StartMonitoringCommand = new RelayCommand(StartMonitoring);
            StopMonitoringCommand = new RelayCommand(StopMonitoring);
            RefreshCommand = new RelayCommand(async () => await RefreshDataAsync());
            ClearAlertsCommand = new RelayCommand(ClearAlerts);
            ToggleConnectionsCommand = new RelayCommand(ToggleConnections);
            ToggleConnectionsExpandedCommand = new RelayCommand(() => IsConnectionsExpanded = !IsConnectionsExpanded);
            ToggleRiskyPortsCommand = new RelayCommand(ToggleRiskyPorts);
            DisconnectUnsecuredNetworkCommand = new RelayCommand(async () =>
            {
                if (!string.IsNullOrEmpty(_unsecuredNetworkInterface))
                {
                    StatusMessage = $"Disconnecting from \"{UnsecuredNetworkName}\"...";
                    await _monitor.DisconnectWifiAsync(_unsecuredNetworkInterface);
                    StatusMessage = "Disconnected from unsecured network";
                    IsOnUnsecuredNetwork = false;
                }
            });

            _launchQuickAssistCommand = new RelayCommand(
                async () =>
                {
                    await Windows.System.Launcher.LaunchUriAsync(new Uri("ms-quick-assist:"));
                    _ = WatchForQuickAssistAsync();
                },
                () => CanLaunchQuickAssist);
            DisconnectDeviceCommand = new RelayCommand<ConnectedDevice>(async d => await DisconnectDeviceAsync(d));

            _monitor.ConnectionsUpdated += OnConnectionsUpdated;
            _monitor.OpenPortsDetected += OnOpenPortsDetected;
            _monitor.ConnectedDevicesUpdated += OnConnectedDevicesUpdated;
            _monitor.SecurityAlertRaised += OnSecurityAlertRaised;

            StatusMessage = "Ready to monitor network security";

            // Load WiFi disconnect history and check network security on startup
            _ = LoadWifiEventsAsync();
            _ = CheckUnsecuredNetworkAsync();
        }

        private void StartMonitoring()
        {
            _monitor.StartMonitoring(300_000); // 5 minutes
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
            await CheckUnsecuredNetworkAsync();

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

        private async System.Threading.Tasks.Task WatchForQuickAssistAsync()
        {
            // Poll up to ~45 seconds (15 × 3s) for QuickAssist.exe to appear
            for (int i = 0; i < 15; i++)
            {
                await System.Threading.Tasks.Task.Delay(3000);
                if (System.Diagnostics.Process.GetProcessesByName("QuickAssist").Length > 0)
                {
                    NotificationManagerService.Instance.SendQuickAssistLaunchedNotification();
                    return;
                }
            }
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

        private async System.Threading.Tasks.Task CheckUnsecuredNetworkAsync()
        {
            var unsecuredNets = await _monitor.GetUnsecuredWifiConnectionsAsync();
            _dispatcherQueue.TryEnqueue(() =>
            {
                if (unsecuredNets.Count > 0)
                {
                    var (ssid, iface) = unsecuredNets[0];
                    IsOnUnsecuredNetwork = true;
                    UnsecuredNetworkName = ssid;
                    _unsecuredNetworkInterface = iface;

                    // Add to security alerts (only if not already present)
                    var alertText = $"[{DateTime.Now:HH:mm:ss}] \uE7BA Open (unsecured) network: \"{ssid}\" — traffic is unencrypted";
                    if (!SecurityAlerts.Any(a => a.Contains(ssid) && a.Contains("unsecured")))
                        SecurityAlerts.Insert(0, alertText);

                    // Fire popup event and toast once per network per session
                    if (_popupShownNetworks.Add(ssid))
                    {
                        UnsecuredNetworkDetected?.Invoke(ssid, iface);
                        NotificationManagerService.Instance.SendUnsecuredNetworkNotification(ssid);
                    }
                }
                else
                {
                    IsOnUnsecuredNetwork = false;
                    UnsecuredNetworkName = "";
                    _unsecuredNetworkInterface = "";
                }
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
                // Deduplicate by display name — keep first occurrence per unique device
                var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var d in devices)
                {
                    if (seen.Add(d.Name))
                        ConnectedDevices.Add(d);
                }
            });
        }

        private void OnSecurityAlertRaised(object? sender, string alert)
        {
            _dispatcherQueue.TryEnqueue(() =>
            {
                // Deduplicate: remove existing entry with same core text before re-inserting with fresh timestamp
                for (int i = SecurityAlerts.Count - 1; i >= 0; i--)
                {
                    var core = StripTimestamp(SecurityAlerts[i]);
                    if (string.Equals(core, alert, StringComparison.OrdinalIgnoreCase))
                        SecurityAlerts.RemoveAt(i);
                }

                SecurityAlerts.Insert(0, $"[{DateTime.Now:HH:mm:ss}] {alert}");

                while (SecurityAlerts.Count > 50)
                    SecurityAlerts.RemoveAt(SecurityAlerts.Count - 1);
            });
        }

        private static string StripTimestamp(string alert)
        {
            // Alerts are formatted as "[HH:mm:ss] text"
            if (alert.Length > 11 && alert[0] == '[')
            {
                var end = alert.IndexOf("] ", StringComparison.Ordinal);
                if (end >= 0) return alert.Substring(end + 2);
            }
            return alert;
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
            OnPropertyChanged(nameof(ShowConnectionsMoreButton));
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
