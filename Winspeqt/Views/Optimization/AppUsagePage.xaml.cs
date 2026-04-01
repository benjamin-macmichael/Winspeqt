using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using System;
using Winspeqt.Services;
using Winspeqt.ViewModels.Optimization;

namespace Winspeqt.Views.Optimization
{
    public sealed partial class AppUsagePage : Page
    {
        public AppUsagePage()
        {
            this.InitializeComponent();

            // Get the shared AppUsageService from MainWindow
            // You may need to adjust this based on how your App.xaml.cs exposes the window
            AppUsageService? appUsageService = Views.MainWindow.GetAppUsageService();

            DataContext = new AppUsageViewModel(appUsageService);
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            this.Frame.Navigate(typeof(OptimizationDashboardPage));
        }

        private const double ButtonBreakPoint = 1150;

        private void HeaderGrid_Loaded(object sender, RoutedEventArgs e)
        {
            UpdateButtonsArrayLayout(HeaderGrid.ActualWidth);
        }

        private void HeaderGrid_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            UpdateButtonsArrayLayout(e.NewSize.Width);
        }

        private void UpdateButtonsArrayLayout(double availableWidth)
        {
            bool isNarrow = availableWidth < ButtonBreakPoint;

            if (isNarrow)
            {
                Grid.SetRow(ButtonsArray, 2);
                Grid.SetColumn(ButtonsArray, 1);
                ButtonsArray.Margin = new Thickness(0, 10, 0, 0);
            } else
            {
                Grid.SetRow(ButtonsArray, 0);
                Grid.SetColumn(ButtonsArray, 2);
                ButtonsArray.Margin = new Thickness(0, 0, 0, 0);
            }
        }
    }

    // Converter for tracking button text
    public class TrackingButtonTextConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is bool isTracking)
            {
                return isTracking ? "⏸ Pause Tracking" : "▶ Resume Tracking";
            }
            return "▶ Resume Tracking";
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }

    // Converter for first letter of app name
    public class FirstLetterConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is string str && !string.IsNullOrEmpty(str))
            {
                return str.Substring(0, 1).ToUpper();
            }
            return "?";
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }

    // Converter for string to visibility (for placeholder text)
    public class StringToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            return string.IsNullOrEmpty(value as string) ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }

    // Converter for bool to visibility
    public class BoolToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is bool boolValue)
            {
                return boolValue ? Visibility.Visible : Visibility.Collapsed;
            }
            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }

    // Converter for inverse bool to visibility
    public class InverseBoolToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is bool boolValue)
            {
                return boolValue ? Visibility.Collapsed : Visibility.Visible;
            }
            return Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
