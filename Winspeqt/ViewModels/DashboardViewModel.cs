using Microsoft.UI.Dispatching;
using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Input;
using Winspeqt.Helpers;
using Winspeqt.Services;

namespace Winspeqt.ViewModels
{
    public class DashboardViewModel : ObservableObject
    {
        public ICommand SecurityCommand { get; }
        public ICommand OptimizationCommand { get; }
        public ICommand MonitoringCommand { get; }
        public ICommand SettingsCommand { get; }

        public ICommand SecurityScoreCommand { get; }
        public ICommand DiskCommand { get; }
        public ICommand RamCommand { get; }

        public event EventHandler<string>? NavigationRequested;

        private readonly SecurityService _securityService = new();
        private readonly SystemMonitorService _systemMonitor = new();
        private DispatcherQueueTimer? _fastTimer;
        private DispatcherQueueTimer? _slowTimer;

        // Security Score
        private int _securityScore;
        public int SecurityScore { get => _securityScore; set => SetProperty(ref _securityScore, value); }

        private string _securityScoreText = "--/100";
        public string SecurityScoreText { get => _securityScoreText; set => SetProperty(ref _securityScoreText, value); }

        private string _securityScoreColor = "#808080";
        public string SecurityScoreColor { get => _securityScoreColor; set => SetProperty(ref _securityScoreColor, value); }

        // Disk / Total Memory (C: drive)
        private double _diskPercent;
        public double DiskPercent { get => _diskPercent; set => SetProperty(ref _diskPercent, value); }

        private string _diskCenterText = "--%";
        public string DiskCenterText { get => _diskCenterText; set => SetProperty(ref _diskCenterText, value); }

        private string _diskSubText = "-- / -- GB";
        public string DiskSubText { get => _diskSubText; set => SetProperty(ref _diskSubText, value); }

        private string _diskColor = "#808080";
        public string DiskColor { get => _diskColor; set => SetProperty(ref _diskColor, value); }

        // RAM Usage
        private double _ramPercent;
        public double RamPercent { get => _ramPercent; set => SetProperty(ref _ramPercent, value); }

        private string _ramCenterText = "--%";
        public string RamCenterText { get => _ramCenterText; set => SetProperty(ref _ramCenterText, value); }

        private string _ramSubText = "-- / -- GB";
        public string RamSubText { get => _ramSubText; set => SetProperty(ref _ramSubText, value); }

        private string _ramColor = "#808080";
        public string RamColor { get => _ramColor; set => SetProperty(ref _ramColor, value); }

        public DashboardViewModel()
        {
            SecurityCommand = new RelayCommand(OnSecurityClicked);
            OptimizationCommand = new RelayCommand(OnOptimizationClicked);
            MonitoringCommand = new RelayCommand(OnMonitoringClicked);
            SettingsCommand = new RelayCommand(OnSettingsClicked);

            SecurityScoreCommand = new RelayCommand(OnSecurityClicked);
            DiskCommand = new RelayCommand(OnOptimizationClicked);
            RamCommand = new RelayCommand(() => NavigationRequested?.Invoke(this, "ResourceTrends"));
        }

        public void StartRefresh(DispatcherQueue dispatcher)
        {
            _ = LoadSecurityAsync();
            _ = LoadSystemMetricsAsync();

            // RAM + Disk refresh every 5 seconds
            _fastTimer = dispatcher.CreateTimer();
            _fastTimer.Interval = TimeSpan.FromSeconds(5);
            _fastTimer.Tick += async (s, e) => await LoadSystemMetricsAsync();
            _fastTimer.Start();

            // Security score refresh every 60 seconds
            _slowTimer = dispatcher.CreateTimer();
            _slowTimer.Interval = TimeSpan.FromSeconds(60);
            _slowTimer.Tick += async (s, e) => await LoadSecurityAsync();
            _slowTimer.Start();
        }

        public void StopRefresh()
        {
            _fastTimer?.Stop();
            _fastTimer = null;
            _slowTimer?.Stop();
            _slowTimer = null;
        }

        private async Task LoadSecurityAsync()
        {
            try
            {
                var status = await _securityService.GetSecurityStatusAsync();
                var score = status.OverallSecurityScore;
                SecurityScore = score;
                SecurityScoreText = $"{score}/100";
                SecurityScoreColor = score >= 80 ? "#4CAF50" : score >= 60 ? "#FFC107" : "#F44336";
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading security score: {ex.Message}");
                SecurityScoreText = "Error";
                SecurityScoreColor = "#808080";
            }
        }

        private async Task LoadSystemMetricsAsync()
        {
            try
            {
                // RAM
                var totalMb = await _systemMonitor.GetTotalMemoryMBAsync();
                var availMb = await _systemMonitor.GetAvailableMemoryMBAsync();
                var usedMb = totalMb - availMb;
                var ramPct = totalMb > 0 ? (double)usedMb / totalMb * 100.0 : 0;
                var usedGb = Math.Round(usedMb / 1024.0, 1);
                var totalGb = Math.Round(totalMb / 1024.0, 1);

                RamPercent = ramPct;
                RamCenterText = $"{(int)ramPct}%";
                RamSubText = $"{usedGb} GB / {totalGb} GB";
                RamColor = ramPct <= 60 ? "#4CAF50" : ramPct <= 80 ? "#FFC107" : "#F44336";

                // Disk (C: drive) — synchronous but fast
                var drive = new DriveInfo("C");
                var diskTotalGb = Math.Round(drive.TotalSize / 1_073_741_824.0, 1);
                var diskFreeGb = Math.Round(drive.AvailableFreeSpace / 1_073_741_824.0, 1);
                var diskUsedGb = Math.Round(diskTotalGb - diskFreeGb, 1);
                var diskPct = diskTotalGb > 0 ? diskUsedGb / diskTotalGb * 100.0 : 0;

                DiskPercent = diskPct;
                DiskCenterText = $"{(int)diskPct}%";
                DiskSubText = $"{diskUsedGb} GB / {diskTotalGb} GB";
                DiskColor = diskPct <= 70 ? "#4CAF50" : diskPct <= 85 ? "#FFC107" : "#F44336";
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading system metrics: {ex.Message}");
            }
        }

        private void OnSecurityClicked() => NavigationRequested?.Invoke(this, "Security");
        private void OnOptimizationClicked() => NavigationRequested?.Invoke(this, "Optimization");
        private void OnMonitoringClicked() => NavigationRequested?.Invoke(this, "Monitoring");
        private void OnSettingsClicked() => NavigationRequested?.Invoke(this, "Settings");
    }
}
