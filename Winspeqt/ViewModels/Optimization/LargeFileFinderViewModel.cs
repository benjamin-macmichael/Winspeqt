using Microsoft.UI.Xaml.Shapes;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Winspeqt.Helpers;
using Winspeqt.Models;

namespace Winspeqt.ViewModels.Optimization
{
    public class LargeFileFinderViewModel : ObservableObject
    {
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
            IsLoading = true;

            FolderItems = [];

            string initialFolder = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

            _ = RetrieveFolderItems(initialFolder);
        }

        public async Task RetrieveFolderItems(string folder)
        {
            FolderItems.Clear();

            try
            {
                PathToObject(Directory.GetDirectories(folder), "folder");
                PathToObject(Directory.GetFiles(folder), "file");
            }
            catch
            {
                System.Diagnostics.Debug.Print("Error retrieving large files for path ", folder);
            }
            IsLoading = false;
        }

        void PathToObject(string[] paths, string type)
        {
            List<FileSearchItem> temp = [];
            foreach (var path in paths)
            {
                string name = System.IO.Path.GetFileName(path);
                long size = type == "file" ? new System.IO.FileInfo(path).Length : DirSize(new DirectoryInfo(path));
                ObservableCollection<FileSearchItem>? subdirectories = null; //type == "folder" ? new ObservableCollection<FileSearchItem>(FindFilesForFolder(path)) : null;

                FolderItems.Add(
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
