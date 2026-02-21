// ViewModels/PerformanceTrendsViewModel.cs

using LiveChartsCore;
using LiveChartsCore.Kernel.Sketches;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using Microsoft.UI.Dispatching;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows.Input;
using Winspeqt.Helpers;
using Winspeqt.Models;
using Winspeqt.Services;

namespace Winspeqt.ViewModels.Monitoring
{
    public class PerformanceTrendsViewModel : ObservableObject
    {
        private readonly SystemMonitorService _monitorService;
        private readonly DispatcherQueue _dispatcherQueue;
        private System.Threading.Timer? _refreshTimer;

        const int _secondsTracked = 60;

        private Queue<double> _cpuUsage = new Queue<double>(new double[_secondsTracked]);
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

        public ISeries[] CpuSeries { get; }
        public IEnumerable<ICartesianAxis> CpuYAxes { get; }
        public IEnumerable<ICartesianAxis> XAxes { get; }

        private Queue<double> _memoryUsage = new Queue<double>(new double[_secondsTracked]);
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

        public ISeries[] MemorySeries { get; }
        public IEnumerable<ICartesianAxis> MemoryYAxes { get; }

        private Queue<double> _diskUsage = new Queue<double>(new double[_secondsTracked]);
        public Queue<double> DiskUsage
        {
            get => _diskUsage;
            set => SetProperty(ref _diskUsage, value);
        }

        private ObservableCollection<double> _diskUsageValues = new();

        public ObservableCollection<double> DiskUsageValues
        {
            get => _diskUsageValues;
            set => SetProperty(ref _diskUsageValues, value);
        }

        public ISeries[] DiskSeries { get; }
        public IEnumerable<ICartesianAxis> DiskYAxes { get; }

        private Queue<double> _networkSent = new Queue<double>(new double[_secondsTracked]);
        public Queue<double> NetworkSent
        {
            get => _networkSent;
            set => SetProperty(ref _networkSent, value);
        }

        private ObservableCollection<double> _networkSentValues = new();

        public ObservableCollection<double> NetworkSentValues
        {
            get => _networkSentValues;
            set => SetProperty(ref _networkSentValues, value);
        }

        private Queue<double> _networkReceived = new Queue<double>(new double[_secondsTracked]);
        public Queue<double> NetworkReceived
        {
            get => _networkReceived;
            set => SetProperty(ref _networkReceived, value);
        }

        private ObservableCollection<double> _networkReceivedValues = new();

        public ObservableCollection<double> NetworkReceivedValues
        {
            get => _networkReceivedValues;
            set => SetProperty(ref _networkReceivedValues, value);
        }

        public ISeries[] NetworkSeries { get; }
        public IEnumerable<ICartesianAxis> NetworkYAxes { get; }

        private bool _isLoading;
        public bool IsLoading
        {
            get => _isLoading;
            set => SetProperty(ref _isLoading, value);
        }

        private string _cpuStatusMessage = string.Empty;
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
            _dispatcherQueue = DispatcherQueue.GetForCurrentThread();

            var axisTextColor = new SolidColorPaint(SKColor.Parse("#8A8A8A"));
            const int labelSize = 12;
            const int nameSize = 18;
            var yAxisNamePadding = new LiveChartsCore.Drawing.Padding(0, 0, 0, -6);

            // Sets the formatting for the graphs
            CpuSeries =
            [
                new LineSeries<double>
                {
                    Values = CpuUsageValues,
                    Fill = new SolidColorPaint(SKColor.Parse("#2196F3")),
                    Stroke = null,
                    GeometryFill = null,
                    GeometryStroke = null,
                }
            ];

            CpuYAxes =
            [
                new Axis
                {
                    MinLimit = 0,
                    MaxLimit = 100,
                    Name = "CPU Usage (%)",
                    NamePaint = axisTextColor,
                    LabelsPaint = axisTextColor,
                    TextSize = labelSize,
                    NameTextSize = nameSize,
                    NamePadding = yAxisNamePadding,
                }
            ];

            XAxes = [
                new Axis {
                    MinLimit = 0,
                    MaxLimit = _secondsTracked,
                    MinStep = 10,
                    Labeler = values => (_secondsTracked - values).ToString(),
                    Name = "Seconds Elapsed",
                    NamePaint = axisTextColor,
                    LabelsPaint = axisTextColor,
                    TextSize = labelSize,
                    NameTextSize = nameSize,
                    NamePadding = new LiveChartsCore.Drawing.Padding(0,-10,0,0),
                }
            ];

