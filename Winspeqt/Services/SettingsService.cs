using System;
using System.Diagnostics;
using Windows.Storage;
using Winspeqt.Models;

namespace Winspeqt.Services
{
    public class SettingsService
    {
        private readonly ApplicationDataContainer _localSettings;

        public SettingsService()
        {
            _localSettings = ApplicationData.Current.LocalSettings;
        }

        public SettingsModel LoadSettings()
        {
            return new SettingsModel
            {
                LaunchAtStartup = GetSetting("LaunchAtStartup", true),
            };
        }

        public void SaveSettings(SettingsModel settings)
        {
            SetSetting("LaunchAtStartup", settings.LaunchAtStartup);
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
            try
            {
                _localSettings.Values[key] = value;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error saving setting '{key}': {ex.Message}");
            }
        }
    }
}