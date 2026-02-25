using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using System;
using Winspeqt.ViewModels;

namespace Winspeqt.Views
{
    public sealed partial class SettingsPage : Page
    {
        public SettingsViewModel ViewModel { get; }

        public SettingsPage()
        {
            this.InitializeComponent();
            ViewModel = new SettingsViewModel();
            this.DataContext = ViewModel;
            ViewModel.NavigationRequested += OnNavigationRequested;
        }

        private void StartupButton_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.LaunchAtStartup = !ViewModel.LaunchAtStartup;
        }

        private void OnNavigationRequested(object sender, string destination)
        {
            if (destination == "Back" && Frame.CanGoBack)
                Frame.GoBack();
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            if (Frame.CanGoBack)
                Frame.GoBack();
        }
    }

    public class StartupButtonColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is bool enabled)
                return new Microsoft.UI.Xaml.Media.SolidColorBrush(
                    enabled ? Microsoft.UI.ColorHelper.FromArgb(255, 196, 43, 43) : Microsoft.UI.ColorHelper.FromArgb(255, 0, 122, 204));
            return new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 0, 122, 204));
        }
        public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotImplementedException();
    }

    public class InverseBoolConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
            => value is bool b ? !b : true;

        public object ConvertBack(object value, Type targetType, object parameter, string language)
            => value is bool b ? !b : false;
    }
}