// SPDX-License-Identifier: GPL-3.0-only
//
// Model.cs - the data a user's layout is made of, and nothing else.
//
// These classes are what gets written to disk (see Store.cs). They are
// public with public fields only because XmlSerializer requires it.
//
// Two coordinate spaces appear here - keep them apart:
//   * Crop*  : pixels inside the SOURCE window's client area.
//   * X, Y   : pixels on the DESKTOP, where the mirror window is placed.

using System;
using System.Collections.Generic;
using System.IO;

namespace EchoHud
{
    /// <summary>One mirrored rectangle: what to copy, and where and how to show it.</summary>
    public class MirrorRegion
    {
        public const int MinScale = 25, MaxScale = 800;
        public const int MinOpacity = 20, MaxOpacity = 100;

        public string Name = "";

        // What to show: a rectangle inside the source window...
        public int CropX, CropY, CropWidth = 64, CropHeight = 64;

        // ...and how big that source window was when the rectangle was drawn.
        // Needed to keep the crop meaningful if the source is later resized.
        public int SourceWidth = 1920, SourceHeight = 1080;

        // Where and how to show it.
        public int X = 100, Y = 100;
        public int ScalePercent = 100;
        public int OpacityPercent = 100;
        public bool Visible = true;

        /// <summary>Draw a thin highlight frame around the mirror even when locked.</summary>
        public bool Border;

        /// <summary>
        /// Name of the group this mirror belongs to, or "" for none. Holding
        /// Shift while dragging one member moves the whole group.
        /// </summary>
        public string Group = "";

        /// <summary>Throws if a value is outside what the program can safely display.</summary>
        public void Validate()
        {
            if (Name == null || Name.Length > 60) throw new InvalidDataException("Region name is missing or too long.");
            if (SourceWidth < 16 || SourceHeight < 16 || SourceWidth > 20000 || SourceHeight > 20000)
                throw new InvalidDataException("Region source size is out of range.");
            if (CropWidth < 1 || CropHeight < 1 || CropX < 0 || CropY < 0
                || (long)CropX + CropWidth > SourceWidth || (long)CropY + CropHeight > SourceHeight)
                throw new InvalidDataException("Region crop does not fit inside its source.");
            if (ScalePercent < MinScale || ScalePercent > MaxScale) throw new InvalidDataException("Region scale is out of range.");
            if (OpacityPercent < MinOpacity || OpacityPercent > MaxOpacity) throw new InvalidDataException("Region opacity is out of range.");
            if (Math.Abs((long)X) > 100000 || Math.Abs((long)Y) > 100000) throw new InvalidDataException("Region position is out of range.");
            if (!Groups.IsValidName(Group)) throw new InvalidDataException("Region group name is not valid.");
        }

        public MirrorRegion Clone()
        {
            return (MirrorRegion)MemberwiseClone();
        }
    }

    /// <summary>Rules for group names, shared by mirrors and timers.</summary>
    public static class Groups
    {
        public const int MaxNameLength = 40;

        /// <summary>"" means "no group"; otherwise a short single-line name.</summary>
        public static bool IsValidName(string name)
        {
            if (name == null || name.Length > MaxNameLength || name != name.Trim()) return false;
            foreach (char c in name) if (Char.IsControl(c)) return false;
            return true;
        }
    }

    /// <summary>How a timer is drawn.</summary>
    public enum TimerShape
    {
        Circle,
        Bar
    }

    /// <summary>
    /// A countdown the user starts by hand (hotkey, double-click or button),
    /// shown as a small circle. It knows nothing about the game: it is a
    /// kitchen timer that happens to float on screen.
    /// </summary>
    public class CooldownTimer
    {
        public const int MinSeconds = 1, MaxSeconds = 24 * 60 * 60;
        public const int MinSize = 40, MaxSize = 240;
        public const int ColourCount = 5;

        public string Name = "";
        public int Seconds = 600;

        // Where and how to show it (desktop pixels).
        public int X = 100, Y = 100;
        public int Size = 72;
        public int OpacityPercent = 100;
        public bool Visible = true;

