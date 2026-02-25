using Microsoft.UI.Dispatching;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Winspeqt.Helpers;
using Winspeqt.Models;
using Winspeqt.Services;

namespace Winspeqt.ViewModels.Monitoring
{
    public class BackgroundProcessViewModel : INotifyPropertyChanged
    {
        private readonly SystemMonitorService _systemMonitor;
        private readonly DispatcherQueue _dispatcherQueue;
        private DispatcherQueueTimer? _refreshTimer;

        private bool _isLoading;
        private string _searchQuery = string.Empty;
        private bool _isRefreshing;

        // Process categorization dictionaries
        private readonly HashSet<string> _browserProcesses = new(StringComparer.OrdinalIgnoreCase)
        {
            "chrome", "firefox", "msedge", "opera", "brave", "vivaldi", "safari",
            "iexplore", "browser", "edge", "chromium"
        };

        private readonly HashSet<string> _gamingProcesses = new(StringComparer.OrdinalIgnoreCase)
        {
            "steam", "epicgameslauncher", "origin", "uplay", "battlenet", "gog",
            "discord", "xbox", "nvidia", "geforce", "amd", "radeon", "game"
        };

        private readonly HashSet<string> _cloudStorageProcesses = new(StringComparer.OrdinalIgnoreCase)
        {
            "onedrive", "dropbox", "googledrive", "box", "sync", "backup",
            "icloud", "mega", "pcloud"
        };

        private readonly HashSet<string> _communicationProcesses = new(StringComparer.OrdinalIgnoreCase)
        {
            "slack", "teams", "zoom", "skype", "discord", "telegram", "whatsapp",
            "signal", "messenger", "webex", "meet"
        };

        private readonly HashSet<string> _mediaProcesses = new(StringComparer.OrdinalIgnoreCase)
        {
            "spotify", "itunes", "vlc", "media", "wmplayer", "musicbee", "foobar",
            "audacity", "obs", "handbrake", "plex", "kodi"
        };

        private readonly HashSet<string> _developmentProcesses = new(StringComparer.OrdinalIgnoreCase)
        {
            "devenv", "code", "vscode", "rider", "intellij", "pycharm", "webstorm",
            "visualstudio", "eclipse", "netbeans", "atom", "sublime", "notepad++",
            "git", "docker", "node", "python", "java", "dotnet"
        };

        private readonly HashSet<string> _systemServicesProcesses = new(StringComparer.OrdinalIgnoreCase)
        {
            "svchost", "system", "services", "winlogon", "csrss", "lsass",
            "spoolsv", "explorer", "dwm", "taskhost", "searchindexer", "runtime"
        };

        public BackgroundProcessViewModel()
        {
            _systemMonitor = new SystemMonitorService();
            _dispatcherQueue = DispatcherQueue.GetForCurrentThread();

            AllProcesses = new ObservableCollection<ProcessInfo>();
            FilteredProcesses = new ObservableCollection<ProcessInfo>();
            BatteryDevices = new ObservableCollection<BatteryInfo>();

            BrowserProcesses = new ObservableCollection<ProcessInfo>();
            GamingProcesses = new ObservableCollection<ProcessInfo>();
            CloudStorageProcesses = new ObservableCollection<ProcessInfo>();
            CommunicationProcesses = new ObservableCollection<ProcessInfo>();
            SystemServicesProcesses = new ObservableCollection<ProcessInfo>();
            MediaProcesses = new ObservableCollection<ProcessInfo>();
            DevelopmentProcesses = new ObservableCollection<ProcessInfo>();
            OtherProcesses = new ObservableCollection<ProcessInfo>();

            RefreshCommand = new RelayCommand(async () => await RefreshDataAsync());

            // Delay initialization to let the UI load first
            _dispatcherQueue.TryEnqueue(async () =>
            {
                await Task.Delay(100);
                await InitializeAsync();
            });
        }

        public ObservableCollection<ProcessInfo> AllProcesses { get; }
        public ObservableCollection<ProcessInfo> FilteredProcesses { get; }
        public ObservableCollection<BatteryInfo> BatteryDevices { get; }

        public ObservableCollection<ProcessInfo> BrowserProcesses { get; }
        public ObservableCollection<ProcessInfo> GamingProcesses { get; }
        public ObservableCollection<ProcessInfo> CloudStorageProcesses { get; }
        public ObservableCollection<ProcessInfo> CommunicationProcesses { get; }
        public ObservableCollection<ProcessInfo> SystemServicesProcesses { get; }
        public ObservableCollection<ProcessInfo> MediaProcesses { get; }
        public ObservableCollection<ProcessInfo> DevelopmentProcesses { get; }
        public ObservableCollection<ProcessInfo> OtherProcesses { get; }

