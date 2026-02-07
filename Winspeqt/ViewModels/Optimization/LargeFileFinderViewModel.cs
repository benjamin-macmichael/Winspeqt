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
            List<FileSearchItem> folders = PathToObject(Directory.GetDirectories(folder), "folder");
            List<FileSearchItem> files = PathToObject(Directory.GetFiles(folder), "file");

            return [..folders, ..files];
        }

        List<FileSearchItem> PathToObject(string[] paths, string type)
        {
            List<FileSearchItem> temp = [];
            foreach (var path in paths)
            {
                temp.Add(new FileSearchItem("", path, type));
            }

            return temp;
        }
    }
}
