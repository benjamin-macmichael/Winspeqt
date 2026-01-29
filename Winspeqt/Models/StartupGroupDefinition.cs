using StartupInventory;
using System;
using System.Collections.Generic;

namespace Winspeqt.Models
{
    internal sealed class StartupGroupDefinition
    {
        public StartupGroupDefinition(
            string title,
            Func<StartupApp, IReadOnlyList<StartupItem>> itemsSelector,
            string description,
            string link,
            string buttonText)
        {
            Title = title;
            ItemsSelector = itemsSelector;
            Description = description;
            Link = link;
            ButtonText = buttonText;
        }

        public string Title { get; }
        public Func<StartupApp, IReadOnlyList<StartupItem>> ItemsSelector { get; }
        public string Description { get; }
        public string Link { get; }
        public string ButtonText { get; }
    }
}
