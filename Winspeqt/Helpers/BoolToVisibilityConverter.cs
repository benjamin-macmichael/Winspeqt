using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using System;

namespace Winspeqt.Helpers
{
    public partial class BoolToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            var b = value is bool boolean && boolean;
            if (parameter is string s && s.Equals("Invert", StringComparison.OrdinalIgnoreCase))
                b = !b;
            return b ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            var visible = value is Visibility v && v == Visibility.Visible;
            if (parameter is string s && s.Equals("Invert", StringComparison.OrdinalIgnoreCase))
                visible = !visible;
            return visible;
        }
    }
}