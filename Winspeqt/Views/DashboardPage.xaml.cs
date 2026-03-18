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

            Loaded += DashboardPage_Loaded;
            Unloaded += DashboardPage_Unloaded;
            SizeChanged += DashboardPage_SizeChanged;
        }

        private void DashboardPage_Loaded(object sender, RoutedEventArgs e)
        {
            ApplyResponsiveState();

            // Re-apply after first layout pass to avoid occasional startup timing misses.
            DispatcherQueue.TryEnqueue(ApplyResponsiveState);

            ViewModel.StartRefresh(DispatcherQueue);
        }

        private void DashboardPage_Unloaded(object sender, RoutedEventArgs e)
        {
            ViewModel.StopRefresh();
        }

        private void DashboardPage_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            ApplyResponsiveState();
        }

        private void ApplyResponsiveState()
        {
            // Keep in sync with WideState AdaptiveTrigger MinWindowWidth in DashboardPage.xaml.
            const double MinWideWidth = 1250;
            var state = ActualWidth >= MinWideWidth ? "WideState" : "NarrowState";
            VisualStateManager.GoToState(this, state, false);
        }

        private void OnNavigationRequested(object? sender, string feature)
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
                case "Settings":
                    Frame.Navigate(typeof(SettingsPage));
                    break;
            }
        }
    }
}
