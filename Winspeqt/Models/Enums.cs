using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Winspeqt.Models
{
    public class Enums
    {
        // For easy reference to data size. If we get bigger than this, why are they using our program?
        public enum DataSize
        {
            B,
            KB,
            MB,
            GB,
            TB,
        }
    }
}
