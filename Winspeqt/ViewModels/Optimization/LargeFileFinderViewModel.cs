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
        /// Current folder node being viewed; its <see cref="FileSearchItem.Children"/> are displayed in the list.
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
        public ObservableCollection<string> SortOptions { get; } = ["Default", "Name", "Size"];


        private long _totalHardDrive;
        /// <summary>
        /// Total drive capacity reduced for display.
        /// </summary>
        public long TotalHardDrive
        {
            get => DataSizeConverter.ReduceSize(_totalHardDrive).size;
            set
            {
                if (SetProperty(ref _totalHardDrive, value))
                {
                    OnPropertyChanged(nameof(TotalDriveLabel));
                    OnPropertyChanged(nameof(UsedHardDrive));
                    OnPropertyChanged(nameof(UsedDriveLabel));
                    OnPropertyChanged(nameof(DriveUsageText));
                    OnPropertyChanged(nameof(DriveUsageHighlight));
                    OnPropertyChanged(nameof(DriveUsageSuffix));
                    OnPropertyChanged(nameof(DriveUsageColor));
                }
            }
        }
        /// <summary>
        /// Unit label for <see cref="TotalHardDrive"/>.
        /// </summary>
        public Enums.DataSize TotalDriveLabel { get => DataSizeConverter.ReduceSize(_totalHardDrive).label; }

        private long _availableHardDrive;

        /// <summary>
        /// Available drive space reduced for display.
        /// </summary>
        public long AvailableHardDrive
        {
            get => DataSizeConverter.ReduceSize(_availableHardDrive).size;
            set
            {
                if (SetProperty(ref _availableHardDrive, value))
                {
                    OnPropertyChanged(nameof(AvailableDriveLabel));
                    OnPropertyChanged(nameof(UsedHardDrive));
                    OnPropertyChanged(nameof(UsedDriveLabel));
                    OnPropertyChanged(nameof(DriveUsageText));
                    OnPropertyChanged(nameof(DriveUsageHighlight));
                    OnPropertyChanged(nameof(DriveUsageSuffix));
                    OnPropertyChanged(nameof(DriveUsageColor));
                }
            }
        }
        /// <summary>
        /// Unit label for <see cref="AvailableHardDrive"/>.
        /// </summary>
        public Enums.DataSize AvailableDriveLabel { get => DataSizeConverter.ReduceSize(_availableHardDrive).label; }

        /// <summary>
        /// Used drive space reduced for display.
        /// </summary>
        public long UsedHardDrive
        {
            get => DataSizeConverter.ReduceSize(_totalHardDrive - _availableHardDrive).size;
        }

        /// <summary>
        /// Unit label for <see cref="UsedHardDrive"/>.
        /// </summary>
        public Enums.DataSize UsedDriveLabel
        {
            get => DataSizeConverter.ReduceSize(_totalHardDrive - _availableHardDrive).label;
        }

        /// <summary>
        /// User-facing sentence summarizing used versus total capacity.
        /// </summary>
        public string DriveUsageText =>
            $"You are using {UsedHardDrive} {UsedDriveLabel} of {TotalHardDrive} {TotalDriveLabel} on this drive";

        /// <summary>
        /// The colored portion of the drive usage line, e.g. "359 GB".
        /// </summary>
        public string DriveUsageHighlight =>
            $"{UsedHardDrive} {UsedDriveLabel}";

        /// <summary>
        /// The plain-text suffix after the colored portion, e.g. " of 474 GB on this drive".
        /// </summary>
        public string DriveUsageSuffix =>
            $" of {TotalHardDrive} {TotalDriveLabel} on this drive";

        /// <summary>
        /// Zone color for the used-space highlight: green / orange / red.
        ///   Green  — under 70% used
        ///   Orange — 70–85% used
        ///   Red    — over 85% used
        /// </summary>
        public string DriveUsageColor
        {
            get
            {
                if (_totalHardDrive <= 0) return "#FFFFFF";
                double usedPct = (double)(_totalHardDrive - _availableHardDrive) / _totalHardDrive * 100.0;
                return usedPct switch
                {
                    < 70 => "#4CAF50",  // green
                    < 85 => "#FF9800",  // orange
                    _ => "#F44336"   // red
                };
            }
        }

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

            (long totalSize, long availableSize) = GetDriveSize();
            TotalHardDrive = totalSize;
            AvailableHardDrive = availableSize;

            SaveDriveHealthToStorage(totalSize, availableSize);

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

            ActiveNode = new(System.IO.Path.GetFileName(initialFolder), initialFolder, "folder", 0, null, true);

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
                FileSearchItem? parent;
                if (ancestrialFolders.Count > 0)
                {
                    parent = ancestrialFolders[ancestrialFolders.Count - 1];
                }
                else
                {
                    parent = null;
                }

                var name = System.IO.Path.GetFileName(path);
                var item = new FileSearchItem(name, path, "folder", 0, parent, true);
                ancestrialFolders.Add(item);
                PathItems.Add(new PathItem(path, PathItems.Count));
            }

            ActiveNode.Parent = ancestrialFolders[ancestrialFolders.Count - 1];
            await RetrieveFolderItems(ActiveNode);
        }

        /// <summary>
        /// Changes the active folder and loads its contents.
        /// </summary>
        /// <param name="newNode">The folder node to make active.</param>
        public async Task ChangeActiveNode(FileSearchItem newNode)
        {
            IsLoading = true;
            await RetrieveFolderItems(newNode);
            ActiveNode = newNode;
            SortFiles();
            IsLoading = false;
        }

        /// <summary>
        /// Appends the target folder to breadcrumbs, ensures its children are populated, and schedules folder-size updates.
        /// </summary>
        /// <param name="folder">Folder node to display and hydrate.</param>
        public async Task RetrieveFolderItems(FileSearchItem folder)
        {
            IsLoading = true;

            PathItems.Add(new PathItem(folder.FilePath, PathItems.Count));

            if (folder.Children.Count > 0)
            {
                SortFiles();
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
        /// <param name="folder">Folder to enumerate.</param>
        /// <param name="sizeTasks">Collection that tracks spawned folder-size background tasks.</param>
        /// <returns>An async stream of discovered file and directory items.</returns>
        private async IAsyncEnumerable<FileSearchItem> EnumerateFolderItemsAsync(FileSearchItem folder, System.Collections.Concurrent.ConcurrentBag<Task> sizeTasks)
        {
            var channel = Channel.CreateUnbounded<FileSearchItem>();

            _ = Task.Run(() =>
            {
                try
                {
                    Dictionary<string, FileSearchItem> ancestors = [];

                    FileSearchItem? ancestorNode = ActiveNode;

                    while (ancestorNode != null) {
                        ancestors[ancestorNode.FilePath] = ancestorNode;
                        ancestorNode = ancestorNode.Parent;
                    }

                    foreach (var dir in Directory.EnumerateDirectories(folder.FilePath))
                    {
                        FileSearchItem item;
                        if (!ancestors.TryGetValue(dir, out FileSearchItem? value))
                        {
                            var fileItem = new System.IO.DirectoryInfo(dir);
                            if (fileItem.LinkTarget != null) {
                                continue;
                            }
                            item = new FileSearchItem(fileItem.Name, dir, "folder", 0, folder, false);
                        }
                        else
                        {
                            item = value;
                        }

                        channel.Writer.TryWrite(item);
                        sizeTasks.Add(UpdateFolderSizeAsync(item));
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
        /// <param name="item">Folder item to update.</param>
        /// <param name="path">Physical directory path for size calculation.</param>
        private async Task UpdateFolderSizeAsync(FileSearchItem item)
        {
            try
            {
                // The upside to this is that it is much faster. The downside is that it assumes that you have loaded all of the 
                // child elements already.
                if (item.Children.Count == 0)
                {
                    await Task.Run(() => GetDirectorySize(item));
                }
                else
                {
                    await Task.Run(() => item.Children.Sum(child => child.ByteSize));
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.Print("Error calculating directory size for path {0}: {1}", item.FilePath, ex.ToString());
            }
        }

        /// <summary>
        /// Computes total size of all files under <paramref name="folder"/>, ignoring access failures.
        /// </summary>
        /// <param name="folder">Root folder path to measure.</param>
        /// <returns>Total file size in bytes.</returns>
        private void GetDirectorySize(FileSearchItem folder)
        {
            long size = 0;
            try
            {
                foreach (var file in Directory.EnumerateFiles(folder.FilePath))
                {
                    try
                    {
                        var fileInfo = new FileInfo(file);
                        var temp = new FileSearchItem(fileInfo.Name, "", "file", fileInfo.Length, folder, true);
                        folder.Children.Add(temp);
                        size += fileInfo.Length;
                    }
                    catch
                    {
                        System.Diagnostics.Debug.Print("Failed to get size info for: " + file);
                    }
                }

                try
                {
                    _dispatcher.TryEnqueue(() => folder.UpdateSize(size));
                }
                catch (ArgumentException e)
                {
                    System.Diagnostics.Debug.Print($"Couldn't update size for {folder.Name}; error: {e.Message}");
                }
            }
            catch
            {
                System.Diagnostics.Debug.Print($"Access denied or transient IO error; skip files in this directory: {folder.Name}");
            }

            try
            {
                foreach (var dir in Directory.EnumerateDirectories(folder.FilePath))
                {
                    var fileItem = new System.IO.DirectoryInfo(dir);
                    if (fileItem.LinkTarget != null)
                    {
                        continue;
                    }
                    var child = new FileSearchItem(fileItem.Name, dir, "folder", 0, folder, false);
                    GetDirectorySize(child);
                    folder.Children.Add(child);
                }
            }
            catch
            {
                System.Diagnostics.Debug.Print($"Access denied or transient IO error; skip subdirectories for this directory: {folder.Name}");
            } finally
            {
                _dispatcher.TryEnqueue(() => folder.Finished = true);
            }
        }

        /// <summary>
        /// Truncates breadcrumbs so the selected index becomes the active tail item.
        /// </summary>
        /// <param name="index">Zero-based breadcrumb index to keep as the last entry.</param>
        public void ResetBreadCrumb(int index)
        {
            IEnumerable<PathItem> test = PathItems.Take(index + 1);
            PathItems = new ObservableCollection<PathItem>(test);
        }

        /// <summary>
        /// Reorders <see cref="ActiveNode"/> children according to the active sort option.
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
                listItems = listItems.OrderByDescending(item => item.ByteSize);
            }
            else
            {
                listItems = listItems.OrderBy(item => item.Name).OrderByDescending(item => item.Type);
            }

            ActiveNode.Children = new ObservableCollection<FileSearchItem>(listItems);
        }

        /// <summary>
        /// Removes all of the children items for the active node and each of its ancestors in order to refresh the file system.
        /// </summary>
        public async void Refresh()
        {
            IsLoading = true;
            FileSearchItem? clearItem = ActiveNode;

            while (clearItem != null)
            {
                clearItem.Children = [];
                clearItem = clearItem.Parent;
            }

            await RetrieveFolderItems(ActiveNode);
            IsLoading = false;
        }

        /// <summary>
        /// Retrieves total and free bytes for the drive containing the user profile directory.
        /// </summary>
        /// <returns>Total and available bytes as raw values.</returns>
        private static (long totalSize, long availableSize) GetDriveSize()
        {
            string? initialFolder = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (string.IsNullOrEmpty(initialFolder))
            {
                System.Diagnostics.Debug.Print("could not find initial folder to get drive size. Will not display drive size");
                return (0, 0);
            }
            string? driveName = Path.GetPathRoot(initialFolder);
            if (string.IsNullOrEmpty(driveName))
            {
                System.Diagnostics.Debug.Print("could not find initial folder to get drive size. Will not display drive size");
                return (0, 0);
            }
            DriveInfo driveInfo = new(driveName);

            // I chose to use TotalFreeSpace over AvailableFreeSpace because this will better reflect what is left after 
            // apps have taken their space
            System.Diagnostics.Debug.Print($"{driveInfo.TotalSize}, {driveInfo.TotalFreeSpace}");
            return (driveInfo.TotalSize, driveInfo.TotalFreeSpace);
        }

        /// <summary>
        /// Determines a drive health zone (Green/Orange/Red) based on used disk space percentage,
        /// then saves the zone and raw drive values to ApplicationData for the NotificationManagerService.
        ///   Green  — under 70% used
        ///   Orange — 70–85% used
        ///   Red    — over 85% used
        /// </summary>
        /// <param name="totalBytes">Total drive capacity in bytes.</param>
        /// <param name="availableBytes">Free space remaining in bytes.</param>
        private static void SaveDriveHealthToStorage(long totalBytes, long availableBytes)
        {
            try
            {
                if (totalBytes <= 0) return;

                double usedPercent = (double)(totalBytes - availableBytes) / totalBytes * 100.0;

                string zone = usedPercent switch
                {
                    < 70 => "Green",
                    < 85 => "Orange",
                    _ => "Red"
                };

                var c = Windows.Storage.ApplicationData.Current.LocalSettings.Values;
                c["LargeFileFinder_Zone"] = zone;
                c["LargeFileFinder_TotalBytes"] = totalBytes;
                c["LargeFileFinder_AvailableBytes"] = availableBytes;
                c["LargeFileFinder_UsedPercent"] = (int)Math.Round(usedPercent);
                c["LargeFileFinder_LastScanTime"] = DateTime.Now.Ticks;

                System.Diagnostics.Debug.WriteLine($"[LargeFileFinder] Drive health saved — zone={zone}, used={usedPercent:F1}%");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[LargeFileFinder] Failed to save drive health: {ex.Message}");
            }
        }
    }
}