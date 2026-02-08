using Microsoft.UI.Dispatching;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using Winspeqt.Helpers;
using Winspeqt.Models;
using Winspeqt.Services;

namespace Winspeqt.ViewModels.Security
{
    public class AppSecurityViewModel : ObservableObject
    {
        private readonly AppSecurityService _securityService;
        private readonly DispatcherQueue _dispatcherQueue;
        private readonly SemaphoreSlim _scanLock = new SemaphoreSlim(1, 1);
        private Microsoft.UI.Xaml.XamlRoot _xamlRoot;

        public void SetXamlRoot(Microsoft.UI.Xaml.XamlRoot xamlRoot)
        {
            _xamlRoot = xamlRoot;
        }

        private bool _isScanning;
        public bool IsScanning
        {
            get => _isScanning;
            set => SetProperty(ref _isScanning, value);
        }

        private bool _hasScanned;
        public bool HasScanned
        {
            get => _hasScanned;
            set => SetProperty(ref _hasScanned, value);
        }

        private string _statusMessage;
        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        private double _scanProgress;
        public double ScanProgress
        {
            get => _scanProgress;
            set => SetProperty(ref _scanProgress, value);
        }

        private int _totalAppsScanned;
        public int TotalAppsScanned
        {
            get => _totalAppsScanned;
            set
            {
                if (SetProperty(ref _totalAppsScanned, value))
                {
                    OnPropertyChanged(nameof(SummaryMessage));
                }
            }
        }

        private int _outdatedAppsCount;
        public int OutdatedAppsCount
        {
            get => _outdatedAppsCount;
            set
            {
                if (SetProperty(ref _outdatedAppsCount, value))
                {
                    OnPropertyChanged(nameof(SummaryMessage));
                    OnPropertyChanged(nameof(HasIssues));
                }
            }
        }

        private int _criticalAppsCount;
        public int CriticalAppsCount
        {
            get => _criticalAppsCount;
            set
            {
                if (SetProperty(ref _criticalAppsCount, value))
                {
                    OnPropertyChanged(nameof(SummaryMessage));
                    OnPropertyChanged(nameof(HasIssues));
                }
            }
        }

        private int _upToDateAppsCount;
        public int UpToDateAppsCount
        {
            get => _upToDateAppsCount;
            set
            {
                if (SetProperty(ref _upToDateAppsCount, value))
                {
                    OnPropertyChanged(nameof(SummaryMessage));
                }
            }
        }

        private AppSecurityInfo _selectedApp;
        public AppSecurityInfo SelectedApp
        {
            get => _selectedApp;
            set => SetProperty(ref _selectedApp, value);
        }

        private string _filterOption = "All Apps";
        public string FilterOption
        {
            get => _filterOption;
            set
            {
                if (SetProperty(ref _filterOption, value))
                {
                    ApplyFilter();
                }
            }
        }

        public bool HasIssues => CriticalAppsCount > 0 || OutdatedAppsCount > 0;

        public string SummaryMessage
        {
            get
            {
                if (!HasScanned || TotalAppsScanned == 0)
                    return "Click 'Check for Updates' to check for outdated software.";

                if (CriticalAppsCount > 0)
                    return $"⚠️ {CriticalAppsCount} critical update{(CriticalAppsCount == 1 ? "" : "s")} needed! Update these apps immediately.";

                if (OutdatedAppsCount > 0)
                    return $"Found {OutdatedAppsCount} outdated app{(OutdatedAppsCount == 1 ? "" : "s")}. Consider updating when you have time.";

                return $"✓ Great! All {TotalAppsScanned} checked apps are up to date.";
            }
        }

        public ObservableCollection<AppSecurityInfo> ScannedApps { get; set; }
        public ObservableCollection<AppSecurityInfo> FilteredApps { get; set; }
        public ObservableCollection<string> FilterOptions { get; set; }

        public ICommand ScanCommand { get; }
        public ICommand OpenUpdateInstructionsCommand { get; }

        private List<AppSecurityInfo> _allApps = new List<AppSecurityInfo>();

