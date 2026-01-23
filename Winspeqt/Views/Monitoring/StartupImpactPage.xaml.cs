using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Winspeqt.ViewModels.Monitoring;

namespace Winspeqt.Views.Monitoring
{
    public sealed partial class StartupImpactPage : Page
    {
        public StartupImpactViewModel ViewModel { get; }

        public StartupImpactPage()
        {
            InitializeComponent();
            this.ViewModel = new StartupImpactViewModel();
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
