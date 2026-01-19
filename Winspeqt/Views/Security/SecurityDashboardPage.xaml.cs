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

        private void SecurityStatusCard_Click(object sender, RoutedEventArgs e)
        {
            Frame.Navigate(typeof(SecurityStatusPage));
        }

        private async void AppSecurityScannerCard_Click(object sender, RoutedEventArgs e)
        {
            await ShowComingSoonDialog("App Security Scanner");
        }

        private async void NetworkSecurityCard_Click(object sender, RoutedEventArgs e)
        {
            await ShowComingSoonDialog("Network Security Monitor");
        }

        private async void SettingsRecommendationsCard_Click(object sender, RoutedEventArgs e)
        {
            await ShowComingSoonDialog("Windows Settings Recommendations");
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