using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Microsoft.UI.Dispatching;
using Winspeqt.Models;
using Winspeqt.Services;
using Winspeqt.Helpers;

namespace Winspeqt.ViewModels.Optimization
{
    public class AppUsageViewModel : ObservableObject
    {
        private readonly AppUsageService _appUsageService;
        private readonly DispatcherQueue _dispatcherQueue;
        private DispatcherQueueTimer _updateTimer;

        private ObservableCollection<AppUsageModel> _applications;
        private ObservableCollection<InstalledAppModel> _installedApps;
        private AppUsageStats _usageStats;
        private bool _isLoading;
        private string _searchText;
        private bool _isTrackingEnabled;
        private bool _showOnlyUnused;
        private bool _showInstalledAppsView;

        public AppUsageViewModel() : this(null)
        {
        }

        public AppUsageViewModel(AppUsageService appUsageService)
        {
            // Use provided service or create new one (for design-time)
            _appUsageService = appUsageService ?? new AppUsageService();
            Applications = new ObservableCollection<AppUsageModel>();
            InstalledApps = new ObservableCollection<InstalledAppModel>();
            IsTrackingEnabled = true;
            ShowOnlyUnused = false; // Default to showing ALL apps

            // Get dispatcher queue for WinUI 3
            _dispatcherQueue = DispatcherQueue.GetForCurrentThread();

            RefreshCommand = new RelayCommand(Refresh);
            ResetTrackingCommand = new RelayCommand(ResetTracking);
            ToggleTrackingCommand = new RelayCommand(ToggleTracking);
            RefreshInstalledAppsCommand = new RelayCommand(RefreshInstalledApps);
            ShowUsageViewCommand = new RelayCommand(() => ShowInstalledAppsView = false);
            ShowInstalledAppsCommand = new RelayCommand(() =>
            {
                ShowInstalledAppsView = true;
                RefreshInstalledApps(); // Always refresh when switching to this view
            });

            // WinUI 3 timer
            _updateTimer = _dispatcherQueue.CreateTimer();
            _updateTimer.Interval = TimeSpan.FromSeconds(5);
            _updateTimer.Tick += async (s, e) => await RefreshDataAsync();
            _updateTimer.Start();

            _ = InitializeAsync();
        }

        public ObservableCollection<AppUsageModel> Applications
        {
            get => _applications;
            set => SetProperty(ref _applications, value);
        }

        public ObservableCollection<InstalledAppModel> InstalledApps
        {
            get => _installedApps;
            set => SetProperty(ref _installedApps, value);
        }

        public AppUsageStats UsageStats
        {
            get => _usageStats;
            set
            {
                SetProperty(ref _usageStats, value);
                OnPropertyChanged(nameof(TotalScreenTimeFormatted));
                OnPropertyChanged(nameof(TrackingDuration));
            }
        }

        public bool IsLoading
        {
            get => _isLoading;
            set => SetProperty(ref _isLoading, value);
        }

        public string SearchText
        {
            get => _searchText;
            set
            {
                SetProperty(ref _searchText, value);
                _ = RefreshDataAsync();
            }
        }

        public bool IsTrackingEnabled
        {
            get => _isTrackingEnabled;
            set => SetProperty(ref _isTrackingEnabled, value);
        }

        public bool ShowOnlyUnused
        {
            get => _showOnlyUnused;
            set
            {
                SetProperty(ref _showOnlyUnused, value);
                RefreshInstalledApps();
            }
        }

        public bool ShowInstalledAppsView
        {
            get => _showInstalledAppsView;
            set => SetProperty(ref _showInstalledAppsView, value);
        }

        public string TotalScreenTimeFormatted
        {
            get
            {
                if (UsageStats == null) return "0h 0m";
                var ts = UsageStats.TotalScreenTime;
                return $"{(int)ts.TotalHours}h {ts.Minutes}m";
            }
        }

        public string TrackingDuration
        {
            get
            {
                if (UsageStats == null) return "0 min";
                var duration = DateTime.Now - UsageStats.TrackingStartTime;
                if (duration.TotalDays >= 1)
                    return $"{(int)duration.TotalDays} days";
                else if (duration.TotalHours >= 1)
                    return $"{(int)duration.TotalHours} hours";
                else
                    return $"{(int)duration.TotalMinutes} min";
            }
        }

