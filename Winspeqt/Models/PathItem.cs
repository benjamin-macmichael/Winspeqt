using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Winspeqt.Models
{
    public class PathItem
    {
        public string Name { get; }
        public string Path { get; }
        public int Index { get; }
        public PathItem(string path, int index) 
        {
            Path = path;
            Name = System.IO.Path.GetFileName(path);
            Index = index;
        }
    }
}
