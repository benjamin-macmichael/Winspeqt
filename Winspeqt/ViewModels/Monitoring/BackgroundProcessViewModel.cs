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
        private bool _isTableView;
        private string _sortColumn = "Memory";
        private bool _sortAscending = false;

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

            // Card view collections (top 5 per category)
            BrowserProcesses = new ObservableCollection<ProcessInfo>();
            GamingProcesses = new ObservableCollection<ProcessInfo>();
            CloudStorageProcesses = new ObservableCollection<ProcessInfo>();
            CommunicationProcesses = new ObservableCollection<ProcessInfo>();
            SystemServicesProcesses = new ObservableCollection<ProcessInfo>();
            MediaProcesses = new ObservableCollection<ProcessInfo>();
            DevelopmentProcesses = new ObservableCollection<ProcessInfo>();
            OtherProcesses = new ObservableCollection<ProcessInfo>();

            // Table view collections (all processes, grouped as trees)
            BrowserGroups = new ObservableCollection<ProcessGroup>();
            GamingGroups = new ObservableCollection<ProcessGroup>();
            CloudStorageGroups = new ObservableCollection<ProcessGroup>();
            CommunicationGroups = new ObservableCollection<ProcessGroup>();
            MediaGroups = new ObservableCollection<ProcessGroup>();
            DevelopmentGroups = new ObservableCollection<ProcessGroup>();
            SystemServicesGroups = new ObservableCollection<ProcessGroup>();
            OtherGroups = new ObservableCollection<ProcessGroup>();

            RefreshCommand = new RelayCommand(async () => await RefreshDataAsync());
            SortCommand = new RelayCommand<string>(ExecuteSort);

            _dispatcherQueue.TryEnqueue(async () =>
            {
                await Task.Delay(100);
                await InitializeAsync();
            });
        }

        // ── Card view collections ────────────────────────────────────────────────
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

        // ── Table view collections ───────────────────────────────────────────────
        public ObservableCollection<ProcessGroup> BrowserGroups { get; }
        public ObservableCollection<ProcessGroup> GamingGroups { get; }
        public ObservableCollection<ProcessGroup> CloudStorageGroups { get; }
        public ObservableCollection<ProcessGroup> CommunicationGroups { get; }
        public ObservableCollection<ProcessGroup> MediaGroups { get; }
        public ObservableCollection<ProcessGroup> DevelopmentGroups { get; }
        public ObservableCollection<ProcessGroup> SystemServicesGroups { get; }
        public ObservableCollection<ProcessGroup> OtherGroups { get; }

        /// <summary>Tracks which root process IDs are expanded, so state survives refresh.</summary>
        public HashSet<int> ExpandedProcessIds { get; } = new();

        // ── View toggle ──────────────────────────────────────────────────────────
        public bool IsTableView
        {
            get => _isTableView;
            set
            {
                _isTableView = value;
                OnPropertyChanged(nameof(IsTableView));
                OnPropertyChanged(nameof(IsCardView));
            }
        }

        public bool IsCardView => !IsTableView;

        // ── Sort state ───────────────────────────────────────────────────────────
        public string SortColumn
        {
            get => _sortColumn;
            private set
            {
                _sortColumn = value;
                OnPropertyChanged(nameof(SortColumn));
                NotifySortHeaders();
            }
        }

        public bool SortAscending
        {
            get => _sortAscending;
            private set
            {
                _sortAscending = value;
                OnPropertyChanged(nameof(SortAscending));
                NotifySortHeaders();
            }
        }

        // Column header labels with sort indicator arrows
        public string NameHeader => "Name" + (SortColumn == "Name" ? (SortAscending ? " ↑" : " ↓") : "");
        public string CpuHeader => "CPU" + (SortColumn == "Cpu" ? (SortAscending ? " ↑" : " ↓") : "");
        public string MemoryHeader => "Memory" + (SortColumn == "Memory" ? (SortAscending ? " ↑" : " ↓") : "");
        public string TimeHeader => "Running" + (SortColumn == "Time" ? (SortAscending ? " ↑" : " ↓") : "");

        private void NotifySortHeaders()
        {
            OnPropertyChanged(nameof(NameHeader));
            OnPropertyChanged(nameof(CpuHeader));
            OnPropertyChanged(nameof(MemoryHeader));
            OnPropertyChanged(nameof(TimeHeader));
        }

        // ── General state ────────────────────────────────────────────────────────
        public bool IsLoading
        {
            get => _isLoading;
            set { _isLoading = value; OnPropertyChanged(nameof(IsLoading)); }
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

        // ── Commands ─────────────────────────────────────────────────────────────
        public ICommand RefreshCommand { get; }
        public ICommand SortCommand { get; }

        // ── Initialization ───────────────────────────────────────────────────────
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
                    try { return await _systemMonitor.GetRunningProcessesAsync(); }
                    catch (AccessViolationException ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"AccessViolation: {ex.Message}");
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
                                process.Category = CategorizeProcess(process);
                                if (process.Category == ProcessCategory.SystemServices)
                                    process.IsProtected = true;
                                AllProcesses.Add(process);
                            }
                        }

                        GroupProcessesByCategory();
                        BuildProcessGroups();
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

        // ── Categorization ───────────────────────────────────────────────────────
        private ProcessCategory CategorizeProcess(ProcessInfo process)
        {
            var name = process.ProcessName?.ToLower() ?? "";
            var desc = process.Description?.ToLower() ?? "";

            if (_browserProcesses.Any(b => name.Contains(b) || desc.Contains(b)))
                return ProcessCategory.Browser;
            if (_gamingProcesses.Any(g => name.Contains(g) || desc.Contains(g)))
                return ProcessCategory.Gaming;
            if (_cloudStorageProcesses.Any(c => name.Contains(c) || desc.Contains(c)))
                return ProcessCategory.CloudStorage;
            if (_communicationProcesses.Any(c => name.Contains(c) || desc.Contains(c)))
                return ProcessCategory.Communication;
            if (_mediaProcesses.Any(m => name.Contains(m) || desc.Contains(m)))
                return ProcessCategory.Media;
            if (_developmentProcesses.Any(d => name.Contains(d) || desc.Contains(d)))
                return ProcessCategory.Development;
            if (_systemServicesProcesses.Any(s => name.Contains(s) || desc.Contains(s)))
                return ProcessCategory.SystemServices;

            return ProcessCategory.Other;
        }

        // ── Card view grouping (top 5 per category) ──────────────────────────────
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

            var grouped = AllProcesses
                .Where(p => p != null)
                .GroupBy(p => p.Category)
                .ToDictionary(g => g.Key, g => g.ToList());

            AddTopProcessesToCategory(grouped, ProcessCategory.Browser, BrowserProcesses);
            AddTopProcessesToCategory(grouped, ProcessCategory.Gaming, GamingProcesses);
            AddTopProcessesToCategory(grouped, ProcessCategory.CloudStorage, CloudStorageProcesses);
            AddTopProcessesToCategory(grouped, ProcessCategory.Communication, CommunicationProcesses);
            AddTopProcessesToCategory(grouped, ProcessCategory.Media, MediaProcesses);
            AddTopProcessesToCategory(grouped, ProcessCategory.Development, DevelopmentProcesses);
            AddTopProcessesToCategory(grouped, ProcessCategory.SystemServices, SystemServicesProcesses);
            AddTopProcessesToCategory(grouped, ProcessCategory.Other, OtherProcesses);
        }

        private static void AddTopProcessesToCategory(
            Dictionary<ProcessCategory, List<ProcessInfo>> grouped,
            ProcessCategory category,
            ObservableCollection<ProcessInfo> target)
        {
            if (!grouped.TryGetValue(category, out var processes)) return;

            var top = processes
                .OrderByDescending(p =>
                {
                    if (!string.IsNullOrEmpty(p.MemoryUsageDisplay))
                    {
                        var memStr = p.MemoryUsageDisplay.Replace("MB", "").Replace("GB", "").Trim();
                        if (double.TryParse(memStr, out var mem))
                            return p.MemoryUsageDisplay.Contains("GB") ? mem * 1024 : mem;
                    }
                    return 0;
                })
                .Take(5);

            foreach (var p in top) target.Add(p);
        }

        // ── Table view grouping (all processes, parent-child tree) ───────────────
        private void BuildProcessGroups()
        {
            BuildCategoryGroups(ProcessCategory.Browser, BrowserGroups);
            BuildCategoryGroups(ProcessCategory.Gaming, GamingGroups);
            BuildCategoryGroups(ProcessCategory.CloudStorage, CloudStorageGroups);
            BuildCategoryGroups(ProcessCategory.Communication, CommunicationGroups);
            BuildCategoryGroups(ProcessCategory.Media, MediaGroups);
            BuildCategoryGroups(ProcessCategory.Development, DevelopmentGroups);
            BuildCategoryGroups(ProcessCategory.SystemServices, SystemServicesGroups);
            BuildCategoryGroups(ProcessCategory.Other, OtherGroups);
        }

        private void BuildCategoryGroups(ProcessCategory category, ObservableCollection<ProcessGroup> target)
        {
            target.Clear();

            var processes = AllProcesses.Where(p => p.Category == category).ToList();
            if (processes.Count == 0) return;

            var pids = new HashSet<int>(processes.Select(p => p.ProcessId));

            // Processes whose parent is NOT in this category are roots
            var roots = processes.Where(p => !pids.Contains(p.ParentProcessId));
            var childrenByParent = processes
                .Where(p => pids.Contains(p.ParentProcessId))
                .GroupBy(p => p.ParentProcessId)
                .ToDictionary(g => g.Key, g => g.ToList());

            foreach (var root in SortProcessList(roots))
            {
                var group = new ProcessGroup { RootProcess = root };
                group.IsExpanded = ExpandedProcessIds.Contains(root.ProcessId);

                if (childrenByParent.TryGetValue(root.ProcessId, out var children))
                    foreach (var child in SortProcessList(children))
                        group.ChildProcesses.Add(child);

                target.Add(group);
            }
        }

        private IEnumerable<ProcessInfo> SortProcessList(IEnumerable<ProcessInfo> processes)
        {
            return _sortColumn switch
            {
                "Name" => _sortAscending
                    ? processes.OrderBy(p => p.Description)
                    : processes.OrderByDescending(p => p.Description),
                "Cpu" => _sortAscending
                    ? processes.OrderBy(p => p.CpuUsagePercent)
                    : processes.OrderByDescending(p => p.CpuUsagePercent),
                "Time" => _sortAscending
                    ? processes.OrderBy(p => p.StartTime)
                    : processes.OrderByDescending(p => p.StartTime),
                _ => _sortAscending  // "Memory" default
                    ? processes.OrderBy(p => p.MemoryUsageMB)
                    : processes.OrderByDescending(p => p.MemoryUsageMB),
            };
        }

        private void ExecuteSort(string column)
        {
            if (SortColumn == column)
                SortAscending = !SortAscending;
            else
            {
                // Use backing fields to avoid double notification before rebuild
                _sortColumn = column;
                _sortAscending = column == "Name"; // ascending for name, descending for metrics
                OnPropertyChanged(nameof(SortColumn));
                OnPropertyChanged(nameof(SortAscending));
                NotifySortHeaders();
            }
            BuildProcessGroups();
        }

        // ── Search filter ────────────────────────────────────────────────────────
        private void FilterProcesses()
        {
            FilteredProcesses.Clear();

            var filtered = string.IsNullOrWhiteSpace(SearchQuery)
                ? AllProcesses
                : AllProcesses.Where(p =>
                    (p.ProcessName?.Contains(SearchQuery, StringComparison.OrdinalIgnoreCase) ?? false) ||
                    (p.Description?.Contains(SearchQuery, StringComparison.OrdinalIgnoreCase) ?? false) ||
                    (p.FriendlyExplanation?.Contains(SearchQuery, StringComparison.OrdinalIgnoreCase) ?? false));

            foreach (var p in filtered) FilteredProcesses.Add(p);
        }

        // ── Auto-refresh ─────────────────────────────────────────────────────────
        private void StartAutoRefresh()
        {
            _refreshTimer = _dispatcherQueue.CreateTimer();
            _refreshTimer.Interval = TimeSpan.FromSeconds(5);
            _refreshTimer.Tick += async (s, e) =>
            {
                try { await RefreshDataAsync(); }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Auto-refresh error: {ex.Message}");
                }
            };
            _refreshTimer.Start();
        }

        public void StopAutoRefresh()
        {
            _refreshTimer?.Stop();
            _refreshTimer = null;
        }

        // ── End process ──────────────────────────────────────────────────────────
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
                        case ProcessCategory.Browser: BrowserProcesses.Remove(process); break;
                        case ProcessCategory.Gaming: GamingProcesses.Remove(process); break;
                        case ProcessCategory.CloudStorage: CloudStorageProcesses.Remove(process); break;
                        case ProcessCategory.Communication: CommunicationProcesses.Remove(process); break;
                        case ProcessCategory.SystemServices: SystemServicesProcesses.Remove(process); break;
                        case ProcessCategory.Development: DevelopmentProcesses.Remove(process); break;
                        case ProcessCategory.Media: MediaProcesses.Remove(process); break;
                        default: OtherProcesses.Remove(process); break;
                    }

                    // Rebuild table groups to remove the process from tree view too
                    BuildProcessGroups();
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

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged(string propertyName) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
