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
        public PathItem(string path) 
        {
            Path = path;
            Name = System.IO.Path.GetFileName(path);
        }
    }
}
