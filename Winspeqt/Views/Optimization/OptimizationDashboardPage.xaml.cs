using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using Winspeqt.Views.Monitoring;

namespace Winspeqt.Views.Optimization
{
    public sealed partial class OptimizationDashboardPage : Page
    {
        public OptimizationDashboardPage()
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

        private async void LargeFileFinderCard_Click(object sender, RoutedEventArgs e)
        {
            // TODO: Navigate to Large File Finder page
            Frame.Navigate(typeof(LargeFileFinder));
        }

        private async void AppUsageTrackerCard_Click(object sender, RoutedEventArgs e)
        {
            Frame.Navigate(typeof(AppUsagePage));
        }

        private async void AppDataCleanupCard_Click(object sender, RoutedEventArgs e)
        {
            // TODO: Navigate to AppData Cleanup page
            await ShowComingSoonDialog("Safe AppData Cleanup");
        }

        private async void OneClickOptimizationCard_Click(object sender, RoutedEventArgs e)
        {
            // TODO: Navigate to One-Click Optimization page
            await ShowComingSoonDialog("One-Click Optimization");
        }

        private async System.Threading.Tasks.Task<ContentDialogResult> ShowComingSoonDialog(string feature)
        {
            ContentDialog dialog = new ContentDialog
            {
                Title = feature,
                Content = $"The {feature} feature is coming soon!",
                CloseButtonText = "OK",
                XamlRoot = this.XamlRoot
            };

            return await dialog.ShowAsync();
        }
    }
}