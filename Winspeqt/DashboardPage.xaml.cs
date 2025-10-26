using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;

namespace Winspeqt
{
    public sealed partial class DashboardPage : Page
    {
        public DashboardPage()
        {
            this.InitializeComponent();
        }

        private void SecurityCard_Click(object sender, RoutedEventArgs e)
        {
            // TODO: Navigate to Security page
            // For now, show a message
            ShowComingSoonDialog("Security");
        }

        private void OptimizationCard_Click(object sender, RoutedEventArgs e)
        {
            // TODO: Navigate to Optimization page
            ShowComingSoonDialog("Optimization");
        }

        private void MonitoringCard_Click(object sender, RoutedEventArgs e)
        {
            // TODO: Navigate to Monitoring page
            ShowComingSoonDialog("Monitoring");
        }

        private async void ShowComingSoonDialog(string feature)
        {
            ContentDialog dialog = new ContentDialog
            {
                Title = $"{feature} Feature",
                Content = $"The {feature} module is coming soon! We're building an amazing experience for you.",
                CloseButtonText = "Got it",
                XamlRoot = this.XamlRoot
            };

            await dialog.ShowAsync();
        }
    }
}