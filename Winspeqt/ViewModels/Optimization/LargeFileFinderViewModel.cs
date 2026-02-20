using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Shapes;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Winspeqt.Helpers;
using Winspeqt.Models;
using static Winspeqt.Models.Enums;

namespace Winspeqt.ViewModels.Optimization
{
    public class LargeFileFinderViewModel : ObservableObject
    {
        private readonly DispatcherQueue _dispatcher = DispatcherQueue.GetForCurrentThread();

        private ObservableCollection<FileSearchItem> _folderItems = new();
        public ObservableCollection<FileSearchItem> FolderItems 
        {
            get => _folderItems;
            set => SetProperty(ref _folderItems, value);
        }

        private ObservableCollection<PathItem> _pathItems = new();
        public ObservableCollection<PathItem> PathItems
        {
            get => _pathItems;
            set => SetProperty(ref _pathItems, value);
        }

        private bool _isLoading;
        public bool IsLoading
        {
            get => _isLoading;
            set
            {
                if (SetProperty(ref _isLoading, value))
                {
                    OnPropertyChanged(nameof(FolderItemOpacity));
                }
            }
        }

        public double FolderItemOpacity => IsLoading ? 0.5 : 1.0;

        private bool _hasError;
        public bool HasError
        {
            get => _hasError;
            set => SetProperty(ref _hasError, value);
        }

        private string _errorMessage = string.Empty;
        public string ErrorMessage
        {
            get => _errorMessage;
            set => SetProperty(ref _errorMessage, value);
        }

        private string _selectedSortOption = "Default";
        public string SelectedSortOption
        {
            get => _selectedSortOption;
            set
            {
                if (SetProperty(ref _selectedSortOption, value))
                {
                    SortFiles();
                }
            }
        }

        public ObservableCollection<string> SortOptions { get; set; }

        public LargeFileFinderViewModel()
        {
            FolderItems = [];
            PathItems = [];
            SortOptions = new ObservableCollection<string> { "Default", "Name", "Size" };
        }

        public async Task LoadAsync()
        {
            IsLoading = true;
            HasError = false;
            ErrorMessage = string.Empty;

            string initialFolder = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

            if (string.IsNullOrWhiteSpace(initialFolder))
            {
                IsLoading = false;
                HasError = true;
                ErrorMessage = "There was a problem calculating storage. If this problem persists, please contact winspeqtsupport@byu.onmicrosoft.com.";
                return;
            }

            DirectoryInfo? parentDirectory = Directory.GetParent(initialFolder);
            List<DirectoryInfo> systemDirectories = new List<DirectoryInfo>();

            while (parentDirectory != null)
            {
                systemDirectories.Add(parentDirectory);
                parentDirectory = Directory.GetParent(parentDirectory.FullName);
                
            }

            for (int i = systemDirectories.Count - 1; i >= 0; i--)
            {
                PathItems.Add(new PathItem(systemDirectories[i].ToString(), PathItems.Count));
            }

            await RetrieveFolderItems(initialFolder);
        }

        public async Task RetrieveFolderItems(string folder)
        {
            IsLoading = true;

            PathItems.Add(new PathItem(folder, PathItems.Count));

            FolderItems.Clear();
            var sizeTasks = new System.Collections.Concurrent.ConcurrentBag<Task>();
            await foreach (var item in EnumerateFolderItemsAsync(folder, sizeTasks))
            {
                FolderItems.Add(item);
            }

            SortFiles();
            IsLoading = false;

            var sizeTaskArray = sizeTasks.ToArray();
            if (SelectedSortOption == "Size" && sizeTaskArray.Length > 0)
            {
                _ = Task.WhenAll(sizeTaskArray).ContinueWith(_ =>
                {
                    _dispatcher.TryEnqueue(SortFiles);
                }, TaskScheduler.Default);
            }
        }

        private async IAsyncEnumerable<FileSearchItem> EnumerateFolderItemsAsync(string folder, System.Collections.Concurrent.ConcurrentBag<Task> sizeTasks)
        {
            var channel = Channel.CreateUnbounded<FileSearchItem>();

            _ = Task.Run(() =>
            {
                try
                {
                    foreach (var dir in Directory.EnumerateDirectories(folder))
                    {
                        var name = System.IO.Path.GetFileName(dir);
                        var item = new FileSearchItem(name, dir, "folder", 0, false);
                        channel.Writer.TryWrite(item);
                        sizeTasks.Add(UpdateFolderSizeAsync(item, dir));
                    }

                    foreach (var file in Directory.EnumerateFiles(folder))
                    {
                        var name = System.IO.Path.GetFileName(file);
                        var size = new System.IO.FileInfo(file).Length;
                        channel.Writer.TryWrite(new FileSearchItem(name, "", "file", size, true));
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.Print("Error retrieving large files for path {0}: {1}", folder, ex.ToString());
                }
                finally
                {
                    channel.Writer.Complete();
                }
            });

            await foreach (var item in channel.Reader.ReadAllAsync())
            {
                yield return item;
            }
        }

        private async Task UpdateFolderSizeAsync(FileSearchItem item, string path)
        {
            try
            {
                var size = await Task.Run(() => GetDirectorySize(path));
                _dispatcher.TryEnqueue(() => item.UpdateSize(size));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.Print("Error calculating directory size for path {0}: {1}", path, ex.ToString());
            }
        }

        private static long GetDirectorySize(string folder)
        {
            long size = 0;
            var directories = new Stack<string>();
            directories.Push(folder);

            while (directories.Count > 0)
            {
                var current = directories.Pop();

                try
                {
                    foreach (var file in Directory.EnumerateFiles(current))
                    {
                        try
                        {
                            size += new FileInfo(file).Length;
                        }
                        catch
                        {
                            System.Diagnostics.Debug.Print("Failed to get size info for: " + file);
                        }
                    }
                }
                catch
                {
                }

                try
                {
                    foreach (var dir in Directory.EnumerateDirectories(current))
                    {
                        directories.Push(dir);
                    }
                }
                catch
                {
                }
            }

            return size;
        }

        public void ResetBreadCrumb(int index)
        {
            IEnumerable<PathItem> test = PathItems.Take(index + 1);
            PathItems = new ObservableCollection<PathItem>(test);
        }

        public void SortFiles()
        {
            IEnumerable<FileSearchItem> listItems = FolderItems.Cast<FileSearchItem>();

            if (this.SelectedSortOption == "Name")
            {
                listItems = listItems.OrderBy(item => item.Name);
            } 
            else if (this.SelectedSortOption == "Size")
            {
                listItems = listItems.OrderByDescending(item => item.Size).OrderByDescending(item => item.DataLabel);
            } 
            else
            {
                listItems = listItems.OrderBy(item => item.Name).OrderByDescending(item => item.Type);
            }

            FolderItems = new ObservableCollection<FileSearchItem>(listItems);
        }
    }
}

