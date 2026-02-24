using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
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
            ViewModel.PropertyChanged += ViewModel_PropertyChanged;
        }

        private void ViewModel_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ViewModel.OverallScore) ||
                e.PropertyName == nameof(ViewModel.OverallScoreColor))
            {
                UpdateScoreVisuals();
            }
        }

        private void UpdateScoreVisuals()
        {
            var hex = ViewModel.OverallScoreColor?.TrimStart('#');
            if (hex == null || hex.Length != 6) return;

            byte r = Convert.ToByte(hex[0..2], 16);
            byte g = Convert.ToByte(hex[2..4], 16);
            byte b = Convert.ToByte(hex[4..6], 16);
            var color = Windows.UI.Color.FromArgb(255, r, g, b);
            var brush = new SolidColorBrush(color);

            ScoreNumber.Foreground = brush;
            DrawScoreArc(ViewModel.OverallScore, brush);
        }

        private void DrawScoreArc(int score, SolidColorBrush brush)
        {
            const double radius = 40;
            const double cx = 48;
            const double cy = 48;
            const double startDeg = -90;

            double pct = Math.Min(score / 100.0, 0.999);
            double endDeg = startDeg + pct * 360.0;

            double startRad = startDeg * Math.PI / 180.0;
            double endRad = endDeg * Math.PI / 180.0;

            double x1 = cx + radius * Math.Cos(startRad);
            double y1 = cy + radius * Math.Sin(startRad);
            double x2 = cx + radius * Math.Cos(endRad);
            double y2 = cy + radius * Math.Sin(endRad);

            bool isLargeArc = pct > 0.5;

            var figure = new PathFigure
            {
                StartPoint = new Windows.Foundation.Point(x1, y1),
                IsClosed = false
            };

            figure.Segments.Add(new ArcSegment
            {
                Point = new Windows.Foundation.Point(x2, y2),
                Size = new Windows.Foundation.Size(radius, radius),
                SweepDirection = SweepDirection.Clockwise,
                IsLargeArc = isLargeArc
            });

            var geometry = new PathGeometry();
            geometry.Figures.Add(figure);

            ScoreArc.Data = geometry;
            ScoreArc.Stroke = brush;
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            if (Frame.CanGoBack) Frame.GoBack();
            else Frame.Navigate(typeof(DashboardPage));
        }

        // ── Info dialogs ──────────────────────────────────────────────────────

        private async void ShowDefenderInfo_Click(object sender, RoutedEventArgs e)
        {
            await new ContentDialog
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
            }.ShowAsync();
        }

        private async void ShowFirewallInfo_Click(object sender, RoutedEventArgs e)
        {
            await new ContentDialog
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
            }.ShowAsync();
        }

        private async void ShowUpdateInfo_Click(object sender, RoutedEventArgs e)
        {
            await new ContentDialog
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
            }.ShowAsync();
        }

        private async void ShowBitLockerInfo_Click(object sender, RoutedEventArgs e)
        {
            await new ContentDialog
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
            }.ShowAsync();
        }

        private async void ShowDriveHealthInfo_Click(object sender, RoutedEventArgs e)
        {
            await new ContentDialog
            {
                Title = "💾 Drive Health",
                Content = "Drive Health monitors the status of your hard drives and SSDs using built-in diagnostic data reported by Windows.\n\n" +
                          "What it checks:\n" +
                          "• Whether Windows detects any drive errors or failures\n" +
                          "• The reported health status of all connected drives\n\n" +
                          "Why it's important:\n" +
                          "Hard drives and SSDs can fail without warning. A failing drive can cause you to lose all your files, photos, and documents permanently.\n\n" +
                          "If a drive shows a warning, back up your important files immediately and consider replacing the drive soon.",
                CloseButtonText = "Got it!",
                XamlRoot = this.XamlRoot
            }.ShowAsync();
        }

        private async void ShowSecureBootInfo_Click(object sender, RoutedEventArgs e)
        {
            await new ContentDialog
            {
                Title = "🔒 Secure Boot",
                Content = "Secure Boot is a feature built into modern PCs that ensures your computer only starts up using trusted software.\n\n" +
                          "What it does:\n" +
                          "• Verifies that your operating system hasn't been tampered with\n" +
                          "• Blocks malicious software that tries to load before Windows starts\n" +
                          "• Protects against a type of malware called 'bootkits'\n\n" +
                          "Why it's important:\n" +
                          "Bootkits are especially dangerous because they load before your antivirus software can catch them. Secure Boot closes this attack vector entirely.\n\n" +
                          "If Secure Boot is disabled, you can enable it in your PC's BIOS/UEFI settings — usually accessed by pressing F2 or DEL at startup.",
                CloseButtonText = "Got it!",
                XamlRoot = this.XamlRoot
            }.ShowAsync();
        }

        // ── Action buttons ────────────────────────────────────────────────────

        private void OpenDefenderSettings_Click(object sender, RoutedEventArgs e) => TryLaunch("windowsdefender:");
        private void OpenFirewallSettings_Click(object sender, RoutedEventArgs e) => TryLaunch("firewall.cpl");
        private void OpenWindowsUpdate_Click(object sender, RoutedEventArgs e) => TryLaunch("ms-settings:windowsupdate");

        private void OpenBitLockerSettings_Click(object sender, RoutedEventArgs e)
        {
            if (!TryLaunch("ms-settings:deviceencryption"))
                TryLaunch("control", "/name Microsoft.BitLockerDriveEncryption");
        }

        private void OpenDiskManagement_Click(object sender, RoutedEventArgs e) => TryLaunch("diskmgmt.msc");
        private void OpenUEFISettings_Click(object sender, RoutedEventArgs e) => TryLaunch("msinfo32.exe");

        // ── Helpers ───────────────────────────────────────────────────────────

        private bool TryLaunch(string fileName, string args = null)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = fileName,
                    Arguments = args ?? string.Empty,
                    UseShellExecute = true
                });
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error launching {fileName}: {ex.Message}");
                return false;
            }
        }
    }
}