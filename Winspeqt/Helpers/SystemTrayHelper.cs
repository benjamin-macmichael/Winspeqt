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
                    // Extract icon from shell32.dll (index 15 is a computer/system icon)
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

            // Force the icon to be visible
            _taskbarIcon.ForceCreate(false);

            // Show notification on startup
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

        public void HideToTray()
        {
            try
            {
                _taskbarIcon?.ShowNotification(
                    "Winspeqt Minimized",
                    "Still tracking in background. Use taskbar to restore.",
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