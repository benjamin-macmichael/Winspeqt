using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Winspeqt.ViewModels.Optimization;

namespace Winspeqt.Views.Optimization
{
    public sealed partial class OptimizationPage : Page
    {
        public OptimizationViewModel ViewModel { get; }

        public OptimizationPage()
        {
            this.InitializeComponent();
            ViewModel = new OptimizationViewModel();
            this.DataContext = ViewModel;
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            if (Frame.CanGoBack) Frame.GoBack();
            else Frame.Navigate(typeof(DashboardPage));
        }

        private void RunAgain_Click(object sender, RoutedEventArgs e)
        {
            // Reset state so the user sees the idle screen again before re-running
            ViewModel.HasCompleted = false;
            ViewModel.Result = null;
            ViewModel.RunOptimizationCommand.Execute(null);
        }
    }
}