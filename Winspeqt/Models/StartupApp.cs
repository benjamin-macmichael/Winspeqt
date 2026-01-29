using StartupInventory;
using System.Collections.Generic;

namespace Winspeqt.Models
{
    public sealed class StartupApp
    {
        public List<StartupItem> RegistryRun { get; } = new();
        public List<StartupItem> RegistryRunOnce { get; } = new();
        public List<StartupItem> StartupFolder { get; } = new();
        public List<StartupItem> ScheduledTask { get; } = new();
    }
}
