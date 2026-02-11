using Microsoft.UI;
using Microsoft.UI.Xaml.Media;
using System;
using System.CodeDom;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Winspeqt.Models
{
    public class FileSearchItem
    {
        public string Name { get; }
        public string FilePath { get; }
        public string Type { get; }
        public int Size { get; }
        public DataSize DataLabel { get; private set; }

        public Brush WarningColor { get; }

        public ObservableCollection<FileSearchItem>? SubDirectories { get; set; }

        // For easy reference to data size. If we get bigger than this, why are they using our program?
        public enum DataSize
        {
            B,
            KB,
            MB,
            GB,
            TB,
        }

        public FileSearchItem (string name, string path, string type, long size, ObservableCollection<FileSearchItem>? subdirectories)
        {
            Name = name;
            FilePath = path;
            Type = type;
            Size = ReduceSize(size);
            SubDirectories = subdirectories;
            WarningColor = GetBrush(this.DataLabel);
        }

        private int ReduceSize(long size)
        {
            int iterations = 0;
            while (size >= 1024)
            {
                size = size / 1024;
                iterations++;
            }
            DataLabel = (DataSize)iterations;
            return Convert.ToInt32(size);
        }

        SolidColorBrush GetBrush(DataSize size)
        {
            return size switch
            {
                DataSize.B => new SolidColorBrush(Windows.UI.Color.FromArgb(255, 0, 255, 0)),
                DataSize.KB => new SolidColorBrush(Windows.UI.Color.FromArgb(255, 0, 0, 255)),
                DataSize.MB => new SolidColorBrush(Windows.UI.Color.FromArgb(255, 0, 255, 0)),
                DataSize.GB => new SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 0, 0)),
                DataSize.TB => new SolidColorBrush(Windows.UI.Color.FromArgb(255, 0, 255, 0)),
                _ => new SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 255, 255))
            };
        }
    }
}
