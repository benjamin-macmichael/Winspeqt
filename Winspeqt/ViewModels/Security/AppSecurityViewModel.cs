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
        private Microsoft.UI.Xaml.XamlRoot? _xamlRoot;
        private static DateTime? _lastSourceUpdate = null;
        private static readonly TimeSpan SourceUpdateCooldown = TimeSpan.FromHours(6);
        private List<AppSecurityInfo> _allApps = new();
        private DateTime? _lastScanTime;

        public void SetXamlRoot(Microsoft.UI.Xaml.XamlRoot xamlRoot)
        {
            _xamlRoot = xamlRoot;
        }

        // -----------------------------------------------------------------------
        // Properties
        // -----------------------------------------------------------------------

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

        private string _statusMessage = string.Empty;
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
                    OnPropertyChanged(nameof(SummaryMessage));
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
                    OnPropertyChanged(nameof(SummaryMessage));
            }
        }

        private AppSecurityInfo _selectedApp = new();
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
                    ApplyFilter();
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

        // -----------------------------------------------------------------------
        // Health score properties
        // -----------------------------------------------------------------------

        private int _healthScore;
        public int HealthScore
        {
            get => _healthScore;
            set
            {
                if (SetProperty(ref _healthScore, value))
                {
                    OnPropertyChanged(nameof(HealthScoreLabel));
                    OnPropertyChanged(nameof(HealthScoreColor));
                    OnPropertyChanged(nameof(HealthScoreSubtext));
                }
            }
        }

        public string HealthScoreLabel => HealthScore switch
        {
            >= 90 => "Excellent",
            >= 70 => "Good",
            >= 50 => "Needs Attention",
            >= 25 => "Poor",
            _ => "Critical"
        };

        public string HealthScoreColor => HealthScore switch
        {
            >= 90 => "#4CAF50",
            >= 70 => "#8BC34A",
            >= 50 => "#FF9800",
            >= 25 => "#FF5722",
            _ => "#F44336"
        };

        public string HealthScoreSubtext
        {
            get
            {
                if (!HasScanned) return string.Empty;
                int issues = CriticalAppsCount + OutdatedAppsCount;
                return issues == 0
                    ? "All tracked apps are up to date"
                    : $"{issues} app{(issues == 1 ? "" : "s")} need{(issues == 1 ? "s" : "")} updating";
            }
        }

        // -----------------------------------------------------------------------
        // Collections & commands
        // -----------------------------------------------------------------------

        public ObservableCollection<AppSecurityInfo> ScannedApps { get; set; }
        public ObservableCollection<AppSecurityInfo> FilteredApps { get; set; }
        public ObservableCollection<string> FilterOptions { get; set; }

        public ICommand ScanCommand { get; }
        public ICommand OpenUpdateInstructionsCommand { get; }

        // -----------------------------------------------------------------------
        // Constructor
        // -----------------------------------------------------------------------

        public AppSecurityViewModel()
        {
            _securityService = new AppSecurityService();
            _dispatcherQueue = DispatcherQueue.GetForCurrentThread();

            // Restore last scan time across sessions
            try
            {
                var container = Windows.Storage.ApplicationData.Current.LocalSettings;
                if (container.Values.ContainsKey("AppUpdateChecker_LastScanTime"))
                {
                    var ticks = (long)container.Values["AppUpdateChecker_LastScanTime"];
                    _lastScanTime = new DateTime(ticks, DateTimeKind.Local);
                }
            }
            catch { }

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
            OpenUpdateInstructionsCommand = new RelayCommand<AppSecurityInfo>(async app => await ShowUpdateInstructionsAsync(app));

            StatusMessage = "Ready to scan your installed applications.";
        }

        // -----------------------------------------------------------------------
        // Health score computation
        // -----------------------------------------------------------------------

        private (int score, string emoji, string message) ComputeHealthScore()
        {
            if (TotalAppsScanned == 0) return (100, "✅", "No apps tracked yet.");

            int weightedIssues = CriticalAppsCount * 2 + OutdatedAppsCount;
            int maxWeight = TotalAppsScanned * 2;
            int score = Math.Max(0, 100 - (int)Math.Round(weightedIssues * 100.0 / maxWeight));

            string emoji = score switch
            {
                >= 90 => "✅",
                >= 70 => "🟡",
                >= 50 => "🟠",
                _ => "🔴"
            };

            string message = score switch
            {
                >= 90 => $"Your apps are in great shape! {UpToDateAppsCount} of {TotalAppsScanned} are up to date.",
                >= 70 => $"{OutdatedAppsCount} app{(OutdatedAppsCount == 1 ? "" : "s")} could use an update. Open Winspeqt to see which ones.",
                >= 50 => $"{OutdatedAppsCount + CriticalAppsCount} apps need attention. Keep your software up to date for best security.",
                _ => $"Your app health is low — {CriticalAppsCount} critical and {OutdatedAppsCount} outdated apps found. Update them soon!"
            };

            return (score, emoji, message);
        }

        private void UpdateHealthScore()
        {
            var (score, _, _) = ComputeHealthScore();
            HealthScore = score;
        }

        // -----------------------------------------------------------------------
        // Scan
        // -----------------------------------------------------------------------

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

                var isOutdated = await CheckWinGetOutdatedAsync();
                if (isOutdated)
                {
                    await ShowWinGetOutdatedWarningAsync();
                    if (!IsScanning) return;
                }

                bool shouldUpdateSources = !_lastSourceUpdate.HasValue ||
                                           (DateTime.Now - _lastSourceUpdate.Value) > SourceUpdateCooldown;

                if (shouldUpdateSources)
                {
                    await DispatchAsync(() =>
                    {
                        StatusMessage = "Updating package databases...";
                        ScanProgress = 0;
                    });

                    try
                    {
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
                        var completed = updateProcess.WaitForExit(30000);

                        if (completed && updateProcess.ExitCode == 0)
                            _lastSourceUpdate = DateTime.Now;
                        else if (!completed)
                        {
                            try { updateProcess.Kill(); } catch { }
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Failed to update WinGet sources: {ex.Message}");
                    }
                }
                else
                {
                    await DispatchAsync(() =>
                    {
                        StatusMessage = "Using cached package database...";
                        ScanProgress = 0;
                    });
                    await Task.Delay(500);
                }

                await DispatchAsync(() => StatusMessage = "Scanning your installed applications...");

                var apps = await _securityService.ScanInstalledAppsAsync();

                await DispatchAsync(() =>
                {
                    StatusMessage = $"Found {apps.Count} apps. Checking for updates online...";
                    ScanProgress = 10;
                });

                int checkedCount = 0;
                int total = apps.Count;

                var tasks = apps.Select(async app =>
                {
                    try
                    {
                        await _securityService.CheckSingleAppVersionAsync(app);
                        Interlocked.Increment(ref checkedCount);
                        var progress = 10 + (checkedCount * 90.0 / total);
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

                _allApps = apps.Where(a => a.Status != SecurityStatus.Unknown).ToList();

                var totalApps = _allApps.Count;
                var outdated = _allApps.Count(a => a.Status == SecurityStatus.Outdated);
                var critical = _allApps.Count(a => a.Status == SecurityStatus.Critical);
                var upToDate = _allApps.Count(a => a.Status == SecurityStatus.UpToDate);

                await DispatchAsync(() =>
                {
                    foreach (var app in apps)
                        ScannedApps.Add(app);

                    TotalAppsScanned = totalApps;
                    OutdatedAppsCount = outdated;
                    CriticalAppsCount = critical;
                    UpToDateAppsCount = upToDate;

                    ApplyFilter();
                    UpdateHealthScore();

                    // Save last scan time and score for persistence across sessions
                    _lastScanTime = DateTime.Now;
                    var container = Windows.Storage.ApplicationData.Current.LocalSettings;
                    container.Values["AppUpdateChecker_LastScanTime"] = DateTime.Now.Ticks;
                    container.Values["AppUpdateChecker_HealthScore"] = HealthScore;
                    container.Values["AppUpdateChecker_TotalApps"] = totalApps;
                    container.Values["AppUpdateChecker_OutdatedApps"] = outdated;
                    container.Values["AppUpdateChecker_CriticalApps"] = critical;
                    container.Values["AppUpdateChecker_UpToDateApps"] = upToDate;

                    IsScanning = false;
                    HasScanned = true;
                    ScanProgress = 100;
                    StatusMessage = $"Scan complete! Found {totalApps} applications.";
                });

                _ = NotificationManagerService.Instance.TriggerCheckAsync();
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

        // -----------------------------------------------------------------------
        // Filter
        // -----------------------------------------------------------------------

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
                FilteredApps.Add(app);
        }

        // -----------------------------------------------------------------------
        // Dialogs
        // -----------------------------------------------------------------------

        private async Task ShowUpdateInstructionsAsync(AppSecurityInfo app)
        {
            if (app == null || _xamlRoot == null) return;

            await DispatchAsync(async () =>
            {
                var contentPanel = new Microsoft.UI.Xaml.Controls.StackPanel { Spacing = 12 };

                var versionPanel = new Microsoft.UI.Xaml.Controls.StackPanel { Spacing = 12 };
                versionPanel.Children.Add(CreateInfoBlock("Current Version:", app.InstalledVersion));
                versionPanel.Children.Add(CreateInfoBlock("Latest Version:", app.LatestVersion));
                contentPanel.Children.Add(versionPanel);

                if (app.ConfidenceScore >= 70 && app.ConfidenceScore < 90)
                {
                    contentPanel.Children.Add(new Microsoft.UI.Xaml.Controls.InfoBar
                    {
                        Severity = Microsoft.UI.Xaml.Controls.InfoBarSeverity.Warning,
                        IsOpen = true,
                        Message = "We're not 100% sure about the exact version. Please verify before updating.",
                        IsClosable = false
                    });
                }
                else if (app.ConfidenceScore < 70)
                {
                    contentPanel.Children.Add(new Microsoft.UI.Xaml.Controls.InfoBar
                    {
                        Severity = Microsoft.UI.Xaml.Controls.InfoBarSeverity.Error,
                        IsOpen = true,
                        Message = "Low confidence match - version info may not be accurate. Please verify manually.",
                        IsClosable = false
                    });
                }

                if (app.ConfidenceScore >= 90 && !string.IsNullOrEmpty(app.WinGetId))
                {
                    var commandSection = new Microsoft.UI.Xaml.Controls.StackPanel { Spacing = 8 };
                    commandSection.Children.Add(new Microsoft.UI.Xaml.Controls.TextBlock
                    {
                        Text = "✨ Quick Update Command",
                        FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                        FontSize = 14
                    });
                    commandSection.Children.Add(new Microsoft.UI.Xaml.Controls.TextBox
                    {
                        Text = $"winget upgrade --id {app.WinGetId} -e",
                        IsReadOnly = true,
                        FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Consolas"),
                        FontSize = 13
                    });
                    commandSection.Children.Add(new Microsoft.UI.Xaml.Controls.TextBlock
                    {
                        Text = "Copy and paste this into Windows Terminal or PowerShell to update",
                        FontSize = 12,
                        Opacity = 0.7,
                        TextWrapping = Microsoft.UI.Xaml.TextWrapping.Wrap
                    });

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
                            var startInfo = new System.Diagnostics.ProcessStartInfo
                            {
                                FileName = "wt.exe",
                                Arguments = $"-w 0 nt cmd /k \"echo Updating WinGet sources... && " +
                                                  $"winget source update && echo. && " +
                                                  $"echo Upgrading {app.AppName}... && echo. && " +
                                                  $"winget upgrade --id {app.WinGetId} -e --accept-package-agreements --accept-source-agreements && " +
                                                  $"(echo. && echo ================================ && echo SUCCESS: {app.AppName} has been updated! && echo ================================) || " +
                                                  $"(echo. && echo ================================ && echo UPDATE FAILED && echo ================================ && echo. && " +
                                                  $"echo This app may not have been installed with WinGet originally. && echo. && " +
                                                  $"echo To update {app.AppName}, please: && echo. && " +
                                                  $"echo   1. Visit the official {app.AppName} website && echo   2. Download the latest installer && echo   3. Run the installer to update && echo.) && pause\"",
                                UseShellExecute = true
                            };

                            try { System.Diagnostics.Process.Start(startInfo); }
                            catch
                            {
                                startInfo.FileName = "cmd.exe";
                                startInfo.Arguments = $"/k winget source update && winget upgrade --id {app.WinGetId} -e --accept-package-agreements --accept-source-agreements && pause";
                                System.Diagnostics.Process.Start(startInfo);
                            }
                        }
                        catch (Exception ex)
                        {
                            var errorDialog = new Microsoft.UI.Xaml.Controls.ContentDialog
                            {
                                Title = "Unable to Launch Update",
                                Content = $"Could not open terminal. Please copy and paste the command manually.\n\nError: {ex.Message}",
                                CloseButtonText = "OK",
                                XamlRoot = _xamlRoot
                            };
                            await errorDialog.ShowAsync();
                        }
                    };

                    commandSection.Children.Add(updateButton);
                    contentPanel.Children.Add(commandSection);
                }

                var searchButton = new Microsoft.UI.Xaml.Controls.Button
                {
                    Content = "🔍 Search How to Update Online",
                    HorizontalAlignment = Microsoft.UI.Xaml.HorizontalAlignment.Stretch,
                    Padding = new Microsoft.UI.Xaml.Thickness(16, 12, 16, 12)
                };
                searchButton.Click += async (s, e) =>
                {
                    var q = Uri.EscapeDataString($"how to update {app.AppName} to latest version");
                    await Windows.System.Launcher.LaunchUriAsync(new Uri($"https://www.google.com/search?q={q}"));
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
            panel.Children.Add(new Microsoft.UI.Xaml.Controls.TextBlock { Text = value, FontSize = 15 });
            return panel;
        }

        // -----------------------------------------------------------------------
        // WinGet version check
        // -----------------------------------------------------------------------

        private async Task<bool> CheckWinGetOutdatedAsync()
        {
            try
            {
                var localVersion = await GetLocalWinGetVersionAsync();
                if (string.IsNullOrEmpty(localVersion)) return false;

                var latestVersion = await GetLatestWinGetVersionAsync();
                if (string.IsNullOrEmpty(latestVersion)) return false;

                var localParts = localVersion.TrimStart('v').Split('.').Select(p => int.TryParse(p, out var n) ? n : 0).ToArray();
                var latestParts = latestVersion.TrimStart('v').Split('.').Select(p => int.TryParse(p, out var n) ? n : 0).ToArray();

                if (localParts.Length >= 2 && latestParts.Length >= 2)
                {
                    if (localParts[0] < latestParts[0]) return true;
                    if (localParts[0] == latestParts[0] && localParts[1] < latestParts[1]) return true;
                }

                return false;
            }
            catch { return false; }
        }

        private async Task<string?> GetLocalWinGetVersionAsync()
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
                if (process.WaitForExit(5000)) return output.Trim();
                try { process.Kill(); } catch { }
            }
            catch { }
            return null;
        }

        private async Task<string?> GetLatestWinGetVersionAsync()
        {
            try
            {
                using var client = new System.Net.Http.HttpClient();
                client.DefaultRequestHeaders.Add("User-Agent", "Winspeqt");
                client.Timeout = TimeSpan.FromSeconds(5);
                var response = await client.GetAsync("https://api.github.com/repos/microsoft/winget-cli/releases/latest");
                if (response.IsSuccessStatusCode)
                {
                    var doc = System.Text.Json.JsonDocument.Parse(await response.Content.ReadAsStringAsync());
                    if (doc.RootElement.TryGetProperty("tag_name", out var tag))
                        return tag.GetString();
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
                    Content = "Your WinGet version is outdated.\n\nFor best results, update WinGet through the Microsoft Store by searching for 'App Installer'.\n\nYou can continue scanning, but some apps may not be found or version info may be inaccurate.",
                    PrimaryButtonText = "Continue Anyway",
                    SecondaryButtonText = "Open Microsoft Store",
                    CloseButtonText = "Cancel",
                    XamlRoot = _xamlRoot
                };

                var result = await dialog.ShowAsync();

                if (result == Microsoft.UI.Xaml.Controls.ContentDialogResult.Secondary)
                {
                    await Windows.System.Launcher.LaunchUriAsync(new Uri("ms-windows-store://pdp/?ProductId=9nblggh4nns1"));
                    IsScanning = false;
                }
                else if (result == Microsoft.UI.Xaml.Controls.ContentDialogResult.None)
                {
                    IsScanning = false;
                }
            });
        }

        // -----------------------------------------------------------------------
        // Helpers
        // -----------------------------------------------------------------------

        private async Task DispatchAsync(Action action)
        {
            var tcs = new TaskCompletionSource<bool>();
            bool enqueued = _dispatcherQueue.TryEnqueue(() =>
            {
                try { action(); tcs.SetResult(true); }
                catch (Exception ex) { tcs.SetException(ex); }
            });
            if (!enqueued) tcs.SetResult(false);
            await tcs.Task;
        }

        public void Cleanup()
        {
            _securityService?.Dispose();
            _scanLock?.Dispose();
        }
    }
}