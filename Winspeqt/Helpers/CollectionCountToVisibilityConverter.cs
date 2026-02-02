using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using System;

namespace Winspeqt.Helpers
{
    public class CollectionCountToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            return Visibility.Visible; //(value is ObservableCollection<object> b && b.Count > 0) ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            return value is Visibility v && v == Visibility.Visible;
        }
    }
}