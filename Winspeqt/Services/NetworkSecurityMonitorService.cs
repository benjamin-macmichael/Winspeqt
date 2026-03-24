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
    public class NetworkSecurityMonitorService
    {
        private Timer? _monitoringTimer = null;
        private readonly Dictionary<string, NetworkTrafficStats> _previousStats = [];
        private readonly Dictionary<int, string> _wellKnownPorts = [];
        private readonly HashSet<int> _riskyPorts = [];

        // Track which alert keys have already fired this session to prevent spam
        private readonly HashSet<string> _firedAlerts = [];

        public event EventHandler<List<NetworkConnection>>? ConnectionsUpdated;
        public event EventHandler<List<NetworkTrafficStats>>? TrafficStatsUpdated;
        public event EventHandler<List<PortScanResult>>? OpenPortsDetected;
        public event EventHandler<List<ConnectedDevice>>? ConnectedDevicesUpdated;
        public event EventHandler<string>? SecurityAlertRaised;

        public NetworkSecurityMonitorService()
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
                foreach (var conn in properties.GetActiveTcpConnections())
                {
                    connections.Add(new NetworkConnection
                    {
                        Protocol = "TCP",
                        LocalAddress = conn.LocalEndPoint.Address.ToString(),
                        LocalPort = conn.LocalEndPoint.Port,
                        RemoteAddress = conn.RemoteEndPoint.Address.ToString(),
                        RemotePort = conn.RemoteEndPoint.Port,
                        State = conn.State.ToString(),
                        DetectedAt = DateTime.Now
                    });
                }

                // TCP Listeners
                foreach (var listener in properties.GetActiveTcpListeners())
                {
                    connections.Add(new NetworkConnection
                    {
                        Protocol = "TCP",
                        LocalAddress = listener.Address.ToString(),
                        LocalPort = listener.Port,
                        State = "LISTENING",
                        DetectedAt = DateTime.Now
                    });
                }

                // UDP Listeners
                foreach (var listener in properties.GetActiveUdpListeners())
                {
                    connections.Add(new NetworkConnection
                    {
                        Protocol = "UDP",
                        LocalAddress = listener.Address.ToString(),
                        LocalPort = listener.Port,
                        State = "LISTENING",
                        DetectedAt = DateTime.Now
                    });
                }

                // Enrich with PID + process name from netstat
                var pidMap = GetConnectionPidMap();
                var processCache = new Dictionary<int, string>();

                foreach (var conn in connections)
                {
                    var key = $"{conn.LocalAddress}:{conn.LocalPort}|{conn.RemoteAddress}:{conn.RemotePort}";
                    if (!pidMap.TryGetValue(key, out int pid))
                    {
                        // Fallback: match on local endpoint only (covers listeners)
                        var localKey = $"{conn.LocalAddress}:{conn.LocalPort}|";
                        foreach (var k in pidMap.Keys)
                        {
                            if (k.StartsWith(localKey, StringComparison.OrdinalIgnoreCase))
                            {
                                pid = pidMap[k];
                                break;
                            }
                        }
                    }

                    if (pid > 0)
                    {
                        conn.ProcessId = pid;
                        if (!processCache.TryGetValue(pid, out var name))
                        {
                            try { name = Process.GetProcessById(pid).ProcessName; }
                            catch { name = $"PID {pid}"; }
                            processCache[pid] = name;
                        }
                        conn.ProcessName = name;
                    }

                    conn.RemoteServiceName = GetServiceName(conn.RemotePort);
                    AnalyzeConnectionRisk(conn);
                }

                return connections.OrderBy(c => c.LocalPort).ToList();
            });
        }

        private Dictionary<string, int> GetConnectionPidMap()
        {
            var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "netstat",
                    Arguments = "-ano",
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardOutput = true
                };
                using var proc = Process.Start(psi);
                var output = proc?.StandardOutput.ReadToEnd() ?? "";
                proc?.WaitForExit(5000);

                foreach (var line in output.Split('\n'))
                {
                    var parts = line.Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length < 4) continue;
                    if (parts[0] != "TCP" && parts[0] != "UDP") continue;
                    if (!int.TryParse(parts[^1], out int pid) || pid == 0) continue;

                    var local = NormalizeNetstatEndpoint(parts[1]);
                    // TCP has State column; UDP goes straight to PID
                    var remote = parts[0] == "TCP" && parts.Length >= 5
                        ? NormalizeNetstatEndpoint(parts[2])
                        : "0.0.0.0:0";

                    map[$"{local}|{remote}"] = pid;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"netstat PID map error: {ex.Message}");
            }
            return map;
        }

        private static string NormalizeNetstatEndpoint(string ep)
        {
            if (ep == "*:*") return "0.0.0.0:0";
            // IPv6 bracket form: [::]:135 or [2001:db8::1]:443
            if (ep.StartsWith("[", StringComparison.Ordinal))
            {
                var close = ep.IndexOf(']');
                if (close >= 0)
                {
                    var addr = ep.Substring(1, close - 1);
                    var port = ep.Substring(close + 2);
                    return $"{addr}:{port}";
                }
            }
            return ep;
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
                var wifiSsids = GetWifiSsidMap();

                // Network interfaces (WiFi and Ethernet) — exclude virtual adapters
                var interfaces = NetworkInterface.GetAllNetworkInterfaces()
                    .Where(ni => ni.OperationalStatus == OperationalStatus.Up
                              && ni.NetworkInterfaceType != NetworkInterfaceType.Loopback
                              && ni.NetworkInterfaceType != NetworkInterfaceType.Tunnel
                              && ni.NetworkInterfaceType != NetworkInterfaceType.Ppp
                              && !ni.Description.Contains("Virtual", StringComparison.OrdinalIgnoreCase)
                              && !ni.Description.Contains("Hyper-V", StringComparison.OrdinalIgnoreCase)
                              && !ni.Description.Contains("WAN Miniport", StringComparison.OrdinalIgnoreCase));

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

                    // For WiFi, use the SSID (e.g. "Michael's iPhone") as the display name
                    var displayName = type == "WiFi" && wifiSsids.TryGetValue(ni.Name, out var ssid)
                        ? ssid
                        : ni.Name;

                    devices.Add(new ConnectedDevice
                    {
                        Name = displayName,
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

        public async Task<List<(string Ssid, string InterfaceName)>> GetUnsecuredWifiConnectionsAsync()
        {
            return await Task.Run(() =>
            {
                var unsecured = new List<(string, string)>();
                try
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName = "netsh",
                        Arguments = "wlan show interfaces",
                        CreateNoWindow = true,
                        UseShellExecute = false,
                        RedirectStandardOutput = true
                    };
                    using var proc = Process.Start(psi);
                    var output = proc?.StandardOutput.ReadToEnd() ?? "";
                    proc?.WaitForExit(3000);

                    string? iface = null, ssid = null, auth = null, cipher = null;

                    void TryAdd()
                    {
                        if (iface != null && ssid != null && auth != null && cipher != null)
                        {
                            bool isOpen = auth.Contains("Open", StringComparison.OrdinalIgnoreCase)
                                       && cipher.Contains("None", StringComparison.OrdinalIgnoreCase);
                            if (isOpen) unsecured.Add((ssid, iface));
                        }
                    }

                    foreach (var line in output.Split('\n'))
                    {
                        var t = line.Trim();
                        if (t.StartsWith("Name") && !t.StartsWith("SSID") && t.Contains(':'))
                        {
                            TryAdd();
                            iface = t.Split(':', 2)[1].Trim();
                            ssid = auth = cipher = null;
                        }
                        else if (t.StartsWith("SSID") && !t.StartsWith("BSSID") && t.Contains(':'))
                            ssid = t.Split(':', 2)[1].Trim();
                        else if (t.StartsWith("Authentication") && t.Contains(':'))
                            auth = t.Split(':', 2)[1].Trim();
                        else if (t.StartsWith("Cipher") && t.Contains(':'))
                            cipher = t.Split(':', 2)[1].Trim();
                    }
                    TryAdd();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Unsecured WiFi check error: {ex.Message}");
                }
                return unsecured;
            });
        }

        private Dictionary<string, string> GetWifiSsidMap()
        {
            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "netsh",
                    Arguments = "wlan show interfaces",
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardOutput = true
                };
                using var proc = Process.Start(psi);
                var output = proc?.StandardOutput.ReadToEnd() ?? "";
                proc?.WaitForExit(3000);

                string? currentInterface = null;
                foreach (var line in output.Split('\n'))
                {
                    var trimmed = line.Trim();
                    if (trimmed.StartsWith("Name") && trimmed.Contains(':') && !trimmed.StartsWith("SSID"))
                        currentInterface = trimmed.Split(':', 2)[1].Trim();
                    else if (trimmed.StartsWith("SSID") && !trimmed.StartsWith("BSSID") && trimmed.Contains(':') && currentInterface != null)
                    {
                        var ssid = trimmed.Split(':', 2)[1].Trim();
                        if (!string.IsNullOrEmpty(ssid))
                            map[currentInterface] = ssid;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"SSID map error: {ex.Message}");
            }
            return map;
        }

        private string TranslateDisconnectReason(string reasonCode)
        {
            // WLAN disconnect reason codes (from IEEE 802.11)
            var description = reasonCode switch
            {
                "0" => "Disconnected normally",
                "1" => "Disconnected for an unknown reason — try reconnecting",
                "2" => "Login credentials expired — forget the network and rejoin",
                "3" => "Kicked by the router — may be out of range or router restarted",
                "4" => "Dropped due to inactivity — the router timed out your idle connection",
                "5" => "Too many devices on the network — the router kicked you to free up space",
                "6" => "Network error — device tried to send data before fully logging in",
                "7" => "Network error — device sent data before fully joining the network",
                "8" => "Left the network's coverage area (roaming or out of range)",
                "9" => "Authentication failed — forget the network and try reconnecting",
                "10" => "Power settings rejected by the router — check power adapter settings",
                "11" => "WiFi channel conflict — device and router couldn't agree on a channel",
                "12" => "Roamed to a better access point — normal on managed/enterprise networks",
                "13" => "Settings mismatch — forget the network and try reconnecting",
                "14" => "Security check failed — wrong password or potential network tampering",
                "15" => "Wrong password — the security handshake timed out; double-check your password",
                "16" => "Security key renewal failed — try reconnecting",
                "17" => "Security settings mismatch between your device and the router",
                "18" => "Encryption conflict — the router's encryption settings may need updating",
                "19" => "Encryption key mismatch — forget the network and try reconnecting",
                "20" => "Authentication method mismatch — forget the network and try reconnecting",
                "21" => "Security protocol version not supported — update router or device firmware",
                "22" => "Incompatible security features — check router security settings",
                "23" => "Enterprise login failed — check your username and password with your IT team",
                "24" => "Encryption type blocked by the network's security policy",
                "34" => "Weak signal — direct device link dropped due to poor connection quality",
                "36" => "Poor signal strength — move closer to the router or access point",
                "45" => "The access point or connected device left the network",
                _ => "Unknown reason — try reconnecting or restarting your router"
            };
            return $"Code {reasonCode} — {description}";
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
