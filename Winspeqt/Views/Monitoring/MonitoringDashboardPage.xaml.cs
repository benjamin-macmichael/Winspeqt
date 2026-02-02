using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;

namespace Winspeqt.Views.Monitoring
{
    public sealed partial class MonitoringDashboardPage : Page
    {
        public MonitoringDashboardPage()
        {
            this.InitializeComponent();
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            if (Frame.CanGoBack)
            {
                Frame.GoBack();
            }
        }

        private async void TaskManagerCard_Click(object sender, RoutedEventArgs e)
        {
            Frame.Navigate(typeof(TaskManagerPage));
        }

        private async void ResourceTrendsCard_Click(object sender, RoutedEventArgs e)
        {
            // TODO: Navigate to Resource Trends page
            Frame.Navigate(typeof(Monitoring.PerformanceTrendsPage));
        }

        private async void StartupImpactCard_Click(object sender, RoutedEventArgs e)
        {
            // TODO: Navigate to Startup Impact page
            Frame.Navigate(typeof(Monitoring.StartupImpactPage));
        }

        private void BackgroundProcessCard_Click(object sender, RoutedEventArgs e)
        {
            // Navigate to Background Process page
            Frame.Navigate(typeof(BackgroundProcessPage));
        }

        private async System.Threading.Tasks.Task ShowComingSoonDialog(string feature)
        {
            ContentDialog dialog = new ContentDialog
            {
                Title = feature,
                Content = $"The {feature} feature is coming soon!",
                CloseButtonText = "OK",
                XamlRoot = this.XamlRoot
            };

            await dialog.ShowAsync();
        }
    }
}