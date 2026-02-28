using Microsoft.UI.Dispatching;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Channels;
using System.Threading.Tasks;
using Winspeqt.Helpers;
using Winspeqt.Models;

namespace Winspeqt.ViewModels.Optimization
{
    /// <summary>
    /// View model for the Large File Finder page, including folder navigation and size calculation.
    /// </summary>
    public class LargeFileFinderViewModel : ObservableObject
    {
        // Dispatcher captured for UI-thread updates (e.g., size calculation completion).
        private readonly DispatcherQueue _dispatcher = DispatcherQueue.GetForCurrentThread();

        private FileSearchItem _activeNode = new("", "", "", 0, null, true);
        /// <summary>
        /// Current folder contents displayed in the list.
        /// </summary>
        public FileSearchItem ActiveNode
        {
            get => _activeNode;
            set => SetProperty(ref _activeNode, value);
        }

        private ObservableCollection<PathItem> _pathItems = [];
        /// <summary>
        /// Breadcrumb path items from root to current folder.
        /// </summary>
        public ObservableCollection<PathItem> PathItems
        {
            get => _pathItems;
            set => SetProperty(ref _pathItems, value);
        }

        private bool _isLoading;
        /// <summary>
        /// Indicates whether the view is loading or recalculating folder sizes.
        /// </summary>
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

        /// <summary>
        /// Opacity for list items while loading, to signal background work.
        /// </summary>
        public double FolderItemOpacity => IsLoading ? 0.5 : 1.0;

        private bool _hasError;
        /// <summary>
        /// True when the view model encountered an error.
        /// </summary>
        public bool HasError
        {
            get => _hasError;
            set => SetProperty(ref _hasError, value);
        }

        private string _errorMessage = string.Empty;
        /// <summary>
        /// User-facing error message shown when <see cref="HasError"/> is true.
        /// </summary>
        public string ErrorMessage
        {
            get => _errorMessage;
            set => SetProperty(ref _errorMessage, value);
        }

        private string _selectedSortOption = "Default";
        /// <summary>
        /// Current sort option ("Default", "Name", or "Size").
        /// </summary>
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

        /// <summary>
        /// List of available sort options for the UI.
        /// </summary>
        public ObservableCollection<string> SortOptions { get; set; } = ["Default", "Name", "Size"];

        /// <summary>
        /// Initializes a new view model with default collections and sort options.
        /// </summary>
        public LargeFileFinderViewModel()
        {
            string userFolder = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (string.IsNullOrWhiteSpace(userFolder))
            {
                IsLoading = false;
                HasError = true;
                ErrorMessage = "There was a problem calculating storage. If this problem persists, please contact winspeqtsupport@byu.onmicrosoft.com.";
                return;
            }

            PathItems = [];
        }

        /// <summary>
        /// Loads the initial folder (user profile) and builds the breadcrumb path.
        /// </summary>
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

            ActiveNode = new(System.IO.Path.GetFileName(initialFolder), initialFolder, "folder", 0, null, false);

            DirectoryInfo? parentDirectory = Directory.GetParent(initialFolder);
            List<DirectoryInfo> systemDirectories = [];
            List<FileSearchItem> ancestrialFolders = [];

            while (parentDirectory != null)
            {
                systemDirectories.Add(parentDirectory);
                parentDirectory = Directory.GetParent(parentDirectory.FullName);
            }

            // Build breadcrumb from root to the user profile directory.
            for (int i = systemDirectories.Count - 1; i >= 0; i--)
            {
                string path = systemDirectories[i].ToString();
                FileSearchItem? daddy;
                if (ancestrialFolders.Count > 0)
                {
                    daddy = ancestrialFolders[ancestrialFolders.Count - 1];
                }
                else
                {
                    daddy = null;
                }

                var name = System.IO.Path.GetFileName(path);
                var item = new FileSearchItem(name, path, "folder", 0, daddy, false);
                ancestrialFolders.Add(item);
                PathItems.Add(new PathItem(path, PathItems.Count));
            }

            ActiveNode.Parent = ancestrialFolders[ancestrialFolders.Count - 1];
            await RetrieveFolderItems(ActiveNode);
        }

        public async Task ChangeActiveNode(FileSearchItem newNode)
        {
            ActiveNode = newNode;
            await RetrieveFolderItems(ActiveNode);
            IsLoading = false;
        }

        /// <summary>
        /// Retrieves and displays the contents of <paramref name="folder"/>.
        /// </summary>
        public async Task RetrieveFolderItems(FileSearchItem folder)
        {
            IsLoading = true;

            PathItems.Add(new PathItem(folder.FilePath, PathItems.Count));

            if (folder.Children.Count > 0)
            {
                return;
            }

            var sizeTasks = new System.Collections.Concurrent.ConcurrentBag<Task>();
            await foreach (var item in EnumerateFolderItemsAsync(folder, sizeTasks))
            {
                folder.Children.Add(item);
            }

            SortFiles();
            IsLoading = false;

            var sizeTaskArray = sizeTasks.ToArray();
            if (SelectedSortOption == "Size" && sizeTaskArray.Length > 0)
            {
                // Re-sort once background folder size calculations complete.
                _ = Task.WhenAll(sizeTaskArray).ContinueWith(_ =>
                {
                    _dispatcher.TryEnqueue(SortFiles);
                }, TaskScheduler.Default);
            }
        }

        /// <summary>
        /// Enumerates files and directories asynchronously while starting folder size calculations.
        /// </summary>
        private async IAsyncEnumerable<FileSearchItem> EnumerateFolderItemsAsync(FileSearchItem folder, System.Collections.Concurrent.ConcurrentBag<Task> sizeTasks)
        {
            var channel = Channel.CreateUnbounded<FileSearchItem>();

            _ = Task.Run(() =>
            {
                try
                {
                    foreach (var dir in Directory.EnumerateDirectories(folder.FilePath))
                    {
                        var name = System.IO.Path.GetFileName(dir);
                        var item = new FileSearchItem(name, dir, "folder", 0, folder, false);
                        channel.Writer.TryWrite(item);
                        sizeTasks.Add(UpdateFolderSizeAsync(item, dir));
                    }

                    foreach (var file in Directory.EnumerateFiles(folder.FilePath))
                    {
                        var name = System.IO.Path.GetFileName(file);
                        var size = new System.IO.FileInfo(file).Length;
                        channel.Writer.TryWrite(new FileSearchItem(name, "", "file", size, folder, true));
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

        /// <summary>
        /// Calculates folder size in the background and updates the item on the UI thread.
        /// </summary>
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

        /// <summary>
        /// Computes total size of all files under <paramref name="folder"/>, ignoring access failures.
        /// </summary>
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
                    // Access denied or transient IO error; skip files in this directory.
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
                    // Access denied or transient IO error; skip subdirectories.
                }
            }

            return size;
        }

        /// <summary>
        /// Truncates breadcrumb items to the specified index.
        /// </summary>
        public void ResetBreadCrumb(int index)
        {
            IEnumerable<PathItem> test = PathItems.Take(index + 1);
            PathItems = new ObservableCollection<PathItem>(test);
        }

        /// <summary>
        /// Sorts <see cref="FolderItems"/> based on <see cref="SelectedSortOption"/>.
        /// </summary>
        public void SortFiles()
        {
            IEnumerable<FileSearchItem> listItems = ActiveNode.Children.Cast<FileSearchItem>();

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

            ActiveNode.Children = new ObservableCollection<FileSearchItem>(listItems);
        }
    }
}

