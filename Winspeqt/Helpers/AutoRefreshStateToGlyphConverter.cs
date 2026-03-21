using Microsoft.UI.Xaml.Data;
using System;

namespace Winspeqt.Helpers
{
    public sealed class AutoRefreshStateToGlyphConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            var isAutoRefreshEnabled = value is bool enabled && enabled;

            return isAutoRefreshEnabled ? "\uE769" : "\uE768";
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
            => throw new NotSupportedException();
    }
}
