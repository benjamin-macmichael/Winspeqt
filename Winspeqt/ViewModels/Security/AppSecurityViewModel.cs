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
                    StatusMessage = "Scanning your installed applications...";
                    ScannedApps.Clear();
                    FilteredApps.Clear();
                });

                // Step 1: Scan registry for installed apps
                var apps = await _securityService.ScanInstalledAppsAsync();

                await DispatchAsync(() =>
                {
                    StatusMessage = $"Found {apps.Count} apps. Checking for updates online...";
                });

                // Step 2: Check versions against online APIs
                await _securityService.CheckAppVersionsAsync(apps);

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
                    Spacing = 16
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
                        Text = $"winget upgrade {app.WinGetId}",
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

                    contentPanel.Children.Add(commandSection);
                }

                // Separator
                var separator = new Microsoft.UI.Xaml.Controls.Border
                {
                    Height = 1,
                    Opacity = 0.2,
                    Margin = new Microsoft.UI.Xaml.Thickness(0, 8, 0, 8)
                };
                contentPanel.Children.Add(separator);

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