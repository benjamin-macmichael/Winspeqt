// ViewModels/PerformanceTrendsViewModel.cs

using Microsoft.UI.Dispatching;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows.Input;
using Winspeqt.Helpers;
using Winspeqt.Models;
using Winspeqt.Services;
using System.Linq;
using System.Runtime.CompilerServices;

namespace Winspeqt.ViewModels.Monitoring
{
    public class PerformanceTrendsViewModel : ObservableObject
    {
        private readonly SystemMonitorService _monitorService;
        private readonly SystemStatistics _systemStatistics;
        private readonly DispatcherQueue _dispatcherQueue;
        private System.Threading.Timer _refreshTimer;
        
        private Queue<double> _cpuUsage = new Queue<double>(new double[60]);
        public Queue<double> CpuUsage
        {
            get => _cpuUsage;
            set => SetProperty(ref _cpuUsage, value);
        }

        private ObservableCollection<double> _cpuUsageValues = new();

        public ObservableCollection<double> CpuUsageValues
        {
            get => _cpuUsageValues;
            set => SetProperty(ref _cpuUsageValues, value);
        }
        
        private bool _isLoading;
        public bool IsLoading
        {
            get => _isLoading;
            set => SetProperty(ref _isLoading, value);
        }
        
        private string _cpuStatusMessage;
        public string CpuStatusMessage
        {
            get => _cpuStatusMessage;
            set => SetProperty(ref _cpuStatusMessage, value);
        }
        
        public ICommand RefreshCommand { get; }
        public ICommand EndProcessCommand { get; }

        public PerformanceTrendsViewModel()
        {
            _monitorService = new SystemMonitorService();
            _systemStatistics = new SystemStatistics();
            _dispatcherQueue = DispatcherQueue.GetForCurrentThread();
            
            RefreshCommand = new RelayCommand(async () => await RefreshDataAsync());
            EndProcessCommand = new RelayCommand<ProcessInfo>(async (process) => await EndProcessAsync(process));

            // Set loading to true initially
            IsLoading = true;

            // Start auto-refresh every 2 seconds
            StartAutoRefresh();

            // Initial load
            _ = RefreshDataAsync();
        }
        
        private void StartAutoRefresh()
        {
            _refreshTimer = new System.Threading.Timer(
                async _ => await RefreshDataAsync(),
                null,
                TimeSpan.Zero,
                TimeSpan.FromSeconds(2)
            );
        }

        private async Task RefreshDataAsync()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("Starting refresh...");

                double cpu = 0;
                
                try
                {
                    cpu = await _systemStatistics.CpuUsage(_monitorService);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"CPU error: {ex.Message}");
                }

                CpuUsage.Dequeue();
                CpuUsage.Enqueue(cpu);
                
                CpuUsageValues.Clear();
                foreach (var v in CpuUsage)
                    CpuUsageValues.Add(v);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ERROR in RefreshDataAsync: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");

                // Try to update error message on UI thread
                try
                {
                    _dispatcherQueue.TryEnqueue(() =>
                    {
                        IsLoading = false;
                        // Don't overwrite good data with error message if we already have it
                        if (string.IsNullOrEmpty(CpuStatusMessage))
                        {
                            CpuStatusMessage = $"Error loading data: {ex.Message}";
                        }
                    });
                }
                catch
                {
                    // Failed to even show error - just silently fail this update cycle
                }
            }
        }
        
        private async Task EndProcessAsync(ProcessInfo processInfo)
        {
            if (processInfo == null) return;

            try
            {
                var process = System.Diagnostics.Process.GetProcessById(processInfo.ProcessId);
                process.Kill();
                await RefreshDataAsync();
            }
            catch
            {
                // Process might already be closed or we don't have permission
            }
        }

        public void StopAutoRefresh()
        {
            _refreshTimer?.Dispose();
        }
    }

}