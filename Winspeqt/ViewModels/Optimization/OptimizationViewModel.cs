using Microsoft.UI.Dispatching;
using System;
using System.Threading.Tasks;
using System.Windows.Input;
using Winspeqt.Helpers;
using Winspeqt.Models;
using Winspeqt.Services;

namespace Winspeqt.ViewModels.Optimization
{
    public class OptimizationViewModel : ObservableObject
    {
        private readonly OptimizationService _service;
        private readonly DispatcherQueue _dispatcherQueue;

        // ── State ─────────────────────────────────────────────────────────────

        private bool _isRunning;
        public bool IsRunning
        {
            get => _isRunning;
            set => SetProperty(ref _isRunning, value);
        }

        private bool _hasCompleted;
        public bool HasCompleted
        {
            get => _hasCompleted;
            set => SetProperty(ref _hasCompleted, value);
        }

        private string _currentTaskLabel = "";
        public string CurrentTaskLabel
        {
            get => _currentTaskLabel;
            set => SetProperty(ref _currentTaskLabel, value);
        }

        private OptimizationResult? _result;
        public OptimizationResult? Result
        {
            get => _result;
            set => SetProperty(ref _result, value);
        }

        // ── Options ───────────────────────────────────────────────────────────

        public OptimizationOptions Options { get; } = new();

        // Optional toggles exposed individually for binding
        private bool _cleanUpdateCache;
        public bool CleanUpdateCache
        {
            get => _cleanUpdateCache;
            set { SetProperty(ref _cleanUpdateCache, value); Options.CleanWindowsUpdateCache = value; }
        }

        private bool _cleanEventLogs;
        public bool CleanEventLogs
        {
            get => _cleanEventLogs;
            set { SetProperty(ref _cleanEventLogs, value); Options.CleanEventLogs = value; }
        }

        private bool _cleanBrowserCache;
        public bool CleanBrowserCache
        {
            get => _cleanBrowserCache;
            set { SetProperty(ref _cleanBrowserCache, value); Options.CleanBrowserCache = value; }
        }

        // ── Command ───────────────────────────────────────────────────────────

        public ICommand RunOptimizationCommand { get; }

        public OptimizationViewModel()
        {
            _service = new OptimizationService();
            _dispatcherQueue = DispatcherQueue.GetForCurrentThread();
            RunOptimizationCommand = new RelayCommand(async () => await RunOptimizationAsync(), () => !IsRunning);
        }

        private async Task RunOptimizationAsync()
        {
            IsRunning = true;
            HasCompleted = false;
            Result = null;
            CurrentTaskLabel = "Starting optimization...";

            var progress = new Progress<string>(msg =>
                _dispatcherQueue.TryEnqueue(() => CurrentTaskLabel = msg));

            try
            {
                var result = await _service.RunOptimizationAsync(Options, progress);

                _dispatcherQueue.TryEnqueue(() =>
                {
                    Result = result;
                    IsRunning = false;
                    HasCompleted = true;
                    CurrentTaskLabel = "Optimization complete!";

                    // Save optimization score to LocalSettings
                    try
                    {
                        var settings = Windows.Storage.ApplicationData.Current.LocalSettings.Values;
                        settings["Optimization_LastRunTime"] = DateTime.Now.Ticks;
                        settings["Optimization_LastBytesFreed"] = result.TotalBytesFreed;
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[OptimizationViewModel] LocalSettings error: {ex.Message}");
                    }
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[OptimizationViewModel] Error: {ex.Message}");
                _dispatcherQueue.TryEnqueue(() =>
                {
                    IsRunning = false;
                    CurrentTaskLabel = "An error occurred during optimization.";
                });
            }
        }
    }
}