using StartupInventory;
using System;
using System.Collections.Generic;
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
        public StartupApp StartupApp { get; }
        public IReadOnlyList<StartupAppGroup> StartupAppGroups { get; }
        public IReadOnlyList<StartupItem> StartupApps { get; set; }
        public StartupImpactViewModel() {
            _getStartupPrograms = new getStartupPrograms();
            _startupEnumerator = new StartupEnumerator();

            StartupApp = _startupEnumerator.GetStartupItems();
            StartupApps = FlattenStartupApps(StartupApp);
            StartupAppGroups = BuildGroups(StartupApp);
        }

        private static IReadOnlyList<StartupItem> FlattenStartupApps(StartupApp apps)
        {
            var items = new List<StartupItem>();
            items.AddRange(apps.RegistryRun);
            items.AddRange(apps.RegistryRunOnce);
            items.AddRange(apps.StartupFolder);
            items.AddRange(apps.ScheduledTask);
            return items;
        }

        private static IReadOnlyList<StartupAppGroup> BuildGroups(StartupApp apps)
        {
            var groups = new List<StartupAppGroup>();

            AddGroup(groups, "Registry Run", apps.RegistryRun);
            AddGroup(groups, "Registry Run Once", apps.RegistryRunOnce);
            AddGroup(groups, "Startup Folder", apps.StartupFolder);
            AddGroup(groups, "Scheduled Tasks", apps.ScheduledTask);

            return groups;
        }

        private static void AddGroup(
            List<StartupAppGroup> groups,
            string title,
            IReadOnlyList<StartupItem> items)
        {
            if (items.Count == 0)
                return;

            groups.Add(new StartupAppGroup(title, items));
        }

        public sealed class StartupAppGroup
        {
            public StartupAppGroup(string title, IReadOnlyList<StartupItem> items)
            {
                Title = title;
                Items = items;
            }

            public string Title { get; }
            public IReadOnlyList<StartupItem> Items { get; }
        }
    }
}
