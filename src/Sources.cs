// SPDX-License-Identifier: GPL-3.0-only
//
// Sources.cs - finding the window a mirror takes its picture from.
//
// A "source" is a top-level window that Windows can thumbnail for us:
//
//   Tibia         the game window itself. Recognised ONLY by its window title
//                 and window class - public metadata every program can see.
//                 We never open, query or touch the game process.
//   ObsProjector  a "Windowed Projector" opened from OBS Studio. Useful as a
//                 fallback when the game window cannot be thumbnailed (for
//                 example exclusive fullscreen).
//   Demo          a fake HUD drawn by this program, for trying things out
//                 without the game running.

using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace EchoHud
{
    public enum SourceKind
    {
        Tibia,
        ObsProjector,
        Demo
    }

    /// <summary>One window that can currently be mirrored.</summary>
    internal sealed class SourceWindow
    {
        internal IntPtr Handle;
        internal SourceKind Kind;

        /// <summary>
        /// Text shown in the source list. For Tibia this is a generic label:
        /// the real title contains the character name, which we neither show
        /// nor store.
        /// </summary>
        internal string Label;

        public override string ToString()
        {
            return Label;
        }
    }

    /// <summary>Pure recognition rules, kept free of Windows calls so they can be unit tested.</summary>
    internal static class SourcePolicy
    {
        /// <summary>
        /// The Tibia client is a Qt application whose title is "Tibia" or
        /// "Tibia - Character Name". Requiring a Qt window class keeps
        /// browser tabs and folders named "Tibia" out of the list.
        /// </summary>
        internal static bool IsTibiaWindow(string title, string className)
        {
            if (String.IsNullOrEmpty(title) || String.IsNullOrEmpty(className)) return false;
            if (!className.StartsWith("Qt", StringComparison.Ordinal)) return false;
            return title == "Tibia" || title.StartsWith("Tibia - ", StringComparison.Ordinal);
        }

        /// <summary>OBS projector titles in the languages OBS ships with most often.</summary>
        internal static bool IsProjectorTitle(string title)
        {
            if (String.IsNullOrEmpty(title)) return false;
            foreach (string word in new[] { "Projector", "Proyector", "Projetor", "Projektor", "Projecteur" })
                if (title.IndexOf(word, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            return false;
        }

        internal static bool IsObsProcessName(string name)
        {
            return String.Equals(name, "obs64", StringComparison.OrdinalIgnoreCase)
                || String.Equals(name, "obs32", StringComparison.OrdinalIgnoreCase)
                || String.Equals(name, "obs", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Chooses which discovered window a profile should use.
        /// Order of preference: the window already in use, then a window of
        /// the wanted kind with the remembered label, then any window of the
        /// wanted kind. Returns null while the wanted source is not running.
        /// </summary>
        internal static SourceWindow Choose(List<SourceWindow> found, SourceKind wanted, string wantedLabel, IntPtr current)
        {
            SourceWindow sameKind = null, sameLabel = null;
            foreach (SourceWindow candidate in found)
            {
                if (candidate.Kind != wanted) continue;
                if (candidate.Handle == current && current != IntPtr.Zero) return candidate;
                if (sameKind == null) sameKind = candidate;
                if (sameLabel == null && candidate.Label == wantedLabel) sameLabel = candidate;
            }
            return sameLabel ?? sameKind;
        }
    }

    /// <summary>Lists the source windows that exist right now.</summary>
    internal static class Sources
    {
        internal static List<SourceWindow> Discover()
        {
            List<SourceWindow> found = new List<SourceWindow>();
            int tibiaCount = 0;

            Native.EnumWindows(delegate(IntPtr window, IntPtr unused)
            {
                if (!Native.IsWindowVisible(window)) return true;
                string title = Native.Title(window);

                if (SourcePolicy.IsTibiaWindow(title, Native.ClassName(window)))
                {
                    tibiaCount++;
                    found.Add(new SourceWindow
                    {
                        Handle = window,
                        Kind = SourceKind.Tibia,
                        Label = tibiaCount == 1 ? "Tibia" : "Tibia (" + tibiaCount + ")"
                    });
                }
                else if (SourcePolicy.IsProjectorTitle(title) && BelongsToObs(window))
                {
                    found.Add(new SourceWindow { Handle = window, Kind = SourceKind.ObsProjector, Label = "OBS: " + title });
                }
                return true;
            }, IntPtr.Zero);

            if (DemoWindow.IsOpen)
                found.Add(new SourceWindow { Handle = DemoWindow.Current.Handle, Kind = SourceKind.Demo, Label = Strings.Get("source.demo") });
            return found;
        }

        /// <summary>True while the window still exists and has something to show.</summary>
        internal static bool IsUsable(SourceWindow source)
        {
            return source != null && Native.IsWindow(source.Handle) && !Native.IsIconic(source.Handle);
        }

        /// <summary>
        /// Checks the owning program's name. Only ever called for windows whose
        /// title already says "Projector", so it is never applied to the game.
        /// The name comes from the system process list; no process is opened.
        /// </summary>
        static bool BelongsToObs(IntPtr window)
        {
            try
            {
                uint processId;
                Native.GetWindowThreadProcessId(window, out processId);
                using (Process process = Process.GetProcessById((int)processId))
                    return SourcePolicy.IsObsProcessName(process.ProcessName);
            }
            catch (Exception)
            {
                return false; // The window closed while we were looking, or access was denied.
            }
        }
    }
}
