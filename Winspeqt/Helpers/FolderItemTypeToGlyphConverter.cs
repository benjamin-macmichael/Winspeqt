using Microsoft.UI.Xaml.Data;
using System;
using Windows.UI.Xaml.Data;

namespace Winspeqt.Helpers
{
    public sealed class FolderItemTypeToGlyphConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            var type = value?.ToString()?.Trim().ToLowerInvariant();

            return type switch
            {
                "file" => "\uE7C3",
                "folder" => "\uF12B",
                _ => "\uF142"
            };
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
            => throw new NotSupportedException();
    }
}
