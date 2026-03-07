using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Winspeqt.Models
{
    /// <summary>
    /// Represents a potentially orphaned AppData folder with no matching installed application.
    /// </summary>
    public class OrphanedAppDataEntry : INotifyPropertyChanged
    {
        private bool _isSelected;

        /// <summary>Full path to the AppData folder.</summary>
        public string FolderPath { get; init; } = string.Empty;

        /// <summary>Display name of the folder (last path segment).</summary>
        public string FolderName { get; init; } = string.Empty;

        /// <summary>Which AppData location this came from (Roaming, Local, LocalLow).</summary>
        public AppDataLocation Location { get; init; }

        /// <summary>Human-readable label for the location.</summary>
        public string LocationLabel => Location switch
        {
            AppDataLocation.Roaming => "AppData\\Roaming",
            AppDataLocation.Local => "AppData\\Local",
            AppDataLocation.LocalLow => "AppData\\LocalLow",
            _ => "Unknown"
        };

        /// <summary>Total size of the folder and all its contents, in bytes.</summary>
        public long SizeBytes { get; init; }

        /// <summary>Human-readable size string (e.g. "14.2 MB").</summary>
        public string SizeDisplay => FormatBytes(SizeBytes);

        /// <summary>Date the folder was last written to.</summary>
        public DateTime LastModified { get; init; }

        /// <summary>How long ago the folder was last modified.</summary>
        public string LastModifiedDisplay
        {
            get
            {
                var age = DateTime.Now - LastModified;
                if (age.TotalDays >= 365)
                    return $"{(int)(age.TotalDays / 365)}y ago";
                if (age.TotalDays >= 30)
                    return $"{(int)(age.TotalDays / 30)}mo ago";
                if (age.TotalDays >= 1)
                    return $"{(int)age.TotalDays}d ago";
                return "Today";
            }
        }

        /// <summary>Whether the user has selected this entry for deletion.</summary>
        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected != value)
                {
                    _isSelected = value;
                    OnPropertyChanged();
                }
            }
        }

        // ── INotifyPropertyChanged ──────────────────────────────────────────

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        // ── Helpers ─────────────────────────────────────────────────────────

        private static string FormatBytes(long bytes)
        {
            if (bytes >= 1_073_741_824) return $"{bytes / 1_073_741_824.0:F1} GB";
            if (bytes >= 1_048_576) return $"{bytes / 1_048_576.0:F1} MB";
            if (bytes >= 1_024) return $"{bytes / 1_024.0:F1} KB";
            return $"{bytes} B";
        }
    }

    public enum AppDataLocation
    {
        Roaming,
        Local,
        LocalLow
    }
}
