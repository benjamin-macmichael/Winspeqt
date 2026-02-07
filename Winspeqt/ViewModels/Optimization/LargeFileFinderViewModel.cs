using Microsoft.UI.Xaml.Shapes;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Winspeqt.ViewModels.Optimization
{
    public class LargeFileFinderViewModel
    {
        public LargeFileFinderViewModel ()
        {
            string initialFolder = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

            System.Diagnostics.Debug.WriteLine(initialFolder);

            string[] folders = FindFilesForFolder(initialFolder);

            foreach (var folder in folders)
            {
                System.Diagnostics.Debug.WriteLine(folder);
            }
        }

        string[] FindFilesForFolder(string folder)
        {
            string[] folders = Directory.GetDirectories(folder);
            string[] files = Directory.GetFiles(folder);

            return [..folders, ..files];
        }
    }
}
