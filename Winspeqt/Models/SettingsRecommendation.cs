using System.Collections.Generic;
using System.Drawing;

namespace Winspeqt.Models
{
    public class SettingsRecommendation
    {
        public string Name { get; }
        public string Description { get; }
        public Color Color { get; }
        public string LogoCode { get; }

        public SettingsRecommendation (string name, string description, int color, string logoCode)
        {
            Dictionary<int, Color> colorPicker = new Dictionary<int, Color> ()
            {
                { 0, Color.FromArgb(254, 184, 0) }, // yellow
                { 1, Color.FromArgb(241, 79, 33) }, // red
                { 2, Color.FromArgb(126, 185, 0) }, // green
                { 3, Color.FromArgb(0, 163, 238) }, // blue
            };
            Name = name;
            Description = description;
            Color = colorPicker[color % 4];
            LogoCode = logoCode;
        }
    }
}
