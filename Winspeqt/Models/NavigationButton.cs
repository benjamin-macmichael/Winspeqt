using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Winspeqt.Models
{
    public class NavigationButton(string label, string target)
    {
        public string Label { get; } = label; public string Target { get; } = target;
    }
}
