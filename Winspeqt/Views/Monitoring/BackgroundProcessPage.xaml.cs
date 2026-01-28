using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using Winspeqt.Models;
using Winspeqt.ViewModels.Monitoring;

namespace Winspeqt.Views
{
    public sealed partial class BackgroundProcessPage : Page
    {
        public BackgroundProcessViewModel ViewModel { get; }

        public BackgroundProcessPage()
        {
            this.InitializeComponent();
            ViewModel = new BackgroundProcessViewModel();
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            if (Frame.CanGoBack)
            {
                Frame.GoBack();
            }
        }

        private async void EndProcess_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            var process = button?.Tag as ProcessInfo;

            if (process == null) return;

            // Show confirmation dialog
            ContentDialog confirmDialog = new ContentDialog
            {
                Title = "End Process?",
                Content = $"Are you sure you want to end '{process.Description}'?\n\n" +
                          $"Process: {process.ProcessName}\n" +
                          $"Memory: {process.MemoryUsageDisplay}\n\n" +
                          $"⚠️ Warning: Ending this process may cause data loss or system instability.",
                PrimaryButtonText = "End Process",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = this.XamlRoot
            };

            var result = await confirmDialog.ShowAsync();

            if (result == ContentDialogResult.Primary)
            {
                // Attempt to end the process
                var success = await ViewModel.EndProcessAsync(process);

                if (success)
                {
                    // Show success message
                    ContentDialog successDialog = new ContentDialog
                    {
                        Title = "Process Ended",
                        Content = $"'{process.Description}' has been successfully ended.",
                        CloseButtonText = "OK",
                        XamlRoot = this.XamlRoot
                    };
                    await successDialog.ShowAsync();
                }
                else
                {
                    // Show error message
                    ContentDialog errorDialog = new ContentDialog
                    {
                        Title = "Failed to End Process",
                        Content = $"Could not end '{process.Description}'.\n\n" +
                                  $"This may happen if:\n" +
                                  $"• You don't have permission to end this process\n" +
                                  $"• The process has already ended\n" +
                                  $"• The process is protected by Windows",
                        CloseButtonText = "OK",
                        XamlRoot = this.XamlRoot
                    };
                    await errorDialog.ShowAsync();
                }
            }
        }
    }
}