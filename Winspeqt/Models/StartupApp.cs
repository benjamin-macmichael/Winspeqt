using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Winspeqt.Models
{
    public class StartupApp
    {
        public int StartupProgramId { get; set; }
        public int ComputerId { get; set; }
        public int StartupClassificationId { get; set; }
        public string StartupClassificationName { get; set; }
        public string ProgramName { get; set; }
        public string ProgramPath { get; set; }
        public bool? IsEnabled { get; set; }
        public bool? IsDeleted { get; set; }
        public string StartupType { get; set; }
        public string Publisher { get; set; }
        public string startupUserName { get; set; }
    }
}
