using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Winspeqt.ViewModels.Security;

namespace Winspeqt.Views.Security
{
    public sealed partial class SettingsRecommendationsPage : Page
    {
        public SettingsRecommendationsViewModel ViewModel { get; }

        public SettingsRecommendationsPage()
        {
            this.InitializeComponent();
            ViewModel = new SettingsRecommendationsViewModel();
            this.DataContext = ViewModel;
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            if (Frame.CanGoBack)
            {
                Frame.GoBack();
            }
        }
    }
}