using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System;
using Winspeqt.ViewModels.Security;
using Microsoft.UI.Xaml.Shapes;
using Microsoft.UI.Xaml.Media.Animation;

namespace Winspeqt.Views.Security
{
    public sealed partial class AppSecurityPage : Page
    {
        public AppSecurityViewModel ViewModel { get; }

        public AppSecurityPage()
        {
            this.InitializeComponent();
            ViewModel = new AppSecurityViewModel();
            ViewModel.PropertyChanged += ViewModel_PropertyChanged;
            this.Loaded += AppSecurityPage_Loaded;
            this.Unloaded += AppSecurityPage_Unloaded;
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            if (Frame.CanGoBack)
                Frame.GoBack();
            else
                Frame.Navigate(typeof(DashboardPage));
        }

        private void ViewModel_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ViewModel.IsScanning))
            {
                ScanButton.IsEnabled = !ViewModel.IsScanning;
                ScanningProgress.Visibility = Visibility.Collapsed;
                ScanProgressBar.Visibility = ViewModel.IsScanning ? Visibility.Visible : Visibility.Collapsed;
                StatusText.Visibility = Visibility.Visible;
            }
            else if (e.PropertyName == nameof(ViewModel.HasScanned))
            {
                bool scanned = ViewModel.HasScanned;
                HealthScoreCard.Visibility = scanned ? Visibility.Visible : Visibility.Collapsed;
                SummaryCard.Visibility = scanned ? Visibility.Visible : Visibility.Collapsed;
                AppsList.Visibility = scanned ? Visibility.Visible : Visibility.Collapsed;
                EmptyState.Visibility = scanned ? Visibility.Collapsed : Visibility.Visible;
            }
            else if (e.PropertyName == nameof(ViewModel.CriticalAppsCount))
            {
                CriticalStack.Visibility = ViewModel.CriticalAppsCount > 0
                    ? Visibility.Visible : Visibility.Collapsed;
            }
            else if (e.PropertyName == nameof(ViewModel.OutdatedAppsCount))
            {
                OutdatedStack.Visibility = ViewModel.OutdatedAppsCount > 0
                    ? Visibility.Visible : Visibility.Collapsed;
            }
            else if (e.PropertyName == nameof(ViewModel.HealthScore))
            {
                UpdateHealthScoreVisuals();
            }
        }

        private void UpdateHealthScoreVisuals()
        {
            var hex = ViewModel.HealthScoreColor.TrimStart('#');
            if (hex.Length != 6) return;

            byte r = System.Convert.ToByte(hex[0..2], 16);
            byte g = System.Convert.ToByte(hex[2..4], 16);
            byte b = System.Convert.ToByte(hex[4..6], 16);
            var color = Windows.UI.Color.FromArgb(255, r, g, b);
            var brush = new SolidColorBrush(color);

            ScoreNumber.Foreground = brush;
            HealthScoreLabelText.Foreground = brush;

            DrawScoreArc(ViewModel.HealthScore, brush);
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

        private void AppSecurityPage_Loaded(object sender, RoutedEventArgs e)
        {
            if (XamlRoot != null)
                ViewModel.SetXamlRoot(XamlRoot);
        }

        private void AppSecurityPage_Unloaded(object sender, RoutedEventArgs e)
        {
            ViewModel.PropertyChanged -= ViewModel_PropertyChanged;
            ViewModel.Cleanup();
        }
    }
}