using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using System;
using WinRT.Interop;
using Winspeqt.Helpers;
using Winspeqt.Services;

namespace Winspeqt.Views
{
    public sealed partial class MainWindow : Window
    {
        private SystemTrayHelper _systemTrayHelper;
        private static AppUsageService _appUsageService;

        public MainWindow()
        {
            this.InitializeComponent();

            // Set window title
            Title = "Winspeqt - Windows System Inspector";

            // Set a nice default size
            AppWindow.Resize(new Windows.Graphics.SizeInt32(1200, 800));

            // Initialize the app usage service (singleton)
            if (_appUsageService == null)
            {
                _appUsageService = new AppUsageService();
            }

            // Initialize system tray
            _systemTrayHelper = new SystemTrayHelper(this, _appUsageService);

            // Navigate to the dashboard
            RootFrame.Navigate(typeof(DashboardPage));

            if (AppWindowTitleBar.IsCustomizationSupported() is true)
            {
                IntPtr hWnd = WindowNative.GetWindowHandle(this);
                WindowId wndId = Win32Interop.GetWindowIdFromWindow(hWnd);
                AppWindow appWindow = AppWindow.GetFromWindowId(wndId);
                appWindow.SetIcon(@"Assets\QuantumLens.ico");
            }

            // Handle window close to minimize to tray
            var hWnd2 = WindowNative.GetWindowHandle(this);
            var windowId = Win32Interop.GetWindowIdFromWindow(hWnd2);
            var appWindow2 = AppWindow.GetFromWindowId(windowId);

            appWindow2.Closing += (s, e) =>
            {
                // Prevent actual closing, hide window instead
                e.Cancel = true;

                // Actually hide/close the window visibility
                this.DispatcherQueue.TryEnqueue(() =>
                {
                    // In WinUI 3, we can't truly "hide" the window, but we can make it invisible
                    // by setting AppWindow to not visible
                    var hWnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
                    var wndId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hWnd);
                    var appWnd = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(wndId);
                    appWnd.Hide();
                });

                _systemTrayHelper.HideToTray();
            };
        }

        // Provide the service to pages
        public static AppUsageService GetAppUsageService()
        {
            return _appUsageService;
        }

        // Call this when actually exiting the application
        public void CleanupAndExit()
        {
            _appUsageService?.SaveData();
            _appUsageService?.Dispose();
            _systemTrayHelper?.Dispose();
        }
    }
}