            MemorySeries =
            [
                new LineSeries<double>
                {
                    Values = MemoryUsageValues,
                    Fill = new SolidColorPaint(SKColor.Parse("#FF9800")),
                    Stroke = null,
                    GeometryFill = null,
                    GeometryStroke = null
                }
            ];

            MemoryYAxes =
            [
                new Axis
                {
                    MinLimit = 0,
                    MaxLimit = 100,
                    Name = "Memory Usage (%)",
                    NamePaint = axisTextColor,
                    LabelsPaint = axisTextColor,
                    TextSize = labelSize,
                    NameTextSize = nameSize,
                    NamePadding = yAxisNamePadding,
                }
            ];

            DiskSeries =
            [
                new LineSeries<double>
                {
                    Values = DiskUsageValues,
                    Fill = new SolidColorPaint(SKColor.Parse("#4CAF50")),
                    Stroke = null,
                    GeometryFill = null,
                    GeometryStroke = null
                }
            ];

            DiskYAxes =
            [
                new Axis
                {
                    MinLimit = 0,
                    MaxLimit = 100,
                    Name = "Disk Activity (%)",
                    NamePaint = axisTextColor,
                    LabelsPaint = axisTextColor,
                    TextSize = labelSize,
                    NameTextSize = nameSize,
                    NamePadding = yAxisNamePadding,
                }
            ];

            NetworkSeries =
            [
                new LineSeries<double>
                {
                    Values = NetworkSentValues,
                    Fill = null,
                    Stroke = new SolidColorPaint(SKColor.Parse("#FF9800"), 3),
                    GeometryFill = null,
                    GeometryStroke = null,
                    Name = "Upload"
                },
                new LineSeries<double>
                {
                    Values = NetworkReceivedValues,
                    Fill = null,
                    Stroke = new SolidColorPaint(SKColor.Parse("#3F51B5"), 3),
                    GeometryFill = null,
                    GeometryStroke = null,
                    Name = "Download"
                }
            ];

            NetworkYAxes =
            [
                new Axis
                {
                    MinLimit = 0,
                    Name = "Throughput (Mbps)",
                    NamePaint = axisTextColor,
                    LabelsPaint = axisTextColor,
                    TextSize = labelSize,
                    NameTextSize = nameSize,
                    NamePadding = yAxisNamePadding,
                }
            ];

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
                double diskActivePercent = 0;
                double networkSentMbps = 0;
                double networkReceivedMbps = 0;
                try
                {
                    System.Diagnostics.Debug.WriteLine("Getting CPU...");
                    cpu = await _monitorService.GetTotalCpuUsageAsync();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"CPU error: {ex.Message}");
                }

                try
                {
                    System.Diagnostics.Debug.WriteLine("Getting memory...");
                    var availableMb = await _monitorService.GetAvailableMemoryMBAsync();
                    var totalMb = await _monitorService.GetTotalMemoryMBAsync();
                    if (totalMb > 0)
                    {
                        memoryUsedPercent = (totalMb - availableMb) * 100.0 / totalMb;
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Memory error: {ex.Message}");
                }

                try
                {
                    System.Diagnostics.Debug.WriteLine("Getting disk usage...");
                    diskActivePercent = await _monitorService.GetDiskActiveTimePercentAsync();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Disk error: {ex.Message}");
                }
                if (diskActivePercent < 0)
                    diskActivePercent = 0;
                else if (diskActivePercent > 100)
                    diskActivePercent = 100;

                try
                {
                    System.Diagnostics.Debug.WriteLine("Getting network throughput...");
                    var network = await _monitorService.GetNetworkThroughputMbpsAsync();
                    networkSentMbps = Math.Max(0, network.SentMbps);
                    networkReceivedMbps = Math.Max(0, network.ReceivedMbps);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Network error: {ex.Message}");
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

                    _diskUsage.Dequeue();
                    _diskUsage.Enqueue(diskActivePercent);

                    DiskUsageValues.Clear();
                    foreach (var v in _diskUsage)
                        DiskUsageValues.Add(v);

                    _networkSent.Dequeue();
                    _networkSent.Enqueue(networkSentMbps);
                    NetworkSentValues.Clear();
                    foreach (var v in _networkSent)
                        NetworkSentValues.Add(v);

                    _networkReceived.Dequeue();
                    _networkReceived.Enqueue(networkReceivedMbps);
                    NetworkReceivedValues.Clear();
                    foreach (var v in _networkReceived)
                        NetworkReceivedValues.Add(v);

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
