using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
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
    }
}