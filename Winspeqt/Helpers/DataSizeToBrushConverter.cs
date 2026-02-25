using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using System;
using Windows.UI;
using Winspeqt.Models;

namespace Winspeqt.Helpers
{
    /// <summary>
    /// Maps a <see cref="Enums.DataSize"/> value to a color used in the UI.
    /// </summary>
    public sealed class DataSizeToBrushConverter : IValueConverter
    {
        /// <summary>
        /// Converts a <see cref="Enums.DataSize"/> (or a parsable string) to a <see cref="SolidColorBrush"/>.
        /// </summary>
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            var size = value is Enums.DataSize dataSize
                ? dataSize
                : Enum.TryParse(value?.ToString(), out Enums.DataSize parsed)
                    ? parsed
                    : Enums.DataSize.B;

            // Smaller sizes are green, medium sizes move toward yellow, and large sizes are red.
            return size switch
            {
                Enums.DataSize.B => new SolidColorBrush(Color.FromArgb(255, 26, 163, 54)),
                Enums.DataSize.KB => new SolidColorBrush(Color.FromArgb(255, 21, 176, 52)),
                Enums.DataSize.MB => new SolidColorBrush(Color.FromArgb(255, 232, 201, 28)),
                Enums.DataSize.GB => new SolidColorBrush(Color.FromArgb(255, 214, 9, 9)),
                Enums.DataSize.TB => new SolidColorBrush(Color.FromArgb(255, 148, 9, 9)),
                _ => new SolidColorBrush(Color.FromArgb(255, 255, 255, 255))
            };
        }

        /// <summary>
        /// Reverse conversion is not supported.
        /// </summary>
        public object ConvertBack(object value, Type targetType, object parameter, string language)
            => throw new NotSupportedException();
    }
}
