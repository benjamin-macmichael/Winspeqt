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
        public ObservableCollection<FileSearchItem> FolderItems { get; set; }
        public LargeFileFinderViewModel()
        {
            string initialFolder = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

            System.Diagnostics.Debug.WriteLine(initialFolder);

            FolderItems = new ObservableCollection<FileSearchItem>(FindFilesForFolder(initialFolder));

            foreach (var folder in FolderItems)
            {
                System.Diagnostics.Debug.WriteLine(folder);
            }
        }

        List<FileSearchItem> FindFilesForFolder(string folder)
        {
            try
            {
                List<FileSearchItem> folders = PathToObject(Directory.GetDirectories(folder), "folder");
                List<FileSearchItem> files = PathToObject(Directory.GetFiles(folder), "file");

                return [.. folders, .. files];

            } catch
            {
                return [];
            }
        }

        List<FileSearchItem> PathToObject(string[] paths, string type)
        {
            List<FileSearchItem> temp = [];
            foreach (var path in paths)
            {
                string name = System.IO.Path.GetFileName(path);
                long size = type == "file" ? new System.IO.FileInfo(path).Length : DirSize(new DirectoryInfo(path));
                ObservableCollection<FileSearchItem>? subdirectories = null; //type == "folder" ? new ObservableCollection<FileSearchItem>(FindFilesForFolder(path)) : null;

                temp.Add(
                    new FileSearchItem(
                        name, 
                        path, 
                        type, 
                        size,
                        subdirectories
                    )
                );
            }

            return temp;
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
