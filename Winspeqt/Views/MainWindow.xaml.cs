using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using WinRT.Interop;
using Winspeqt.Helpers;
using Winspeqt.Services;
using Winspeqt.Views.Monitoring;
using Winspeqt.Views.Optimization;
using Winspeqt.Views.Security;

namespace Winspeqt.Views
{
    public sealed partial class MainWindow : Window
    {
        private static readonly Dictionary<string, Type> NavigationRoutes = new()
        {
            {"Home", typeof(DashboardPage)},
            {"SecurityDashboard", typeof(SecurityDashboardPage)},
            {"SecurityStatus", typeof(SecurityStatusPage)},
            {"AppSecurity", typeof(AppSecurityPage)},
            {"NetworkSecurity", typeof(NetworkSecurityPage)},
            {"SettingsRecommendations", typeof(SettingsRecommendationsPage)},
            {"OptimizationDashboard", typeof(OptimizationDashboardPage)},
            {"LargeFileFinder", typeof(LargeFileFinder)},
            {"AppUsage", typeof(AppUsagePage)},
            {"AppDataCleanup", typeof(AppDataCleanupCard)},
            {"Optimization", typeof(OptimizationPage)},
            {"MonitoringDashboard", typeof(MonitoringDashboardPage)},
            {"TaskManager", typeof(TaskManagerPage)},
            {"PerformanceTrends", typeof(PerformanceTrendsPage)},
            {"StartupImpact", typeof(StartupImpactPage)},
            {"BackgroundProcess", typeof(BackgroundProcessPage)},
        };

        private static readonly Dictionary<Type, string> PageToTagMap = new()
        {
            {typeof(DashboardPage), "Home"},
            {typeof(SecurityDashboardPage), "SecurityDashboard"},
            {typeof(SecurityStatusPage), "SecurityStatus"},
            {typeof(AppSecurityPage), "AppSecurity"},
            {typeof(NetworkSecurityPage), "NetworkSecurity"},
            {typeof(SettingsRecommendationsPage), "SettingsRecommendations"},
            {typeof(OptimizationDashboardPage), "OptimizationDashboard"},
            {typeof(LargeFileFinder), "LargeFileFinder"},
            {typeof(AppUsagePage), "AppUsage"},
            {typeof(AppDataCleanupCard), "AppDataCleanup"},
            {typeof(OptimizationPage), "Optimization"},
            {typeof(MonitoringDashboardPage), "MonitoringDashboard"},
            {typeof(TaskManagerPage), "TaskManager"},
            {typeof(PerformanceTrendsPage), "PerformanceTrends"},
            {typeof(StartupImpactPage), "StartupImpact"},
            {typeof(BackgroundProcessPage), "BackgroundProcess"},
        };

        private SystemTrayHelper _systemTrayHelper;
        private static AppUsageService? _appUsageService;
        private bool _isSyncingNavigationSelection;

        public MainWindow(string? initialFeature = null)
        {
            this.InitializeComponent();
            Title = "Winspeqt - Windows System Inspector";
            AppWindow.Resize(new Windows.Graphics.SizeInt32(1200, 800));
            RootFrame.Navigated += RootFrame_Navigated;

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
                appWindow.SetIcon(@"Assets\Quantum Lens Transparent (Icon).ico");
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

                switch (feature)
                {
                    case "AppUpdateChecker":
                        RootFrame.Navigate(typeof(AppSecurityPage));
                        break;
                    case "SecurityStatus":
                        RootFrame.Navigate(typeof(SecurityStatusPage));
                        break;
                    case "Optimization":
                        RootFrame.Navigate(typeof(OptimizationPage));
                        break;
                    case "LargeFileFinder":
                        RootFrame.Navigate(typeof(LargeFileFinder));
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

        // Provide the service to pages
        public static AppUsageService? GetAppUsageService() => _appUsageService;

        public void CleanupAndExit()
        {
            _appUsageService?.SaveData();
            _appUsageService?.Dispose();
            _systemTrayHelper?.Dispose();
        }

        private async void FeedbackButton_Click(object sender, RoutedEventArgs e)
        {
            _ = await Windows.System.Launcher.LaunchUriAsync(new Uri("https://forms.cloud.microsoft/Pages/ResponsePage.aspx?id=m278xvtRqEi3eZ7lZLQEE3SxlEbNs7pKmP3fkIYe7phUNDVXOFJONzNNWk5CWTc5Q0tLSEM2RTFVNS4u"));
        }

        /// <summary>
        /// Handles navigation from the side bar. If the last navigation is still loading it will stop. Otherwise goes
        /// to next frame if page is found.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        private void NavigationView_SelectionChanged(Microsoft.UI.Xaml.Controls.NavigationView sender, Microsoft.UI.Xaml.Controls.NavigationViewSelectionChangedEventArgs args)
        {
            if (_isSyncingNavigationSelection)
                return;

            if (args.IsSettingsSelected)
            {
                RootFrame.Navigate(typeof(SettingsPage));
            }
            else
            {
                var selectedItem = (Microsoft.UI.Xaml.Controls.NavigationViewItem)args.SelectedItem;
                string selectedItemTag = (string)selectedItem.Tag;
                if (NavigationRoutes.TryGetValue(selectedItemTag, out Type? pageType))
                {
                    RootFrame.Navigate(pageType);
                }
            }
            nvCategories.IsBackEnabled = RootFrame.CanGoBack;
        }

        /// <summary>
        /// Tries to go back. Checks if user can still go back and updates accordingly.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        private void NavView_BackRequested(NavigationView sender,
                                   NavigationViewBackRequestedEventArgs args)
        {
            TryGoBack();
            nvCategories.IsBackEnabled = RootFrame.CanGoBack;
        }

        /// <summary>
        /// Checks if able to go back to prevent error. Will go back if it can.
        /// </summary>
        /// <returns></returns>
        private bool TryGoBack()
        {
            if (!RootFrame.CanGoBack)
                return false;

            RootFrame.GoBack();
            return true;
        }

        /// <summary>
        /// Sets the selected item in the navigation view in case the navigation came from a source other than the navigation
        /// view.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void RootFrame_Navigated(object sender, Microsoft.UI.Xaml.Navigation.NavigationEventArgs e)
        {
            nvCategories.IsBackEnabled = RootFrame.CanGoBack;

            if (e.SourcePageType == typeof(SettingsPage))
            {
                _isSyncingNavigationSelection = true;
                nvCategories.SelectedItem = nvCategories.SettingsItem;
                _isSyncingNavigationSelection = false;
                return;
            }

            if (!PageToTagMap.TryGetValue(e.SourcePageType, out string? tag))
                return;

            NavigationViewItem? navItem = FindNavigationViewItemByTag(nvCategories.MenuItems, tag);
            if (navItem == null)
                return;

            _isSyncingNavigationSelection = true;
            nvCategories.SelectedItem = navItem;
            _isSyncingNavigationSelection = false;
        }

        /// <summary>
        /// Finds the item related to the navigation view request
        /// </summary>
        /// <param name="items"></param>
        /// <param name="tag"></param>
        /// <returns></returns>
        private static NavigationViewItem? FindNavigationViewItemByTag(IList<object> items, string tag)
        {
            foreach (object item in items)
            {
                if (item is not NavigationViewItem navItem)
                    continue;

                if (string.Equals(navItem.Tag as string, tag, StringComparison.Ordinal))
                    return navItem;

                NavigationViewItem? childItem = FindNavigationViewItemByTag(navItem.MenuItems, tag);
                if (childItem != null)
                    return childItem;
            }

            return null;
        }
    }
}
