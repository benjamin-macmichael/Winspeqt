using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Diagnostics;
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

        private void TipAction_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button { CommandParameter: string key })
                ExecuteTipAction(key);
        }

        private void ExecuteTipAction(string key)
        {
            switch (key)
            {
                case "restart":
                    TryStartProcess("shutdown.exe", "/r /t 0", "restart");
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
                case "startup-section":
                    ViewModel.ShowStartupDetail = true;
                    break;
            }
        }

        // ── Uptime ────────────────────────────────────────────────────────────

        private void RestartNow_Click(object sender, RoutedEventArgs e)
        {
            ExecuteTipAction("restart");
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