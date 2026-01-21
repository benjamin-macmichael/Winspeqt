using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Winspeqt.ViewModels.Security;
using System.Diagnostics;

namespace Winspeqt.Views.Security
{
    public sealed partial class SecurityStatusPage : Page
    {
        public SecurityStatusViewModel ViewModel { get; }

        public SecurityStatusPage()
        {
            this.InitializeComponent();
            ViewModel = new SecurityStatusViewModel();
            this.DataContext = ViewModel;
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            if (Frame.CanGoBack)
            {
                Frame.GoBack();
            }
        }

        private void OpenDefenderSettings_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Open Windows Security app
                Process.Start(new ProcessStartInfo
                {
                    FileName = "windowsdefender:",
                    UseShellExecute = true
                });
            }
            catch (System.Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error opening Windows Security: {ex.Message}");
            }
        }

        private void OpenFirewallSettings_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Open Windows Firewall settings
                Process.Start(new ProcessStartInfo
                {
                    FileName = "firewall.cpl",
                    UseShellExecute = true
                });
            }
            catch (System.Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error opening firewall settings: {ex.Message}");
            }
        }

        private void OpenWindowsUpdate_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Open Windows Update settings
                Process.Start(new ProcessStartInfo
                {
                    FileName = "ms-settings:windowsupdate",
                    UseShellExecute = true
                });
            }
            catch (System.Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error opening Windows Update: {ex.Message}");
            }
        }

        private void OpenBitLockerSettings_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Open BitLocker/Device Encryption settings
                // Try Device Encryption first (Windows 11/Home), fall back to BitLocker control panel
                Process.Start(new ProcessStartInfo
                {
                    FileName = "ms-settings:deviceencryption",
                    UseShellExecute = true
                });
            }
            catch
            {
                try
                {
                    // Fallback to BitLocker control panel (Pro/Enterprise)
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "control",
                        Arguments = "/name Microsoft.BitLockerDriveEncryption",
                        UseShellExecute = true
                    });
                }
                catch (System.Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error opening BitLocker settings: {ex.Message}");
                }
            }
        }
    }
}