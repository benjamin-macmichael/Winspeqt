using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;

namespace Winspeqt.Views.Optimization
{
    /// <summary>
    /// Landing page for optimization features and navigation.
    /// </summary>
    public sealed partial class OptimizationDashboardPage : Page
    {
        /// <summary>
        /// Initializes the dashboard page.
        /// </summary>
        public OptimizationDashboardPage()
        {
            this.InitializeComponent();
        }

        /// <summary>
        /// Navigates back when possible.
        /// </summary>
        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            if (Frame.CanGoBack)
            {
                Frame.GoBack();
            }
        }

        /// <summary>
        /// Opens the Large File Finder feature page.
        /// </summary>
        private async void LargeFileFinderCard_Click(object sender, RoutedEventArgs e)
        {
            // TODO: Navigate to Large File Finder page
            Frame.Navigate(typeof(LargeFileFinder));
        }

        /// <summary>
        /// Opens the App Usage Tracker feature page.
        /// </summary>
        private async void AppUsageTrackerCard_Click(object sender, RoutedEventArgs e)
        {
            Frame.Navigate(typeof(AppUsagePage));
        }

        /// <summary>
        /// Shows a placeholder dialog for the Safe AppData Cleanup feature.
        /// </summary>
        private async void AppDataCleanupCard_Click(object sender, RoutedEventArgs e)
        {
            // TODO: Navigate to AppData Cleanup page
            await ShowComingSoonDialog("Safe AppData Cleanup");
        }

        /// <summary>
        /// Shows a placeholder dialog for the One-Click Optimization feature.
        /// </summary>
        private async void OneClickOptimizationCard_Click(object sender, RoutedEventArgs e)
        {
            // TODO: Navigate to One-Click Optimization page
            await ShowComingSoonDialog("One-Click Optimization");
        }

        /// <summary>
        /// Displays a "coming soon" dialog for unimplemented features.
        /// </summary>
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
