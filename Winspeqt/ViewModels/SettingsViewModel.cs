using System;
using System.Windows.Input;
using Winspeqt.Helpers;
using Winspeqt.Services;

namespace Winspeqt.ViewModels
{
    public class SettingsViewModel : ObservableObject
    {
        private readonly SettingsService _settingsService;
        private bool _launchAtStartup;

        public SettingsViewModel()
        {
            _settingsService = new SettingsService();
            NavigateBackCommand = new RelayCommand(NavigateBack);

            var localSettings = Windows.Storage.ApplicationData.Current.LocalSettings;
            if (localSettings.Values.ContainsKey("LaunchAtStartup"))
                _launchAtStartup = (bool)localSettings.Values["LaunchAtStartup"];
            else
            {
                _launchAtStartup = true;
                localSettings.Values["LaunchAtStartup"] = true;
                _settingsService.SetStartupRegistry(true);
            }
        }

        public event EventHandler<string> NavigationRequested;

        public bool LaunchAtStartup
        {
            get => _launchAtStartup;
            set
            {
                if (SetProperty(ref _launchAtStartup, value))
                {
                    Windows.Storage.ApplicationData.Current.LocalSettings.Values["LaunchAtStartup"] = value;
                    _settingsService.SetStartupRegistry(value);
                    OnPropertyChanged(nameof(StartupButtonText));
                }
            }
        }

        public string StartupButtonText => _launchAtStartup ? "⏸ Disable Startup" : "▶ Enable Startup";

        public ICommand NavigateBackCommand { get; }

        private void NavigateBack() => NavigationRequested?.Invoke(this, "Back");
    }
}