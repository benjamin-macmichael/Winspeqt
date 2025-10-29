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
            // TODO: Navigate to Task Manager page
            await ShowComingSoonDialog("Friendly Task Manager");
        }

        private async void ResourceTrendsCard_Click(object sender, RoutedEventArgs e)
        {
            // TODO: Navigate to Resource Trends page
            await ShowComingSoonDialog("Resource Trends");
        }

        private async void StartupImpactCard_Click(object sender, RoutedEventArgs e)
        {
            // TODO: Navigate to Startup Impact page
            await ShowComingSoonDialog("Startup Impact Analyzer");
        }

        private async void BackgroundProcessCard_Click(object sender, RoutedEventArgs e)
        {
            // TODO: Navigate to Background Process page
            await ShowComingSoonDialog("Background Process Insights");
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