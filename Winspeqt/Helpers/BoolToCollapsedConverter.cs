using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using System;

namespace Winspeqt.Helpers
{
    /// <summary>
    /// Converts a boolean into a <see cref="Visibility"/> value, collapsing on <c>true</c>.
    /// </summary>
    public partial class BoolToCollapsedConverter : IValueConverter
    {
        /// <summary>
        /// Returns <see cref="Visibility.Collapsed"/> for <c>true</c>, otherwise <see cref="Visibility.Visible"/>.
        /// </summary>
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            return (value is bool b && b) ? Visibility.Collapsed : Visibility.Visible;
        }

        /// <summary>
        /// Returns <c>true</c> when <paramref name="value"/> is <see cref="Visibility.Collapsed"/>.
        /// </summary>
        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            return value is Visibility v && v == Visibility.Collapsed;
        }
    }
}
