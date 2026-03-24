using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Winspeqt.ViewModels.Monitoring;

namespace Winspeqt.Views.Monitoring
{
    /// <summary>
    /// Page that helps users diagnose why their PC is running slow.
    /// Also hosts the startup applications detail view as an in-page second screen.
    /// </summary>
    public sealed partial class StartupImpactPage : Page
    {
        public StartupImpactViewModel ViewModel { get; }

        public StartupImpactPage()
        {
            InitializeComponent();
            ViewModel = new StartupImpactViewModel();
            DataContext = ViewModel;
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            await ViewModel.RefreshDataAsync();
        }

        // ── Screen 1 navigation ───────────────────────────────────────────────

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            Frame.Navigate(typeof(MonitoringDashboardPage));
        }

        // ── Screen 2 navigation ───────────────────────────────────────────────

        private void BackToMain_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.ShowStartupDetail = false;
        }

        // ── Tip action dispatcher ─────────────────────────────────────────────

        private async void TipAction_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button { CommandParameter: string key })
                await ExecuteTipActionAsync(key);
        }

        private async Task ExecuteTipActionAsync(string key)
        {
            switch (key)
            {
                case "restart":
                    await ConfirmAndRestartAsync();
                    break;
                case "restart-explorer":
                    RestartExplorer();
                    break;
                case "speedtest":
                    TryStartProcess("https://www.speedtest.net", null, "speed test");
                    break;
                case "storage":
                    TryStartProcess("ms-settings:storagesense", null, "storage settings");
                    break;
                case "windows-update":
                    TryStartProcess("ms-settings:windowsupdate", null, "Windows Update");
                    break;
                case "power":
                    TryStartProcess("ms-settings:powersleep", null, "power settings");
                    break;
                case "security":
                    TryStartProcess("windowsdefender:", null, "Windows Security");
                    break;
                case "sfc":
                    TryStartElevatedProcess("cmd.exe", "/k sfc /scannow", "System File Checker");
                    break;
                case "startup-section":
                    ViewModel.ShowStartupDetail = true;
                    break;
            }
        }

        // ── Restart confirmation ──────────────────────────────────────────────

        private async Task ConfirmAndRestartAsync()
        {
            var dialog = new ContentDialog
            {
                Title = "Restart your PC?",
                Content = "If you confirm, your PC will restart in 10 seconds. Make sure you've saved any open work before continuing.",
                PrimaryButtonText = "Restart Now",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = XamlRoot
            };

            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
                TryStartProcess("shutdown.exe", "/r /g /t 10", "restart");
        }

        // ── Startup detail screen handlers ────────────────────────────────────

        private void OpenStartupSettings_Click(object sender, RoutedEventArgs e)
        {
            TryStartProcess("ms-settings:startupapps", null, "startup apps settings");
        }

        private void LinkButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button { CommandParameter: string link })
                OpenLink(link);
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

        // ── Helpers ───────────────────────────────────────────────────────────

        private static void RestartExplorer()
        {
            try
            {
                foreach (var p in Process.GetProcessesByName("explorer"))
                {
                    p.Kill();
                    p.WaitForExit(3000);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error killing explorer: {ex.Message}");
            }
            TryStartProcess("explorer.exe", null, "Explorer");
        }

        private static void TryStartElevatedProcess(string fileName, string? arguments, string errorContext)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = fileName,
                    Arguments = arguments ?? string.Empty,
                    UseShellExecute = true,
                    Verb = "runas"
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error opening {errorContext}: {ex.Message}");
            }
        }

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