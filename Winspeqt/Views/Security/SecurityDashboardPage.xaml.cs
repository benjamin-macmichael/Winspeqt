using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;

namespace Winspeqt.Views.Security
{
    public sealed partial class SecurityDashboardPage : Page
    {
        public SecurityDashboardPage()
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

        private async void SecurityStatusCard_Click(object sender, RoutedEventArgs e)
        {
            // TODO: Navigate to Security Status page
            await ShowComingSoonDialog("Security Status Dashboard");
        }

        private async void VulnerabilityCheckerCard_Click(object sender, RoutedEventArgs e)
        {
            // TODO: Navigate to Vulnerability Checker page
            await ShowComingSoonDialog("App Vulnerability Checker");
        }

        private async void UpdateRecommendationsCard_Click(object sender, RoutedEventArgs e)
        {
            // TODO: Navigate to Update Recommendations page
            await ShowComingSoonDialog("Update Recommendations");
        }

        private async void SettingsRecommendationsCard_Click(object sender, RoutedEventArgs e)
        {
            // TODO: Navigate to Settings Recommendations page
            await ShowComingSoonDialog("Windows Settings Recommendations");
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