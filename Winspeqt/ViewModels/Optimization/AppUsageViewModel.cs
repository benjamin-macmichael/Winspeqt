using System;
using System.Collections.ObjectModel;
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
        private AppUsageStats _usageStats;
        private bool _isLoading;
        private string _searchText;
        private bool _isTrackingEnabled;

        public AppUsageViewModel() : this(null)
        {
        }

        public AppUsageViewModel(AppUsageService appUsageService)
        {
            // Use provided service or create new one (for design-time)
            _appUsageService = appUsageService ?? new AppUsageService();
            Applications = new ObservableCollection<AppUsageModel>();
            IsTrackingEnabled = true;

            // Get dispatcher queue for WinUI 3
            _dispatcherQueue = DispatcherQueue.GetForCurrentThread();

            RefreshCommand = new RelayCommand(Refresh);
            ResetTrackingCommand = new RelayCommand(ResetTracking);
            ToggleTrackingCommand = new RelayCommand(ToggleTracking);

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

        private void Refresh() => _ = RefreshDataAsync();

        private async Task InitializeAsync()
        {
            await RefreshDataAsync();
        }

        private async Task RefreshDataAsync()
        {
            IsLoading = true;
            try
            {
                var apps = await _appUsageService.GetAppUsageDataAsync();
                var stats = await _appUsageService.GetUsageStatsAsync();

                Applications.Clear();
                foreach (var app in apps)
                {
                    if (string.IsNullOrWhiteSpace(SearchText) ||
                        app.AppName.Contains(SearchText, StringComparison.OrdinalIgnoreCase))
                    {
                        Applications.Add(app);
                    }
                }

                UsageStats = stats;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error refreshing app usage data: {ex.Message}");
            }
            finally
            {
                IsLoading = false;
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
            if (IsTrackingEnabled)
            {
                _appUsageService.StartTracking();
                _updateTimer.Start();
            }
            else
            {
                _appUsageService.StopTracking();
                _updateTimer.Stop();
            }
        }
    }
}