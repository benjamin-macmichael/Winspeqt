using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Winspeqt.Services
{
    public class getStartupPrograms
    {
        public void showStartupPrograms()  {
            System.Diagnostics.Debug.WriteLine("you in");
            List<string> startupProgramsRegistryNames_Enabled = new List<string>()
            {
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", //HKLM, HKCU
                @"SOFTWARE\Wow6432Node\Microsoft\Windows\CurrentVersion\Run",  //HKLM
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\RunOnce"
            };
            List<string> startupProgramsRegistryNames_Disabled = new List<string>()
            {
                @"SOFTWARE\Microsoft\Shared Tools\MSConfig\startupfolder",  //HKLM
                @"SOFTWARE\Microsoft\Shared Tools\MSConfig\startupreg"  //HKLM          
            };
            // Source - https://stackoverflow.com/q
            // Posted by User6996, modified by community. See post 'Timeline' for change history
            // Retrieved 2026-01-22, License - CC BY-SA 4.0

            foreach (string registryName in startupProgramsRegistryNames_Enabled.Concat(startupProgramsRegistryNames_Disabled))
            {
                RegistryKey HKCU = Registry.CurrentUser.OpenSubKey(registryName);
                if (HKCU is not null)
                {
                    foreach (string Programs in HKCU.GetValueNames())
                    {
                        string GetValue = HKCU.GetValue(Programs).ToString();
                        System.Diagnostics.Debug.WriteLine((GetValue)); //Environment.NewLine
                    }
                    HKCU.Close();
                }
            }

        
            

            System.Diagnostics.Debug.WriteLine("you out");
        }
    }
}
