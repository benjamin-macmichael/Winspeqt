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

        public int[] Test
        {
            get => [1, 2, 3, 1, 6];
        }

        private ObservableCollection<double> _cpuUsageValues = new();

        public ObservableCollection<double> CpuUsageValues
        {
            get => _cpuUsageValues;
            set => SetProperty(ref _cpuUsageValues, value);
        }

        private Queue<double> _memoryUsage = new Queue<double>(new double[60]);
        public Queue<double> MemoryUsage
        {
            get => _memoryUsage;
            set => SetProperty(ref _memoryUsage, value);
        }

        private ObservableCollection<double> _memoryUsageValues = new();

        public ObservableCollection<double> MemoryUsageValues
        {
            get => _memoryUsageValues;
            set => SetProperty(ref _memoryUsageValues, value);
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

            // Start auto-refresh every second
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
                TimeSpan.FromSeconds(1)
            );
        }

        private async Task RefreshDataAsync()
        {
            try
            {
                double cpu = 0;
                double memoryUsedPercent = 0;
                try
                {
                    cpu = await _systemStatistics.CpuUsage(_monitorService);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"CPU error: {ex.Message}");
                }

                try
                {
                    var availableMb = await _systemStatistics.AvailableMemory(_monitorService);
                    var totalMb = await _systemStatistics.TotalMemory(_monitorService);
                    if (totalMb > 0)
                    {
                        memoryUsedPercent = (totalMb - availableMb) * 100.0 / totalMb;
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Memory error: {ex.Message}");
                }

                _dispatcherQueue.TryEnqueue(() =>
                {
                    // update your rolling buffer
                    _cpuUsage.Dequeue();
                    _cpuUsage.Enqueue(cpu);

                    // update bindable collection
                    CpuUsageValues.Clear();
                    foreach (var v in _cpuUsage)
                        CpuUsageValues.Add(v);

                    _memoryUsage.Dequeue();
                    _memoryUsage.Enqueue(memoryUsedPercent);

                    MemoryUsageValues.Clear();
                    foreach (var v in _memoryUsage)
                        MemoryUsageValues.Add(v);

                    IsLoading = false;
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ERROR in RefreshDataAsync: {ex.Message}");
                _dispatcherQueue.TryEnqueue(() =>
                {
                    IsLoading = false;
                    if (string.IsNullOrEmpty(CpuStatusMessage))
                        CpuStatusMessage = $"Error loading data: {ex.Message}";
                });
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
