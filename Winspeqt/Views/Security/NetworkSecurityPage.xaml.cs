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
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            if (Frame.CanGoBack)
                Frame.GoBack();
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
