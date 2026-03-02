using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Eventing.Reader;
using System.Linq;
using System.Management;
using System.Net.NetworkInformation;
using System.Threading;
using System.Threading.Tasks;
using Winspeqt.Models;

namespace Winspeqt.Services
{
    public class NetworkSecurityMonitor
    {
        private Timer _monitoringTimer;
        private readonly Dictionary<string, NetworkTrafficStats> _previousStats = new();
        private readonly Dictionary<int, string> _wellKnownPorts = new();
        private readonly HashSet<int> _riskyPorts = new();

        // Track which alert keys have already fired this session to prevent spam
        private readonly HashSet<string> _firedAlerts = new();

        public event EventHandler<List<NetworkConnection>> ConnectionsUpdated;
        public event EventHandler<List<NetworkTrafficStats>> TrafficStatsUpdated;
        public event EventHandler<List<PortScanResult>> OpenPortsDetected;
        public event EventHandler<List<ConnectedDevice>> ConnectedDevicesUpdated;
        public event EventHandler<string> SecurityAlertRaised;

        public NetworkSecurityMonitor()
        {
            InitializeWellKnownPorts();
            InitializeRiskyPorts();
        }

        public void StartMonitoring(int intervalMilliseconds = 5000)
        {
            _monitoringTimer = new Timer(async _ => await MonitorNetworkAsync(),
                null, 0, intervalMilliseconds);
        }

        public void StopMonitoring()
        {
            _monitoringTimer?.Dispose();
            _firedAlerts.Clear();
        }

        private async Task MonitorNetworkAsync()
        {
            try
            {
                var connections = await GetActiveConnectionsAsync();
                ConnectionsUpdated?.Invoke(this, connections);

                var openPorts = await ScanOpenPortsAsync();
                OpenPortsDetected?.Invoke(this, openPorts);

                var devices = await GetConnectedDevicesAsync();
                ConnectedDevicesUpdated?.Invoke(this, devices);

                CheckForSecurityRisks(connections, openPorts);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Monitoring error: {ex.Message}");
            }
        }

        public async Task<List<NetworkConnection>> GetActiveConnectionsAsync()
        {
            return await Task.Run(() =>
            {
                var connections = new List<NetworkConnection>();
                var properties = IPGlobalProperties.GetIPGlobalProperties();

                // TCP Connections
                var tcpConnections = properties.GetActiveTcpConnections();
                foreach (var conn in tcpConnections)
                {
                    connections.Add(new NetworkConnection
                    {
                        Protocol = "TCP",
                        LocalAddress = conn.LocalEndPoint.Address.ToString(),
                        LocalPort = conn.LocalEndPoint.Port,
                        RemoteAddress = conn.RemoteEndPoint.Address.ToString(),
                        RemotePort = conn.RemoteEndPoint.Port,
                        State = conn.State.ToString(),
                        ProcessId = 0,
                        DetectedAt = DateTime.Now
                    });
                }

                // TCP Listeners
                var tcpListeners = properties.GetActiveTcpListeners();
                foreach (var listener in tcpListeners)
                {
                    connections.Add(new NetworkConnection
                    {
                        Protocol = "TCP",
                        LocalAddress = listener.Address.ToString(),
                        LocalPort = listener.Port,
                        RemoteAddress = string.Empty,
                        RemotePort = 0,
                        State = "LISTENING",
                        ProcessId = 0,
                        DetectedAt = DateTime.Now
                    });
                }

                // UDP Listeners
                var udpListeners = properties.GetActiveUdpListeners();
                foreach (var listener in udpListeners)
                {
                    connections.Add(new NetworkConnection
                    {
                        Protocol = "UDP",
                        LocalAddress = listener.Address.ToString(),
                        LocalPort = listener.Port,
                        RemoteAddress = string.Empty,
                        RemotePort = 0,
                        State = "LISTENING",
                        ProcessId = 0,
                        DetectedAt = DateTime.Now
                    });
                }

                foreach (var conn in connections)
                {
                    AnalyzeConnectionRisk(conn);
                }

                return connections.OrderBy(c => c.LocalPort).ToList();
            });
        }

