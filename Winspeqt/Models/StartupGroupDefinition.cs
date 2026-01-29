using StartupInventory;
using System;
using System.Collections.Generic;

namespace Winspeqt.Models
{
    /// <summary>
    /// Defines how a startup app group is built and labeled in the UI.
    /// </summary>
    internal sealed class StartupGroupDefinition
    {
        /// <summary>
        /// Creates a definition for a group of startup items.
        /// </summary>
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

        /// <summary>
        /// Display title for the group.
        /// </summary>
        public string Title { get; }
        /// <summary>
        /// Selector for the items belonging to the group.
        /// </summary>
        public Func<StartupApp, IReadOnlyList<StartupItem>> ItemsSelector { get; }
        /// <summary>
        /// Description shown in the info flyout.
        /// </summary>
        public string Description { get; }
        /// <summary>
        /// Link identifier used by the view to open the relevant tool.
        /// </summary>
        public string Link { get; }
        /// <summary>
        /// Label for the link action button.
        /// </summary>
        public string ButtonText { get; }
    }
}
