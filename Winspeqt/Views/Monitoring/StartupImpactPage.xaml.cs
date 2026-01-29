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
            ViewModel = new StartupImpactViewModel();
            DataContext = ViewModel;
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
                OpenLink(link);
            }
        }

        private void OpenStartupSettings_Click(object sender, RoutedEventArgs e)
        {
            TryStartProcess("ms-settings:startupapps", null, "startup apps settings");
        }

        private void OpenRegedit_Click(object sender, RoutedEventArgs e)
        {
            TryStartProcess("regedit", null, "Regedit");
        }

        private void OpenTaskScheduler_Click(object sender, RoutedEventArgs e)
        {
            TryStartProcess("taskschd.msc", null, "Task Scheduler");
        }

        private void OpenStartupFolder_Click(object sender, RoutedEventArgs e)
        {
            var startupPath = Environment.GetFolderPath(Environment.SpecialFolder.Startup);
            TryStartProcess("explorer.exe", startupPath, "Startup folder");
        }

        private void ShowAdvancedSettings_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.ToggleAdvancedSettings();
        }

        private void OpenLink(string link)
        {
            switch (link)
            {
                case "regedit":
                    TryStartProcess("regedit", null, "Regedit");
                    break;
                case "startup":
                    var startupPath = Environment.GetFolderPath(Environment.SpecialFolder.Startup);
                    TryStartProcess("explorer.exe", startupPath, "Startup folder");
                    break;
                case "schd":
                    TryStartProcess("taskschd.msc", null, "Task Scheduler");
                    break;
                default:
                    TryStartProcess("ms-settings:startupapps", null, "startup apps settings");
                    break;
            }
        }

        private static void TryStartProcess(string fileName, string arguments, string errorContext)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = fileName,
                    Arguments = arguments ?? string.Empty,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error opening {errorContext}: {ex.Message}");
            }
        }
    }
}
