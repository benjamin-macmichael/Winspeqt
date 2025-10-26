using Microsoft.UI.Xaml;

namespace Winspeqt
{
    public sealed partial class MainWindow : Window
    {
        public MainWindow()
        {
            this.InitializeComponent();

            // Set window title
            Title = "Winspeqt - Windows System Inspector";

            // Set a nice default size
            AppWindow.Resize(new Windows.Graphics.SizeInt32(1200, 800));

            // Navigate to the dashboard
            RootFrame.Navigate(typeof(DashboardPage));
        }
    }
}