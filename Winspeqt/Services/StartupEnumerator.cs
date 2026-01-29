#nullable enable
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Xml.Linq;
using Winspeqt.Models;

namespace StartupInventory
{
    public enum StartupSource
    {
        RegistryRun,
        RegistryRunOnce,
        StartupFolder,
        ScheduledTask
    }

    public sealed record StartupItem(
        string Name,
        string Command,
        StartupSource Source,
        string Location,
        bool? Enabled
    );

    public sealed class StartupEnumerator
    {
        public StartupApp GetStartupItems(bool includeScheduledTasks = true)
        {
            var apps = new StartupApp();
            var seen = new HashSet<(StartupSource Source, string Location, string Name)>();

            // Registry: HKLM/HKCU Run + RunOnce (32/64 where applicable)
            AddItems(apps, ReadRunKeys(RegistryHive.LocalMachine), seen);
            AddItems(apps, ReadRunKeys(RegistryHive.CurrentUser), seen);

            // Startup folders: current user + all users
            AddItems(apps, ReadStartupFolders(), seen);

            // Scheduled tasks with logon triggers
            if (includeScheduledTasks)
                AddItems(apps, ReadScheduledTasks_LogonTriggers(), seen);

            SortByName(apps.RegistryRun);
            SortByName(apps.RegistryRunOnce);
            SortByName(apps.StartupFolder);
            SortByName(apps.ScheduledTask);

            return apps;
        }

        private static void AddItems(
            StartupApp apps,
            IEnumerable<StartupItem> items,
            HashSet<(StartupSource Source, string Location, string Name)> seen)
        {
            foreach (var item in items)
            {
                if (!seen.Add((item.Source, item.Location, item.Name)))
                    continue;

                switch (item.Source)
                {
                    case StartupSource.RegistryRun:
                        apps.RegistryRun.Add(item);
                        break;
                    case StartupSource.RegistryRunOnce:
                        apps.RegistryRunOnce.Add(item);
                        break;
                    case StartupSource.StartupFolder:
                        apps.StartupFolder.Add(item);
                        break;
                    case StartupSource.ScheduledTask:
                        apps.ScheduledTask.Add(item);
                        break;
                }
            }
        }

        private static void SortByName(List<StartupItem> items)
        {
            items.Sort((left, right) =>
                StringComparer.OrdinalIgnoreCase.Compare(left.Name, right.Name));
        }

        private static IEnumerable<StartupItem> ReadRunKeys(RegistryHive hive)
        {
            foreach (var view in GetViewsForHive(hive))
            {
                foreach (var (subKey, source) in new[]
                {
                    (@"Software\Microsoft\Windows\CurrentVersion\Run", StartupSource.RegistryRun),
                    (@"Software\Microsoft\Windows\CurrentVersion\RunOnce", StartupSource.RegistryRunOnce),
                })
                {
                    foreach (var item in ReadRegistryValues(hive, view, subKey, source))
                        yield return item;

                    // Optional: also surface enabled/disabled state from StartupApproved if present
                    // (Task Manager uses these to show disabled entries.)
                    // You can later join by Name across these sources if you want to apply enabled flags.
                }
            }
        }

        private static List<StartupItem> ReadRegistryValues(
            RegistryHive hive,
            RegistryView view,
            string subKeyPath,
            StartupSource source)
        {
            var results = new List<StartupItem>();

            try
            {
                using var baseKey = RegistryKey.OpenBaseKey(hive, view);
                using var subKey = baseKey.OpenSubKey(subKeyPath, false);
                if (subKey is null) return results;

                foreach (var valueName in subKey.GetValueNames())
                {
                    var val = subKey.GetValue(valueName, null,
                        RegistryValueOptions.DoNotExpandEnvironmentNames);
                    if (val is null) continue;

                    results.Add(new StartupItem(
                        Name: valueName,
                        Command: ExpandIfNeeded(val.ToString() ?? ""),
                        Source: source,
                        Location: $@"{HiveName(hive)}\{subKeyPath} ({view})",
                        Enabled: null
                    ));
                }
            }
            catch
            {
                // ignore per-key errors
            }

            return results;
        }


        private static IEnumerable<RegistryView> GetViewsForHive(RegistryHive hive)
        {
            // HKCU is not really “32/64 separated” the same way for startup entries,
            // but OpenBaseKey requires a view, and using Default is fine.
            if (hive == RegistryHive.LocalMachine)
            {
                yield return RegistryView.Registry64;
                yield return RegistryView.Registry32;
            }
            else
            {
                yield return RegistryView.Default;
            }
        }

        private static string HiveName(RegistryHive hive) => hive switch
        {
            RegistryHive.LocalMachine => "HKLM",
            RegistryHive.CurrentUser => "HKCU",
            _ => hive.ToString()
        };

        private static IEnumerable<StartupItem> ReadStartupFolders()
        {
            var dirs = new[]
            {
                // Current user
                Environment.GetFolderPath(Environment.SpecialFolder.Startup),
                // All users
                Environment.GetFolderPath(Environment.SpecialFolder.CommonStartup),
            }
            .Where(d => !string.IsNullOrWhiteSpace(d))
            .Distinct(StringComparer.OrdinalIgnoreCase);

            foreach (var dir in dirs)
            {
                if (!Directory.Exists(dir)) continue;

                IEnumerable<string> files;
                try
                {
                    files = Directory.EnumerateFiles(dir, "*", SearchOption.TopDirectoryOnly);
                }
                catch
                {
                    continue;
                }

                foreach (var file in files)
                {
                    var ext = Path.GetExtension(file).ToLowerInvariant();
                    if (ext is not (".lnk" or ".exe" or ".bat" or ".cmd" or ".ps1" or ".vbs" or ".js"))
                        continue;

                    yield return new StartupItem(
                        Name: Path.GetFileNameWithoutExtension(file),
                        Command: file,
                        Source: StartupSource.StartupFolder,
                        Location: dir,
                        Enabled: true // If the file exists, it's effectively enabled
                    );
                }
            }
        }

