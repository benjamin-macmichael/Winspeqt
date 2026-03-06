using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using System;

namespace Winspeqt.Helpers
{
    public class CollectionCountToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            // The binding passes Count (int) when using x:Bind Collection.Count
            if (value is int count)
                return count > 0 ? Visibility.Visible : Visibility.Collapsed;

            // Fallback: handle any ICollection directly
            if (value is System.Collections.ICollection collection)
                return collection.Count > 0 ? Visibility.Visible : Visibility.Collapsed;

            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            return value is Visibility v && v == Visibility.Visible;
        }
    }
}