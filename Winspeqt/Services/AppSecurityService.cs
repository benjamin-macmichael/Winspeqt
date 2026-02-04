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
            _httpClient.Timeout = TimeSpan.FromSeconds(10);
        }

        public async Task<List<AppSecurityInfo>> ScanInstalledAppsAsync()
        {
            return await Task.Run(() =>
            {
                var apps = new List<AppSecurityInfo>();

                apps.AddRange(ScanRegistryKey(Registry.LocalMachine, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall"));
                apps.AddRange(ScanRegistryKey(Registry.LocalMachine, @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall"));
                apps.AddRange(ScanRegistryKey(Registry.CurrentUser, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall"));

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
                    CheckAppVersionFallback(app);
                }
            });

            await Task.WhenAll(tasks);
        }

        private async Task CheckSingleAppVersionAsync(AppSecurityInfo app)
        {
            string wingetVersion = null;
            string chocolateyVersion = null;
            string wingetId = null;
            string chocolateyId = null;
            int wingetScore = 0;

            // Try direct mapping first (WinGet)
            wingetId = TryGetWinGetId(app.AppName);

            if (!string.IsNullOrEmpty(wingetId))
            {
                wingetVersion = await GetLatestVersionFromWinGetAsync(wingetId);
                if (!string.IsNullOrEmpty(wingetVersion))
                {
                    wingetScore = 95; // High confidence for direct match
                }
            }

            // If no direct match, try searching WinGet
            if (string.IsNullOrEmpty(wingetVersion))
            {
                var searchResult = await SearchWinGetAsync(app.AppName, app.Publisher);
                if (searchResult != null)
                {
                    wingetVersion = searchResult.LatestVersion;
                    wingetId = searchResult.WinGetId;
                    wingetScore = searchResult.MatchScore;
                }
            }

            // Also check Chocolatey for validation
            var chocoResult = await SearchChocolateyAsync(app.AppName, app.Publisher);
            if (chocoResult != null)
            {
                chocolateyVersion = chocoResult.LatestVersion;
                chocolateyId = chocoResult.WinGetId; // We reuse this field for Choco ID
            }

            // Store the IDs
            app.WinGetId = wingetId;
            app.ChocolateyId = chocolateyId;

            // Determine best version and confidence
            if (!string.IsNullOrEmpty(wingetVersion) && !string.IsNullOrEmpty(chocolateyVersion))
            {
                // Both sources agree
                if (wingetVersion == chocolateyVersion)
                {
                    app.LatestVersion = wingetVersion;
                    app.ConfidenceScore = 98; // Very high confidence
                    app.DataSource = $"✓ Verified by both WinGet and Chocolatey (v{wingetVersion})";
                }
                // Both sources have data but versions differ
                else
                {
                    // Use the newer version
                    var comparison = CompareVersions(wingetVersion, chocolateyVersion);
                    if (comparison >= 0)
                    {
                        app.LatestVersion = wingetVersion;
                        app.DataSource = $"⚠️ Sources differ - Using WinGet: {wingetVersion} | Chocolatey: {chocolateyVersion}";
                    }
                    else
                    {
                        app.LatestVersion = chocolateyVersion;
                        app.DataSource = $"⚠️ Sources differ - Using Chocolatey: {chocolateyVersion} | WinGet: {wingetVersion}";
                    }
                    app.ConfidenceScore = 75; // Medium confidence when sources disagree
                }
            }
            // Only WinGet has data
            else if (!string.IsNullOrEmpty(wingetVersion))
            {
                app.LatestVersion = wingetVersion;
                app.ConfidenceScore = wingetScore;
                app.DataSource = wingetScore >= 90
                    ? "WinGet (direct match) - Chocolatey: not found"
                    : $"WinGet (search match: {wingetScore}%) - Chocolatey: not found";
            }
            // Only Chocolatey has data
            else if (!string.IsNullOrEmpty(chocolateyVersion))
            {
                app.LatestVersion = chocolateyVersion;
                app.ConfidenceScore = chocoResult.MatchScore;
                app.DataSource = $"Chocolatey (match: {chocoResult.MatchScore}%) - WinGet: not found";
            }
            // No data from either source
            else
            {
                CheckAppVersionFallback(app);
                return;
            }

            app.UpdateInstructions = GenerateGenericUpdateInstructions(app.AppName);
            CompareAndSetStatus(app);
        }

        private async Task<string> GetLatestVersionFromWinGetAsync(string wingetId)
        {
            try
            {
                var url = $"https://api.winget.run/v2/packages/{wingetId}";

                System.Diagnostics.Debug.WriteLine($"Fetching from: {url}");

                var response = await _httpClient.GetAsync(url);

                System.Diagnostics.Debug.WriteLine($"Response status: {response.StatusCode}");

                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    System.Diagnostics.Debug.WriteLine($"Response content: {content.Substring(0, Math.Min(200, content.Length))}...");

                    var doc = JsonDocument.Parse(content);

                    if (doc.RootElement.TryGetProperty("Packages", out var packages) && packages.GetArrayLength() > 0)
                    {
                        var firstPackage = packages[0];
                        if (firstPackage.TryGetProperty("Versions", out var versions) && versions.GetArrayLength() > 0)
                        {
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
            }

            return null;
        }

        private async Task<SearchResult> SearchWinGetAsync(string appName, string publisher)
        {
            try
            {
                var searchQuery = CleanAppNameForSearch(appName);
                var url = $"https://api.winget.run/v2/packages?query={Uri.EscapeDataString(searchQuery)}&take=5";

                System.Diagnostics.Debug.WriteLine($"Searching WinGet for: {searchQuery}");

                var response = await _httpClient.GetAsync(url);

                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    var doc = JsonDocument.Parse(content);

                    if (doc.RootElement.TryGetProperty("Packages", out var packages) && packages.GetArrayLength() > 0)
                    {
                        var bestMatch = FindBestMatch(packages, appName, publisher);

                        if (bestMatch != null)
                        {
                            System.Diagnostics.Debug.WriteLine($"Found match: {bestMatch.WinGetId} -> {bestMatch.LatestVersion}");
                            return bestMatch;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error searching WinGet: {ex.Message}");
            }

            return null;
        }

        private string CleanAppNameForSearch(string appName)
        {
            var cleaned = appName
                .Replace(" (x64)", "")
                .Replace(" (x86)", "")
                .Replace(" (64-bit)", "")
                .Replace(" (32-bit)", "")
                .Replace(" 64-bit", "")
                .Replace(" 32-bit", "")
                .Trim();

            var parts = cleaned.Split(' ');
            if (parts.Length > 1 && (parts[^1].All(c => char.IsDigit(c) || c == '.') ||
                                     int.TryParse(parts[^1], out _)))
            {
                cleaned = string.Join(" ", parts.Take(parts.Length - 1));
            }

            return cleaned;
        }

        private SearchResult FindBestMatch(JsonElement packages, string appName, string publisher)
        {
            var appNameLower = appName.ToLower();
            var publisherLower = publisher.ToLower();

            SearchResult bestMatch = null;
            int bestScore = 0;

            for (int i = 0; i < packages.GetArrayLength(); i++)
            {
                var package = packages[i];

                if (!package.TryGetProperty("Id", out var idProp) ||
                    !package.TryGetProperty("Latest", out var latestProp))
                    continue;

                var packageId = idProp.GetString();
                var packageName = "";
                var packagePublisher = "";

                if (latestProp.TryGetProperty("Name", out var nameProp))
                    packageName = nameProp.GetString()?.ToLower() ?? "";

                if (latestProp.TryGetProperty("Publisher", out var pubProp))
                    packagePublisher = pubProp.GetString()?.ToLower() ?? "";

                string version = null;
                if (package.TryGetProperty("Versions", out var versions) && versions.GetArrayLength() > 0)
                {
                    version = versions[0].GetString();
                }

                if (string.IsNullOrEmpty(version))
                    continue;

                int score = 0;

                if (packageName == appNameLower)
                    score += 100;
                else if (packageName.Contains(appNameLower) || appNameLower.Contains(packageName))
                    score += 50;
                else
                {
                    var appWords = appNameLower.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    var pkgWords = packageName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    var commonWords = appWords.Intersect(pkgWords).Count();
                    score += commonWords * 20;
                }

                if (!string.IsNullOrEmpty(publisherLower) && !string.IsNullOrEmpty(packagePublisher))
                {
                    if (packagePublisher == publisherLower)
                        score += 50;
                    else if (packagePublisher.Contains(publisherLower) || publisherLower.Contains(packagePublisher))
                        score += 25;
                }

                if (score >= 50 && score > bestScore)
                {
                    bestMatch = new SearchResult
                    {
                        LatestVersion = version,
                        WinGetId = packageId,
                        MatchScore = score
                    };
                    bestScore = score;
                }
            }

            return bestMatch;
        }

        private async Task<SearchResult> SearchChocolateyAsync(string appName, string publisher)
        {
            try
            {
                var searchQuery = CleanAppNameForSearch(appName);

                // Use simpler Chocolatey search - just query parameter, no complex filters
                var url = $"https://community.chocolatey.org/api/v2/Packages()?$filter=IsLatestVersion&$orderby=DownloadCount desc&$top=10&searchTerm='{Uri.EscapeDataString(searchQuery)}'";

                System.Diagnostics.Debug.WriteLine($"Searching Chocolatey for: {searchQuery}");
                System.Diagnostics.Debug.WriteLine($"Chocolatey URL: {url}");

                var response = await _httpClient.GetAsync(url);

                System.Diagnostics.Debug.WriteLine($"Chocolatey response status: {response.StatusCode}");

                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();

                    // Parse XML response (Chocolatey uses OData/XML)
                    var bestMatch = ParseChocolateyResponse(content, appName, publisher);

                    if (bestMatch != null)
                    {
                        System.Diagnostics.Debug.WriteLine($"Found Chocolatey match: {bestMatch.WinGetId} -> {bestMatch.LatestVersion}");
                        return bestMatch;
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine("No match found in Chocolatey response");
                    }
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"Chocolatey API error: {response.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error searching Chocolatey: {ex.Message}");
            }

            return null;
        }

        private SearchResult ParseChocolateyResponse(string xmlContent, string appName, string publisher)
        {
            try
            {
                var appNameLower = appName.ToLower();
                var publisherLower = publisher.ToLower();

                // Simple XML parsing - look for entry elements
                var entries = xmlContent.Split(new[] { "<entry>" }, StringSplitOptions.RemoveEmptyEntries);

                SearchResult bestMatch = null;
                int bestScore = 0;

                foreach (var entry in entries.Skip(1)) // Skip first split (before first entry)
                {
                    if (!entry.Contains("</entry>")) continue;

                    // Extract title
                    var titleMatch = System.Text.RegularExpressions.Regex.Match(entry, @"<title[^>]*>([^<]+)</title>");
                    if (!titleMatch.Success) continue;
                    var title = titleMatch.Groups[1].Value.ToLower();

                    // Extract version
                    var versionMatch = System.Text.RegularExpressions.Regex.Match(entry, @"<d:Version[^>]*>([^<]+)</d:Version>");
                    if (!versionMatch.Success) continue;
                    var version = versionMatch.Groups[1].Value;

                    // Extract ID
                    var idMatch = System.Text.RegularExpressions.Regex.Match(entry, @"<id>https://community\.chocolatey\.org/api/v2/Packages\(Id='([^']+)',Version='[^']+'\)</id>");
                    var packageId = idMatch.Success ? idMatch.Groups[1].Value : title;

                    // Calculate match score (similar to WinGet scoring)
                    int score = 0;

                    if (title == appNameLower)
                        score += 100;
                    else if (title.Contains(appNameLower) || appNameLower.Contains(title))
                        score += 50;
                    else
                    {
                        var appWords = appNameLower.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                        var titleWords = title.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                        var commonWords = appWords.Intersect(titleWords).Count();
                        score += commonWords * 20;
                    }

                    // Chocolatey doesn't always have publisher info in search results, so be lenient
                    if (score >= 50 && score > bestScore)
                    {
                        bestMatch = new SearchResult
                        {
                            LatestVersion = version,
                            WinGetId = packageId, // Store Chocolatey ID here
                            MatchScore = score
                        };
                        bestScore = score;
                    }
                }

                return bestMatch;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error parsing Chocolatey XML: {ex.Message}");
                return null;
            }
        }

        private string TryGetWinGetId(string appName)
        {
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
                ["OBS Studio"] = "OBSProject.OBSStudio"
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
            app.Status = SecurityStatus.Unknown;
            app.StatusMessage = "Unable to check version online right now.";
            app.UpdateInstructions = "We couldn't verify if this app is up to date. Please try scanning again later when you have a stable internet connection.\n\n" +
                                   $"You can also check for updates manually:\n" +
                                   $"1. Open {app.AppName}\n" +
                                   $"2. Look for 'Help' or 'Settings' menu\n" +
                                   $"3. Find 'Check for Updates' option\n\n" +
                                   $"Or visit the official {app.AppName} website for the latest version.";
            app.LatestVersion = "Unable to check";
            app.ConfidenceScore = 0;
            app.DataSource = "No match found";
        }

        private void CompareAndSetStatus(AppSecurityInfo app)
        {
            var comparison = CompareVersions(app.InstalledVersion, app.LatestVersion);

            if (comparison < 0)
            {
                // Determine if this is a critical update
                bool isCritical = false;

                // Option 3: Apps where ANY outdated version is critical (browsers, security software)
                var alwaysCriticalApps = new[]
                {
                    "google chrome",
                    "mozilla firefox",
                    "microsoft edge",
                    "brave",
                    "opera",
                    "safari",
                    "windows defender",
                    "malwarebytes",
                    "avg antivirus",
                    "avast",
                    "norton",
                    "mcafee",
                    "bitdefender",
                    "kaspersky"
                };

                var appNameLower = app.AppName.ToLower();
                if (alwaysCriticalApps.Any(critical => appNameLower.Contains(critical)))
                {
                    isCritical = true;
                }

                // Option 2: Check if version gap is 2+ major versions
                if (!isCritical)
                {
                    var versionGap = CalculateVersionGap(app.InstalledVersion, app.LatestVersion);
                    if (versionGap >= 2)
                    {
                        isCritical = true;
                    }
                }

                // Set status based on criticality
                if (isCritical)
                {
                    app.Status = SecurityStatus.Critical;
                    app.StatusMessage = "Critical update needed - update immediately for security!";
                }
                else
                {
                    app.Status = SecurityStatus.Outdated;
                    app.StatusMessage = "A newer version is available.";
                }

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

        private int CalculateVersionGap(string installedVersion, string latestVersion)
        {
            try
            {
                var v1 = CleanVersion(installedVersion);
                var v2 = CleanVersion(latestVersion);

                var parts1 = v1.Split('.').Select(p => int.TryParse(p, out var n) ? n : 0).ToArray();
                var parts2 = v2.Split('.').Select(p => int.TryParse(p, out var n) ? n : 0).ToArray();

                // Compare major version (first number)
                if (parts1.Length > 0 && parts2.Length > 0)
                {
                    return parts2[0] - parts1[0];
                }

                return 0;
            }
            catch
            {
                return 0;
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
                // Microsoft development tools
                "microsoft visual c++",
                "microsoft .net",
                "asp.net core",
                "shared framework",
                "targeting pack",
                "windows software development kit",
                "windows sdk",
                "visual studio",
                "vs_",
                "vcpp",
                "entity framework",
                "microsoft.net.workload",
                "microsoft.net.sdk",
                "aspire.manifest",
                "emscripten.manifest",
                "mono.toolchain",
                "testplatform",
                "msi development tools",
                
                // System runtimes and redistributables
                "redistributable",
                "runtime",
                "vcredist",
                "directx",
                "visual c++ library",
                "universal crt",
                
                // Windows components
                "windows app certification kit",
                "windows desktop extension sdk",
                "windows iot extension sdk",
                "windows mobile extension sdk",
                "windows team extension sdk",
                "winrt intellisense",
                "kits configuration installer",
                "sdk arm additions",
                
                // Microsoft Edge/Browser components
                "microsoft edge update",
                "microsoft edge webview",
                
                // Development/Debug tools
                "diagnosticshub",
                "icecap_collection",
                "intellitraceprofilerproxy",
                "clickonce",
                "filetracker",
                "winappdeploy",
                "application verifier",
                
                // Office components (managed by Office)
                "office 16 click-to-run",
                "microsoft office",
                "click-to-run extensibility",
                "click-to-run licensing",
                
                // Driver and hardware utilities (vendor-managed)
                "nvidia",
                "amd",
                "intel driver",
                "intel graphics",
                "realtek",
                "qualcomm",
                
                // OEM bloatware/utilities
                "lenovo vantage",
                "lenovo system",
                "lenovo smart",
                "lenovo now",
                "dell supportassist",
                "dell update",
                "hp support",
                "hp system event",
                "asus",
                "acer",
                
                // Game launcher prerequisites (managed by launcher)
                "launcher prerequisites",
                "epic games launcher prerequisites",
                "battle.net helper",
                
                // Windows system services
                "windows subsystem",
                "app installer",
                "windows toolscorepkg",
                "minion", // Windows Minion system service
                
                // Update services (managed automatically)
                "update health tools",
                "google update",
                "adobe refresh manager",
                
                // Java (often dependency, not user app)
                "java se development kit",
                "java(tm)",
                
                // System fonts and UI
                "vs_coreeditorfonts",
                "font",
                
                // Background services
                "service host",
                "background task",
                "helper",
                
                // Database components (often dependencies)
                "mysql connector",
                "sql server",
                "clr types",
                
                // Compatibility and legacy
                "compatibility database",
                "shim infrastructure"
            };

            var appNameLower = appName.ToLower();
            return systemKeywords.Any(keyword => appNameLower.Contains(keyword));
        }

        public void Dispose()
        {
            _httpClient?.Dispose();
        }

        private class SearchResult
        {
            public string LatestVersion { get; set; }
            public string WinGetId { get; set; }
            public int MatchScore { get; set; }
        }
    }
}