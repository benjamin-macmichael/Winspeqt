using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Winspeqt.ViewModels.Monitoring;

namespace Winspeqt.Views.Monitoring
{
    public sealed partial class TaskManagerPage : Page
    {
        public TaskManagerViewModel ViewModel { get; }

        public TaskManagerPage()
        {
            this.InitializeComponent();
            ViewModel = new TaskManagerViewModel();
            this.DataContext = ViewModel;

            // Set XamlRoot after page is loaded
            this.Loaded += TaskManagerPage_Loaded;
        }

        private void TaskManagerPage_Loaded(object sender, RoutedEventArgs e)
        {
            ViewModel.SetXamlRoot(this.XamlRoot);
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            if (Frame.CanGoBack)
            {
                Frame.GoBack();
            }
        }

        protected override void OnNavigatedFrom(Microsoft.UI.Xaml.Navigation.NavigationEventArgs e)
        {
            base.OnNavigatedFrom(e);
            ViewModel.StopAutoRefresh();
        }
    }
}