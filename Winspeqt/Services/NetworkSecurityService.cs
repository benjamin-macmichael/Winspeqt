using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Threading.Tasks;
using Winspeqt.Models;

namespace Winspeqt.Services
{
    public class NetworkSecurityService
    {
        public async Task<NetworkInfo> GetNetworkInfoAsync()
        {
            return await Task.Run(() =>
            {
                try
                {
                    var networkInfo = new NetworkInfo
                    {
                        NetworkName = "Unknown Network",
                        SecurityType = "Unknown",
                        NetworkBand = "Unknown",
                        SignalStrength = 0,
                        SignalQuality = "Unknown",
                        IsSecure = false,
                        ConnectedDevicesCount = 0,
                        IpAddress = "0.0.0.0",
                        MacAddress = "00:00:00:00:00:00"
                    };

                    // Get active network interface
                    var activeInterface = NetworkInterface.GetAllNetworkInterfaces()
                        .FirstOrDefault(ni => ni.OperationalStatus == OperationalStatus.Up &&
                                            (ni.NetworkInterfaceType == NetworkInterfaceType.Wireless80211 ||
                                             ni.NetworkInterfaceType == NetworkInterfaceType.Ethernet));

                    if (activeInterface != null)
                    {
                        networkInfo.NetworkName = activeInterface.Name;
                        networkInfo.MacAddress = activeInterface.GetPhysicalAddress().ToString();

                        // Get IP address
                        var ipProperties = activeInterface.GetIPProperties();
                        var ipv4Address = ipProperties.UnicastAddresses
                            .FirstOrDefault(addr => addr.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork);

                        if (ipv4Address != null)
                        {
                            networkInfo.IpAddress = ipv4Address.Address.ToString();
                        }

                        // For WiFi, get more details (this is simplified - real implementation would use Native WiFi API)
                        if (activeInterface.NetworkInterfaceType == NetworkInterfaceType.Wireless80211)
                        {
                            // Simulated data - in real app, you'd use Windows.Devices.WiFi or Native WiFi API
                            networkInfo.SecurityType = "WPA2-Personal"; // Default assumption
                            networkInfo.NetworkBand = "2.4GHz"; // Would need to detect actual band
                            networkInfo.SignalStrength = 75; // Would get from WiFi API
                            networkInfo.SignalQuality = GetSignalQuality(networkInfo.SignalStrength);
                            networkInfo.IsSecure = networkInfo.SecurityType.Contains("WPA");
                            networkInfo.ConnectedDevicesCount = 5; // Would need router API access
                        }
                        else
                        {
                            // Wired connection
                            networkInfo.SecurityType = "Ethernet (Secure)";
                            networkInfo.NetworkBand = "Wired";
                            networkInfo.SignalStrength = 100;
                            networkInfo.SignalQuality = "Excellent";
                            networkInfo.IsSecure = true;
                            networkInfo.ConnectedDevicesCount = 1;
                        }
                    }

                    return networkInfo;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error getting network info: {ex.Message}");
                    return new NetworkInfo
                    {
                        NetworkName = "Error",
                        SecurityType = "Unknown",
                        IsSecure = false
                    };
                }
            });
        }

        public async Task<List<OpenPort>> GetOpenPortsAsync()
        {
            return await Task.Run(() =>
            {
                var openPorts = new List<OpenPort>();

                try
                {
                    // Get active TCP connections
                    var properties = IPGlobalProperties.GetIPGlobalProperties();
                    var tcpConnections = properties.GetActiveTcpListeners();

                    var portRisks = GetPortRiskDatabase();

                    foreach (var endpoint in tcpConnections)
                    {
                        var port = endpoint.Port;

                        // Skip common safe ports that would clutter the list
                        if (port < 1024 && !IsCommonRiskyPort(port))
                            continue;

                        var riskInfo = portRisks.ContainsKey(port)
                            ? portRisks[port]
                            : ("Unknown Service", PortRiskLevel.Low, "Unknown service on this port");

                        openPorts.Add(new OpenPort
                        {
                            PortNumber = port,
                            Protocol = "TCP",
                            Service = riskInfo.Item1,
                            RiskLevel = riskInfo.Item2,
                            Description = riskInfo.Item3
                        });
                    }

                    return openPorts.OrderByDescending(p => p.RiskLevel).ThenBy(p => p.PortNumber).ToList();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error getting open ports: {ex.Message}");
                    return openPorts;
                }
            });
        }

