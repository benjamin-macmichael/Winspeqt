using Microsoft.UI.Xaml.Data;
using System;

namespace Winspeqt.Helpers
{
    public sealed class AutoRefreshStateToTextConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            var isAutoRefreshEnabled = value is bool enabled && enabled;

            return isAutoRefreshEnabled ? "Pause" : "Resume";
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
            => throw new NotSupportedException();
    }
}
