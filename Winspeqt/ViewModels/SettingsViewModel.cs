using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows.Input;
using Windows.ApplicationModel;
using Winspeqt.Helpers;
using Winspeqt.Models;
using Winspeqt.Services;

namespace Winspeqt.ViewModels
{
    public class SettingsViewModel : ObservableObject
    {
        private readonly SettingsService _settingsService;
        private SettingsModel _settings;

        private bool _launchAtStartup;
        private bool _isStartupToggleBusy;
        private string _startupStatusMessage;

        public SettingsViewModel()
        {
            _settingsService = new SettingsService();
            _settings = _settingsService.LoadSettings();

            NavigateBackCommand = new RelayCommand(NavigateBack);

            _ = InitializeAsync();
        }

        public event EventHandler<string> NavigationRequested;

        public bool LaunchAtStartup
        {
            get => _launchAtStartup;
            set
            {
                if (SetProperty(ref _launchAtStartup, value))
                {
                    _ = SetStartupAsync(value);
                }
            }
        }

        public bool IsStartupToggleBusy
        {
            get => _isStartupToggleBusy;
            set => SetProperty(ref _isStartupToggleBusy, value);
        }

        public string StartupStatusMessage
        {
            get => _startupStatusMessage;
            set => SetProperty(ref _startupStatusMessage, value);
        }

        public ICommand NavigateBackCommand { get; }

        private async Task InitializeAsync()
        {
            try
            {
                // Read the actual startup task state from Windows, not just our saved preference
                var startupTask = await StartupTask.GetAsync("WinspeqtStartupTask");
                _launchAtStartup = startupTask.State == StartupTaskState.Enabled ||
                                   startupTask.State == StartupTaskState.EnabledByPolicy;
                OnPropertyChanged(nameof(LaunchAtStartup));
                UpdateStartupStatusMessage(startupTask.State);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error reading startup task state: {ex.Message}");
                // Fall back to persisted preference
                _launchAtStartup = _settings.LaunchAtStartup;
                OnPropertyChanged(nameof(LaunchAtStartup));
            }
        }

        private async Task SetStartupAsync(bool enable)
        {
            IsStartupToggleBusy = true;
            try
            {
                var startupTask = await StartupTask.GetAsync("WinspeqtStartupTask");

                if (enable)
                {
                    var newState = await startupTask.RequestEnableAsync();
                    _launchAtStartup = newState == StartupTaskState.Enabled ||
                                       newState == StartupTaskState.EnabledByPolicy;
                    OnPropertyChanged(nameof(LaunchAtStartup));
                    UpdateStartupStatusMessage(newState);
                }
                else
                {
                    startupTask.Disable();
                    UpdateStartupStatusMessage(StartupTaskState.Disabled);
                }

                // Persist the user's preference
                _settings.LaunchAtStartup = _launchAtStartup;
                _settingsService.SaveSettings(_settings);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error setting startup task: {ex.Message}");
                StartupStatusMessage = "Could not update startup setting.";
            }
            finally
            {
                IsStartupToggleBusy = false;
            }
        }

        private void UpdateStartupStatusMessage(StartupTaskState state)
        {
            StartupStatusMessage = state switch
            {
                StartupTaskState.Enabled => "Winspeqt will launch automatically at startup.",
                StartupTaskState.EnabledByPolicy => "Startup is enabled and managed by system policy.",
                StartupTaskState.Disabled => "Winspeqt will not launch at startup.",
                StartupTaskState.DisabledByPolicy => "Startup has been disabled by system policy.",
                StartupTaskState.DisabledByUser => "Startup was disabled in Task Manager. Re-enable it there to turn it back on.",
                _ => ""
            };
        }

        private void NavigateBack() => NavigationRequested?.Invoke(this, "Back");
    }
}