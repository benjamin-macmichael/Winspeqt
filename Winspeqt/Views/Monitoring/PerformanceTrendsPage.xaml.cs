// Views/PerformanceTrendsPage.xaml.cs

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Winspeqt.ViewModels;
using Winspeqt.ViewModels.Monitoring;

namespace Winspeqt.Views.Monitoring
{
    public sealed partial class PerformanceTrendsPage : Page
    {
        private const double ChartsBreakpointWidth = 750;
        public PerformanceTrendsViewModel ViewModel { get; }

        public PerformanceTrendsPage()
        {
            this.InitializeComponent();
            ViewModel = new PerformanceTrendsViewModel();
            this.DataContext = ViewModel;
        }

        private void ChartsGrid_Loaded(object sender, RoutedEventArgs e)
        {
            UpdateChartsLayout(ChartsGrid.ActualWidth);
        }

        private void ChartsGrid_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            UpdateChartsLayout(e.NewSize.Width);
        }

        private void UpdateChartsLayout(double availableWidth)
        {
            bool isNarrow = availableWidth < ChartsBreakpointWidth;

            if (isNarrow)
            {
                ChartsColumn1.Width = new GridLength(0);
                Grid.SetRow(CpuCard, 0);
                Grid.SetColumn(CpuCard, 0);
                CpuCard.Margin = new Thickness(0, 0, 0, 12);

                Grid.SetRow(MemoryCard, 1);
                Grid.SetColumn(MemoryCard, 0);
                MemoryCard.Margin = new Thickness(0, 0, 0, 12);

                Grid.SetRow(DiskCard, 2);
                Grid.SetColumn(DiskCard, 0);
                DiskCard.Margin = new Thickness(0);
            }
            else
            {
                ChartsColumn1.Width = new GridLength(1, GridUnitType.Star);
                Grid.SetRow(CpuCard, 0);
                Grid.SetColumn(CpuCard, 0);
                CpuCard.Margin = new Thickness(0, 0, 12, 0);

                Grid.SetRow(MemoryCard, 0);
                Grid.SetColumn(MemoryCard, 1);
                MemoryCard.Margin = new Thickness(12, 0, 0, 0);

                Grid.SetRow(DiskCard, 1);
                Grid.SetColumn(DiskCard, 0);
                DiskCard.Margin = new Thickness(0, 12, 12, 0);
            }
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            // Stop auto-refresh when leaving page
            ViewModel.StopAutoRefresh();

            if (Frame.CanGoBack)
            {
                Frame.GoBack();
            }
        }
    }
}