        public AppSecurityViewModel()
        {
            _securityService = new AppSecurityService();
            _dispatcherQueue = DispatcherQueue.GetForCurrentThread();

            ScannedApps = new ObservableCollection<AppSecurityInfo>();
            FilteredApps = new ObservableCollection<AppSecurityInfo>();
            FilterOptions = new ObservableCollection<string>
            {
                "All Apps",
                "Needs Updates",
                "Critical Updates",
                "Up to Date",
                "Unknown Status"
            };

            ScanCommand = new RelayCommand(async () => await ScanAppsAsync());
            OpenUpdateInstructionsCommand = new RelayCommand<AppSecurityInfo>(async (app) => await ShowUpdateInstructionsAsync(app));

            StatusMessage = "Ready to scan your installed applications.";
        }

        private async Task ScanAppsAsync()
        {
            if (!await _scanLock.WaitAsync(0))
            {
                System.Diagnostics.Debug.WriteLine("Scan already in progress");
                return;
            }

            try
            {
                await DispatchAsync(() =>
                {
                    IsScanning = true;
                    StatusMessage = "Checking WinGet version...";
                    ScanProgress = 0;
                    ScannedApps.Clear();
                    FilteredApps.Clear();
                });

                // Check if WinGet is outdated
                var isOutdated = await CheckWinGetOutdatedAsync();
                if (isOutdated)
                {
                    await ShowWinGetOutdatedWarningAsync();
                    // If user cancels, IsScanning will already be set to false
                    if (!IsScanning)
                    {
                        return;
                    }
                }

                await DispatchAsync(() =>
                {
                    StatusMessage = "Updating package databases...";
                    ScanProgress = 0;
                });

                // Update WinGet sources first for accurate version info
                try
                {
                    System.Diagnostics.Debug.WriteLine("Starting WinGet source update...");

                    var updateProcess = new System.Diagnostics.Process
                    {
                        StartInfo = new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = "winget",
                            Arguments = "source update",
                            UseShellExecute = false,
                            CreateNoWindow = true,
                            RedirectStandardOutput = true,
                            RedirectStandardError = true
                        }
                    };

                    updateProcess.Start();

                    // Wait max 30 seconds for source update
                    var completed = updateProcess.WaitForExit(30000); // 30 seconds in milliseconds

                    if (completed)
                    {
                        System.Diagnostics.Debug.WriteLine($"WinGet source update completed with exit code: {updateProcess.ExitCode}");
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine("WinGet source update timed out after 30 seconds - continuing anyway");
                        try { updateProcess.Kill(); } catch { }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to update WinGet sources: {ex.Message}");
                    // Continue anyway - not critical if this fails
                }

                await DispatchAsync(() =>
                {
                    StatusMessage = "Scanning your installed applications...";
                });

                // Step 1: Scan registry for installed apps
                var apps = await _securityService.ScanInstalledAppsAsync();

                await DispatchAsync(() =>
                {
                    StatusMessage = $"Found {apps.Count} apps. Checking for updates online...";
                    ScanProgress = 10; // 10% after finding apps
                });

                // Step 2: Check versions in parallel with progress updates
                int checkedCount = 0;
                int total = apps.Count;

                var tasks = apps.Select(async app =>
                {
                    try
                    {
                        await _securityService.CheckSingleAppVersionAsync(app);

                        // Update progress
                        Interlocked.Increment(ref checkedCount);
                        var progress = 10 + (checkedCount * 90.0 / total); // 10% base + 90% for checking
                        await DispatchAsync(() =>
                        {
                            StatusMessage = $"Checking for updates... ({checkedCount} of {total})";
                            ScanProgress = progress;
                        });
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error checking {app.AppName}: {ex.Message}");
                        Interlocked.Increment(ref checkedCount);
                    }
                });

                await Task.WhenAll(tasks);

                _allApps = apps;

                // Calculate statistics
                var totalApps = apps.Count;
                var outdated = apps.Count(a => a.Status == SecurityStatus.Outdated);
                var critical = apps.Count(a => a.Status == SecurityStatus.Critical);
                var upToDate = apps.Count(a => a.Status == SecurityStatus.UpToDate);

                await DispatchAsync(() =>
                {
                    foreach (var app in apps)
                    {
                        ScannedApps.Add(app);
                    }

                    TotalAppsScanned = totalApps;
                    OutdatedAppsCount = outdated;
                    CriticalAppsCount = critical;
                    UpToDateAppsCount = upToDate;

                    ApplyFilter();

                    IsScanning = false;
                    HasScanned = true;
                    ScanProgress = 100;
                    StatusMessage = $"Scan complete! Found {totalApps} applications.";
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error scanning apps: {ex.Message}");
                await DispatchAsync(() =>
                {
                    IsScanning = false;
                    StatusMessage = $"Error scanning applications: {ex.Message}";
                });
            }
            finally
            {
                _scanLock.Release();
            }
        }

        private void ApplyFilter()
        {
            FilteredApps.Clear();

            IEnumerable<AppSecurityInfo> filtered = FilterOption switch
            {
                "Needs Updates" => _allApps.Where(a => a.Status == SecurityStatus.Outdated),
                "Critical Updates" => _allApps.Where(a => a.Status == SecurityStatus.Critical),
                "Up to Date" => _allApps.Where(a => a.Status == SecurityStatus.UpToDate),
                "Unknown Status" => _allApps.Where(a => a.Status == SecurityStatus.Unknown),
                _ => _allApps
            };

            foreach (var app in filtered)
            {
                FilteredApps.Add(app);
            }
        }

        private async Task ShowUpdateInstructionsAsync(AppSecurityInfo app)
        {
            if (app == null || _xamlRoot == null) return;

            await DispatchAsync(async () =>
            {
                var contentPanel = new Microsoft.UI.Xaml.Controls.StackPanel
                {
                    Spacing = 12
                };

                // Version info section
                var versionPanel = new Microsoft.UI.Xaml.Controls.StackPanel { Spacing = 12 };

                versionPanel.Children.Add(CreateInfoBlock("Current Version:", app.InstalledVersion));
                versionPanel.Children.Add(CreateInfoBlock("Latest Version:", app.LatestVersion));

                contentPanel.Children.Add(versionPanel);

                // Confidence warning (if needed)
                if (app.ConfidenceScore >= 70 && app.ConfidenceScore < 90)
                {
                    var warningBox = new Microsoft.UI.Xaml.Controls.InfoBar
                    {
                        Severity = Microsoft.UI.Xaml.Controls.InfoBarSeverity.Warning,
                        IsOpen = true,
                        Message = "We're not 100% sure about the exact version. Please verify before updating.",
                        IsClosable = false
                    };
                    contentPanel.Children.Add(warningBox);
                }
                else if (app.ConfidenceScore < 70)
                {
                    var warningBox = new Microsoft.UI.Xaml.Controls.InfoBar
                    {
                        Severity = Microsoft.UI.Xaml.Controls.InfoBarSeverity.Error,
                        IsOpen = true,
                        Message = "Low confidence match - version info may not be accurate. Please verify manually.",
                        IsClosable = false
                    };
                    contentPanel.Children.Add(warningBox);
                }

                // WinGet command section (only if high confidence)
                if (app.ConfidenceScore >= 90 && !string.IsNullOrEmpty(app.WinGetId))
                {
                    var commandSection = new Microsoft.UI.Xaml.Controls.StackPanel { Spacing = 8 };

                    var commandHeader = new Microsoft.UI.Xaml.Controls.TextBlock
                    {
                        Text = "✨ Quick Update Command",
                        FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                        FontSize = 14
                    };

                    var commandBox = new Microsoft.UI.Xaml.Controls.TextBox
                    {
                        Text = $"winget upgrade --id {app.WinGetId} -e",
                        IsReadOnly = true,
                        FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Consolas"),
                        FontSize = 13
                    };

                    var commandHelp = new Microsoft.UI.Xaml.Controls.TextBlock
                    {
                        Text = "Copy and paste this into Windows Terminal or PowerShell to update",
                        FontSize = 12,
                        Opacity = 0.7,
                        TextWrapping = Microsoft.UI.Xaml.TextWrapping.Wrap
                    };

                    commandSection.Children.Add(commandHeader);
                    commandSection.Children.Add(commandBox);
                    commandSection.Children.Add(commandHelp);

                    // Add "Update for Me" button
                    var updateButton = new Microsoft.UI.Xaml.Controls.Button
                    {
                        Content = "⚡ Update for Me",
                        HorizontalAlignment = Microsoft.UI.Xaml.HorizontalAlignment.Stretch,
                        Padding = new Microsoft.UI.Xaml.Thickness(16, 12, 16, 12),
                        Margin = new Microsoft.UI.Xaml.Thickness(0, 8, 0, 0)
                    };

                    updateButton.Click += async (s, e) =>
                    {
                        try
                        {
                            // Update WinGet sources first, then upgrade to the latest version
                            var startInfo = new System.Diagnostics.ProcessStartInfo
                            {
                                FileName = "wt.exe", // Windows Terminal
                                Arguments = $"-w 0 nt cmd /k \"echo Updating WinGet sources... && winget source update && echo. && echo Upgrading to latest version... && winget upgrade --id {app.WinGetId} -e --accept-package-agreements --accept-source-agreements && pause\"",
                                UseShellExecute = true
                            };

                            try
                            {
                                System.Diagnostics.Process.Start(startInfo);
                            }
                            catch
                            {
                                // Fallback to regular cmd if Windows Terminal not available
                                startInfo.FileName = "cmd.exe";
                                startInfo.Arguments = $"/k winget source update && winget upgrade --id {app.WinGetId} -e --accept-package-agreements --accept-source-agreements";
                                System.Diagnostics.Process.Start(startInfo);
                            }
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"Error launching update: {ex.Message}");

                            // Show error to user
                            var errorDialog = new Microsoft.UI.Xaml.Controls.ContentDialog
                            {
                                Title = "Unable to Launch Update",
                                Content = $"Could not open terminal to run the update command. Please try copying and pasting the command manually.\n\nError: {ex.Message}",
                                CloseButtonText = "OK",
                                XamlRoot = _xamlRoot
                            };
                            await errorDialog.ShowAsync();
                        }
                    };

                    commandSection.Children.Add(updateButton);
                    contentPanel.Children.Add(commandSection);
                }

                // Search online button (ALWAYS present)
                var searchButton = new Microsoft.UI.Xaml.Controls.Button
                {
                    Content = "🔍 Search How to Update Online",
                    HorizontalAlignment = Microsoft.UI.Xaml.HorizontalAlignment.Stretch,
                    Padding = new Microsoft.UI.Xaml.Thickness(16, 12, 16, 12)
                };

                searchButton.Click += async (s, e) =>
                {
                    var searchQuery = Uri.EscapeDataString($"how to update {app.AppName} to latest version");
                    var searchUrl = $"https://www.google.com/search?q={searchQuery}";
                    await Windows.System.Launcher.LaunchUriAsync(new Uri(searchUrl));
                };

                contentPanel.Children.Add(searchButton);

                var dialog = new Microsoft.UI.Xaml.Controls.ContentDialog
                {
                    Title = $"Update {app.AppName}",
                    Content = new Microsoft.UI.Xaml.Controls.ScrollViewer
                    {
                        Content = contentPanel,
                        MaxHeight = 500
                    },
                    CloseButtonText = "Close",
                    XamlRoot = _xamlRoot
                };

                await dialog.ShowAsync();
            });
        }

        private Microsoft.UI.Xaml.Controls.StackPanel CreateInfoBlock(string label, string value)
        {
            var panel = new Microsoft.UI.Xaml.Controls.StackPanel { Spacing = 4 };

            panel.Children.Add(new Microsoft.UI.Xaml.Controls.TextBlock
            {
                Text = label,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                FontSize = 13,
                Opacity = 0.7
            });

            panel.Children.Add(new Microsoft.UI.Xaml.Controls.TextBlock
            {
                Text = value,
                FontSize = 15
            });

            return panel;
        }

        private async Task<bool> CheckWinGetOutdatedAsync()
        {
            try
            {
                // Get local WinGet version
                var localVersion = await GetLocalWinGetVersionAsync();
                if (string.IsNullOrEmpty(localVersion))
                {
                    System.Diagnostics.Debug.WriteLine("Could not get local WinGet version");
                    return false; // Can't determine, assume it's fine
                }

                System.Diagnostics.Debug.WriteLine($"Local WinGet version: {localVersion}");

                // Get latest WinGet version from GitHub API
                var latestVersion = await GetLatestWinGetVersionAsync();
                if (string.IsNullOrEmpty(latestVersion))
                {
                    System.Diagnostics.Debug.WriteLine("Could not get latest WinGet version from GitHub");
                    return false; // Can't determine, assume it's fine
                }

                System.Diagnostics.Debug.WriteLine($"Latest WinGet version: {latestVersion}");

                // Compare versions (both should be like "v1.7.10861" or "1.7.10861")
                var localClean = localVersion.TrimStart('v');
                var latestClean = latestVersion.TrimStart('v');

                var localParts = localClean.Split('.').Select(p => int.TryParse(p, out var n) ? n : 0).ToArray();
                var latestParts = latestClean.Split('.').Select(p => int.TryParse(p, out var n) ? n : 0).ToArray();

                // Compare major and minor versions (ignore patch)
                if (localParts.Length >= 2 && latestParts.Length >= 2)
                {
                    // If major version is behind
                    if (localParts[0] < latestParts[0])
                    {
                        System.Diagnostics.Debug.WriteLine("WinGet is outdated (major version behind)");
                        return true;
                    }
                    // If same major but minor is behind
                    if (localParts[0] == latestParts[0] && localParts[1] < latestParts[1])
                    {
                        System.Diagnostics.Debug.WriteLine("WinGet is outdated (minor version behind)");
                        return true;
                    }
                }

                System.Diagnostics.Debug.WriteLine("WinGet is up to date");
                return false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error checking WinGet version: {ex.Message}");
                return false; // On error, assume it's fine and continue
            }
        }

        private async Task<string> GetLocalWinGetVersionAsync()
        {
            try
            {
                var process = new System.Diagnostics.Process
                {
                    StartInfo = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = "winget",
                        Arguments = "--version",
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true
                    }
                };

                process.Start();
                var output = await process.StandardOutput.ReadToEndAsync();

                if (process.WaitForExit(5000))
                {
                    return output.Trim();
                }

                try { process.Kill(); } catch { }
            }
            catch { }

            return null;
        }

