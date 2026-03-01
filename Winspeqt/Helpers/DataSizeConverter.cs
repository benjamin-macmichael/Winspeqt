using System;

namespace Winspeqt.Helpers
{
    internal static class DataSizeConverter
    {
        /// <summary>
        /// Reduces a byte count into a (value, unit) pair for display.
        /// </summary>
        /// <param name="size">Raw byte value.</param>
        /// <returns>Tuple containing the normalized numeric value and its unit label.</returns>
        public static (int size, Models.Enums.DataSize label) ReduceSize(long size)
        {
            int iterations = 0;
            while (size >= 1024)
            {
                // Integer division intentionally truncates to keep the UI compact.
                size = size / 1024;
                iterations++;
            }
            return (Convert.ToInt32(size), (Models.Enums.DataSize)iterations);
        }
    }
}
