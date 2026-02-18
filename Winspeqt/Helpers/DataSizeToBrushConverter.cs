using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using System;
using Windows.UI;
using Winspeqt.Models;

namespace Winspeqt.Helpers
{
    public sealed class DataSizeToBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            var size = value is Enums.DataSize dataSize
                ? dataSize
                : Enum.TryParse(value?.ToString(), out Enums.DataSize parsed)
                    ? parsed
                    : Enums.DataSize.B;

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

        public object ConvertBack(object value, Type targetType, object parameter, string language)
            => throw new NotSupportedException();
    }
}
