using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Documents;
using System;
using Windows.ApplicationModel.DataTransfer;
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

        private void OnNavigationRequested(object? sender, string destination)
        {
            if (destination == "Back" && Frame.CanGoBack)
                Frame.GoBack();
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            if (Frame.CanGoBack)
                Frame.GoBack();
        }

        /// <summary>
        /// This will open the default mailing app and address an email to Winspeqt Support. If that doesn't work,
        /// then it copies the email address to the clipboard. We may just want it to do the latter.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        private async void Hyperlink_Click(Hyperlink sender, HyperlinkClickEventArgs args)
        {
            if (sender.NavigateUri is not Uri link)
                return;

            var launched = await Windows.System.Launcher.LaunchUriAsync(link);
            if (launched)
                return;

            var emailAddress = link.OriginalString;
            if (emailAddress.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase))
                emailAddress = emailAddress.Substring("mailto:".Length);

            var querySeparator = emailAddress.IndexOf('?');
            if (querySeparator >= 0)
                emailAddress = emailAddress.Substring(0, querySeparator);

            if (string.IsNullOrWhiteSpace(emailAddress))
                emailAddress = "winspeqtsupport@byu.onmicrosoft.com";

            var dataPackage = new DataPackage();
            dataPackage.SetText(emailAddress);
            Clipboard.SetContent(dataPackage);
            Clipboard.Flush();

            var dialog = new ContentDialog
            {
                XamlRoot = this.XamlRoot,
                Title = "Email app unavailable",
                Content = $"We couldn't open an email app. The address {emailAddress} was copied to your clipboard.",
                CloseButtonText = "OK"
            };

            await dialog.ShowAsync();
        }

        /// <summary>
        /// Opens the feedback form in the user's default browser.
        /// </summary>
        /// <param name="sender">The feedback button.</param>
        /// <param name="e">Routed event data.</param>
        private async void FeedbackButton_Click(object sender, RoutedEventArgs e)
        {
            _ = await Windows.System.Launcher.LaunchUriAsync(new Uri("https://forms.cloud.microsoft/Pages/ResponsePage.aspx?id=m278xvtRqEi3eZ7lZLQEE3SxlEbNs7pKmP3fkIYe7phUNDVXOFJONzNNWk5CWTc5Q0tLSEM2RTFVNS4u"));
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
