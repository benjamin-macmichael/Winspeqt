using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;
using System;
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
            var mainInstance = AppInstance.FindOrRegisterForKey("main");

            // If there's already a running instance, redirect and exit
            if (!mainInstance.IsCurrent)
            {
                var activatedArgs = AppInstance.GetCurrent().GetActivatedEventArgs();
                mainInstance.RedirectActivationToAsync(activatedArgs).AsTask().Wait();
                System.Diagnostics.Process.GetCurrentProcess().Kill();
                return;
            }

            // This IS the main instance — register for future redirected activations
            mainInstance.Activated += OnInstanceActivated;

            // On first ever launch, default startup to enabled
            var settingsService = new SettingsService();
            if (!settingsService.GetSetting("StartupDefaultSet", false))
            {
                settingsService.SetStartupRegistry(true);
                settingsService.SetSetting("StartupDefaultSet", true);
            }

            // Start the background notification manager
            NotificationManagerService.Instance.Start();

            // Check if launched from a toast notification
            string? toastFeature = null;
            var currentArgs = AppInstance.GetCurrent().GetActivatedEventArgs();
            if (currentArgs.Kind == ExtendedActivationKind.ToastNotification)
            {
                var toastArgs = currentArgs.Data as Windows.ApplicationModel.Activation.IToastNotificationActivatedEventArgs;
                if (toastArgs != null)
                    toastFeature = ParseFeature(toastArgs.Argument);
            }

            _window = new Views.MainWindow(toastFeature);
            _window.Activate();
        }

        // Called when a second instance tries to launch (e.g. toast click while app is running)
        private void OnInstanceActivated(object? sender, AppActivationArguments args)
        {
            string? feature = null;
            if (args.Kind == ExtendedActivationKind.ToastNotification)
            {
                var toastArgs = args.Data as Windows.ApplicationModel.Activation.IToastNotificationActivatedEventArgs;
                if (toastArgs != null)
                    feature = ParseFeature(toastArgs.Argument);
            }

            // Marshal back to UI thread
            if (_window is Views.MainWindow mainWindow)
            {
                mainWindow.DispatcherQueue.TryEnqueue(() =>
                {
                    mainWindow.NavigateToFeature(feature ?? "Dashboard");
                });
            }
        }

        private static string? ParseFeature(string argument)
        {
            if (string.IsNullOrEmpty(argument)) return null;
            foreach (var part in argument.Split('&'))
            {
                var kv = part.Split('=');
                if (kv.Length == 2 && kv[0] == "feature")
                    return kv[1];
            }
            return null;
        }
    }
}