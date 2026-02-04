using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Diagnostics;
using Winspeqt.ViewModels.Security;

namespace Winspeqt.Views.Security
{
    public sealed partial class SettingsRecommendationsPage : Page
    {
        public SettingsRecommendationsViewModel ViewModel { get; }

        public SettingsRecommendationsPage()
        {
            this.InitializeComponent();
            ViewModel = new SettingsRecommendationsViewModel();
            this.DataContext = ViewModel;
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            if (Frame.CanGoBack)
            {
                Frame.GoBack();
            }
        }

        private void LinkButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.CommandParameter is string link)
            {
                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = $"{link}",
                        UseShellExecute = true
                    });
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error opening {link}: {ex.Message}");
                }
            }
        }
    }
}