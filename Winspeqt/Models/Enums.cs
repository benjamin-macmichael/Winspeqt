using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Winspeqt.Models
{
    /// <summary>
    /// Shared enums used across models and view models.
    /// </summary>
    public class Enums
    {
        /// <summary>
        /// File size units used for display and color-coding.
        /// </summary>
        public enum DataSize
        {
            /// <summary>Bytes.</summary>
            B,
            /// <summary>Kilobytes.</summary>
            KB,
            /// <summary>Megabytes.</summary>
            MB,
            /// <summary>Gigabytes.</summary>
            GB,
            /// <summary>Terabytes.</summary>
            TB,
        }
    }
}
