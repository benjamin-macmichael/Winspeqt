using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Winspeqt.Models;

namespace Winspeqt.Services
{
    public class AppSecurityService : IDisposable
    {
        private readonly HttpClient _httpClient;

        public AppSecurityService()
        {
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "Winspeqt-SecurityScanner/1.0");
            _httpClient.Timeout = TimeSpan.FromSeconds(10); // Set reasonable timeout
        }

        public async Task<List<AppSecurityInfo>> ScanInstalledAppsAsync()
        {
            return await Task.Run(() =>
            {
                var apps = new List<AppSecurityInfo>();

                // Scan both 32-bit and 64-bit registry locations
                apps.AddRange(ScanRegistryKey(Registry.LocalMachine, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall"));
                apps.AddRange(ScanRegistryKey(Registry.LocalMachine, @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall"));
                apps.AddRange(ScanRegistryKey(Registry.CurrentUser, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall"));

                // Remove duplicates and filter
                var uniqueApps = apps
                    .Where(a => !string.IsNullOrWhiteSpace(a.AppName))
                    .Where(a => !string.IsNullOrWhiteSpace(a.InstalledVersion))
                    .GroupBy(a => a.AppName.ToLower())
                    .Select(g => g.First())
                    .Where(a => !IsSystemComponent(a.AppName))
                    .OrderBy(a => a.AppName)
                    .ToList();

                return uniqueApps;
            });
        }

        public async Task CheckAppVersionsAsync(List<AppSecurityInfo> apps)
        {
            var tasks = apps.Select(async app =>
            {
                try
                {
                    await CheckSingleAppVersionAsync(app);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error checking {app.AppName}: {ex.Message}");
                    // Fall back to local dictionary
                    CheckAppVersionFallback(app);
                }
            });

            await Task.WhenAll(tasks);
        }

        private async Task CheckSingleAppVersionAsync(AppSecurityInfo app)
        {
            // Try to get version from WinGet community repository via GitHub raw content
            var wingetId = TryGetWinGetId(app.AppName);

            if (!string.IsNullOrEmpty(wingetId))
            {
                var latestVersion = await GetLatestVersionFromWinGetAsync(wingetId);
                if (!string.IsNullOrEmpty(latestVersion))
                {
                    app.LatestVersion = latestVersion;
                    CompareAndSetStatus(app);
                    return;
                }
            }

            // Fallback to local dictionary
            CheckAppVersionFallback(app);
        }

        private async Task<string> GetLatestVersionFromWinGetAsync(string wingetId)
        {
            try
            {
                // Query the WinGet REST API (community-run, no key required)
                // Using v2 API as per their documentation
                var url = $"https://api.winget.run/v2/packages/{wingetId}";

                System.Diagnostics.Debug.WriteLine($"Fetching from: {url}");

                var response = await _httpClient.GetAsync(url);

                System.Diagnostics.Debug.WriteLine($"Response status: {response.StatusCode}");

                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    System.Diagnostics.Debug.WriteLine($"Response content: {content.Substring(0, Math.Min(200, content.Length))}...");

                    var doc = JsonDocument.Parse(content);

                    // The API returns: {"Packages":[{"Id":"...","Versions":["latest","older",...]}],"Total":1}
                    if (doc.RootElement.TryGetProperty("Packages", out var packages) && packages.GetArrayLength() > 0)
                    {
                        var firstPackage = packages[0];
                        if (firstPackage.TryGetProperty("Versions", out var versions) && versions.GetArrayLength() > 0)
                        {
                            // The first version in the array is the latest
                            var latestVersion = versions[0].GetString();
                            System.Diagnostics.Debug.WriteLine($"Found version: {latestVersion}");
                            return latestVersion;
                        }
                    }

                    System.Diagnostics.Debug.WriteLine("Could not find version in response structure");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"API returned error: {response.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error fetching from winget.run: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");
            }

            return null;
        }

        private string TryGetWinGetId(string appName)
        {
            // Map common app names to their WinGet package IDs
            var knownMappings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Google Chrome"] = "Google.Chrome",
                ["Mozilla Firefox"] = "Mozilla.Firefox",
                ["Microsoft Edge"] = "Microsoft.Edge",
                ["Visual Studio Code"] = "Microsoft.VisualStudioCode",
                ["7-Zip"] = "7zip.7zip",
                ["VLC media player"] = "VideoLAN.VLC",
                ["Discord"] = "Discord.Discord",
                ["Spotify"] = "Spotify.Spotify",
                ["Zoom"] = "Zoom.Zoom",
                ["Microsoft Teams"] = "Microsoft.Teams",
                ["Steam"] = "Valve.Steam",
                ["Adobe Acrobat"] = "Adobe.Acrobat.Reader.64-bit",
                ["Notepad++"] = "Notepad++.Notepad++",
                ["WinRAR"] = "RARLab.WinRAR",
                ["Git"] = "Git.Git",
                ["Python"] = "Python.Python.3.13",
                ["Node.js"] = "OpenJS.NodeJS",
                ["Docker Desktop"] = "Docker.DockerDesktop",
                ["OBS Studio"] = "OBSProject.OBSStudio",
                ["Slack"] = "SlackTechnologies.Slack",
                ["WhatsApp"] = "WhatsApp.WhatsApp",
                ["Telegram"] = "Telegram.TelegramDesktop",
                ["Brave"] = "Brave.Brave",
                ["Opera"] = "Opera.Opera",
                ["Vivaldi"] = "Vivaldi.Vivaldi",
                ["LibreOffice"] = "TheDocumentFoundation.LibreOffice",
                ["GIMP"] = "GIMP.GIMP",
                ["Audacity"] = "Audacity.Audacity",
                ["HandBrake"] = "HandBrake.HandBrake",
                ["qBittorrent"] = "qBittorrent.qBittorrent",
                ["FileZilla"] = "TimKosse.FileZilla.Client",
                ["PuTTY"] = "PuTTY.PuTTY",
                ["WinSCP"] = "WinSCP.WinSCP",
                ["Paint.NET"] = "dotPDN.PaintDotNet",
                ["Blender"] = "BlenderFoundation.Blender",
                ["Inkscape"] = "Inkscape.Inkscape",
                ["KeePass"] = "KeePassXCTeam.KeePassXC",
                ["Bitwarden"] = "Bitwarden.Bitwarden",
                ["1Password"] = "AgileBits.1Password"
            };

            foreach (var mapping in knownMappings)
            {
                if (appName.IndexOf(mapping.Key, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return mapping.Value;
                }
            }

            return null;
        }

        private void CheckAppVersionFallback(AppSecurityInfo app)
        {
            // Don't use outdated local dictionary - just mark as unable to check
            app.Status = SecurityStatus.Unknown;
            app.StatusMessage = "Unable to check version online right now.";
            app.UpdateInstructions = "We couldn't verify if this app is up to date. Please try scanning again later when you have a stable internet connection.\n\n" +
                                   $"You can also check for updates manually:\n" +
                                   $"1. Open {app.AppName}\n" +
                                   $"2. Look for 'Help' or 'Settings' menu\n" +
                                   $"3. Find 'Check for Updates' option\n\n" +
                                   $"Or visit the official {app.AppName} website for the latest version.";
            app.LatestVersion = "Unable to check";
        }

        private void CompareAndSetStatus(AppSecurityInfo app)
        {
            var comparison = CompareVersions(app.InstalledVersion, app.LatestVersion);

            if (comparison < 0)
            {
                app.Status = SecurityStatus.Outdated;
                app.StatusMessage = "A newer version is available.";

                // Generate generic update instructions if not already set
                if (string.IsNullOrEmpty(app.UpdateInstructions))
                {
                    app.UpdateInstructions = GenerateGenericUpdateInstructions(app.AppName);
                }
            }
            else if (comparison == 0)
            {
                app.Status = SecurityStatus.UpToDate;
                app.StatusMessage = "You have the latest version.";
                app.UpdateInstructions = "No update needed.";
            }
            else
            {
                app.Status = SecurityStatus.UpToDate;
                app.StatusMessage = "You have a newer or beta version.";
                app.UpdateInstructions = "No action needed.";
            }
        }

        private string GenerateGenericUpdateInstructions(string appName)
        {
            return $"To update {appName}:\n\n" +
                   $"1. Open {appName}\n" +
                   $"2. Look for 'Help' or 'Settings' menu\n" +
                   $"3. Find 'Check for Updates' or 'About'\n" +
                   $"4. Follow the prompts to install the latest version\n\n" +
                   $"Alternatively, visit the official {appName} website to download the latest installer.";
        }

        private List<AppSecurityInfo> ScanRegistryKey(RegistryKey root, string subKeyPath)
        {
            var apps = new List<AppSecurityInfo>();

            try
            {
                using var key = root.OpenSubKey(subKeyPath);
                if (key == null) return apps;

                foreach (var subKeyName in key.GetSubKeyNames())
                {
                    try
                    {
                        using var subKey = key.OpenSubKey(subKeyName);
                        if (subKey == null) continue;

                        var displayName = subKey.GetValue("DisplayName")?.ToString();
                        if (string.IsNullOrWhiteSpace(displayName)) continue;

                        // Skip Windows updates and system components
                        if (displayName.StartsWith("KB") ||
                            displayName.Contains("Update for") ||
                            displayName.Contains("Hotfix for"))
                            continue;

                        var version = subKey.GetValue("DisplayVersion")?.ToString();
                        var publisher = subKey.GetValue("Publisher")?.ToString();
                        var installLocation = subKey.GetValue("InstallLocation")?.ToString();
                        var installDateStr = subKey.GetValue("InstallDate")?.ToString();

                        DateTime? installDate = null;
                        if (!string.IsNullOrEmpty(installDateStr) && installDateStr.Length == 8)
                        {
                            if (DateTime.TryParseExact(installDateStr, "yyyyMMdd",
                                System.Globalization.CultureInfo.InvariantCulture,
                                System.Globalization.DateTimeStyles.None, out var date))
                            {
                                installDate = date;
                            }
                        }

                        apps.Add(new AppSecurityInfo
                        {
                            AppName = displayName,
                            InstalledVersion = version ?? "Unknown",
                            Publisher = publisher ?? "Unknown",
                            InstallLocation = installLocation ?? "Unknown",
                            InstallDate = installDate,
                            Status = SecurityStatus.Unknown
                        });
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error reading registry key {subKeyName}: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error accessing registry: {ex.Message}");
            }

            return apps;
        }

        private int CompareVersions(string version1, string version2)
        {
            try
            {
                var v1 = CleanVersion(version1);
                var v2 = CleanVersion(version2);

                var parts1 = v1.Split('.').Select(p => int.TryParse(p, out var n) ? n : 0).ToArray();
                var parts2 = v2.Split('.').Select(p => int.TryParse(p, out var n) ? n : 0).ToArray();

                int maxLength = Math.Max(parts1.Length, parts2.Length);

                for (int i = 0; i < maxLength; i++)
                {
                    int p1 = i < parts1.Length ? parts1[i] : 0;
                    int p2 = i < parts2.Length ? parts2[i] : 0;

                    if (p1 < p2) return -1;
                    if (p1 > p2) return 1;
                }

                return 0;
            }
            catch
            {
                return 0;
            }
        }

        private string CleanVersion(string version)
        {
            if (string.IsNullOrEmpty(version)) return "0";

            var cleaned = new System.Text.StringBuilder();
            foreach (char c in version)
            {
                if (char.IsDigit(c) || c == '.')
                    cleaned.Append(c);
            }

            return cleaned.ToString().Trim('.');
        }

        private bool IsSystemComponent(string appName)
        {
            var systemKeywords = new[]
            {
                "microsoft visual c++",
                "microsoft .net",
                "windows software development kit",
                "redistributable",
                "runtime",
                "microsoft edge update",
                "microsoft edge webview"
            };

            var appNameLower = appName.ToLower();
            return systemKeywords.Any(keyword => appNameLower.Contains(keyword));
        }

        public void Dispose()
        {
            _httpClient?.Dispose();
        }
    }
}