        public async Task<List<NetworkTrafficStats>> GetNetworkTrafficStatsAsync()
        {
            return await Task.Run(() =>
            {
                var statsList = new List<NetworkTrafficStats>();
                var interfaces = NetworkInterface.GetAllNetworkInterfaces();

                foreach (var netInterface in interfaces)
                {
                    if (netInterface.OperationalStatus != OperationalStatus.Up)
                        continue;

                    var stats = netInterface.GetIPv4Statistics();
                    var currentStats = new NetworkTrafficStats
                    {
                        InterfaceName = netInterface.Name,
                        BytesSent = stats.BytesSent,
                        BytesReceived = stats.BytesReceived,
                        PacketsSent = stats.UnicastPacketsSent,
                        PacketsReceived = stats.UnicastPacketsReceived,
                        LastUpdated = DateTime.Now
                    };

                    if (_previousStats.TryGetValue(netInterface.Name, out var prevStats))
                    {
                        var timeDiff = (currentStats.LastUpdated - prevStats.LastUpdated).TotalSeconds;
                        if (timeDiff > 0)
                        {
                            currentStats.SendRate = (currentStats.BytesSent - prevStats.BytesSent) / timeDiff;
                            currentStats.ReceiveRate = (currentStats.BytesReceived - prevStats.BytesReceived) / timeDiff;
                        }
                    }

                    _previousStats[netInterface.Name] = currentStats;
                    statsList.Add(currentStats);
                }

                return statsList;
            });
        }

        public async Task<List<PortScanResult>> ScanOpenPortsAsync()
        {
            return await Task.Run(() =>
            {
                var openPorts = new List<PortScanResult>();
                var properties = IPGlobalProperties.GetIPGlobalProperties();

                var tcpListeners = properties.GetActiveTcpListeners();
                foreach (var listener in tcpListeners)
                {
                    var port = listener.Port;
                    openPorts.Add(new PortScanResult
                    {
                        Port = port,
                        Protocol = "TCP",
                        State = "LISTENING",
                        ServiceName = GetServiceName(port),
                        IsKnownRisky = _riskyPorts.Contains(port),
                        RiskDescription = _riskyPorts.Contains(port)
                            ? GetRiskDescription(port)
                            : "Normal",
                        DetectedAt = DateTime.Now
                    });
                }

                var udpListeners = properties.GetActiveUdpListeners();
                foreach (var listener in udpListeners)
                {
                    var port = listener.Port;
                    openPorts.Add(new PortScanResult
                    {
                        Port = port,
                        Protocol = "UDP",
                        State = "LISTENING",
                        ServiceName = GetServiceName(port),
                        IsKnownRisky = _riskyPorts.Contains(port),
                        RiskDescription = _riskyPorts.Contains(port)
                            ? GetRiskDescription(port)
                            : "Normal",
                        DetectedAt = DateTime.Now
                    });
                }

                return openPorts.OrderBy(p => p.Port).ToList();
            });
        }

