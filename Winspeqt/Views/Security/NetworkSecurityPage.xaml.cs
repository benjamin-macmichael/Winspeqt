using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using Winspeqt.Models;
using Winspeqt.ViewModels.Security;

namespace Winspeqt.Views.Security
{
    public sealed partial class NetworkSecurityPage : Page
    {
        public NetworkSecurityViewModel ViewModel { get; }

        public NetworkSecurityPage()
        {
            this.InitializeComponent();
            ViewModel = new NetworkSecurityViewModel();
            ViewModel.UnsecuredNetworkDetected += OnUnsecuredNetworkDetected;
        }

        private async void OnUnsecuredNetworkDetected(string networkName, string interfaceName)
        {
            var dialog = new ContentDialog
            {
                Title = "⚠️ Unsecured Network Detected",
                Content = $"You are connected to \"{networkName}\", which is an open network with no password or encryption.\n\n" +
                          "Risks on unsecured networks:\n" +
                          "  • Anyone nearby can see your unencrypted traffic\n" +
                          "  • Passwords and personal data may be exposed\n" +
                          "  • You could be targeted by man-in-the-middle attacks\n\n" +
                          "Consider using a VPN if you must stay connected, or disconnect and use mobile data instead.",
                PrimaryButtonText = "Disconnect Now",
                CloseButtonText = "Stay Connected",
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = this.XamlRoot
            };

            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                ViewModel.DisconnectUnsecuredNetworkCommand.Execute(null);
            }
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            Frame.Navigate(typeof(SecurityDashboardPage));
        }

        private async void DisconnectDevice_Click(object sender, RoutedEventArgs e)
        {
            var device = (sender as Button)?.Tag as ConnectedDevice;
            if (device == null || !device.CanDisconnect) return;

            ContentDialog confirmDialog = new ContentDialog
            {
                Title = $"Disconnect from {device.Name}?",
                Content = $"This will disconnect your PC from \"{device.Name}\".\n\nYou may lose internet access until you reconnect.",
                PrimaryButtonText = "Disconnect",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = this.XamlRoot
            };

            var result = await confirmDialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                ViewModel.DisconnectDeviceCommand.Execute(device);
            }
        }
    }
}
