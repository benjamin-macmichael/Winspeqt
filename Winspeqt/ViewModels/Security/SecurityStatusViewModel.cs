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

                var status = await _securityService.GetSecurityStatusAsync();

                _dispatcherQueue.TryEnqueue(() =>
                {
                    DefenderStatus = status.WindowsDefenderStatus;
                    FirewallStatus = status.FirewallStatus;
                    UpdateStatus = status.WindowsUpdateStatus;
                    BitLockerStatus = status.BitLockerStatus;
                    OverallScore = status.OverallSecurityScore;
                    OverallStatus = status.OverallStatus;
                    OverallScoreColor = GetScoreColor(status.OverallSecurityScore);

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