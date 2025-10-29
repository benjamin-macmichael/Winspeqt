using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Winspeqt.ViewModels;

namespace Winspeqt.Views
{
    public sealed partial class DashboardPage : Page
    {
        public DashboardViewModel ViewModel { get; }

        public DashboardPage()
        {
            this.InitializeComponent();
            ViewModel = new DashboardViewModel();
            this.DataContext = ViewModel;

            // Subscribe to the ViewModel's navigation event
            ViewModel.NavigationRequested += OnNavigationRequested;
        }

        private void OnNavigationRequested(object sender, string feature)
        {
            // Navigate to the appropriate dashboard page
            switch (feature)
            {
                case "Security":
                    Frame.Navigate(typeof(Security.SecurityDashboardPage));
                    break;
                case "Optimization":
                    Frame.Navigate(typeof(Optimization.OptimizationDashboardPage));
                    break;
                case "Monitoring":
                    Frame.Navigate(typeof(Monitoring.MonitoringDashboardPage));
                    break;
            }
        }
    }
}