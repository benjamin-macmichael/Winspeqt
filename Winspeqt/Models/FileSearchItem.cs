using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Winspeqt.Models
{
    public class FileSearchItem
    {
        string Name { get; }
        string FilePath { get; }
        string Type { get; }

        public FileSearchItem (string name, string path, string type)
        {
            Name = name;
            FilePath = path;
            Type = type;
        }
    }
}
