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
[assembly: AssemblyVersion("1.0.0.0")]

namespace EchoHud
{
    internal static class Program
    {
        [STAThread]
        static void Main()
        {
            // Two copies would each show their own set of mirrors on top of
            // each other and fight over the same profile file.
            bool first;
            using (Mutex single = new Mutex(true, "EchoHUD-single-instance", out first))
            {
                if (!first) return;

                Native.EnablePixelAccurateCoordinates(); // must come before any window exists
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Theme.Initialize();
                Application.Run(new MainForm());
            }
        }
    }
}
