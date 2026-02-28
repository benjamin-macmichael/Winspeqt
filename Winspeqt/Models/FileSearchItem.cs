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
        /// <param name="path">File system path used for navigation or lookup.</param>
        /// <param name="type">UI type discriminator such as "file" or "folder".</param>
        /// <param name="size">Initial raw size in bytes.</param>
        /// <param name="parent">Parent folder node, or <see langword="null"/> for roots.</param>
        /// <param name="finished">Initial completion state for size calculation.</param>
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
        /// <param name="size">Raw size in bytes.</param>
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
        /// <param name="size">Raw byte value.</param>
        /// <returns>Tuple containing the normalized numeric value and its unit label.</returns>
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
