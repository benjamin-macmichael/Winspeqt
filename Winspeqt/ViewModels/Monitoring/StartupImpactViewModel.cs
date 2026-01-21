using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Winspeqt.Helpers;
using Winspeqt.Models;

namespace Winspeqt.ViewModels.Monitoring
{
    public class StartupImpactViewModel : ObservableObject
    {
        public ObservableCollection<StartupApp> StartupApps { get; set; }
        public StartupImpactViewModel() {
            StartupApps = new ObservableCollection<StartupApp>();
        }
    }
}
