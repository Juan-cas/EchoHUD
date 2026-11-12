// SPDX-License-Identifier: GPL-3.0-only
//
// Autostart.cs - the optional "Start with Windows" setting.
//
// Windows starts every program listed under the current user's "Run" key
// when that user signs in. Switching the setting on writes ONE value there,
// named EchoHUD, holding the path of this program; switching it off deletes
// that value. Nothing else in the registry is read or written, no
// administrator rights are involved, and other users of the PC are not
// affected. You can see and remove the entry yourself in
// Task Manager -> Startup apps.

using System;
using System.Windows.Forms;
using Microsoft.Win32;

namespace EchoHud
{
    internal static class Autostart
    {
        const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        const string ValueName = "EchoHUD";

        /// <summary>Tells the program to start in the notification area without showing the panel.</summary>
        internal const string MinimizedArgument = "--minimized";

        /// <summary>The command line stored in the registry for a given program path.</summary>
        internal static string CommandFor(string programPath)
        {
            return "\"" + programPath + "\" " + MinimizedArgument;
        }

        /// <summary>
        /// True if the entry exists AND points at this copy of the program.
        /// If the program was moved since, this is false, and switching the
        /// setting on again repairs the entry.
        /// </summary>
        internal static bool IsEnabled
        {
            get
            {
                try
                {
                    using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RunKey))
                    {
                        string stored = key == null ? null : key.GetValue(ValueName) as string;
                        return String.Equals(stored, CommandFor(Application.ExecutablePath), StringComparison.OrdinalIgnoreCase);
                    }
                }
                catch (Exception)
                {
                    return false;
                }
            }
        }

        internal static void Set(bool enabled)
        {
            using (RegistryKey key = Registry.CurrentUser.CreateSubKey(RunKey))
            {
                if (enabled) key.SetValue(ValueName, CommandFor(Application.ExecutablePath));
                else key.DeleteValue(ValueName, false);
            }
        }
    }
}
