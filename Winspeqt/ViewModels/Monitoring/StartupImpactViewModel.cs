using StartupInventory;
using Microsoft.UI.Dispatching;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Winspeqt.Helpers;
using Winspeqt.Models;
using Winspeqt.Services;
using Microsoft.UI.Xaml;

namespace Winspeqt.ViewModels.Monitoring
{
    public class StartupImpactViewModel : ObservableObject
    {
        private  readonly getStartupPrograms _getStartupPrograms;
        private readonly StartupEnumerator _startupEnumerator;
        private readonly DispatcherQueue _dispatcherQueue;

        private StartupApp _startupApp;
        public StartupApp StartupApp
        {
            get => _startupApp;
            private set => SetProperty(ref _startupApp, value);
        }

        private IReadOnlyList<StartupAppGroup> _startupAppGroups;
        public IReadOnlyList<StartupAppGroup> StartupAppGroups
        {
            get => _startupAppGroups;
            private set => SetProperty(ref _startupAppGroups, value);
        }

        public StartupImpactViewModel() {
            _getStartupPrograms = new getStartupPrograms();
            _startupEnumerator = new StartupEnumerator();
            _dispatcherQueue = DispatcherQueue.GetForCurrentThread();

            StartupApp = _startupEnumerator.GetStartupItems(false);
            StartupAppGroups = BuildGroups(StartupApp);

            _ = LoadScheduledTasksAsync();
        }

        private static IReadOnlyList<StartupAppGroup> BuildGroups(StartupApp apps)
        {
            var groups = new List<StartupAppGroup>();

            var registryDescription = "Registry description";
            var registryLink = "regedit";
            var registryButtonText = "Delete my registry (not a joke)"; //"View Regedit";
            var startupDescription = "Startup description";
            var startupLink = "startup";
            var startupButtonText = "View Startup Folder";
            var scheduleDescription = "Schedule description";
            var scheduleLink = "schd";
            var scheduleButtonText = "View Task Scheduler";

            AddGroup(groups, "Registry Run", apps.RegistryRun, registryDescription, registryLink, registryButtonText);
            AddGroup(groups, "Registry Run Once", apps.RegistryRunOnce, registryDescription, registryLink, registryButtonText);
            AddGroup(groups, "Startup Folder", apps.StartupFolder, startupDescription, startupLink, startupButtonText);
            AddGroup(groups, "Scheduled Tasks", apps.ScheduledTask, scheduleDescription, scheduleLink, scheduleButtonText);

            return groups;
        }

        private static void AddGroup(
            List<StartupAppGroup> groups,
            string title,
            IReadOnlyList<StartupItem> items,
            string description,
            string link,
            string buttonText)
        {
            if (items.Count == 0)
                return;

            groups.Add(new StartupAppGroup(title, description, link, buttonText, items));
        }

        public sealed class StartupAppGroup
        {
            public StartupAppGroup(string title, string description, string link, string buttonText, IReadOnlyList<StartupItem> items)
            {
                Title = title;
                Description = description;
                Link = link;
                ButtonText = buttonText;
                Items = items;
            }

            public string Title { get; }

            public string Description { get; }

            public string Link { get; }

            public string ButtonText { get; }
            public IReadOnlyList<StartupItem> Items { get; }
        }

        private async Task LoadScheduledTasksAsync()
        {
            var fullStartupApp = await Task.Run(() => _startupEnumerator.GetStartupItems());

            _dispatcherQueue.TryEnqueue(() =>
            {
                StartupApp = fullStartupApp;
                StartupAppGroups = BuildGroups(StartupApp);
            });
        }
    }
}
