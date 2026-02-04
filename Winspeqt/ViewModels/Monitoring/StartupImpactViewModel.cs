using Microsoft.UI.Dispatching;
using StartupInventory;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows.Input;
using Winspeqt.Helpers;
using Winspeqt.Models;

namespace Winspeqt.ViewModels.Monitoring
{
    /// <summary>
    /// View model for the Startup Impact page. Loads startup items and exposes grouped data for display.
    /// </summary>
    public class StartupImpactViewModel : ObservableObject
    {
        private readonly StartupEnumerator _startupEnumerator;
        private readonly DispatcherQueue _dispatcherQueue;
        private readonly IReadOnlyList<StartupGroupDefinition> _groupDefinitions;

        private bool _isLoading;
        /// <summary>
        /// Indicates whether the view model is currently loading data.
        /// </summary>
        public bool IsLoading
        {
            get => _isLoading;
            set => SetProperty(ref _isLoading, value);
        }

        private bool _showAdvancedSettings;
        /// <summary>
        /// Controls the visibility of advanced settings in the view.
        /// </summary>
        public bool ShowAdvancedSettings
        {
            get => _showAdvancedSettings;
            set
            {
                SetProperty(ref _showAdvancedSettings, value);
                OnPropertyChanged();
                OnPropertyChanged(nameof(AdvancedSettingsText));
            }
        }

        /// <summary>
        /// Button label that reflects the current advanced settings state.
        /// </summary>
        public string AdvancedSettingsText => $"{(ShowAdvancedSettings ? "Hide" : "Show")} Advanced Settings";

        private StartupApp _startupApp;
        /// <summary>
        /// Raw startup app data from the enumerator.
        /// </summary>
        public StartupApp StartupApp
        {
            get => _startupApp;
            private set => SetProperty(ref _startupApp, value);
        }

        private IReadOnlyList<StartupAppGroup> _startupAppGroups;
        /// <summary>
        /// Startup apps grouped by source for display.
        /// </summary>
        public IReadOnlyList<StartupAppGroup> StartupAppGroups
        {
            get => _startupAppGroups;
            private set => SetProperty(ref _startupAppGroups, value);
        }

        /// <summary>
        /// Refreshes startup app data on demand.
        /// </summary>
        public ICommand RefreshCommand { get; }

        /// <summary>
        /// Initializes the view model, preloads data, and kicks off the full refresh.
        /// </summary>
        public StartupImpactViewModel()
        {
            _startupEnumerator = new StartupEnumerator();
            _dispatcherQueue = DispatcherQueue.GetForCurrentThread();
            _groupDefinitions = BuildGroupDefinitions();

            IsLoading = true;
            ShowAdvancedSettings = false;

            StartupApp = _startupEnumerator.GetStartupItems(false);
            StartupAppGroups = BuildGroups(StartupApp, _groupDefinitions);

            RefreshCommand = new RelayCommand(async () => await RefreshDataAsync());

            _ = RefreshDataAsync(initialLoad: true);
        }

        /// <summary>
        /// Toggles the advanced settings section.
        /// </summary>
        public void ToggleAdvancedSettings()
        {
            ShowAdvancedSettings = !ShowAdvancedSettings;
        }

        /// <summary>
        /// Creates the definitions used to build the grouped view.
        /// </summary>
        private static IReadOnlyList<StartupGroupDefinition> BuildGroupDefinitions()
        {
            // https://learn.microsoft.com/en-us/windows/win32/setupapi/run-and-runonce-registry-keys
            // https://learn.microsoft.com/en-us/troubleshoot/windows-server/performance/windows-registry-advanced-users
            var registryRunDescription = "The Registry contains information that Windows continually references during operation. The Run key makes the program run every time the user logs on. Registry keys can be viewed and edited in Registry Editor.";
            var registryRunOnceDescription = "The Registry contains information that Windows continually references during operation. The RunOnce key makes the program run one time, and then the key is deleted. Registry keys can be viewed and edited in Registry Editor.";
            var registryLink = "regedit";
            var registryButtonText = "View Registry Editor";
            // https://www.lenovo.com/us/en/glossary/startup-folder/?orgRef=https%253A%252F%252Fwww.google.com%252F
            var startupDescription = "The startup folder contains shortcuts to programs that run when your computer starts up. It can contain executable files (.exe), shortcuts (.lnk), or script files (ex. .bat or .cmd).";
            var startupLink = "startup";
            var startupButtonText = "View Startup Folder";
            var scheduleDescription = "Scheduled tasks are programs that run when certain criteria are met. Criteria can be specific times, system events, when your computer starts up, and more.";
            var scheduleLink = "schd";
            var scheduleButtonText = "View Task Scheduler";

            return new List<StartupGroupDefinition>
            {
                new StartupGroupDefinition("Registry Run", app => app.RegistryRun, registryRunDescription, registryLink, registryButtonText),
                new StartupGroupDefinition("Registry Run Once", app => app.RegistryRunOnce, registryRunOnceDescription, registryLink, registryButtonText),
                new StartupGroupDefinition("Startup Folder", app => app.StartupFolder, startupDescription, startupLink, startupButtonText),
                new StartupGroupDefinition("Scheduled Tasks", app => app.ScheduledTask, scheduleDescription, scheduleLink, scheduleButtonText)
            };
        }

        /// <summary>
        /// Builds grouped results from the raw app list and definition set.
        /// </summary>
        private static IReadOnlyList<StartupAppGroup> BuildGroups(StartupApp apps, IReadOnlyList<StartupGroupDefinition> definitions)
        {
            var groups = new List<StartupAppGroup>();

            foreach (var definition in definitions)
            {
                var items = definition.ItemsSelector(apps);
                if (items == null || items.Count == 0)
                    continue;

                groups.Add(new StartupAppGroup(
                    definition.Title,
                    definition.Description,
                    definition.Link,
                    definition.ButtonText,
                    items));
            }

            return groups;
        }

        /// <summary>
        /// Display model for a group of startup items.
        /// </summary>
        public sealed class StartupAppGroup
        {
            /// <summary>
            /// Creates a display group with a title, description, and items.
            /// </summary>
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

        /// <summary>
        /// Loads the latest startup data and updates the UI.
        /// </summary>
        private async Task RefreshDataAsync(bool initialLoad = false)
        {
            if (IsLoading && !initialLoad)
                return;

            IsLoading = true;

            try
            {
                var fullStartupApp = await Task.Run(() => _startupEnumerator.GetStartupItems());

                _dispatcherQueue.TryEnqueue(() =>
                {
                    StartupApp = fullStartupApp;
                    StartupAppGroups = BuildGroups(StartupApp, _groupDefinitions);
                });
            }
            catch
            {
                // Keep existing data if refresh fails.
            }
            finally
            {
                IsLoading = false;
            }
        }
    }
}