        public bool IsLoading
        {
            get => _isLoading;
            set
            {
                _isLoading = value;
                OnPropertyChanged(nameof(IsLoading));
            }
        }

        public string SearchQuery
        {
            get => _searchQuery;
            set
            {
                _searchQuery = value;
                OnPropertyChanged(nameof(SearchQuery));
                FilterProcesses();
            }
        }

        public ICommand RefreshCommand { get; }

        private async Task InitializeAsync()
        {
            try
            {
                await RefreshDataAsync();
                StartAutoRefresh();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error initializing: {ex.Message}");
            }
        }

        private async Task RefreshDataAsync()
        {
            if (_isRefreshing) return;
            _isRefreshing = true;

            IsLoading = true;

            try
            {
                var processes = await Task.Run(async () =>
                {
                    try
                    {
                        return await _systemMonitor.GetRunningProcessesAsync();
                    }
                    catch (AccessViolationException ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"AccessViolation in GetRunningProcessesAsync: {ex.Message}");
                        return new List<ProcessInfo>();
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error getting processes: {ex.Message}");
                        return new List<ProcessInfo>();
                    }
                });

                _dispatcherQueue.TryEnqueue(() =>
                {
                    try
                    {
                        AllProcesses.Clear();
                        BatteryDevices.Clear();

                        foreach (var process in processes)
                        {
                            if (process != null)
                            {
                                // Categorize the process
                                process.Category = CategorizeProcess(process);
                                AllProcesses.Add(process);
                            }
                        }

                        GroupProcessesByCategory();
                        FilterProcesses();
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error updating UI: {ex.Message}");
                    }
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error refreshing data: {ex.Message}");
            }
            finally
            {
                IsLoading = false;
                _isRefreshing = false;
            }
        }

        private ProcessCategory CategorizeProcess(ProcessInfo process)
        {
            var processName = process.ProcessName?.ToLower() ?? "";
            var description = process.Description?.ToLower() ?? "";

            // Check each category
            if (_browserProcesses.Any(b => processName.Contains(b) || description.Contains(b)))
                return ProcessCategory.Browser;

            if (_gamingProcesses.Any(g => processName.Contains(g) || description.Contains(g)))
                return ProcessCategory.Gaming;

            if (_cloudStorageProcesses.Any(c => processName.Contains(c) || description.Contains(c)))
                return ProcessCategory.CloudStorage;

            if (_communicationProcesses.Any(c => processName.Contains(c) || description.Contains(c)))
                return ProcessCategory.Communication;

            if (_mediaProcesses.Any(m => processName.Contains(m) || description.Contains(m)))
                return ProcessCategory.Media;

            if (_developmentProcesses.Any(d => processName.Contains(d) || description.Contains(d)))
                return ProcessCategory.Development;

            if (_systemServicesProcesses.Any(s => processName.Contains(s) || description.Contains(s)))
                return ProcessCategory.SystemServices;

            return ProcessCategory.Other;
        }

        private void GroupProcessesByCategory()
        {
            BrowserProcesses.Clear();
            GamingProcesses.Clear();
            CloudStorageProcesses.Clear();
            CommunicationProcesses.Clear();
            SystemServicesProcesses.Clear();
            MediaProcesses.Clear();
            DevelopmentProcesses.Clear();
            OtherProcesses.Clear();

            // Group processes by category
            var categorizedProcesses = AllProcesses
                .Where(p => p != null)
                .GroupBy(p => p.Category)
                .ToDictionary(g => g.Key, g => g.ToList());

            // Add top 5 by memory usage to each category
            AddTopProcessesToCategory(categorizedProcesses, ProcessCategory.Browser, BrowserProcesses);
            AddTopProcessesToCategory(categorizedProcesses, ProcessCategory.Gaming, GamingProcesses);
            AddTopProcessesToCategory(categorizedProcesses, ProcessCategory.CloudStorage, CloudStorageProcesses);
            AddTopProcessesToCategory(categorizedProcesses, ProcessCategory.Communication, CommunicationProcesses);
            AddTopProcessesToCategory(categorizedProcesses, ProcessCategory.Media, MediaProcesses);
            AddTopProcessesToCategory(categorizedProcesses, ProcessCategory.Development, DevelopmentProcesses);
            AddTopProcessesToCategory(categorizedProcesses, ProcessCategory.SystemServices, SystemServicesProcesses);
            AddTopProcessesToCategory(categorizedProcesses, ProcessCategory.Other, OtherProcesses);
        }

        private void AddTopProcessesToCategory(
            Dictionary<ProcessCategory, List<ProcessInfo>> categorizedProcesses,
            ProcessCategory category,
            ObservableCollection<ProcessInfo> targetCollection)
        {
            if (categorizedProcesses.TryGetValue(category, out var processes))
            {
                // Sort by memory usage (descending) and take top 5
                // Try to use WorkingSet64, PrivateMemorySize64, or fall back to ProcessId
                var topProcesses = processes
                    .OrderByDescending(p =>
                    {
                        // Parse memory from display string if available
                        if (!string.IsNullOrEmpty(p.MemoryUsageDisplay))
                        {
                            var memStr = p.MemoryUsageDisplay.Replace("MB", "").Replace("GB", "").Trim();
                            if (double.TryParse(memStr, out var mem))
                            {
                                return p.MemoryUsageDisplay.Contains("GB") ? mem * 1024 : mem;
                            }
                        }
                        return 0;
                    })
                    .Take(5);

                foreach (var process in topProcesses)
                {
                    targetCollection.Add(process);
                }
            }
        }

        private void FilterProcesses()
        {
            FilteredProcesses.Clear();

            var filtered = string.IsNullOrWhiteSpace(SearchQuery)
                ? AllProcesses
                : AllProcesses.Where(p =>
                    (p.ProcessName?.Contains(SearchQuery, StringComparison.OrdinalIgnoreCase) ?? false) ||
                    (p.Description?.Contains(SearchQuery, StringComparison.OrdinalIgnoreCase) ?? false) ||
                    (p.FriendlyExplanation?.Contains(SearchQuery, StringComparison.OrdinalIgnoreCase) ?? false));

            foreach (var process in filtered)
            {
                FilteredProcesses.Add(process);
            }
        }

        private void StartAutoRefresh()
        {
            _refreshTimer = _dispatcherQueue.CreateTimer();
            _refreshTimer.Interval = TimeSpan.FromSeconds(5);
            _refreshTimer.Tick += async (s, e) =>
            {
                try
                {
                    await RefreshDataAsync();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Auto-refresh error: {ex.Message}");
                }
            };
            _refreshTimer.Start();
        }

        public async Task<bool> EndProcessAsync(ProcessInfo process)
        {
            System.Diagnostics.Process? systemProcess = null;
            try
            {
                systemProcess = System.Diagnostics.Process.GetProcessById(process.ProcessId);
                systemProcess.Kill();
                await systemProcess.WaitForExitAsync();

                _dispatcherQueue.TryEnqueue(() =>
                {
                    AllProcesses.Remove(process);
                    FilteredProcesses.Remove(process);

                    switch (process.Category)
                    {
                        case ProcessCategory.Browser:
                            BrowserProcesses.Remove(process);
                            break;
                        case ProcessCategory.Gaming:
                            GamingProcesses.Remove(process);
                            break;
                        case ProcessCategory.CloudStorage:
                            CloudStorageProcesses.Remove(process);
                            break;
                        case ProcessCategory.Communication:
                            CommunicationProcesses.Remove(process);
                            break;
                        case ProcessCategory.SystemServices:
                            SystemServicesProcesses.Remove(process);
                            break;
                        case ProcessCategory.Development:
                            DevelopmentProcesses.Remove(process);
                            break;
                        case ProcessCategory.Media:
                            MediaProcesses.Remove(process);
                            break;
                        default:
                            OtherProcesses.Remove(process);
                            break;
                    }
                });

                return true;
            }
            catch (ArgumentException)
            {
                System.Diagnostics.Debug.WriteLine($"Process {process.ProcessId} already exited");
                return true;
            }
            catch (InvalidOperationException ex)
            {
                System.Diagnostics.Debug.WriteLine($"Cannot end process: {ex.Message}");
                return false;
            }
            catch (System.ComponentModel.Win32Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Access denied ending process: {ex.Message}");
                return false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error ending process: {ex.Message}");
                return false;
            }
            finally
            {
                systemProcess?.Dispose();
            }
        }

        public void StopAutoRefresh()
        {
            _refreshTimer?.Stop();
            _refreshTimer = null;
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}