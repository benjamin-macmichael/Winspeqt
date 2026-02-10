using H.NotifyIcon;
using H.NotifyIcon.Core;
using Microsoft.UI.Xaml;
using System;
using System.Drawing;
using System.Runtime.InteropServices;
using Winspeqt.Services;

namespace Winspeqt.Helpers
{
    public class SystemTrayHelper : IDisposable
    {
        private TaskbarIcon _taskbarIcon;
        private AppUsageService _appUsageService;
        private Window _mainWindow;

        // Import shell32.dll to extract system icons
        [DllImport("shell32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr ExtractIcon(IntPtr hInst, string lpszExeFileName, int nIconIndex);

        [DllImport("kernel32.dll")]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        public SystemTrayHelper(Window mainWindow, AppUsageService appUsageService)
        {
            _mainWindow = mainWindow;
            _appUsageService = appUsageService;
            InitializeTrayIcon();
        }

        private void InitializeTrayIcon()
        {
            _taskbarIcon = new TaskbarIcon
            {
                ToolTipText = "Winspeqt - App Usage Tracker"
            };

            // Try multiple possible icon paths
            bool iconSet = false;
            string[] possiblePaths = new[]
            {
                System.IO.Path.Combine(AppContext.BaseDirectory, "Assets", "QuantumLens.ico"),
                System.IO.Path.Combine(AppContext.BaseDirectory, "QuantumLens.ico"),
                "Assets/QuantumLens.ico",
                "QuantumLens.ico"
            };

            foreach (var iconPath in possiblePaths)
            {
                try
                {
                    System.Diagnostics.Debug.WriteLine($"Trying icon path: {iconPath}");

                    if (System.IO.File.Exists(iconPath))
                    {
                        System.Diagnostics.Debug.WriteLine($"Icon found at: {iconPath}");
                        var icon = new System.Drawing.Icon(iconPath);
                        _taskbarIcon.Icon = icon;
                        iconSet = true;
                        break;
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to load icon from {iconPath}: {ex.Message}");
                }
            }

            // Fallback to default Windows icon if app icon didn't work
            if (!iconSet)
            {
                try
                {
                    System.Diagnostics.Debug.WriteLine("Using fallback Windows icon");
                    IntPtr hIcon = ExtractIcon(GetModuleHandle(null), "shell32.dll", 15);

                    if (hIcon != IntPtr.Zero)
                    {
                        var icon = System.Drawing.Icon.FromHandle(hIcon);
                        _taskbarIcon.Icon = icon;
                        iconSet = true;
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to load Windows icon: {ex.Message}");
                }
            }

            System.Diagnostics.Debug.WriteLine($"Icon set: {iconSet}");

            // Add click handlers
            try
            {
                var taskbarType = _taskbarIcon.GetType();

                var leftClickEvent = taskbarType.GetEvent("TrayLeftMouseUp");
                if (leftClickEvent != null)
                {
                    leftClickEvent.AddEventHandler(_taskbarIcon, new Action<object, EventArgs>((s, e) => ShowMainWindow()));
                }

                var rightClickEvent = taskbarType.GetEvent("TrayRightMouseUp");
                if (rightClickEvent != null)
                {
                    rightClickEvent.AddEventHandler(_taskbarIcon, new Action<object, EventArgs>((s, e) => ShowContextMenu()));
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to attach click events: {ex.Message}");
            }

            _taskbarIcon.ForceCreate(false);

            try
            {
                _taskbarIcon.ShowNotification(
                    "Winspeqt Running",
                    "App usage tracking is active in the background",
                    NotificationIcon.Info
                );
            }
            catch
            {
                // Notifications not supported in this version
            }
        }

        private void ShowMainWindow()
        {
            _mainWindow?.DispatcherQueue.TryEnqueue(() =>
            {
                var hWnd = WinRT.Interop.WindowNative.GetWindowHandle(_mainWindow);
                var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hWnd);
                var appWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(windowId);

                var presenter = appWindow.Presenter as Microsoft.UI.Windowing.OverlappedPresenter;
                if (presenter != null && presenter.State == Microsoft.UI.Windowing.OverlappedPresenterState.Minimized)
                {
                    presenter.Restore();
                }

                _mainWindow.Activate();
            });
        }

        private void ShowContextMenu()
        {
            _mainWindow?.DispatcherQueue.TryEnqueue(async () =>
            {
                var dialog = new Microsoft.UI.Xaml.Controls.ContentDialog
                {
                    Title = "Winspeqt",
                    Content = "What would you like to do?",
                    PrimaryButtonText = "Open Window",
                    SecondaryButtonText = "Exit App",
                    CloseButtonText = "Cancel",
                    XamlRoot = _mainWindow.Content.XamlRoot
                };

                var result = await dialog.ShowAsync();

                if (result == Microsoft.UI.Xaml.Controls.ContentDialogResult.Primary)
                {
                    ShowMainWindow();
                }
                else if (result == Microsoft.UI.Xaml.Controls.ContentDialogResult.Secondary)
                {
                    ExitApplication();
                }
            });
        }

        private void ExitApplication()
        {
            _appUsageService.SaveData();
            _appUsageService.Dispose();

            _taskbarIcon?.Dispose();

            _mainWindow?.DispatcherQueue.TryEnqueue(() =>
            {
                if (_mainWindow is Views.MainWindow mainWin)
                {
                    mainWin.CleanupAndExit();
                }
                Microsoft.UI.Xaml.Application.Current.Exit();
            });
        }

        public void HideToTray()
        {
            try
            {
                _taskbarIcon?.ShowNotification(
                    "Winspeqt Minimized",
                    "Still tracking in background. Click tray icon to restore.",
                    NotificationIcon.Info
                );
            }
            catch
            {
                // Notifications not supported
            }
        }

        public void Dispose()
        {
            try
            {
                _taskbarIcon?.Dispose();
            }
            catch
            {
                // Ignore disposal errors
            }
        }
    }
}