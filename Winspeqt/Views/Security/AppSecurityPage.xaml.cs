using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Winspeqt.ViewModels.Security;

namespace Winspeqt.Views.Security
{
    public sealed partial class AppSecurityPage : Page
    {
        public AppSecurityViewModel ViewModel { get; }

        public AppSecurityPage()
        {
            this.InitializeComponent();
            ViewModel = new AppSecurityViewModel();
            // Subscribe to property changes to update visibility
            ViewModel.PropertyChanged += ViewModel_PropertyChanged;
            this.Loaded += AppSecurityPage_Loaded;
            this.Unloaded += AppSecurityPage_Unloaded;
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            if (Frame.CanGoBack)
            {
                Frame.GoBack();
            }
        }

        private void ViewModel_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            // Update UI visibility based on ViewModel state
            if (e.PropertyName == nameof(ViewModel.IsScanning))
            {
                ScanButton.IsEnabled = !ViewModel.IsScanning;
                ScanningProgress.Visibility = ViewModel.IsScanning ? Visibility.Visible : Visibility.Collapsed;
                StatusText.Visibility = ViewModel.IsScanning ? Visibility.Collapsed : Visibility.Visible;
            }
            else if (e.PropertyName == nameof(ViewModel.HasScanned))
            {
                if (ViewModel.HasScanned)
                {
                    SummaryCard.Visibility = Visibility.Visible;
                    AppsList.Visibility = Visibility.Visible;
                    EmptyState.Visibility = Visibility.Collapsed;
                }
                else
                {
                    SummaryCard.Visibility = Visibility.Collapsed;
                    AppsList.Visibility = Visibility.Collapsed;
                    EmptyState.Visibility = Visibility.Visible;
                }
            }
            else if (e.PropertyName == nameof(ViewModel.CriticalAppsCount))
            {
                CriticalStack.Visibility = ViewModel.CriticalAppsCount > 0 ? Visibility.Visible : Visibility.Collapsed;
            }
            else if (e.PropertyName == nameof(ViewModel.OutdatedAppsCount))
            {
                OutdatedStack.Visibility = ViewModel.OutdatedAppsCount > 0 ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        private void AppSecurityPage_Loaded(object sender, RoutedEventArgs e)
        {
            if (XamlRoot != null)
            {
                ViewModel.SetXamlRoot(XamlRoot);
            }
        }

        private void AppSecurityPage_Unloaded(object sender, RoutedEventArgs e)
        {
            ViewModel.PropertyChanged -= ViewModel_PropertyChanged;
            ViewModel.Cleanup();
        }
    }
}