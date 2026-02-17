using System;
using System.Collections.ObjectModel;
using System.Reflection;
using static Winspeqt.Models.Enums;

namespace Winspeqt.Models
{
    public class FileSearchItem
    {
        public string Name { get; }
        
        public string FilePath { get; }
        public string Type { get; }
        public int Size { get; }
        public DataSize DataLabel { get; private set; }

        public ObservableCollection<FileSearchItem>? SubDirectories { get; set; }

        public FileSearchItem(string name, string path, string type, long size, ObservableCollection<FileSearchItem>? subdirectories)
        {
            Name = name;
            FilePath = path;
            Type = type;
            Size = ReduceSize(size);
            SubDirectories = subdirectories;
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
    }
}