        /// <summary>Play a sound when the countdown reaches zero.</summary>
        public bool Sound = true;

        /// <summary>
        /// Full path of a .wav file on this computer to play instead of the
        /// standard Windows sound; "" for the standard sound. Removed when a
        /// profile is exported, because paths often contain a user name.
        /// </summary>
        public string SoundFile = "";

        public TimerShape Shape = TimerShape.Circle;

        /// <summary>Which of the built-in colours to draw with (0 to ColourCount - 1).</summary>
        public int Colour;

        /// <summary>After reaching zero, show how long ago that was ("+0:42").</summary>
        public bool CountUp = true;

        /// <summary>Group name, or "" for none. See <see cref="MirrorRegion.Group"/>.</summary>
        public string Group = "";

        /// <summary>
        /// Key combination that starts the timer, stored as a
        /// System.Windows.Forms.Keys value (key plus modifier bits); 0 = none.
        /// </summary>
        public int Hotkey;

        public void Validate()
        {
            if (Name == null || Name.Length > 60) throw new InvalidDataException("Timer name is missing or too long.");
            if (Seconds < MinSeconds || Seconds > MaxSeconds) throw new InvalidDataException("Timer duration is out of range.");
            if (Size < MinSize || Size > MaxSize) throw new InvalidDataException("Timer size is out of range.");
            if (OpacityPercent < MirrorRegion.MinOpacity || OpacityPercent > MirrorRegion.MaxOpacity) throw new InvalidDataException("Timer opacity is out of range.");
            if (Math.Abs((long)X) > 100000 || Math.Abs((long)Y) > 100000) throw new InvalidDataException("Timer position is out of range.");
            if (Hotkey < 0) throw new InvalidDataException("Timer hotkey is not valid.");
            if (SoundFile == null || SoundFile.Length > 260) throw new InvalidDataException("Timer sound file is not valid.");
            if (!Enum.IsDefined(typeof(TimerShape), Shape)) throw new InvalidDataException("Timer shape is not valid.");
            if (Colour < 0 || Colour >= ColourCount) throw new InvalidDataException("Timer colour is not valid.");
            if (!Groups.IsValidName(Group)) throw new InvalidDataException("Timer group name is not valid.");
        }

        /// <summary>
        /// Only a .wav file on a local drive is ever played. This matters
        /// because profiles can come from other people: a web address or a
        /// network share here must never make the program reach out.
        /// </summary>
        public static bool IsPlayableSoundPath(string path)
        {
            if (String.IsNullOrEmpty(path) || path.Length > 260 || path.Length < 8) return false;
            if (!Char.IsLetter(path[0]) || path[1] != ':' || path[2] != '\\') return false; // "C:\..." only: no shares, no URLs
            if (path.IndexOfAny(Path.GetInvalidPathChars()) >= 0 || path.IndexOf(':', 2) >= 0) return false;
            return path.EndsWith(".wav", StringComparison.OrdinalIgnoreCase);
        }
    }

    /// <summary>
    /// Preferences that belong to this computer rather than to one profile.
    /// Stored in settings.xml next to the profiles.
    /// </summary>
    public class Settings
    {
        public static readonly string[] Languages = { "", "en", "es", "pt", "pl" };

        /// <summary>Show mirrors and timers only while the game (or Echo HUD) is the window in front.</summary>
        public bool OnlyWithGame;

        /// <summary>Mirrors click into place next to each other and at screen edges while dragged.</summary>
        public bool Snap = true;

        /// <summary>Minimising the panel hides it in the notification area instead of the taskbar.</summary>
        public bool MinimizeToTray = true;

        /// <summary>Hotkeys for lock/unlock and show/hide, stored like <see cref="CooldownTimer.Hotkey"/>; 0 = none.</summary>
        public int LockHotkey, ShowHotkey;

        /// <summary>Two-letter language code, or "" to follow Windows.</summary>
        public string Language = "";

