// This is the memory type that Task manager uses. It is what I'd assume we want.
// Requires NuGet: System.Diagnostics.PerformanceCounter
using System;
using System.Collections.Generic;
using System.Diagnostics;

//this is too slow. Try using option B from chat. If that doesn't work, see about creating a dictionary based on pid
namespace Winspeqt.Services
{
    public static class ActivePrivateWorkingSet
    {
        public static Dictionary<int, long> GetActivePrivateWorkingSetDictionary()
        {
            var category = new PerformanceCounterCategory("Process");
            var result = new Dictionary<int, long>();

            foreach (var instanceName in category.GetInstanceNames())
            {
                try
                {
                    using var pidCounter = new PerformanceCounter(
                        "Process",
                        "ID Process",
                        instanceName,
                        readOnly: true);

                    using var wsPrivateCounter = new PerformanceCounter(
                        "Process",
                        "Working Set - Private",
                        instanceName,
                        readOnly: true);

                    int pid = (int)pidCounter.NextValue();
                    long workingSetPrivate = (long)wsPrivateCounter.NextValue();

                    // Avoid duplicate PIDs (can happen briefly with instance reuse)
                    if (pid > 0 && !result.ContainsKey(pid))
                    {
                        result[pid] = workingSetPrivate;
                    }
                }
                catch
                {
                    // Process may have exited; ignore
                }
            }

            return result;
        }
    }
}