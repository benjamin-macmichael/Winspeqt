// ViewModels/PerformanceTrendsViewModel.cs

using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Winspeqt.Models;
using Winspeqt.Services;

namespace Winspeqt.ViewModels
{
    public sealed class PerformanceTrendsViewModel : INotifyPropertyChanged
    {
        private readonly ProcessService _service = new();

        public ObservableCollection<ProcessInfo> Processes { get; } = new();

        public ICommand RefreshCommand { get; }

        public PerformanceTrendsViewModel()
        {
            RefreshCommand = new RelayCommand(Refresh);
            Refresh();
        }

        private void Refresh()
        {
            Processes.Clear();
            foreach (var proc in _service.GetRunningProcesses().OrderBy(p => p.Name))
                Processes.Add(proc);
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    // Simple ICommand implementation (no extra packages needed)
    public sealed class RelayCommand : ICommand
    {
        private readonly Action _execute;
        private readonly Func<bool>? _canExecute;

        public RelayCommand(Action execute, Func<bool>? canExecute = null)
        {
            _execute = execute;
            _canExecute = canExecute;
        }

        public bool CanExecute(object? parameter) => _canExecute?.Invoke() ?? true;
        public void Execute(object? parameter) => _execute();

        public event EventHandler? CanExecuteChanged;
        public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }
}