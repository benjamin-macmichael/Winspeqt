using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using Winspeqt.ViewModels;

namespace Winspeqt.Views
{
    public sealed partial class DashboardPage : Page
    {
        public DashboardViewModel ViewModel { get; }

        public DashboardPage()
        {
            this.InitializeComponent();
            ViewModel = new DashboardViewModel();
            this.DataContext = ViewModel;

            // Subscribe to the ViewModel's navigation event
            ViewModel.NavigationRequested += OnNavigationRequested;
        }

        private async void OnNavigationRequested(object sender, string feature)
        {
            // TODO: Navigate to the actual page later
            // For now, show a dialog
            ContentDialog dialog = new ContentDialog
            {
                Title = $"{feature} Feature",
                Content = $"The {feature} module is coming soon! We're building an amazing experience for you.",
                CloseButtonText = "Got it",
                XamlRoot = this.XamlRoot
            };

            await dialog.ShowAsync();
        }
    }
}