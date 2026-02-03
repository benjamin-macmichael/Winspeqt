using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Winspeqt.Models;

namespace Winspeqt.ViewModels.Security
{
    public class SettingsRecommendationsViewModel
    {
        public ObservableCollection<SettingsRecommendation> SettingsRecommendations { get; set; }

        public SettingsRecommendationsViewModel() 
        { 
            SettingsRecommendations =
            [
                new(
                        "Location",
                        "Location access can reveal where you are and where you have been. Restricting it limits tracking and reduces exposure if an app is compromised.",
                        1,
                        "\ue707",
                        "View Location Settings",
                        "ms-settings:privacy-location"
                    ),                
                new(
                        "Screen Time Out",
                        "Shorter timeouts lock the screen faster when you step away. This lowers the risk of someone accessing your session without permission.",
                        2,
                        "\uf182",
                        "View Time Out Settings",
                        "ms-settings:powersleep"
                    ),
                new(
                        "Windows Update",
                        "Updates patch known vulnerabilities in Windows and drivers. Staying current reduces exposure to malware and exploit chains.",
                        3,
                        "\ue777",
                        "View Update Settings",
                        "ms-settings:windowsupdate"
                    ),                
                new(
                        "Sign-in Options",
                        "Stronger sign-in methods like PINs, biometrics, or MFA help prevent unauthorized access if your device or password is compromised.",
                        4,
                        "\ue72e",
                        "View Sign-in Settings Settings",
                        "ms-settings:signinoptions"
                    ),
                new(
                        "Smart App Control",
                        "Smart App Control adds significant protection from new and emerging threats by blocking apps that are malicious or untrusted.",
                        1,
                        "\ue774",
                        "Open Windows Security",
                        "windowsdefender://appbrowser"
                    ),
                new(
                        "Remote Access",
                        "Remote Desktop lets others connect to this device. Disabling or restricting it reduces exposure to credential and brute-force attacks.",
                        2,
                        "\ue703",
                        "View Remote Desktop Settings",
                        "ms-settings:remotedesktop"
                    ),
                new(
                        "Windows Anti-virus",
                        "Real-time antivirus scanning detects and blocks known threats. Turning it off increases the chance of malware persisting undetected.",
                        3,
                        "\ue730",
                        "Open Windows Security",
                        "windowsdefender://providers"
                    ),
                new(
                        "Screen Recording",
                        "Screen capture features can expose sensitive on-screen data. Limit access and recording to trusted users and apps only.",
                        4,
                        "\ue714",
                        "View Capture Settings",
                        "ms-settings:privacy-graphicscaptureprogrammatic"
                    ),
            ];
        }
    }
}
