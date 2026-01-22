using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Diagnostics;
using Winspeqt.ViewModels.Security;

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

        private async void ShowDefenderInfo_Click(object sender, RoutedEventArgs e)
        {
            ContentDialog dialog = new ContentDialog
            {
                Title = "🛡️ Windows Defender",
                Content = "Windows Defender (also called Windows Security) is your computer's built-in antivirus protection.\n\n" +
                          "What it does:\n" +
                          "• Protects against viruses, malware, and spyware\n" +
                          "• Scans files and downloads automatically\n" +
                          "• Blocks suspicious programs and websites\n\n" +
                          "Why it's important:\n" +
                          "Without antivirus protection, your computer is vulnerable to harmful software that could steal your data, slow down your PC, or lock your files for ransom.\n\n" +
                          "Keep it ON at all times for protection!",
                CloseButtonText = "Got it!",
                XamlRoot = this.XamlRoot
            };
            await dialog.ShowAsync();
        }

        private async void ShowFirewallInfo_Click(object sender, RoutedEventArgs e)
        {
            ContentDialog dialog = new ContentDialog
            {
                Title = "🔥 Windows Firewall",
                Content = "Windows Firewall is like a security guard for your computer's internet connection.\n\n" +
                          "What it does:\n" +
                          "• Controls what can connect to your PC from the internet\n" +
                          "• Blocks hackers from accessing your computer\n" +
                          "• Stops unauthorized programs from using your network\n\n" +
                          "Why it's important:\n" +
                          "Without a firewall, hackers could potentially access your computer over the internet, steal information, or take control of your system.\n\n" +
                          "Keep it ON for all network types (Private and Public)!",
                CloseButtonText = "Got it!",
                XamlRoot = this.XamlRoot
            };
            await dialog.ShowAsync();
        }

        private async void ShowUpdateInfo_Click(object sender, RoutedEventArgs e)
        {
            ContentDialog dialog = new ContentDialog
            {
                Title = "🔄 Windows Update",
                Content = "Windows Update keeps your computer secure and running smoothly.\n\n" +
                          "What it does:\n" +
                          "• Fixes security vulnerabilities that hackers exploit\n" +
                          "• Improves Windows features and performance\n" +
                          "• Updates drivers for your hardware\n\n" +
                          "Why it's important:\n" +
                          "Outdated software has security holes that hackers can use to attack your computer. Regular updates patch these holes and keep you safe.\n\n" +
                          "Check for updates at least once a month. Many updates install automatically, which is good!",
                CloseButtonText = "Got it!",
                XamlRoot = this.XamlRoot
            };
            await dialog.ShowAsync();
        }

        private async void ShowBitLockerInfo_Click(object sender, RoutedEventArgs e)
        {
            ContentDialog dialog = new ContentDialog
            {
                Title = "🔐 BitLocker/Device Encryption",
                Content = "BitLocker (or Device Encryption) scrambles all the data on your hard drive.\n\n" +
                          "What it does:\n" +
                          "• Encrypts your entire hard drive\n" +
                          "• Makes your data unreadable without your password\n" +
                          "• Protects files even if someone steals your computer\n\n" +
                          "Why it's important:\n" +
                          "If your laptop is lost or stolen, encryption prevents thieves from accessing your personal files, photos, and documents.\n\n" +
                          "Important: Save your recovery key in a safe place (like your Microsoft account) in case you forget your password!",
                CloseButtonText = "Got it!",
                XamlRoot = this.XamlRoot
            };
            await dialog.ShowAsync();
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