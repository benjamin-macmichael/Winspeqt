using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using System;
using WinRT.Interop;

namespace Winspeqt.Views
{
    public sealed partial class MainWindow : Window
    {
        public MainWindow()
        {
            this.InitializeComponent();

            // Set window title
            Title = "Winspeqt - Windows System Inspector";

            // Set a nice default size
            AppWindow.Resize(new Windows.Graphics.SizeInt32(1200, 800));

            // Navigate to the dashboard
            RootFrame.Navigate(typeof(DashboardPage));

            if (AppWindowTitleBar.IsCustomizationSupported() is true)
            {
                IntPtr hWnd = WindowNative.GetWindowHandle(this);
                WindowId wndId = Win32Interop.GetWindowIdFromWindow(hWnd);
                AppWindow appWindow = AppWindow.GetFromWindowId(wndId);
                appWindow.SetIcon(@"Assets\QuantumLens.ico");
            }
        }
    }
}