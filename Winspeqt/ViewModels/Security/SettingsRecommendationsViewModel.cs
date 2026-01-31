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
                        "e707;"
                    ),                
                new(
                        "Location",
                        "Super spies like to download trackers onto your computer. You think you downloaded a cute little clicker game? No, it's the KGB finding a way to spy on your every move! Catch them in the act and see what apps are being used to keep you under the threat of espionage.",
                        2,
                        "\ue707;"
                    ),
                new(
                        "Location",
                        "Super spies like to download trackers onto your computer. You think you downloaded a cute little clicker game? No, it's the KGB finding a way to spy on your every move! Catch them in the act and see what apps are being used to keep you under the threat of espionage.",
                        3,
                        "\ue707;"
                    ),                
                new(
                        "Location",
                        "Super spies like to download trackers onto your computer. You think you downloaded a cute little clicker game? No, it's the KGB finding a way to spy on your every move! Catch them in the act and see what apps are being used to keep you under the threat of espionage.",
                        4,
                        "\ue707;"
                    ),
            ];
        }
    }
}
