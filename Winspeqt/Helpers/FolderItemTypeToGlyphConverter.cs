using Microsoft.UI.Xaml.Data;
using System;

namespace Winspeqt.Helpers
{
    /// <summary>
    /// Converts a folder item type string (file/folder) into a Segoe MDL2 glyph.
    /// </summary>
    public sealed class FolderItemTypeToGlyphConverter : IValueConverter
    {
        /// <summary>
        /// Converts a type string to a glyph used by <see cref="FontIcon"/>.
        /// </summary>
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

        /// <summary>
        /// Reverse conversion is not supported.
        /// </summary>
        public object ConvertBack(object value, Type targetType, object parameter, string language)
            => throw new NotSupportedException();
    }
}
