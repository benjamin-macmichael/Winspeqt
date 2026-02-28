using System;
using System.Collections.ObjectModel;
using Winspeqt.Helpers;
using static Winspeqt.Models.Enums;

namespace Winspeqt.Models
{
    /// <summary>
    /// Represents a file or folder entry displayed in the Large File Finder list.
    /// </summary>
    public class FileSearchItem : ObservableObject
    {
        /// <summary>
        /// Display name of the file or folder.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Full file system path (folders only). Files may use an empty string.
        /// </summary>
        public string FilePath { get; }
        /// <summary>
        /// Item type label used by the UI ("file" or "folder").
        /// </summary>
        public string Type { get; }
        private int _size;
        /// <summary>
        /// Size value normalized to the associated <see cref="DataLabel"/>.
        /// </summary>
        public int Size
        {
            get => _size;
            set => SetProperty(ref _size, value);
        }

        private DataSize _dataLabel;
        /// <summary>
        /// Unit label that matches <see cref="Size"/>.
        /// </summary>
        public DataSize DataLabel
        {
            get => _dataLabel;
            private set => SetProperty(ref _dataLabel, value);
        }

        private bool _finished;
        /// <summary>
        /// Whether size calculation has finished (used to show/hide progress UI).
        /// </summary>
        public bool Finished
        {
            get => _finished;
            private set => SetProperty(ref _finished, value);
        }

        private ObservableCollection<FileSearchItem> _children = [];

        public ObservableCollection<FileSearchItem> Children
        {
            get => _children;
            set => SetProperty(ref _children, value);
        }

        private FileSearchItem? _parent;

        public FileSearchItem? Parent
        {
            get => _parent;
            set => SetProperty(ref _parent, value);
        }

        /// <summary>
        /// Initializes a new item and derives the display size from the raw byte count.
        /// </summary>
        public FileSearchItem(string name, string path, string type, long size, FileSearchItem? parent, bool finished)
        {
            Name = name;
            FilePath = path;
            Type = type;
            UpdateSize(size);
            Parent = parent;
            Finished = finished;
        }

        /// <summary>
        /// Updates the size and marks the item as finished.
        /// </summary>
        public void UpdateSize(long size)
        {
            var result = ReduceSize(size);
            Size = result.size;
            DataLabel = result.label;
            Finished = true;
        }

        /// <summary>
        /// Reduces a byte count into a (value, unit) pair for display.
        /// </summary>
        private static (int size, DataSize label) ReduceSize(long size)
        {
            int iterations = 0;
            while (size >= 1024)
            {
                // Integer division intentionally truncates to keep the UI compact.
                size = size / 1024;
                iterations++;
            }
            return (Convert.ToInt32(size), (DataSize)iterations);
        }
    }
}
