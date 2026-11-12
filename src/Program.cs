// SPDX-License-Identifier: GPL-3.0-only
//
// Program.cs - where the application starts.

using System;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;

[assembly: AssemblyTitle("Echo HUD")]
[assembly: AssemblyDescription("Floating live mirrors of parts of a window, using Windows DWM thumbnails.")]
[assembly: AssemblyProduct("Echo HUD")]
[assembly: AssemblyCopyright("GPL-3.0-only")]
[assembly: AssemblyVersion("1.2.0.0")]

namespace EchoHud
{
    internal static class Program
    {
        [STAThread]
        static void Main(string[] arguments)
        {
            // Two copies would each show their own set of mirrors on top of
            // each other and fight over the same profile file. Starting the
            // program again instead tells the running copy to show its panel
            // (it may be hidden in the notification area). The "wake" signal
            // is a named Windows event: a flag both copies can see, carrying
            // no data.
            bool first;
            using (Mutex single = new Mutex(true, "EchoHUD-single-instance", out first))
            using (EventWaitHandle wake = new EventWaitHandle(false, EventResetMode.AutoReset, "EchoHUD-show-panel"))
            {
                if (!first)
                {
                    wake.Set();
                    return;
                }

                Native.EnablePixelAccurateCoordinates(); // must come before any window exists
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Theme.Initialize();
                Strings.Use(Store.LoadSettings().Language); // "" follows Windows
                // "--minimized" is what the "Start with Windows" setting puts on the command line.
                bool hidden = Array.IndexOf(arguments, Autostart.MinimizedArgument) >= 0;
                Application.Run(new MainForm(hidden, wake));
            }
        }
    }
}
