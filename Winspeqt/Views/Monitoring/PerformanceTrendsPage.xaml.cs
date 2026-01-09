// Views/PerformanceTrendsPage.xaml.cs
using Microsoft.UI.Xaml.Controls;
using Winspeqt.ViewModels;

namespace Winspeqt.Views.Monitoring
{
    public sealed partial class PerformanceTrendsPage : Page
    {
        public PerformanceTrendsViewModel ViewModel { get; } = new();

        public PerformanceTrendsPage()
        {
            InitializeComponent();
        }
    }
}