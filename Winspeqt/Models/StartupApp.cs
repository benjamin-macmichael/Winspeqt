using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Winspeqt.Models
{
    public enum Impact
    {
        Unknown,
        Low,
        Medium,
        High,
        None,

    }
    public class StartupApp
    {
        public int Pid {  get;  }
        public string Name { get;  }
        public string Description { get;  }
        public Impact StartupImpact { get;  }

        public StartupApp(int pid, string name, string description = "", Impact startupImpact = Impact.Unknown)
        {
            Pid = pid;
            Name = name;
            Description = description;
            StartupImpact = startupImpact;
        }
    }
}
