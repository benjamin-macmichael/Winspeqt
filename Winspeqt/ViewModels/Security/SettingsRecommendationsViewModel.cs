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
                        "Super spies like to download trackers onto your computer. You think you downloaded a cute little clicker game? No, it's the KGB finding a way to spy on your every move! Catch them in the act and see what apps are being used to keep you under the threat of espionage.",
                        1,
                        "\ue707",
                        "View Location Settings",
                        ""
                    ),                
                new(
                        "Screen Time Out",
                        "Super spies like to download trackers onto your computer. You think you downloaded a cute little clicker game? No, it's the KGB finding a way to spy on your every move! Catch them in the act and see what apps are being used to keep you under the threat of espionage.",
                        2,
                        "\uf182",
                        "View Time Out Settings",
                        ""
                    ),
                new(
                        "Windows Update",
                        "Super spies like to download trackers onto your computer. You think you downloaded a cute little clicker game? No, it's the KGB finding a way to spy on your every move! Catch them in the act and see what apps are being used to keep you under the threat of espionage.Super spies like to download trackers onto your computer. You think you downloaded a cute little clicker game? No, it's the KGB finding a way to spy on your every move! Catch them in the act and see what apps are being used to keep you under the threat of espionage.",
                        3,
                        "\ue777",
                        "View Update Settings",
                        ""
                    ),                
                new(
                        "Sign-in Options",
                        "Super spies like to download trackers onto your computer. You think you downloaded a cute little clicker game? No, it's the KGB finding a way to spy on your every move! Catch them in the act and see what apps are being used to keep you under the threat of espionage.",
                        4,
                        "\ue72e",
                        "View Sign-in Settings Settings",
                        ""
                    ),
            ];
        }
    }
}
