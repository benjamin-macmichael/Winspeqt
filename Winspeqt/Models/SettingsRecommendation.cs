using Microsoft.UI.Xaml.Media;
using System.Collections.Generic;

namespace Winspeqt.Models
{
    public class SettingsRecommendation
    {
        public string Name { get; }
        public string Description { get; }
        public Brush IconColor { get; }
        public string LogoCode { get; }
        public NavigationButton Button { get; }

        public SettingsRecommendation(string name, string description, int color, string logoCode, string buttonTitle, string buttonLink)
        {
            Dictionary<int, Brush> colorPicker = new Dictionary<int, Brush>()
            {
                { 0, new SolidColorBrush(Windows.UI.Color.FromArgb(255,254,184,0)) }, // yellow
                { 1, new SolidColorBrush(Windows.UI.Color.FromArgb(255,241,79,33)) }, // red
                { 2, new SolidColorBrush(Windows.UI.Color.FromArgb(255,126,185,0)) }, // green
                { 3, new SolidColorBrush(Windows.UI.Color.FromArgb(255,0,163,238)) }, // blue
            };
            Name = name;
            Description = description;
            IconColor = colorPicker[color % 4];
            LogoCode = logoCode;
            Button = new NavigationButton(buttonTitle, buttonLink);
        }
    }
}
