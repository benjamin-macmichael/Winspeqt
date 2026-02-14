using Microsoft.UI.Xaml.Shapes;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.UI.Dispatching;
using Winspeqt.Helpers;
using Winspeqt.Models;

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

        private bool _isLoading;
        public bool IsLoading
        {
            get => _isLoading;
            set => SetProperty(ref _isLoading, value);
        }

        public LargeFileFinderViewModel()
        {
            FolderItems = [];
        }

        public async Task LoadAsync()
        {
            IsLoading = true;

            string initialFolder = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

            await RetrieveFolderItems(initialFolder);
        }

        public async Task RetrieveFolderItems(string folder)
        {
            IsLoading = true;

            var items = await Task.Run(() => BuildFolderItems(folder));

            _dispatcher.TryEnqueue(() =>
            {
                FolderItems.Clear();
                foreach (var item in items)
                {
                    FolderItems.Add(item);
                }

                IsLoading = false;
            });
        }

        private List<FileSearchItem> BuildFolderItems(string folder)
        {
            List<FileSearchItem> temp = [];

            try
            {
                PathToObject(temp, Directory.GetDirectories(folder), "folder");
                PathToObject(temp, Directory.GetFiles(folder), "file");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.Print("Error retrieving large files for path {0}: {1}", folder, ex.ToString());
            }

            return temp;
        }

        void PathToObject(List<FileSearchItem> target, string[] paths, string type)
        {
            foreach (var path in paths)
            {
                string name = System.IO.Path.GetFileName(path);
                long size = type == "file" ? new System.IO.FileInfo(path).Length : DirSize(new DirectoryInfo(path));
                ObservableCollection<FileSearchItem>? subdirectories = null; //type == "folder" ? new ObservableCollection<FileSearchItem>(FindFilesForFolder(path)) : null;

                target.Add(
                    new FileSearchItem(
                        name, 
                        type == "folder" ? path : "", 
                        type, 
                        size,
                        subdirectories
                    )
                );
            }
        }


        // Source - https://stackoverflow.com/a/468131
        // Posted by hao, modified by community. See post 'Timeline' for change history
        // Retrieved 2026-02-07, License - CC BY-SA 3.0
        public static long DirSize(DirectoryInfo d)
        {
            try
            {
                long size = 0;
                // Add file sizes.
                FileInfo[] fis = d.GetFiles();
                foreach (FileInfo fi in fis)
                {
                    size += fi.Length;
                }
                // Add subdirectory sizes.
                DirectoryInfo[] dis = d.GetDirectories();
                foreach (DirectoryInfo di in dis)
                {
                    size += DirSize(di);
                }
                return size;
            } catch
            {
                return 0;
            }
        }

    }
}