        public ICommand RefreshCommand { get; }
        public ICommand ResetTrackingCommand { get; }
        public ICommand ToggleTrackingCommand { get; }
        public ICommand RefreshInstalledAppsCommand { get; }
        public ICommand ShowUsageViewCommand { get; }
        public ICommand ShowInstalledAppsCommand { get; }

        private void Refresh() => _ = RefreshDataAsync();

        private void RefreshInstalledApps() => _ = RefreshInstalledAppsAsync();

        private bool GetTrackingPreference()
        {
            try
            {
                var localSettings = Windows.Storage.ApplicationData.Current.LocalSettings;
                if (localSettings.Values.ContainsKey("AppUsageTrackingEnabled"))
                {
                    return (bool)localSettings.Values["AppUsageTrackingEnabled"];
                }
                // Default to false - user must opt in
                return false;
            }
            catch
            {
                return false;
            }
        }

        private void SaveTrackingPreference(bool enabled)
        {
            try
            {
                var localSettings = Windows.Storage.ApplicationData.Current.LocalSettings;
                localSettings.Values["AppUsageTrackingEnabled"] = enabled;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error saving tracking preference: {ex.Message}");
            }
        }

        private async Task InitializeAsync()
        {
            await RefreshDataAsync();
            await RefreshInstalledAppsAsync();
        }

        private async Task RefreshInstalledAppsAsync()
        {
            try
            {
                var allApps = await _appUsageService.GetInstalledAppsAsync();

                InstalledApps.Clear();
                foreach (var app in allApps)
                {
                    if (!ShowOnlyUnused || app.IsUnused)
                    {
                        InstalledApps.Add(app);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading installed apps: {ex.Message}");
            }
        }

        private async Task RefreshDataAsync()
        {
            try
            {
                var apps = await _appUsageService.GetAppUsageDataAsync();
                var stats = await _appUsageService.GetUsageStatsAsync();

                // Update existing items or add new ones instead of clearing
                foreach (var app in apps)
                {
                    if (string.IsNullOrWhiteSpace(SearchText) ||
                        app.AppName.Contains(SearchText, StringComparison.OrdinalIgnoreCase))
                    {
                        var existing = Applications.FirstOrDefault(a => a.ProcessName == app.ProcessName);
                        if (existing != null)
                        {
                            // Update existing item properties
                            var index = Applications.IndexOf(existing);
                            Applications[index] = app;
                        }
                        else
                        {
                            // Add new item
                            Applications.Add(app);
                        }
                    }
                }

                // Remove items that are no longer in the list
                var currentProcessNames = apps.Where(a =>
                    string.IsNullOrWhiteSpace(SearchText) ||
                    a.AppName.Contains(SearchText, StringComparison.OrdinalIgnoreCase))
                    .Select(a => a.ProcessName).ToHashSet();

                for (int i = Applications.Count - 1; i >= 0; i--)
                {
                    if (!currentProcessNames.Contains(Applications[i].ProcessName))
                    {
                        Applications.RemoveAt(i);
                    }
                }

                UsageStats = stats;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error refreshing app usage data: {ex.Message}");
            }
        }

        private void ResetTracking()
        {
            _appUsageService.ResetTracking();
            _ = RefreshDataAsync();
        }

        private void ToggleTracking()
        {
            IsTrackingEnabled = !IsTrackingEnabled;
            SaveTrackingPreference(IsTrackingEnabled);

            if (IsTrackingEnabled)
            {
                _appUsageService.StartTracking();

                // Start timer if not already started
                if (_updateTimer == null)
                {
                    _updateTimer = _dispatcherQueue.CreateTimer();
                    _updateTimer.Interval = TimeSpan.FromSeconds(5);
                    _updateTimer.Tick += async (s, e) => await RefreshDataAsync();
                }
                _updateTimer.Start();

                _ = InitializeAsync();
            }
            else
            {
                _appUsageService.StopTracking();
                _updateTimer?.Stop();
            }
        }
    }
}