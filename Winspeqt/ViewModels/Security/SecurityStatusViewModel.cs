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

        private bool _isDefenderLoading = true;
        public bool IsDefenderLoading
        {
            get => _isDefenderLoading;
            set => SetProperty(ref _isDefenderLoading, value);
        }

        private bool _isFirewallLoading = true;
        public bool IsFirewallLoading
        {
            get => _isFirewallLoading;
            set => SetProperty(ref _isFirewallLoading, value);
        }

        private bool _isUpdateLoading = true;
        public bool IsUpdateLoading
        {
            get => _isUpdateLoading;
            set => SetProperty(ref _isUpdateLoading, value);
        }

        private bool _isBitLockerLoading = true;
        public bool IsBitLockerLoading
        {
            get => _isBitLockerLoading;
            set => SetProperty(ref _isBitLockerLoading, value);
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

            // Set initial "Checking..." status for all cards
            DefenderStatus = new SecurityComponentStatus { Status = "Checking...", Message = "Please wait...", Icon = "⏳", Color = "#9E9E9E" };
            FirewallStatus = new SecurityComponentStatus { Status = "Checking...", Message = "Please wait...", Icon = "⏳", Color = "#9E9E9E" };
            UpdateStatus = new SecurityComponentStatus { Status = "Checking...", Message = "Please wait...", Icon = "⏳", Color = "#9E9E9E" };
            BitLockerStatus = new SecurityComponentStatus { Status = "Checking...", Message = "Please wait...", Icon = "⏳", Color = "#9E9E9E" };

            OverallStatus = "Checking security status...";
            OverallScore = 0;
            OverallScoreColor = "#9E9E9E";

            // Initial load - no blocking
            _ = LoadSecurityStatusAsync();
        }

        private async Task LoadSecurityStatusAsync()
        {
            try
            {
                // Set all cards to loading
                _dispatcherQueue.TryEnqueue(() =>
                {
                    IsDefenderLoading = true;
                    IsFirewallLoading = true;
                    IsUpdateLoading = true;
                    IsBitLockerLoading = true;
                });

                // Check all in parallel - each updates independently as it completes
                var defenderTask = Task.Run(async () =>
                {
                    var result = _securityService.CheckWindowsDefender();
                    _dispatcherQueue.TryEnqueue(() =>
                    {
                        DefenderStatus = result;
                        IsDefenderLoading = false;
                    });
                });

                var firewallTask = Task.Run(async () =>
                {
                    var result = _securityService.CheckFirewall();
                    _dispatcherQueue.TryEnqueue(() =>
                    {
                        FirewallStatus = result;
                        IsFirewallLoading = false;
                    });
                });

                var updateTask = Task.Run(async () =>
                {
                    var result = _securityService.CheckWindowsUpdate();
                    _dispatcherQueue.TryEnqueue(() =>
                    {
                        UpdateStatus = result;
                        IsUpdateLoading = false;
                    });
                });

                var bitlockerTask = Task.Run(async () =>
                {
                    var result = _securityService.CheckBitLocker();
                    _dispatcherQueue.TryEnqueue(() =>
                    {
                        BitLockerStatus = result;
                        IsBitLockerLoading = false;
                    });
                });

                // Wait for all to complete
                await Task.WhenAll(defenderTask, firewallTask, updateTask, bitlockerTask);

                // Calculate overall score once everything is done
                var status = new SecurityStatusInfo
                {
                    WindowsDefenderStatus = DefenderStatus,
                    FirewallStatus = FirewallStatus,
                    WindowsUpdateStatus = UpdateStatus,
                    BitLockerStatus = BitLockerStatus
                };

                var score = _securityService.CalculateSecurityScore(status);
                var overallStatus = _securityService.GetOverallStatus(score);

                _dispatcherQueue.TryEnqueue(() =>
                {
                    OverallScore = score;
                    OverallStatus = overallStatus;
                    OverallScoreColor = GetScoreColor(score);
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading security status: {ex.Message}");

                _dispatcherQueue.TryEnqueue(() =>
                {
                    OverallStatus = "Error loading security status";
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