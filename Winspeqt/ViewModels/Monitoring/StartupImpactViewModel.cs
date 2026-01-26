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

        private bool _isLoading;
        public bool IsLoading
        {
            get => _isLoading;
            set => SetProperty(ref _isLoading, value);
        }

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
            IsLoading = true;

            StartupApp = _startupEnumerator.GetStartupItems(false);
            StartupAppGroups = BuildGroups(StartupApp);

            _ = LoadScheduledTasksAsync();
        }

        private static IReadOnlyList<StartupAppGroup> BuildGroups(StartupApp apps)
        {
            var groups = new List<StartupAppGroup>();

            // https://learn.microsoft.com/en-us/windows/win32/setupapi/run-and-runonce-registry-keys
            // https://learn.microsoft.com/en-us/troubleshoot/windows-server/performance/windows-registry-advanced-users
            var registryRunDescription = "The Registry contains information that Windows continually references during operation. The Run key makes the program run every time the user logs on. Registry keys can be viewed and edited in Registry Editor.";
            var registryRunOnceDescription = "The Registry contains information that Windows continually references during operation. The RunOnce key makes the program run one time, and then the key is deleted. Registry keys can be viewed and edited in Registry Editor.";
            var registryLink = "regedit";
            var registryButtonText = "Delete my registry (not a joke)"; //"View Registry Editor";
            // https://www.lenovo.com/us/en/glossary/startup-folder/?orgRef=https%253A%252F%252Fwww.google.com%252F
            var startupDescription = "The startup folder contains shortcuts to programs that run when your computer starts up. It can contain executable files (.exe), shortcuts (.lnk), or script files (ex. .bat or .cmd).";
            var startupLink = "startup";
            var startupButtonText = "View Startup Folder";
            var scheduleDescription = "Scheduled tasks are programs that run when certain criteria are met. Criteria can be specific times, system events, when your computer starts up, and more.";
            var scheduleLink = "schd";
            var scheduleButtonText = "View Task Scheduler";

            AddGroup(groups, "Registry Run", apps.RegistryRun, registryRunDescription, registryLink, registryButtonText);
            AddGroup(groups, "Registry Run Once", apps.RegistryRunOnce, registryRunOnceDescription, registryLink, registryButtonText);
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

            IsLoading = false;
        }
    }
}
