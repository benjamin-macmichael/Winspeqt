// Services/ProcessService.cs

using System.Collections.Generic;
using System.Diagnostics;
using Winspeqt.Models;

namespace Winspeqt.Services
{
    public sealed class ProcessService
    {
        public IEnumerable<ProcessInfo> GetRunningProcesses()
        {
            var activePrivateWsDict = ActivePrivateWorkingSet.GetActivePrivateWorkingSetDictionary();
            foreach (var p in Process.GetProcesses())
            {
                ProcessInfo? info = null;

                try
                {
                    var pid = p.Id;

                    info = new ProcessInfo
                    {
                        Id = pid,
                        Name = p.ProcessName,
                        WorkingSet64Bytes = 
                            p.WorkingSet64,
                        ActivePrivateWorkingSetBytes = activePrivateWsDict[pid]
                    };
                }
                catch
                {
                    // ignore inaccessible/exited processes
                }
                finally
                {
                    p.Dispose();
                }

                if (info != null)
                    yield return info;
            }
        }
    }
}