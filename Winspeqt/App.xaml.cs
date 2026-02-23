using Microsoft.UI.Xaml;
using Winspeqt.Services;

namespace Winspeqt
{
    public partial class App : Application
    {
        private Window? _window;

        public App()
        {
            InitializeComponent();
        }

        protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
        {
            // On first ever launch, default startup to enabled
            var settingsService = new SettingsService();
            if (!settingsService.GetSetting("StartupDefaultSet", false))
            {
                settingsService.SetStartupRegistry(true);
                settingsService.SetSetting("StartupDefaultSet", true);
            }

            // Start the background notification manager (rotating health score toasts)
            NotificationManagerService.Instance.Start();

            _window = new Views.MainWindow();
            _window.Activate();
        }
    }
}