        public void Validate()
        {
            if (LockHotkey < 0 || ShowHotkey < 0) throw new InvalidDataException("A hotkey setting is not valid.");
            if (Language == null || Array.IndexOf(Languages, Language) < 0) throw new InvalidDataException("The language setting is not valid.");
        }
    }

    /// <summary>Turning durations into text and back, e.g. 600 seconds and "10:00".</summary>
    public static class Durations
    {
        /// <summary>600 becomes "10:00", 45 becomes "0:45", 3725 becomes "1:02:05".</summary>
        public static string Format(int seconds)
        {
            if (seconds < 0) seconds = 0;
            int hours = seconds / 3600, minutes = seconds / 60 % 60, rest = seconds % 60;
            return hours > 0
                ? hours + ":" + minutes.ToString("00") + ":" + rest.ToString("00")
                : minutes + ":" + rest.ToString("00");
        }

        /// <summary>
        /// Accepts "10" (minutes), "10:30" (minutes:seconds) or "1:00:00"
        /// (hours:minutes:seconds). Returns false for anything else or for a
        /// result outside the allowed range.
        /// </summary>
        public static bool TryParse(string text, out int seconds)
        {
            seconds = 0;
            if (text == null || text.Trim().Length == 0) return false;
            string[] parts = text.Trim().Split(':');
            if (parts.Length > 3) return false;
            long total = 0;
            foreach (string part in parts)
            {
                int value;
                if (!Int32.TryParse(part, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out value)) return false;
                if (value > 100000) return false;
                total = total * 60 + value;
            }
            if (parts.Length == 1) total *= 60; // a bare number means minutes
            if (total < CooldownTimer.MinSeconds || total > CooldownTimer.MaxSeconds) return false;
            seconds = (int)total;
            return true;
        }
    }

    /// <summary>A named layout: which source to mirror and the regions taken from it.</summary>
    public class Profile
    {
        public const int MaxRegions = 32, MaxTimers = 12;

        public int Version = 1;
        public string Name = "Default";
        public SourceKind Source = SourceKind.Tibia;

        /// <summary>
        /// Remembered list label, used to find the same OBS projector again.
        /// For Tibia this is always the generic "Tibia" label, never a character name.
        /// </summary>
        public string SourceLabel = "";

        /// <summary>Locked mirrors cannot be moved and let mouse clicks pass through.</summary>
        public bool Locked;

        /// <summary>Master switch for showing or hiding every mirror at once.</summary>
        public bool MirrorsVisible = true;

        public List<MirrorRegion> Regions = new List<MirrorRegion>();
        public List<CooldownTimer> Timers = new List<CooldownTimer>();

        /// <summary>Every group name in use, alphabetically.</summary>
        public List<string> GroupNames()
        {
            List<string> names = new List<string>();
            foreach (MirrorRegion region in Regions)
                if (region.Group.Length > 0 && !names.Contains(region.Group)) names.Add(region.Group);
            foreach (CooldownTimer timer in Timers)
                if (timer.Group.Length > 0 && !names.Contains(timer.Group)) names.Add(timer.Group);
            names.Sort(StringComparer.CurrentCultureIgnoreCase);
            return names;
        }

        public void Validate()
        {
            if (!Store.IsValidName(Name)) throw new InvalidDataException("Profile name is not valid.");
            if (SourceLabel == null || SourceLabel.Length > 300) throw new InvalidDataException("Profile source label is not valid.");
            if (!Enum.IsDefined(typeof(SourceKind), Source)) throw new InvalidDataException("Profile source kind is not valid.");
            if (Regions == null || Regions.Count > MaxRegions) throw new InvalidDataException("Profile has too many regions.");
            foreach (MirrorRegion region in Regions)
            {
                if (region == null) throw new InvalidDataException("Profile contains an empty region.");
                region.Validate();
            }
            if (Timers == null || Timers.Count > MaxTimers) throw new InvalidDataException("Profile has too many timers.");
            foreach (CooldownTimer timer in Timers)
            {
                if (timer == null) throw new InvalidDataException("Profile contains an empty timer.");
                timer.Validate();
            }
        }
    }
}
