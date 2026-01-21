using Microsoft.UI.Dispatching;
using System;
using System.Threading.Tasks;
using System.Windows.Input;
using Winspeqt.Helpers;
using Winspeqt.Models;
using Winspeqt.Services;

namespace Winspeqt.ViewModels.Security
{
    public class SecurityStatusViewModel : ObservableObject
    {
        private readonly SecurityService _securityService;
        private readonly DispatcherQueue _dispatcherQueue;

        private int _loadingProgress;
        public int LoadingProgress
        {
            get => _loadingProgress;
            set => SetProperty(ref _loadingProgress, value);
        }

        private string _loadingMessage = "Checking security...";
        public string LoadingMessage
        {
            get => _loadingMessage;
            set => SetProperty(ref _loadingMessage, value);
        }

        private bool _isLoading;
        public bool IsLoading
        {
            get => _isLoading;
            set => SetProperty(ref _isLoading, value);
        }

        private SecurityComponentStatus _defenderStatus;
        public SecurityComponentStatus DefenderStatus
        {
            get => _defenderStatus;
            set => SetProperty(ref _defenderStatus, value);
        }

        private SecurityComponentStatus _firewallStatus;
        public SecurityComponentStatus FirewallStatus
        {
            get => _firewallStatus;
            set => SetProperty(ref _firewallStatus, value);
        }

        private SecurityComponentStatus _updateStatus;
        public SecurityComponentStatus UpdateStatus
        {
            get => _updateStatus;
            set => SetProperty(ref _updateStatus, value);
        }

        private SecurityComponentStatus _bitlockerStatus;
        public SecurityComponentStatus BitLockerStatus
        {
            get => _bitlockerStatus;
            set => SetProperty(ref _bitlockerStatus, value);
        }

        private int _overallScore;
        public int OverallScore
        {
            get => _overallScore;
            set => SetProperty(ref _overallScore, value);
        }

        private string _overallStatus;
        public string OverallStatus
        {
            get => _overallStatus;
            set => SetProperty(ref _overallStatus, value);
        }

        private string _overallScoreColor;
        public string OverallScoreColor
        {
            get => _overallScoreColor;
            set => SetProperty(ref _overallScoreColor, value);
        }

        public ICommand RefreshCommand { get; }

        public SecurityStatusViewModel()
        {
            _securityService = new SecurityService();
            _dispatcherQueue = DispatcherQueue.GetForCurrentThread();

            RefreshCommand = new RelayCommand(async () => await LoadSecurityStatusAsync());

            // Initial load
            IsLoading = true;
            _ = LoadSecurityStatusAsync();
        }

        private async Task LoadSecurityStatusAsync()
        {
            try
            {
                IsLoading = true;
                LoadingProgress = 0;

                // Check Windows Defender
                var defenderResult = await Task.Run(() => _securityService.CheckWindowsDefender());
                _dispatcherQueue.TryEnqueue(() => { DefenderStatus = defenderResult; LoadingProgress = 25; });

                // Check Firewall
                var firewallResult = await Task.Run(() => _securityService.CheckFirewall());
                _dispatcherQueue.TryEnqueue(() => { FirewallStatus = firewallResult; LoadingProgress = 50; });

                // Check Windows Update
                var updateResult = await Task.Run(() => _securityService.CheckWindowsUpdate());
                _dispatcherQueue.TryEnqueue(() => { UpdateStatus = updateResult; LoadingProgress = 75; });

                // Check BitLocker
                var bitlockerResult = await Task.Run(() => _securityService.CheckBitLocker());
                _dispatcherQueue.TryEnqueue(() => { BitLockerStatus = bitlockerResult; LoadingProgress = 90; });

                // Calculate scores
                var status = new SecurityStatusInfo
                {
                    WindowsDefenderStatus = defenderResult,
                    FirewallStatus = firewallResult,
                    WindowsUpdateStatus = updateResult,
                    BitLockerStatus = bitlockerResult
                };

                var score = _securityService.CalculateSecurityScore(status);
                var overallStatus = _securityService.GetOverallStatus(score);

                _dispatcherQueue.TryEnqueue(() =>
                {
                    OverallScore = score;
                    OverallStatus = overallStatus;
                    OverallScoreColor = GetScoreColor(score);
                    LoadingProgress = 100;
                    IsLoading = false;
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading security status: {ex.Message}");

                _dispatcherQueue.TryEnqueue(() =>
                {
                    OverallStatus = "Error loading security status";
                    IsLoading = false;
                });
            }
        }

        private string GetScoreColor(int score)
        {
            if (score >= 85)
                return "#4CAF50"; // Green
            else if (score >= 70)
                return "#8BC34A"; // Light Green
            else if (score >= 50)
                return "#FF9800"; // Orange
            else
                return "#F44336"; // Red
        }
    }
}