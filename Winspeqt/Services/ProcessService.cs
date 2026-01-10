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
            // Map to a safe DTO for binding (Process properties can throw).
            foreach (var p in Process.GetProcesses())
            {
                ProcessInfo info;
                try
                {
                    info = new ProcessInfo { Id = p.Id, Name = p.ProcessName, Memory = p.PrivateMemorySize64 / 1024 / 1024 };
                }
                catch
                {
                    // Skip processes that can't be inspected
                    continue;
                }
                finally
                {
                    p.Dispose();
                }

                yield return info;
            }
        }
    }
}