// Views/PerformanceTrendsPage.xaml.cs

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Winspeqt.ViewModels;
using Winspeqt.ViewModels.Monitoring;

namespace Winspeqt.Views.Monitoring
{
    public sealed partial class PerformanceTrendsPage : Page
    {
        public PerformanceTrendsViewModel ViewModel { get; }

        public PerformanceTrendsPage()
        {
            this.InitializeComponent();
            ViewModel = new PerformanceTrendsViewModel();
            this.DataContext = ViewModel;
        }
        
        
        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            // Stop auto-refresh when leaving page
            ViewModel.StopAutoRefresh();

            if (Frame.CanGoBack)
            {
                Frame.GoBack();
            }
        }
    }
}