        public async Task<List<SecurityVulnerability>> GetSecurityVulnerabilitiesAsync(NetworkInfo networkInfo, List<OpenPort> openPorts)
        {
            return await Task.Run(() =>
            {
                var vulnerabilities = new List<SecurityVulnerability>();

                // Check network security
                if (!networkInfo.IsSecure)
                {
                    vulnerabilities.Add(new SecurityVulnerability
                    {
                        Title = "Unsecured Network Connection",
                        Description = "You are connected to an unsecured network without encryption.",
                        Severity = "Critical",
                        RecommendedAction = "Avoid accessing sensitive information. Use a VPN or switch to a secure network."
                    });
                }

                if (networkInfo.SecurityType.Contains("WEP") || networkInfo.SecurityType == "Open")
                {
                    vulnerabilities.Add(new SecurityVulnerability
                    {
                        Title = "Weak Network Encryption",
                        Description = "Your network uses outdated or weak encryption (WEP or Open).",
                        Severity = "Critical",
                        RecommendedAction = "Update your router to use WPA2 or WPA3 encryption."
                    });
                }

                if (networkInfo.SecurityType.Contains("WPA2") && !networkInfo.SecurityType.Contains("WPA3"))
                {
                    vulnerabilities.Add(new SecurityVulnerability
                    {
                        Title = "Consider Upgrading to WPA3",
                        Description = "Your network uses WPA2. WPA3 offers improved security.",
                        Severity = "Low",
                        RecommendedAction = "Check if your router supports WPA3 and upgrade if possible."
                    });
                }

                // Check for risky open ports
                var riskyPorts = openPorts.Where(p => p.RiskLevel >= PortRiskLevel.High).ToList();
                if (riskyPorts.Any())
                {
                    vulnerabilities.Add(new SecurityVulnerability
                    {
                        Title = $"{riskyPorts.Count} High-Risk Port(s) Open",
                        Description = $"Ports {string.Join(", ", riskyPorts.Take(3).Select(p => p.PortNumber))} are open and may pose security risks.",
                        Severity = riskyPorts.Any(p => p.RiskLevel == PortRiskLevel.Critical) ? "Critical" : "High",
                        RecommendedAction = "Close unnecessary ports or ensure they are protected by a firewall."
                    });
                }

                if (networkInfo.SignalStrength < 30)
                {
                    vulnerabilities.Add(new SecurityVulnerability
                    {
                        Title = "Weak Network Signal",
                        Description = "Poor signal strength may cause connection drops and security issues.",
                        Severity = "Medium",
                        RecommendedAction = "Move closer to your router or consider a WiFi extender."
                    });
                }

                return vulnerabilities.OrderByDescending(v => v.Severity).ToList();
            });
        }

        private string GetSignalQuality(int signalStrength)
        {
            if (signalStrength >= 75) return "Excellent";
            if (signalStrength >= 50) return "Good";
            if (signalStrength >= 25) return "Fair";
            return "Poor";
        }

        private bool IsCommonRiskyPort(int port)
        {
            var riskyPorts = new[] { 21, 22, 23, 25, 80, 110, 135, 139, 143, 443, 445, 3389 };
            return riskyPorts.Contains(port);
        }

        private Dictionary<int, (string, PortRiskLevel, string)> GetPortRiskDatabase()
        {
            return new Dictionary<int, (string, PortRiskLevel, string)>
            {
                { 21, ("FTP", PortRiskLevel.High, "File Transfer Protocol - unencrypted, often exploited") },
                { 22, ("SSH", PortRiskLevel.Low, "Secure Shell - generally safe if properly configured") },
                { 23, ("Telnet", PortRiskLevel.Critical, "Unencrypted remote access - highly vulnerable") },
                { 25, ("SMTP", PortRiskLevel.Medium, "Email server - can be exploited for spam") },
                { 80, ("HTTP", PortRiskLevel.Medium, "Web server - unencrypted web traffic") },
                { 110, ("POP3", PortRiskLevel.Medium, "Email retrieval - unencrypted") },
                { 135, ("RPC", PortRiskLevel.High, "Windows RPC - commonly exploited") },
                { 139, ("NetBIOS", PortRiskLevel.High, "Windows file sharing - vulnerable to attacks") },
                { 143, ("IMAP", PortRiskLevel.Medium, "Email access - unencrypted") },
                { 443, ("HTTPS", PortRiskLevel.Low, "Secure web server - encrypted") },
                { 445, ("SMB", PortRiskLevel.Critical, "Windows file sharing - major attack vector") },
                { 3389, ("RDP", PortRiskLevel.High, "Remote Desktop - frequent brute-force target") },
                { 5900, ("VNC", PortRiskLevel.High, "Remote desktop access - often insecure") },
                { 8080, ("HTTP-Alt", PortRiskLevel.Medium, "Alternative web server port") }
            };
        }
    }
}