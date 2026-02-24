using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using System;
using WinRT.Interop;
using Winspeqt.Helpers;
using Winspeqt.Services;
using Winspeqt.Views.Security;
using Winspeqt.Views.Monitoring;
using Winspeqt.Views.Optimization;

namespace Winspeqt.Views
{
    public sealed partial class MainWindow : Window
    {
        private SystemTrayHelper _systemTrayHelper;
        private static AppUsageService _appUsageService;

        public MainWindow(string? initialFeature = null)
        {
            this.InitializeComponent();
            Title = "Winspeqt - Windows System Inspector";
            AppWindow.Resize(new Windows.Graphics.SizeInt32(1200, 800));

            if (_appUsageService == null)
                _appUsageService = new AppUsageService();

            _systemTrayHelper = new SystemTrayHelper(this, _appUsageService);

            // Navigate directly to the feature page if launched from a toast
            if (initialFeature != null)
                NavigateToFeature(initialFeature);
            else
                RootFrame.Navigate(typeof(DashboardPage));

            if (AppWindowTitleBar.IsCustomizationSupported() is true)
            {
                IntPtr hWnd = WindowNative.GetWindowHandle(this);
                WindowId wndId = Win32Interop.GetWindowIdFromWindow(hWnd);
                AppWindow appWindow = AppWindow.GetFromWindowId(wndId);
                appWindow.SetIcon(@"Assets\QuantumLens.ico");
            }

            var hWnd2 = WindowNative.GetWindowHandle(this);
            var windowId = Win32Interop.GetWindowIdFromWindow(hWnd2);
            var appWindow2 = AppWindow.GetFromWindowId(windowId);
            appWindow2.Closing += (s, e) =>
            {
                e.Cancel = true;
                this.DispatcherQueue.TryEnqueue(() =>
                {
                    var hWnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
                    var wndId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hWnd);
                    var appWnd = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(wndId);
                    appWnd.Hide();
                });
                _systemTrayHelper.HideToTray();
            };
        }

        public void NavigateToFeature(string feature)
        {
            // Make sure window is visible first
            this.DispatcherQueue.TryEnqueue(() =>
            {
                var hWnd = WindowNative.GetWindowHandle(this);
                var wndId = Win32Interop.GetWindowIdFromWindow(hWnd);
                var appWnd = AppWindow.GetFromWindowId(wndId);
                appWnd.Show();
                this.Activate();

                // Navigate to the right page based on feature key
                switch (feature)
                {
                    case "AppUpdateChecker":
                        RootFrame.Navigate(typeof(AppSecurityPage));
                        break;
                    case "SecurityStatus":
                        RootFrame.Navigate(typeof(SecurityStatusPage));
                        break;
                    case "SystemOptimization":
                        RootFrame.Navigate(typeof(OptimizationDashboardPage));
                        break;
                    case "SystemMonitoring":
                        RootFrame.Navigate(typeof(MonitoringDashboardPage));
                        break;
                    default:
                        RootFrame.Navigate(typeof(DashboardPage));
                        break;
                }
            });
        }

        public static AppUsageService GetAppUsageService() => _appUsageService;

        public void CleanupAndExit()
        {
            _appUsageService?.SaveData();
            _appUsageService?.Dispose();
            _systemTrayHelper?.Dispose();
        }

        private async void FeedbackButton_Click(object sender, RoutedEventArgs e)
        {
            _ = await Windows.System.Launcher.LaunchUriAsync(new Uri("https://forms.office.com/Pages/ResponsePage.aspx?id=DQSIkWdsW0yxEjajBLZtrQAAAAAAAAAAAAN__rlZTTtUNVBIVU5GRDJaUVVJT0lFQVNRSkJJUVY5RC4u"));
        }
    }
}