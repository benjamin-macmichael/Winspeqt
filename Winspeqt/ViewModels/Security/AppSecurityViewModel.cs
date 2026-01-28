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
                    return "Click 'Scan My Apps' to check for outdated software.";

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
                var dialog = new Microsoft.UI.Xaml.Controls.ContentDialog
                {
                    Title = $"How to Update {app.AppName}",
                    Content = new Microsoft.UI.Xaml.Controls.ScrollViewer
                    {
                        Content = new Microsoft.UI.Xaml.Controls.StackPanel
                        {
                            Spacing = 12,
                            Children =
                            {
                                new Microsoft.UI.Xaml.Controls.TextBlock
                                {
                                    Text = "Current Version:",
                                    FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
                                },
                                new Microsoft.UI.Xaml.Controls.TextBlock
                                {
                                    Text = app.InstalledVersion,
                                    Margin = new Microsoft.UI.Xaml.Thickness(0, 0, 0, 12)
                                },
                                new Microsoft.UI.Xaml.Controls.TextBlock
                                {
                                    Text = "Latest Version:",
                                    FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
                                },
                                new Microsoft.UI.Xaml.Controls.TextBlock
                                {
                                    Text = app.LatestVersion,
                                    Margin = new Microsoft.UI.Xaml.Thickness(0, 0, 0, 12)
                                },
                                new Microsoft.UI.Xaml.Controls.TextBlock
                                {
                                    Text = "Update Instructions:",
                                    FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
                                },
                                new Microsoft.UI.Xaml.Controls.TextBlock
                                {
                                    Text = app.UpdateInstructions,
                                    TextWrapping = Microsoft.UI.Xaml.TextWrapping.Wrap
                                }
                            }
                        }
                    },
                    CloseButtonText = "Got it!",
                    XamlRoot = _xamlRoot
                };

                await dialog.ShowAsync();
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