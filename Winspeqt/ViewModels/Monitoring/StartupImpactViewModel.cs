using StartupInventory;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Winspeqt.Helpers;
using Winspeqt.Models;
using Winspeqt.Services;

namespace Winspeqt.ViewModels.Monitoring
{
    public class StartupImpactViewModel : ObservableObject
    {
        private  readonly getStartupPrograms _getStartupPrograms;
        private readonly StartupEnumerator _startupEnumerator;
        public IReadOnlyList<StartupItem> StartupApps { get; set; }
        public StartupImpactViewModel() {
            StartupApps = new ObservableCollection<StartupItem>();
            _getStartupPrograms = new getStartupPrograms();
            _startupEnumerator = new StartupEnumerator();

            StartupApps = _startupEnumerator.GetStartupItems(false);
        }
    }
}
