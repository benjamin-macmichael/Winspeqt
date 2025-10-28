using System;
using System.Windows.Input;
using Winspeqt.Helpers;

namespace Winspeqt.ViewModels
{
    public class DashboardViewModel : ObservableObject
    {
        public ICommand SecurityCommand { get; }
        public ICommand OptimizationCommand { get; }
        public ICommand MonitoringCommand { get; }

        // Events that the View can subscribe to
        public event EventHandler<string> NavigationRequested;

        public DashboardViewModel()
        {
            SecurityCommand = new RelayCommand(OnSecurityClicked);
            OptimizationCommand = new RelayCommand(OnOptimizationClicked);
            MonitoringCommand = new RelayCommand(OnMonitoringClicked);
        }

        private void OnSecurityClicked()
        {
            // Raise event that View will handle
            NavigationRequested?.Invoke(this, "Security");
        }

        private void OnOptimizationClicked()
        {
            NavigationRequested?.Invoke(this, "Optimization");
        }

        private void OnMonitoringClicked()
        {
            NavigationRequested?.Invoke(this, "Monitoring");
        }
    }
}