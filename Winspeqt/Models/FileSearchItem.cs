using System;
using System.Collections.ObjectModel;
using Winspeqt.Helpers;
using static Winspeqt.Models.Enums;

namespace Winspeqt.Models
{
    public class FileSearchItem : ObservableObject
    {
        public string Name { get; }
        
        public string FilePath { get; }
        public string Type { get; }
        private int _size;
        public int Size
        {
            get => _size;
            set => SetProperty(ref _size, value);
        }

        private DataSize _dataLabel;
        public DataSize DataLabel
        {
            get => _dataLabel;
            private set => SetProperty(ref _dataLabel, value);
        }

        private bool _finished;
        public bool Finished 
        { 
            get => _finished; 
            private set => SetProperty(ref _finished, value); 
        }

        public FileSearchItem(string name, string path, string type, long size, bool finished)
        {
            Name = name;
            FilePath = path;
            Type = type;
            UpdateSize(size);
            Finished = finished;
        }

        public void UpdateSize(long size)
        {
            var result = ReduceSize(size);
            Size = result.size;
            DataLabel = result.label;
            Finished = true;
        }

        private static (int size, DataSize label) ReduceSize(long size)
        {
            int iterations = 0;
            while (size >= 1024)
            {
                size = size / 1024;
                iterations++;
            }
            return (Convert.ToInt32(size), (DataSize)iterations);
        }
    }
}