        private async Task<string> GetLatestWinGetVersionAsync()
        {
            try
            {
                using var client = new System.Net.Http.HttpClient();
                client.DefaultRequestHeaders.Add("User-Agent", "Winspeqt");
                client.Timeout = TimeSpan.FromSeconds(5);

                // GitHub API to get latest release
                var response = await client.GetAsync("https://api.github.com/repos/microsoft/winget-cli/releases/latest");

                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    var doc = System.Text.Json.JsonDocument.Parse(json);

                    if (doc.RootElement.TryGetProperty("tag_name", out var tagName))
                    {
                        return tagName.GetString(); // Returns like "v1.7.10861"
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error fetching latest WinGet version: {ex.Message}");
            }

            return null;
        }

        private async Task ShowWinGetOutdatedWarningAsync()
        {
            if (_xamlRoot == null) return;

            await DispatchAsync(async () =>
            {
                var dialog = new Microsoft.UI.Xaml.Controls.ContentDialog
                {
                    Title = "Outdated WinGet",
                    Content = "Your WinGet version is outdated.\n\n" +
                             "For best results, update WinGet through the Microsoft Store by searching for 'App Installer'.\n\n" +
                             "You can continue scanning, but some apps may not be found or version info may be inaccurate.",
                    PrimaryButtonText = "Continue Anyway",
                    SecondaryButtonText = "Open Microsoft Store",
                    CloseButtonText = "Cancel",
                    XamlRoot = _xamlRoot
                };

                var result = await dialog.ShowAsync();

                if (result == Microsoft.UI.Xaml.Controls.ContentDialogResult.Secondary)
                {
                    // Open Microsoft Store to App Installer
                    await Windows.System.Launcher.LaunchUriAsync(
                        new Uri("ms-windows-store://pdp/?ProductId=9nblggh4nns1"));

                    // Cancel the scan
                    IsScanning = false;
                }
                else if (result == Microsoft.UI.Xaml.Controls.ContentDialogResult.None)
                {
                    // User clicked Cancel
                    IsScanning = false;
                }
                // Primary button (Continue Anyway) - just continue with the scan
            });
        }

        private async Task DispatchAsync(Action action)
        {
            var tcs = new TaskCompletionSource<bool>();

            bool enqueued = _dispatcherQueue.TryEnqueue(() =>
            {
                try
                {
                    action();
                    tcs.SetResult(true);
                }
                catch (Exception ex)
                {
                    tcs.SetException(ex);
                }
            });

            if (!enqueued)
            {
                System.Diagnostics.Debug.WriteLine("Failed to enqueue UI update");
                tcs.SetResult(false);
            }

            await tcs.Task;
        }

        public void Cleanup()
        {
            _securityService?.Dispose();
            _scanLock?.Dispose();
        }
    }
}