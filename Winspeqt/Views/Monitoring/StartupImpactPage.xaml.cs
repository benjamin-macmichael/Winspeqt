using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Diagnostics;
using Winspeqt.ViewModels.Monitoring;

namespace Winspeqt.Views.Monitoring
{
    public sealed partial class StartupImpactPage : Page
    {
        public StartupImpactViewModel ViewModel { get; }

        public StartupImpactPage()
        {
            InitializeComponent();
            this.ViewModel = new StartupImpactViewModel();
            this.DataContext = ViewModel;
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            if (Frame.CanGoBack)
            {
                Frame.GoBack();
            }
        }


        private void OpenStartupSettings_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Open Windows Update settings
                Process.Start(new ProcessStartInfo
                {
                    FileName = "ms-settings:startupapps",
                    UseShellExecute = true
                });
            }
            catch (System.Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error opening startup apps: {ex.Message}");
            }
        }

        private void OpenRegedit_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Open Windows Update settings
                Process.Start(new ProcessStartInfo
                {
                    FileName = "regedit",
                    UseShellExecute = true
                });
            }
            catch (System.Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error opening Regedit: {ex.Message}");
            }
        }

        private void OpenTaskScheduler_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Open Windows Update settings
                Process.Start(new ProcessStartInfo
                {
                    FileName = "taskschd.msc",
                    UseShellExecute = true
                });
            }
            catch (System.Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error opening task scheduler: {ex.Message}");
            }
        }

        private void OpenStartupFolder_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string startupPath = Environment.GetFolderPath(Environment.SpecialFolder.Startup);

                Process.Start(new ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = startupPath,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error opening Startup folder: {ex.Message}");
            }
        }
    }
}
