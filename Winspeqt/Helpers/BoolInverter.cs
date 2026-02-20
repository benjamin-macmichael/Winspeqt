using Microsoft.UI.Xaml.Data;
using System;

namespace Winspeqt.Helpers
{
    /// <summary>
    /// Inverts a boolean value for XAML bindings.
    /// </summary>
    public class BoolInverter : IValueConverter
    {
        /// <summary>
        /// Returns <c>true</c> when <paramref name="value"/> is <c>false</c>.
        /// </summary>
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            return value is bool b && !b;
        }

        /// <summary>
        /// Returns <c>true</c> when <paramref name="value"/> is <c>false</c>.
        /// </summary>
        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            return value is bool b && !b;
        }
    }
}
