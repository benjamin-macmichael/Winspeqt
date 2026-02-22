using System;
using System.Diagnostics;
using Microsoft.Win32;
using Windows.Storage;
using Winspeqt.Models;

namespace Winspeqt.Services
{
    public class SettingsService
    {
        private const string StartupKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
        private const string AppName = "Winspeqt";
        private readonly ApplicationDataContainer _localSettings;

        public SettingsService()
        {
            _localSettings = ApplicationData.Current.LocalSettings;
        }

        public SettingsModel LoadSettings()
        {
            return new SettingsModel
            {
                LaunchAtStartup = GetStartupRegistryValue(),
            };
        }

        public void SaveSettings(SettingsModel settings)
        {
            SetStartupRegistry(settings.LaunchAtStartup);
        }

        public bool GetStartupRegistryValue()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(StartupKey, false);
                return key?.GetValue(AppName) != null;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error reading startup registry: {ex.Message}");
                return false;
            }
        }

        public void SetStartupRegistry(bool enable)
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(StartupKey, true);
                if (enable)
                {
                    string exePath = Process.GetCurrentProcess().MainModule.FileName;
                    key?.SetValue(AppName, $"\"{exePath}\"");
                }
                else
                {
                    key?.DeleteValue(AppName, false);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error setting startup registry: {ex.Message}");
            }
        }

        public bool GetSetting(string key, bool defaultValue)
        {
            try
            {
                if (_localSettings.Values.ContainsKey(key))
                    return (bool)_localSettings.Values[key];
                return defaultValue;
            }
            catch { return defaultValue; }
        }

        public void SetSetting(string key, bool value)
        {
            try { _localSettings.Values[key] = value; }
            catch (Exception ex) { Debug.WriteLine($"Error saving setting '{key}': {ex.Message}"); }
        }
    }
}