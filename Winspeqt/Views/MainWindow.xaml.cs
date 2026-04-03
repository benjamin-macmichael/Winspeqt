using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;
using Windows.UI;
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

        /// <summary>
        /// Initializes the main application window, configures navigation, theme chrome, tray behavior, and the startup page.
        /// </summary>
        /// <param name="initialFeature">Optional feature identifier to open immediately when the app is launched from a toast or shortcut.</param>
        public MainWindow(string? initialFeature = null)
        {
            this.InitializeComponent();
            Title = "Winspeqt - Windows System Inspector";

            var localSettings = Windows.Storage.ApplicationData.Current.LocalSettings;
            if (localSettings.Values.TryGetValue("IsDarkMode", out var themeObj) && themeObj is bool isDark)
                ((FrameworkElement)Content).RequestedTheme = isDark ? ElementTheme.Dark : ElementTheme.Light;
            AppWindow.Resize(new Windows.Graphics.SizeInt32(1200, 800));
            RootFrame.Navigated += RootFrame_Navigated;
            Activated += MainWindow_Activated;
            ((FrameworkElement)Content).ActualThemeChanged += MainWindow_ActualThemeChanged;
            AppWindow.TitleBar.ExtendsContentIntoTitleBar = true;
            AppWindow.SetIcon("Assets/StoreLogo.png");
            ApplyThemeChrome();
            if (AppWindow.TitleBar.ExtendsContentIntoTitleBar)
            {
                AppWindow.TitleBar.PreferredHeightOption = TitleBarHeightOption.Tall;
            }


            OverlappedPresenter presenter = OverlappedPresenter.Create();
            presenter.PreferredMinimumWidth = 1010;

            AppWindow.SetPresenter(presenter);


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

        /// <summary>
        /// Reapplies title bar and navigation chrome colors when the app theme changes.
        /// </summary>
        /// <param name="sender">The root framework element whose theme changed.</param>
        /// <param name="args">Unused event data.</param>
        private void MainWindow_ActualThemeChanged(FrameworkElement sender, object args)
        {
            ApplyThemeChrome();
        }

        /// <summary>
        /// Applies the current themed chrome color to the title bar and navigation surfaces.
        /// </summary>
        private void ApplyThemeChrome()
        {
            bool isDarkMode = ((FrameworkElement)Content).ActualTheme == ElementTheme.Dark;
            Color chromeColor = isDarkMode
                ? Color.FromArgb(255, 26, 26, 26)     // #1A1A1A — matches App.xaml Dark
                : Color.FromArgb(255, 240, 243, 249);  // #F0F3F9 — matches App.xaml Light
            Color buttonHover = isDarkMode
                ? Color.FromArgb(255, 45, 45, 45)
                : Color.FromArgb(255, 233, 233, 233);
            Color buttonPress = isDarkMode
                ? Color.FromArgb(255, 40, 40, 40)
                : Color.FromArgb(255, 238, 238, 238);
            Color foreground = isDarkMode
                ? Color.FromArgb(255, 255, 255, 255)
                : Color.FromArgb(255, 0, 0, 0);
            AppWindow.TitleBar.ButtonBackgroundColor = chromeColor;
            AppWindow.TitleBar.ButtonHoverBackgroundColor = buttonHover;
            AppWindow.TitleBar.ButtonPressedBackgroundColor = buttonPress;
            AppWindow.TitleBar.ButtonInactiveBackgroundColor = chromeColor;
            AppWindow.TitleBar.ButtonForegroundColor = foreground;
            AppWindow.TitleBar.ButtonHoverForegroundColor = foreground;
            AppWindow.TitleBar.ButtonPressedForegroundColor = foreground;
            AppWindow.TitleBar.ButtonInactiveForegroundColor = foreground;
            titleBar.Foreground = new SolidColorBrush(foreground);
            nvCategories.Background = new SolidColorBrush(chromeColor);
            AppTitleBar.Background = new SolidColorBrush(chromeColor);
        }

        /// <summary>
        /// Updates the title bar foreground to match the active or inactive window state.
        /// </summary>
        /// <param name="sender">The window raising the activation event.</param>
        /// <param name="args">Activation state data for the window.</param>
        private void MainWindow_Activated(object sender, WindowActivatedEventArgs args)
        {
            if (args.WindowActivationState == WindowActivationState.Deactivated)
            {
                titleBar.Foreground =
                    (SolidColorBrush)App.Current.Resources["WindowCaptionForegroundDisabled"];
            }
            else
            {
                titleBar.Foreground = ((FrameworkElement)Content).ActualTheme == ElementTheme.Dark
                    ? new SolidColorBrush(Color.FromArgb(255, 255, 255, 255))
                    : new SolidColorBrush(Color.FromArgb(255, 0, 0, 0));
            }
        }

        /// <summary>
        /// Brings the window to the foreground and navigates to the requested feature page.
        /// </summary>
        /// <param name="feature">Feature identifier used to select the destination page.</param>
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

        /// <summary>
        /// Returns the shared app usage service instance for pages that need usage data access.
        /// </summary>
        /// <returns>The shared <see cref="AppUsageService"/> instance, or <see langword="null"/> if it has not been created.</returns>
        public static AppUsageService? GetAppUsageService() => _appUsageService;

        /// <summary>
        /// Saves persisted data and disposes long-lived services before the application exits.
        /// </summary>
        public void CleanupAndExit()
        {
            _appUsageService?.SaveData();
            _appUsageService?.Dispose();
            _systemTrayHelper?.Dispose();
        }

        /// <summary>
        /// Handles side navigation selection changes and routes the frame to the matching page.
        /// </summary>
        /// <param name="sender">The navigation view whose selection changed.</param>
        /// <param name="args">Selection change data that identifies the selected item.</param>
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
        /// Navigates the frame backward when a previous page is available.
        /// </summary>
        /// <param name="sender">The title bar control that raised the back request.</param>
        /// <param name="args">Unused event data.</param>
        private void TitleBar_BackRequested(TitleBar sender,
                                   object args)
        {
            if (this.RootFrame.CanGoBack)
            {
                this.RootFrame.GoBack();
            }
        }

        /// <summary>
        /// Toggles the visibility of the navigation pane from the custom title bar.
        /// </summary>
        /// <param name="sender">The title bar control that raised the pane toggle request.</param>
        /// <param name="args">Unused event data.</param>
        private void TitleBar_PaneToggleRequested(TitleBar sender, object args)
        {
            nvCategories.IsPaneOpen = !nvCategories.IsPaneOpen;
        }

        /// <summary>
        /// Synchronizes the selected navigation item after frame navigation initiated outside the navigation view.
        /// </summary>
        /// <param name="sender">The frame that completed navigation.</param>
        /// <param name="e">Navigation event data containing the destination page type.</param>
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
        /// Recursively finds the navigation view item whose tag matches the requested page tag.
        /// </summary>
        /// <param name="items">Navigation view items to search.</param>
        /// <param name="tag">Tag value associated with the target page.</param>
        /// <returns>The matching <see cref="NavigationViewItem"/>, or <see langword="null"/> when no match exists.</returns>
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
