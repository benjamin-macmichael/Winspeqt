using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
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

        public event EventHandler<List<NetworkConnection>> ConnectionsUpdated;
        public event EventHandler<List<NetworkTrafficStats>> TrafficStatsUpdated;
        public event EventHandler<List<PortScanResult>> OpenPortsDetected;
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
        }

        private async Task MonitorNetworkAsync()
        {
            try
            {
                // Monitor connections
                var connections = await GetActiveConnectionsAsync();
                ConnectionsUpdated?.Invoke(this, connections);

                // Monitor traffic
                var trafficStats = await GetNetworkTrafficStatsAsync();
                TrafficStatsUpdated?.Invoke(this, trafficStats);

                // Scan for open ports
                var openPorts = await ScanOpenPortsAsync();
                OpenPortsDetected?.Invoke(this, openPorts);

                // Check for security risks
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
                        ProcessId = 0, // Will be populated by GetExtendedTcpTable if available
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

                // Analyze risks
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

                    // Calculate rates if we have previous data
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

                // Get all listening TCP ports
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

                // Get all listening UDP ports
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

        private void AnalyzeConnectionRisk(NetworkConnection connection)
        {
            var risks = new List<string>();

            // Check for risky ports
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

            // Check for suspicious remote connections
            if (!string.IsNullOrEmpty(connection.RemoteAddress))
            {
                // Check for non-standard HTTP/HTTPS ports
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
            // Check for risky open ports
            var riskyOpenPorts = openPorts.Where(p => p.IsKnownRisky).ToList();
            if (riskyOpenPorts.Any())
            {
                var alert = $"⚠️ Warning: {riskyOpenPorts.Count} potentially risky ports are open: " +
                           string.Join(", ", riskyOpenPorts.Select(p => $"{p.Port} ({p.ServiceName})"));
                SecurityAlertRaised?.Invoke(this, alert);
            }

            // Check for unusual number of connections
            if (connections.Count > 100)
            {
                SecurityAlertRaised?.Invoke(this,
                    $"⚠️ High number of active connections detected: {connections.Count}");
            }

            // Check for connections to unusual ports
            var unusualConnections = connections.Where(c =>
                c.RemotePort > 0 &&
                c.RemotePort != 80 &&
                c.RemotePort != 443 &&
                c.RemotePort < 1024).ToList();

            if (unusualConnections.Count > 5)
            {
                SecurityAlertRaised?.Invoke(this,
                    $"⚠️ Multiple connections to unusual low ports detected");
            }
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
            // Commonly exploited or risky ports
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