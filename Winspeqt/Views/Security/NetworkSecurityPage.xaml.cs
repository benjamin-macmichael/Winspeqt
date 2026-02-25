using Microsoft.UI.Xaml.Controls;
using Winspeqt.ViewModels.Security;

namespace Winspeqt.Views.Security
{
    public sealed partial class NetworkSecurityPage : Page
    {
        public NetworkSecurityViewModel ViewModel { get; }

        public NetworkSecurityPage()
        {
            this.InitializeComponent();
            ViewModel = new NetworkSecurityViewModel();
        }

        private void BackButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            if (Frame.CanGoBack)
            {
                Frame.GoBack();
            }
        }
    }
}