        /// <summary>
        /// Enumerates scheduled tasks that have a LogonTrigger by calling schtasks.exe and parsing XML.
        /// No external packages required.
        /// </summary>
        private static IEnumerable<StartupItem> ReadScheduledTasks_LogonTriggers()
        {
            // NOTE: schtasks output/parsing can be localized; XML is the most stable route.
            // We query all tasks, then pull XML for each and detect logon triggers + actions.
            var taskNames = GetAllTaskNamesViaSchtasks().ToList();
            foreach (var taskName in taskNames)
            {
                var xml = QueryTaskXml(taskName);
                if (xml is null) continue;

                if (!TryParseTaskXml(xml, out var hasLogonTrigger, out var execCommands, out var enabled))
                    continue;

                if (!hasLogonTrigger) continue;

                var cmd = string.Join(" | ", execCommands.Where(s => !string.IsNullOrWhiteSpace(s)));
                if (string.IsNullOrWhiteSpace(cmd)) cmd = "(no Exec action)";

                yield return new StartupItem(
                    Name: taskName,
                    Command: cmd,
                    Source: StartupSource.ScheduledTask,
                    Location: @"Task Scheduler",
                    Enabled: enabled
                );
            }
        }

        private static IEnumerable<string> GetAllTaskNamesViaSchtasks()
        {
            // schtasks /Query /FO LIST /V yields "TaskName: \Microsoft\..." lines
            var output = RunProcessCaptureStdout("schtasks.exe", "/Query /FO LIST");
            if (string.IsNullOrWhiteSpace(output)) yield break;

            using var sr = new StringReader(output);
            string? line;
            while ((line = sr.ReadLine()) != null)
            {
                // English: "TaskName: \Some\Path"
                // We’ll accept "TaskName:" prefix only; for non-English systems this may miss tasks.
                const string prefix = "TaskName:";
                if (!line.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) continue;

                var name = line.Substring(prefix.Length).Trim();
                if (string.IsNullOrWhiteSpace(name)) continue;

                yield return name;
            }
        }

        private static string? QueryTaskXml(string taskName)
        {
            // schtasks /Query /TN "<name>" /XML
            // taskName often includes leading "\"; keep it.
            var args = $"/Query /TN \"{taskName}\" /XML";
            var xml = RunProcessCaptureStdout("schtasks.exe", args);
            if (string.IsNullOrWhiteSpace(xml)) return null;

            // Some tasks may write errors to stdout; quick sanity check:
            if (!xml.Contains("<Task", StringComparison.OrdinalIgnoreCase)) return null;

            return xml;
        }

        private static bool TryParseTaskXml(
            string xml,
            out bool hasLogonTrigger,
            out List<string> execCommands,
            out bool? enabled)
        {
            hasLogonTrigger = false;
            execCommands = new List<string>();
            enabled = null;

            try
            {
                var doc = XDocument.Parse(xml);

                // Namespaces vary; use local-name() style via LINQ to XML by matching Name.LocalName
                var triggers = doc.Descendants().Where(e => e.Name.LocalName == "Triggers");
                foreach (var t in triggers.Descendants())
                {
                    if (t.Name.LocalName == "LogonTrigger")
                    {
                        hasLogonTrigger = true;
                        break;
                    }
                }

                // Enabled: <Settings><Enabled>true/false</Enabled></Settings>
                var enabledEl = doc.Descendants().FirstOrDefault(e => e.Name.LocalName == "Enabled");
                if (enabledEl != null && bool.TryParse(enabledEl.Value.Trim(), out var b))
                    enabled = b;

                // Actions: <Actions><Exec><Command>...</Command><Arguments>...</Arguments></Exec></Actions>
                foreach (var exec in doc.Descendants().Where(e => e.Name.LocalName == "Exec"))
                {
                    var cmd = exec.Descendants().FirstOrDefault(e => e.Name.LocalName == "Command")?.Value?.Trim();
                    var args = exec.Descendants().FirstOrDefault(e => e.Name.LocalName == "Arguments")?.Value?.Trim();

                    if (!string.IsNullOrWhiteSpace(cmd) && !string.IsNullOrWhiteSpace(args))
                        execCommands.Add($"{cmd} {args}");
                    else if (!string.IsNullOrWhiteSpace(cmd))
                        execCommands.Add(cmd);
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

        private static string RunProcessCaptureStdout(string fileName, string arguments)
        {
            try
            {
                var psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = fileName,
                    Arguments = arguments,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    StandardOutputEncoding = Encoding.UTF8
                };

                using var p = System.Diagnostics.Process.Start(psi);
                if (p is null) return "";

                var stdout = p.StandardOutput.ReadToEnd();
                // You can also read stderr if you want diagnostics:
                // var stderr = p.StandardError.ReadToEnd();
                p.WaitForExit(5000);

                return stdout;
            }
            catch
            {
                return "";
            }
        }

        private static string ExpandIfNeeded(string s)
        {
            // Registry can store expandable strings; we read without expansion above, so expand here.
            try { return Environment.ExpandEnvironmentVariables(s); }
            catch { return s; }
        }
    }
}