        public async Task<List<ConnectedDevice>> GetConnectedDevicesAsync()
        {
            return await Task.Run(() =>
            {
                var devices = new List<ConnectedDevice>();

                // Network interfaces (WiFi and Ethernet)
                var interfaces = NetworkInterface.GetAllNetworkInterfaces()
                    .Where(ni => ni.OperationalStatus == OperationalStatus.Up
                              && ni.NetworkInterfaceType != NetworkInterfaceType.Loopback
                              && ni.NetworkInterfaceType != NetworkInterfaceType.Tunnel
                              && ni.NetworkInterfaceType != NetworkInterfaceType.Ppp);

                foreach (var ni in interfaces)
                {
                    var type = ni.NetworkInterfaceType switch
                    {
                        NetworkInterfaceType.Wireless80211 => "WiFi",
                        NetworkInterfaceType.Ethernet => "Ethernet",
                        NetworkInterfaceType.GigabitEthernet => "Ethernet",
                        NetworkInterfaceType.FastEthernetFx => "Ethernet",
                        NetworkInterfaceType.FastEthernetT => "Ethernet",
                        _ => "Network"
                    };

                    devices.Add(new ConnectedDevice
                    {
                        Name = ni.Name,
                        Description = ni.Description,
                        DeviceType = type,
                        Status = "Connected",
                        InterfaceName = ni.Name
                    });
                }

                // Bluetooth devices via WMI
                try
                {
                    using var searcher = new ManagementObjectSearcher(
                        "SELECT * FROM Win32_PnPEntity WHERE DeviceID LIKE 'BTHENUM%' AND Status = 'OK'");
                    foreach (ManagementObject obj in searcher.Get())
                    {
                        var name = obj["Name"]?.ToString() ?? "";
                        if (!string.IsNullOrWhiteSpace(name))
                        {
                            devices.Add(new ConnectedDevice
                            {
                                Name = name,
                                Description = "Bluetooth Device",
                                DeviceType = "Bluetooth",
                                Status = "Connected",
                                InterfaceName = ""
                            });
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Bluetooth enumeration error: {ex.Message}");
                }

                return devices;
            });
        }

        public async Task<List<WifiDisconnectEvent>> GetWifiDisconnectEventsAsync(int maxEvents = 15)
        {
            return await Task.Run(() =>
            {
                var events = new List<WifiDisconnectEvent>();
                try
                {
                    // Event ID 8003 = disconnected from wireless network
                    var query = new EventLogQuery(
                        "Microsoft-Windows-WLAN-AutoConfig/Operational",
                        PathType.LogName,
                        "*[System[(EventID=8003)]]")
                    {
                        ReverseDirection = true // newest events first
                    };

                    using var reader = new EventLogReader(query);
                    int count = 0;
                    EventRecord record;

                    while ((record = reader.ReadEvent()) != null && count < maxEvents)
                    {
                        using (record)
                        {
                            try
                            {
                                // Property indices for EventID 8003:
                                // Index 3 = SSID (network name)
                                // Index 7 = reason code
                                var networkName = record.Properties.Count > 3
                                    ? record.Properties[3]?.Value?.ToString() ?? "Unknown"
                                    : "Unknown";
                                var reasonCode = record.Properties.Count > 7
                                    ? record.Properties[7]?.Value?.ToString() ?? "0"
                                    : "0";

                                events.Add(new WifiDisconnectEvent
                                {
                                    Timestamp = record.TimeCreated ?? DateTime.Now,
                                    NetworkName = networkName,
                                    Reason = TranslateDisconnectReason(reasonCode)
                                });
                                count++;
                            }
                            catch (Exception ex)
                            {
                                Debug.WriteLine($"WiFi event parse error: {ex.Message}");
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"WiFi event log error: {ex.Message}");
                }

                return events.OrderByDescending(e => e.Timestamp).ToList();
            });
        }

        public async Task DisconnectWifiAsync(string interfaceName)
        {
            await Task.Run(() =>
            {
                try
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName = "netsh",
                        Arguments = $"wlan disconnect interface=\"{interfaceName}\"",
                        CreateNoWindow = true,
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true
                    };
                    using var process = Process.Start(psi);
                    process?.WaitForExit(5000);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"WiFi disconnect error: {ex.Message}");
                }
            });
        }

        private void AnalyzeConnectionRisk(NetworkConnection connection)
        {
            var risks = new List<string>();

            if (_riskyPorts.Contains(connection.LocalPort))
            {
                risks.Add($"Risky port {connection.LocalPort} ({GetServiceName(connection.LocalPort)})");
                connection.RiskLevel = "High";
            }

            if (_riskyPorts.Contains(connection.RemotePort))
            {
                risks.Add($"Connecting to risky remote port {connection.RemotePort}");
                connection.RiskLevel = connection.RiskLevel == "High" ? "High" : "Medium";
            }

            if (!string.IsNullOrEmpty(connection.RemoteAddress))
            {
                if ((connection.RemotePort != 80 && connection.RemotePort != 443) &&
                    connection.RemotePort > 1024 && connection.RemotePort < 49152)
                {
                    risks.Add("Non-standard port usage");
                    if (string.IsNullOrEmpty(connection.RiskLevel))
                        connection.RiskLevel = "Low";
                }
            }

            connection.SecurityRisk = risks.Any()
                ? string.Join(", ", risks)
                : "No known risks";

            if (string.IsNullOrEmpty(connection.RiskLevel))
                connection.RiskLevel = "Low";
        }

        private void CheckForSecurityRisks(List<NetworkConnection> connections, List<PortScanResult> openPorts)
        {
            var riskyOpenPorts = openPorts.Where(p => p.IsKnownRisky).ToList();
            if (riskyOpenPorts.Any())
            {
                // Build a stable key so we only alert once per unique set of risky ports
                var key = "risky_ports:" + string.Join(",", riskyOpenPorts.Select(p => p.Port).OrderBy(p => p));
                if (_firedAlerts.Add(key))
                {
                    var alert = $"⚠️ {riskyOpenPorts.Count} potentially risky port(s) open: " +
                               string.Join(", ", riskyOpenPorts.Select(p => $"{p.Port} ({p.ServiceName})"));
                    SecurityAlertRaised?.Invoke(this, alert);
                }
            }

            if (connections.Count > 100)
            {
                const string key = "high_connection_count";
                if (_firedAlerts.Add(key))
                    SecurityAlertRaised?.Invoke(this, $"⚠️ High number of active connections: {connections.Count}");
            }

            var unusualConnections = connections.Where(c =>
                c.RemotePort > 0 &&
                c.RemotePort != 80 &&
                c.RemotePort != 443 &&
                c.RemotePort < 1024).ToList();

            if (unusualConnections.Count > 5)
            {
                const string key = "unusual_low_ports";
                if (_firedAlerts.Add(key))
                    SecurityAlertRaised?.Invoke(this, "⚠️ Multiple connections to unusual low-numbered ports detected");
            }
        }

        private string TranslateDisconnectReason(string reasonCode)
        {
            // WLAN disconnect reason codes (from IEEE 802.11)
            return reasonCode switch
            {
                "0" => "Disconnected normally",
                "1" => "Unspecified reason",
                "2" => "Previous authentication no longer valid",
                "3" => "Deauthenticated — station left",
                "4" => "Disassociated due to inactivity",
                "5" => "Too many associated stations",
                "6" => "Class 2 frame from unauthenticated station",
                "7" => "Class 3 frame from unassociated station",
                "8" => "Disassociated — station left BSS",
                "15" => "4-way handshake timeout (wrong password?)",
                "16" => "Group key handshake timeout",
                "17" => "Information element mismatch",
                "23" => "802.1X authentication failed",
                "34" => "TDLS teardown due to poor link",
                "36" => "Disassociated: poor channel conditions",
                _ => $"Code {reasonCode}"
            };
        }

        private void InitializeWellKnownPorts()
        {
            _wellKnownPorts[20] = "FTP Data";
            _wellKnownPorts[21] = "FTP Control";
            _wellKnownPorts[22] = "SSH";
            _wellKnownPorts[23] = "Telnet";
            _wellKnownPorts[25] = "SMTP";
            _wellKnownPorts[53] = "DNS";
            _wellKnownPorts[80] = "HTTP";
            _wellKnownPorts[110] = "POP3";
            _wellKnownPorts[143] = "IMAP";
            _wellKnownPorts[443] = "HTTPS";
            _wellKnownPorts[445] = "SMB";
            _wellKnownPorts[3389] = "RDP";
            _wellKnownPorts[5900] = "VNC";
            _wellKnownPorts[8080] = "HTTP Proxy";
        }

        private void InitializeRiskyPorts()
        {
            _riskyPorts.Add(23);    // Telnet - unencrypted
            _riskyPorts.Add(445);   // SMB - ransomware vector
            _riskyPorts.Add(3389);  // RDP - brute force target
            _riskyPorts.Add(5900);  // VNC - often unsecured
            _riskyPorts.Add(135);   // RPC - exploit vector
            _riskyPorts.Add(139);   // NetBIOS - security risk
            _riskyPorts.Add(1433);  // SQL Server - attack target
            _riskyPorts.Add(3306);  // MySQL - attack target
            _riskyPorts.Add(5432);  // PostgreSQL - attack target
        }

        private string GetServiceName(int port)
        {
            return _wellKnownPorts.TryGetValue(port, out var name)
                ? name
                : $"Port {port}";
        }

        private string GetRiskDescription(int port)
        {
            return port switch
            {
                23 => "Telnet uses unencrypted communication",
                445 => "SMB is a common ransomware attack vector",
                3389 => "RDP is frequently targeted for brute force attacks",
                5900 => "VNC is often left unsecured",
                135 => "RPC can be exploited by malware",
                139 => "NetBIOS has known security vulnerabilities",
                1433 => "SQL Server is a common attack target",
                3306 => "MySQL is frequently targeted by attackers",
                5432 => "PostgreSQL can be a security risk if exposed",
                _ => "Potentially risky port"
            };
        }
    }
}
