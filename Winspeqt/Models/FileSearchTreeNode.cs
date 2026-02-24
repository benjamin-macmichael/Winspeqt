using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Winspeqt.Helpers;
using static Winspeqt.Models.Enums;

namespace Winspeqt.Models
{
    /// <summary>
    /// Initializes a new item and derives the display size from the raw byte count.
    /// </summary>
    public class FileSearchTreeNode(string name, string path, string type, long size, FileSearchTreeNode? parent = null) : ObservableObject
    {
        /// <summary>
        /// Display name of the file or folder.
        /// </summary>
        public string Name { get; } = name;

        /// <summary>
        /// Full file system path (folders only). Files may use an empty string.
        /// </summary>
        public string FilePath { get; } = path;
        /// <summary>
        /// Item type label used by the UI ("file" or "folder").
        /// </summary>
        public string Type { get; } = type;

        private long _size = size;
        /// <summary>
        /// Size value normalized to the associated <see cref="DataLabel"/>.
        /// </summary>
        public long Size
        {
            get => ReduceSize(_size).size;
            set => SetProperty(ref _size, value);
        }
        /// <summary>
        /// Unit label that matches <see cref="Size"/>.
        /// </summary>
        public DataSize DataLabel
        {
            get => ReduceSize(_size).label;
        }

        private bool _finished = type == "file";
        /// <summary>
        /// Whether size calculation has finished (used to show/hide progress UI).
        /// </summary>
        public bool Finished
        {
            get => _finished;
            set => SetProperty(ref _finished, value);
        }

        private ObservableCollection<FileSearchTreeNode>? _children;
        public ObservableCollection<FileSearchTreeNode>? Children
        {
            get => _children;
            set => SetProperty(ref _children, value);
        }

        private readonly FileSearchTreeNode? _parent = parent;
        public FileSearchTreeNode? Parent
        {
            get => _parent;
        }

        public void AddChild(FileSearchTreeNode child)
        {
            if (Type == "folder")
            {
                if (Children == null)
                {
                    Children = [];
                }

                Children.Add(child);
                Size += child.Size;
            }
        }

        /// <summary>
        /// Reduces a byte count into a (value, unit) pair for display.
        /// </summary>
        private (int size, DataSize label) ReduceSize(long size)
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
