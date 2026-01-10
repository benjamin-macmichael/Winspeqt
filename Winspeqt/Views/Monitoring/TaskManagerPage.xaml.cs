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