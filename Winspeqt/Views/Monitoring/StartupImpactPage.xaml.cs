using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Diagnostics;
using Winspeqt.ViewModels.Monitoring;

namespace Winspeqt.Views.Monitoring
{
    /// <summary>
    /// Page that presents startup apps guidance and detailed startup sources.
    /// </summary>
    public sealed partial class StartupImpactPage : Page
    {
        /// <summary>
        /// View model instance used for bindings.
        /// </summary>
        public StartupImpactViewModel ViewModel { get; }

        /// <summary>
        /// Initializes the page and its data context.
        /// </summary>
        public StartupImpactPage()
        {
            InitializeComponent();
            ViewModel = new StartupImpactViewModel();
            DataContext = ViewModel;
        }

        /// <summary>
        /// Navigates back to the previous page when possible.
        /// </summary>
        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            if (Frame.CanGoBack)
            {
                Frame.GoBack();
            }
        }

        /// <summary>
        /// Routes link actions from the info flyout to the correct handler.
        /// </summary>
        private void LinkButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.CommandParameter is string link)
            {
                OpenLink(link);
            }
        }

        /// <summary>
        /// Opens the Windows Startup Apps settings page.
        /// </summary>
        private void OpenStartupSettings_Click(object sender, RoutedEventArgs e)
        {
            TryStartProcess("ms-settings:startupapps", null, "startup apps settings");
        }

        /// <summary>
        /// Opens the Registry Editor.
        /// </summary>
        private void OpenRegedit_Click(object sender, RoutedEventArgs e)
        {
            TryStartProcess("regedit", null, "Regedit");
        }

        /// <summary>
        /// Opens Task Scheduler.
        /// </summary>
        private void OpenTaskScheduler_Click(object sender, RoutedEventArgs e)
        {
            TryStartProcess("taskschd.msc", null, "Task Scheduler");
        }

        /// <summary>
        /// Opens the current user's Startup folder in Explorer.
        /// </summary>
        private void OpenStartupFolder_Click(object sender, RoutedEventArgs e)
        {
            var startupPath = Environment.GetFolderPath(Environment.SpecialFolder.Startup);
            TryStartProcess("explorer.exe", startupPath, "Startup folder");
        }

        /// <summary>
        /// Toggles visibility of the advanced settings section.
        /// </summary>
        private void ShowAdvancedSettings_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.ToggleAdvancedSettings();
        }

        /// <summary>
        /// Executes the link action chosen in the flyout.
        /// </summary>
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

        /// <summary>
        /// Starts a process using shell execute and logs failures.
        /// </summary>
        private static void TryStartProcess(string fileName, string? arguments, string errorContext)
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
