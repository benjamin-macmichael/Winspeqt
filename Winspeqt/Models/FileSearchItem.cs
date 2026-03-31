using Microsoft.UI.Xaml.Media;
using System.Collections.ObjectModel;
using System.Linq;
using Windows.UI;
using Winspeqt.Helpers;
using static Winspeqt.Models.Enums;

namespace Winspeqt.Models
{
    /// <summary>
    /// Represents a file or folder entry displayed in the Large File Finder list.
    /// </summary>
    public partial class FileSearchItem : ObservableObject
    {
        /// <summary>
        /// Display name of the file or folder.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// File system path for this item. Current callers store folder paths and may leave file paths empty.
        /// </summary>
        public string FilePath { get; }
        /// <summary>
        /// Item type label used by the UI ("file" or "folder").
        /// </summary>
        public string Type { get; }
        private long _size;
        /// <summary>
        /// Reduced numeric size for display, normalized to <see cref="DataLabel"/>.
        /// </summary>
        public string Size
        {
            get {
                var (size, label) = DataSizeConverter.ReduceSize(ByteSize);
                DataLabel = label;
                return $"{size} {label}";
            }
        }

        /// <summary>
        /// Raw item size in bytes used for sorting and calculations.
        /// </summary>
        public long ByteSize
        {
            get {
                if (Type == "file")
                {
                    return _size;
                } else if (Children != null && Children.Count > 0) 
                {
                    long test = Children.Sum(child => child.ByteSize);
                    return test;
                } else
                {
                    return 0;
                }
            }
            set
            {
                SetProperty(ref _size, value);
                OnPropertyChanged(nameof(Size));
                OnPropertyChanged(nameof(AnnouncementTextColor));
            }
        }

        private DataSize _dataLabel = DataSize.B;

        /// <summary>
        /// Display unit that matches <see cref="Size"/> (B, KB, MB, GB, TB).
        /// </summary>
        public DataSize DataLabel
        {
            get => _dataLabel;
            private set { 
                SetProperty(ref _dataLabel, value);
                OnPropertyChanged(nameof(AnnouncementTextColor));
            }
        }

        private bool _finished;
        /// <summary>
        /// Whether size calculation has finished (used to show/hide progress UI).
        /// </summary>
        public bool Finished
        {
            get => _finished;
            set
            {
                SetProperty(ref _finished, value);
                OnPropertyChanged(nameof(AnnouncementTextColor));
                OnPropertyChanged(nameof(Size));
            }
        }

        /// <summary>
        /// Text/icon color used by the list row to communicate relative size severity.
        /// </summary>
        public SolidColorBrush AnnouncementTextColor
        {
            get
            {
                if (_finished)
                {
                    //Smaller sizes are green, medium sizes move toward yellow, and large sizes are red.
                    return DataLabel switch
                    {
                        DataSize.B => new SolidColorBrush(Color.FromArgb(255, 26, 163, 54)),
                        DataSize.KB => new SolidColorBrush(Color.FromArgb(255, 21, 176, 52)),
                        DataSize.MB => new SolidColorBrush(Color.FromArgb(255, 232, 201, 28)),
                        DataSize.GB => new SolidColorBrush(Color.FromArgb(255, 214, 9, 9)),
                        DataSize.TB => new SolidColorBrush(Color.FromArgb(255, 148, 9, 9)),
                        _ => new SolidColorBrush(Color.FromArgb(200, 25, 25, 25))
                    };
                }
                return new SolidColorBrush(Color.FromArgb(200, 25, 25, 25));
            }
        }

        private ObservableCollection<FileSearchItem> _children = [];

        /// <summary>
        /// Child entries for this folder node. Empty for file nodes.
        /// </summary>
        public ObservableCollection<FileSearchItem> Children
        {
            get => _children;
            set => SetProperty(ref _children, value);
        }

        private FileSearchItem? _parent;

        /// <summary>
        /// Parent folder node in the navigation hierarchy, if known.
        /// </summary>
        public FileSearchItem? Parent
        {
            get => _parent;
            set => SetProperty(ref _parent, value);
        }

        /// <summary>
        /// Initializes a new item and derives the display size from the raw byte count.
        /// </summary>
        /// <param name="name">Display name shown in the list.</param>
        /// <param name="path">File system path for the item (currently used primarily for folders).</param>
        /// <param name="type">UI type discriminator such as "file" or "folder".</param>
        /// <param name="size">Initial raw size in bytes.</param>
        /// <param name="parent">Parent folder node, or <see langword="null"/> for roots.</param>
        /// <param name="finished">Initial completion state for size calculation.</param>
        public FileSearchItem(string name, string path, string type, long size, FileSearchItem? parent, bool finished)
        {
            Name = name;
            FilePath = path;
            Type = type;
            ByteSize = size;
            Parent = parent;
            Finished = finished;
        }
    }
}
