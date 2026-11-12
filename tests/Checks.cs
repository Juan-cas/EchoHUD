// SPDX-License-Identifier: GPL-3.0-only
//
// Checks.cs - the automated tests.
//
// There is no test framework to install: this file is compiled together with
// the application sources into a small console program (scripts/test.ps1).
// It covers the logic that can be verified without a screen: geometry, source
// recognition, profile validation and storage, and translations. What needs
// eyes - the windows themselves - is listed in docs/TESTING.md.
//
// To add a test: write a static method that calls Check(...) and call it
// from Main.

using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using EchoHud;

internal static class Checks
{
    static int passed, failed;

    static int Main()
    {
        string scratch = Path.Combine(Path.GetTempPath(), "EchoHUD-tests-" + Guid.NewGuid().ToString("N"));
        Environment.SetEnvironmentVariable("ECHOHUD_DATA_DIR", scratch); // never touch real profiles
        try
        {
            GeometryChecks();
            SourceChecks();
            ModelChecks();
            StoreChecks();
            StringChecks();
            TimerChecks();
            LayoutChecks();
            SettingsChecks();
        }
        finally
        {
            if (Directory.Exists(scratch)) Directory.Delete(scratch, true);
        }
        Console.WriteLine(passed + " checks passed, " + failed + " failed.");
        return failed == 0 ? 0 : 1;
    }

    static void Check(bool condition, string what)
    {
        if (condition) { passed++; return; }
        failed++;
        Console.WriteLine("FAILED: " + what);
    }

    static void Throws<T>(Action action, string what) where T : Exception
    {
        try { action(); }
        catch (T) { passed++; return; }
        catch (Exception other) { failed++; Console.WriteLine("FAILED: " + what + " (threw " + other.GetType().Name + ")"); return; }
        failed++;
        Console.WriteLine("FAILED: " + what + " (did not throw)");
    }

    static MirrorRegion Region(int x, int y, int width, int height, int sourceWidth, int sourceHeight)
    {
        return new MirrorRegion { Name = "r", CropX = x, CropY = y, CropWidth = width, CropHeight = height, SourceWidth = sourceWidth, SourceHeight = sourceHeight };
    }

    static void GeometryChecks()
    {
        Size source = new Size(1000, 500);
        Check(Geometry.FromDrag(new Point(10, 20), new Point(110, 70), source) == new Rectangle(10, 20, 100, 50), "drag down-right");
        Check(Geometry.FromDrag(new Point(110, 70), new Point(10, 20), source) == new Rectangle(10, 20, 100, 50), "drag up-left gives the same rectangle");
        Check(Geometry.FromDrag(new Point(-50, -50), new Point(2000, 2000), source) == new Rectangle(0, 0, 1000, 500), "drag is clipped to the source");
        Check(Geometry.FromDrag(new Point(5, 5), new Point(5, 5), source).Size == Size.Empty, "a click without movement has no size");

        MirrorRegion region = Region(100, 50, 200, 100, 1000, 500);
        Check(Geometry.CurrentCrop(region, new Size(1000, 500), true) == new Rectangle(100, 50, 200, 100), "same size: crop unchanged (stretching source)");
        Check(Geometry.CurrentCrop(region, new Size(1000, 500), false) == new Rectangle(100, 50, 200, 100), "same size: crop unchanged (re-flowing source)");
        Check(Geometry.CurrentCrop(region, new Size(2000, 1000), true) == new Rectangle(200, 100, 400, 200), "stretching source doubles the crop");
        Check(Geometry.CurrentCrop(region, new Size(2000, 1000), false) == new Rectangle(100, 50, 200, 100), "re-flowing source keeps pixel position");
        Check(Geometry.CurrentCrop(region, new Size(250, 120), false) == new Rectangle(50, 20, 200, 100), "re-flowing source pushes the crop back inside");
        Check(Geometry.CurrentCrop(region, new Size(150, 80), false) == new Rectangle(0, 0, 150, 80), "crop shrinks to a smaller source");
        Check(Geometry.CurrentCrop(region, Size.Empty, true).IsEmpty, "no source size gives an empty crop");
        Rectangle tiny = Geometry.CurrentCrop(Region(999, 499, 1, 1, 1000, 500), new Size(100, 50), true);
        Check(tiny.Width >= 1 && tiny.Height >= 1, "a stretched crop never collapses to nothing");

        Check(Geometry.Output(new Size(100, 40), 200) == new Size(200, 80), "200 % doubles the picture");
        Check(Geometry.Output(new Size(2, 2), 25) == new Size(Geometry.MinSide, Geometry.MinSide), "picture never smaller than the minimum");
        Check(Geometry.Output(new Size(3000, 3000), 800) == new Size(Geometry.MaxSide, Geometry.MaxSide), "picture never larger than the maximum");
        Check(Geometry.ScaleForWidth(300, 100) == 300, "width 3x the crop is 300 %");
        Check(Geometry.ScaleForWidth(1, 1000) == MirrorRegion.MinScale, "scale has a lower limit");
        Check(Geometry.ScaleForWidth(100000, 10) == MirrorRegion.MaxScale, "scale has an upper limit");
        Check(Geometry.ScaleForWidth(100, 0) == 100, "empty crop falls back to 100 %");

        Check(Geometry.Fit(new Size(200, 100), new Rectangle(0, 0, 400, 400)) == new Rectangle(0, 100, 400, 200), "wide content is letterboxed");
        Check(Geometry.Fit(new Size(100, 200), new Rectangle(10, 10, 400, 400)) == new Rectangle(110, 10, 200, 400), "tall content is pillarboxed, offset kept");
        Check(Geometry.Fit(Size.Empty, new Rectangle(0, 0, 10, 10)).IsEmpty, "empty content fits nowhere");
        Check(Geometry.Clamp(5, 10, 0) == 10, "clamp with an inverted range returns the minimum");
    }

    static void SourceChecks()
    {
        Check(SourcePolicy.IsTibiaWindow("Tibia", "Qt5QWindowOwnDCIcon"), "bare Tibia title");
        Check(SourcePolicy.IsTibiaWindow("Tibia - Some Character", "Qt6QWindowIcon"), "Tibia title with a character");
        Check(!SourcePolicy.IsTibiaWindow("Tibia - Free Online Game - Browser", "Chrome_WidgetWin_1"), "a browser tab about Tibia is not the game");
        Check(!SourcePolicy.IsTibiaWindow("TibiaVision", "Qt5QWindowIcon"), "a different program whose name starts with Tibia");
        Check(!SourcePolicy.IsTibiaWindow("My Tibia notes", "Qt5QWindowIcon"), "title must start with Tibia");
        Check(!SourcePolicy.IsTibiaWindow("", "Qt5QWindowIcon") && !SourcePolicy.IsTibiaWindow("Tibia", null), "missing metadata is rejected");

        Check(SourcePolicy.IsProjectorTitle("Windowed Projector (Preview)"), "English projector");
        Check(SourcePolicy.IsProjectorTitle("Proyector en ventana (Escena)"), "Spanish projector");
        Check(SourcePolicy.IsProjectorTitle("Projetor em janela"), "Portuguese projector");
        Check(SourcePolicy.IsProjectorTitle("Projektor w oknie"), "Polish projector");
        Check(!SourcePolicy.IsProjectorTitle("OBS 31.0.0 - Profile: Untitled"), "the OBS main window is not a projector");
        Check(SourcePolicy.IsObsProcessName("OBS64") && !SourcePolicy.IsObsProcessName("client"), "OBS process names only");

        SourceWindow tibiaOne = new SourceWindow { Handle = new IntPtr(1), Kind = SourceKind.Tibia, Label = "Tibia" };
        SourceWindow tibiaTwo = new SourceWindow { Handle = new IntPtr(2), Kind = SourceKind.Tibia, Label = "Tibia (2)" };
        SourceWindow projector = new SourceWindow { Handle = new IntPtr(3), Kind = SourceKind.ObsProjector, Label = "OBS: Projector" };
        List<SourceWindow> found = new List<SourceWindow> { tibiaOne, tibiaTwo, projector };
        Check(SourcePolicy.Choose(found, SourceKind.Tibia, "", IntPtr.Zero) == tibiaOne, "first window of the wanted kind by default");
        Check(SourcePolicy.Choose(found, SourceKind.Tibia, "Tibia (2)", IntPtr.Zero) == tibiaTwo, "remembered label wins");
        Check(SourcePolicy.Choose(found, SourceKind.Tibia, "Tibia", new IntPtr(2)) == tibiaTwo, "window already in use wins over the label");
        Check(SourcePolicy.Choose(found, SourceKind.ObsProjector, "something else", IntPtr.Zero) == projector, "falls back to any window of the kind");
        Check(SourcePolicy.Choose(found, SourceKind.Demo, "", IntPtr.Zero) == null, "nothing when the wanted kind is not running");
        Check(SourcePolicy.Choose(found, SourceKind.ObsProjector, "", new IntPtr(1)) == projector, "never returns a window of another kind");
    }

    static void ModelChecks()
    {
        Region(0, 0, 10, 10, 100, 100).Validate();
        passed++;
        Throws<InvalidDataException>(delegate { Region(95, 0, 10, 10, 100, 100).Validate(); }, "crop past the right edge");
        Throws<InvalidDataException>(delegate { Region(-1, 0, 10, 10, 100, 100).Validate(); }, "negative crop origin");
        Throws<InvalidDataException>(delegate { Region(0, 0, 0, 10, 100, 100).Validate(); }, "zero-width crop");
        Throws<InvalidDataException>(delegate { Region(0, 0, 10, 10, 8, 8).Validate(); }, "implausibly small source");
        Throws<InvalidDataException>(delegate { MirrorRegion r = Region(0, 0, 10, 10, 100, 100); r.ScalePercent = 5000; r.Validate(); }, "scale out of range");
        Throws<InvalidDataException>(delegate { MirrorRegion r = Region(0, 0, 10, 10, 100, 100); r.OpacityPercent = 0; r.Validate(); }, "fully transparent mirror");
        Throws<InvalidDataException>(delegate { MirrorRegion r = Region(0, 0, 10, 10, 100, 100); r.Name = null; r.Validate(); }, "missing name");
        Throws<InvalidDataException>(delegate { MirrorRegion r = Region(0, 0, 10, 10, 100, 100); r.X = Int32.MaxValue; r.Validate(); }, "position off the charts");

        MirrorRegion original = Region(1, 2, 3, 4, 100, 100);
        MirrorRegion copy = original.Clone();
        copy.CropX = 50;
        Check(original.CropX == 1 && copy.CropY == 2, "clone is independent");

        Profile crowded = new Profile();
        for (int i = 0; i <= Profile.MaxRegions; i++) crowded.Regions.Add(Region(0, 0, 10, 10, 100, 100));
        Throws<InvalidDataException>(delegate { crowded.Validate(); }, "too many regions");
        Throws<InvalidDataException>(delegate { new Profile { Name = "..\\evil" }.Validate(); }, "profile name with a path in it");
    }

    static void StoreChecks()
    {
        Check(Store.IsValidName("Knight 200_EK-v2"), "ordinary profile name");
        Check(Store.IsValidName("Paladín"), "accented letters are allowed");
        foreach (string bad in new[] { "", " lead", "trail ", "a/b", "a\\b", "..", "a.b", "con", "NUL", new string('x', 41), "a:b", "a*b" })
            Check(!Store.IsValidName(bad), "rejected profile name: \"" + bad + "\"");
        Check(!Store.IsValidName(null), "null profile name");

        Check(Store.ListProfiles().Count == 0 && Store.LastProfile == null, "fresh data folder is empty");

        Profile profile = new Profile { Name = "Round trip", Source = SourceKind.ObsProjector, SourceLabel = "OBS: Projector", Locked = true, MirrorsVisible = false };
        MirrorRegion region = Region(10, 20, 30, 40, 800, 600);
        region.Name = "Cooldowns <&> \"quoted\"";
        region.X = -1200;
        region.Y = 345;
        region.ScalePercent = 250;
        region.OpacityPercent = 60;
        region.Visible = false;
        region.Border = true;
        profile.Regions.Add(region);
        Store.Save(profile);
        Store.Save(profile); // second save exercises the replace-existing path
        Check(Store.Exists("Round trip") && Store.ListProfiles().Count == 1, "saved profile is listed once");
        Check(!File.Exists(Path.Combine(Store.DataRoot, "profiles", "Round trip.xml.tmp")), "no temporary file left behind");

        Profile loaded = Store.Load("Round trip");
        MirrorRegion back = loaded.Regions[0];
        Check(loaded.Source == SourceKind.ObsProjector && loaded.SourceLabel == "OBS: Projector" && loaded.Locked && !loaded.MirrorsVisible, "profile fields survive a round trip");
        Check(back.Name == region.Name && back.CropX == 10 && back.CropY == 20 && back.CropWidth == 30 && back.CropHeight == 40, "crop survives a round trip");
        Check(back.SourceWidth == 800 && back.SourceHeight == 600 && back.X == -1200 && back.Y == 345, "geometry survives a round trip");
        Check(back.ScalePercent == 250 && back.OpacityPercent == 60 && !back.Visible && back.Border, "appearance survives a round trip");

        Store.LastProfile = "Round trip";
        Check(Store.LastProfile == "Round trip", "last profile is remembered");
        Store.LastProfile = "..\\..\\somewhere";
        Check(Store.LastProfile == null, "an invalid remembered name is ignored");

        Throws<InvalidDataException>(delegate { Store.Parse("this is not xml"); }, "garbage instead of XML");
        Throws<InvalidDataException>(delegate { Store.Parse("<!DOCTYPE x [<!ENTITY e SYSTEM \"file:///c:/windows/win.ini\">]><Profile><Name>&e;</Name></Profile>"); }, "XML with a DTD is refused");
        Throws<InvalidDataException>(delegate { Store.Parse("<Profile><Name>ok</Name><Regions><MirrorRegion><Name>r</Name><CropWidth>0</CropWidth></MirrorRegion></Regions></Profile>"); }, "invalid region inside a file");
        Throws<InvalidDataException>(delegate { Store.Parse("<Profile><Name>ok</Name><Source>Nonsense</Source></Profile>"); }, "unknown source kind");
        Check(Store.Parse("<Profile><Name>Old file</Name></Profile>").Regions.Count == 0, "a minimal file loads with defaults");
        Throws<InvalidDataException>(delegate { Store.Load("../Round trip"); }, "loading by a path instead of a name");

        Store.Delete("Round trip");
        Check(!Store.Exists("Round trip") && Store.ListProfiles().Count == 0, "deleted profile is gone");
        Store.Delete("Round trip");
        passed++; // deleting twice must not throw
    }

    static void StringChecks()
    {
        foreach (KeyValuePair<string, Dictionary<string, string>> language in Strings.All)
        {
            Dictionary<string, string> table = language.Value;
            foreach (string key in Strings.English.Keys)
                Check(table.ContainsKey(key), language.Key + " is missing \"" + key + "\"");
            foreach (string key in table.Keys)
                Check(Strings.English.ContainsKey(key), language.Key + " has an unknown key \"" + key + "\"");
            foreach (KeyValuePair<string, string> pair in Strings.English)
            {
                string other = table.ContainsKey(pair.Key) ? table[pair.Key] : "";
                Check(pair.Value.Contains("{0}") == other.Contains("{0}") && pair.Value.Contains("{1}") == other.Contains("{1}"),
                    "placeholders differ in " + language.Key + " for \"" + pair.Key + "\"");
                Check(other.Trim().Length > 0, language.Key + " has an empty text for \"" + pair.Key + "\"");
            }
            Check(Array.IndexOf(Settings.Languages, language.Key) >= 0, language.Key + " can be chosen in the settings");
        }
        Strings.Use("es");
        Check(Strings.Get("common.cancel") == "Cancelar", "Spanish can be selected");
        Strings.Use("pt");
        Check(Strings.Get("row.delete") == "Excluir", "Portuguese can be selected");
        Strings.Use("pl");
        Check(Strings.Get("common.cancel") == "Anuluj", "Polish can be selected");
        Strings.Use("zz");
        Check(Strings.Get("common.cancel") == "Cancel", "unknown languages fall back to English");
        Check(Strings.Get("no.such.key") == "no.such.key", "an unknown key is shown as itself");
        Check(Strings.Format("list.count", 2, 32) == "2 / 32", "formatting fills placeholders");
    }

    static void TimerChecks()
    {
        Check(Durations.Format(600) == "10:00", "ten minutes");
        Check(Durations.Format(45) == "0:45", "under a minute");
        Check(Durations.Format(3725) == "1:02:05", "over an hour");
        Check(Durations.Format(-5) == "0:00", "negative durations show as zero");

        int seconds;
        Check(Durations.TryParse("10", out seconds) && seconds == 600, "a bare number means minutes");
        Check(Durations.TryParse("10:30", out seconds) && seconds == 630, "minutes and seconds");
        Check(Durations.TryParse(" 1:00:00 ", out seconds) && seconds == 3600, "hours, with stray spaces");
        Check(Durations.TryParse("0:05", out seconds) && seconds == 5, "a few seconds");
        foreach (string bad in new[] { "", "  ", "abc", "1:2:3:4", "-5", "1.5", "0", "0:00", "25:00:00", "10:", "999999999" })
            Check(!Durations.TryParse(bad, out seconds), "rejected duration: \"" + bad + "\"");
        Check(!Durations.TryParse(null, out seconds), "null duration");
        for (int value = 1; value <= CooldownTimer.MaxSeconds; value += 977)
        {
            int back;
            Check(Durations.TryParse(Durations.Format(value), out back) && back == value, "format/parse round trip for " + value);
        }

        uint modifiers, key;
        Check(Hotkeys.Split((int)(Keys.Control | Keys.F5), out modifiers, out key) && modifiers == Native.ModControl && key == (uint)Keys.F5, "Ctrl+F5 splits into modifier and key");
        Check(Hotkeys.Split((int)(Keys.Control | Keys.Alt | Keys.Shift | Keys.A), out modifiers, out key) && modifiers == (Native.ModControl | Native.ModAlt | Native.ModShift), "all three modifiers");
        Check(Hotkeys.Split((int)Keys.F9, out modifiers, out key) && modifiers == 0, "a bare key is allowed");
        Check(!Hotkeys.Split(0, out modifiers, out key), "zero means no hotkey");
        Check(!Hotkeys.Split((int)(Keys.Control | Keys.ControlKey), out modifiers, out key), "a modifier on its own is not a hotkey");
        Check(!Hotkeys.Split((int)(Keys.Shift | Keys.ShiftKey), out modifiers, out key), "Shift on its own is not a hotkey");
        Check(Hotkeys.Describe((int)(Keys.Control | Keys.Shift | Keys.F5)) == "Ctrl+Shift+F5", "hotkey text");
        Check(Hotkeys.Describe((int)(Keys.Alt | Keys.D1)) == "Alt+1", "digit keys read as digits");
        Check(Hotkeys.Describe((int)Keys.NumPad7) == "Num 7", "numeric keypad keys");
        Check(Hotkeys.Describe(0) == "", "no hotkey has no text");

        new CooldownTimer { Name = "Potion" }.Validate();
        passed++;
        Throws<InvalidDataException>(delegate { new CooldownTimer { Name = "t", Seconds = 0 }.Validate(); }, "zero-length timer");
        Throws<InvalidDataException>(delegate { new CooldownTimer { Name = "t", Seconds = CooldownTimer.MaxSeconds + 1 }.Validate(); }, "timer longer than a day");
        Throws<InvalidDataException>(delegate { new CooldownTimer { Name = "t", Size = 5 }.Validate(); }, "microscopic timer");
        Throws<InvalidDataException>(delegate { new CooldownTimer { Name = "t", OpacityPercent = 0 }.Validate(); }, "invisible timer");
        Throws<InvalidDataException>(delegate { new CooldownTimer { Name = null }.Validate(); }, "timer without a name");
        Throws<InvalidDataException>(delegate { new CooldownTimer { Name = "t", Hotkey = -3 }.Validate(); }, "negative hotkey value");

        Profile crowded = new Profile();
        for (int i = 0; i <= Profile.MaxTimers; i++) crowded.Timers.Add(new CooldownTimer { Name = "t" });
        Throws<InvalidDataException>(delegate { crowded.Validate(); }, "too many timers");

        Profile profile = new Profile { Name = "Timers" };
        profile.Timers.Add(new CooldownTimer { Name = "Potion", Seconds = 600, X = -40, Y = 77, Size = 96, OpacityPercent = 80, Sound = false, Visible = false, Hotkey = (int)(Keys.Control | Keys.F5) });
        Store.Save(profile);
        CooldownTimer back2 = Store.Load("Timers").Timers[0];
        Check(back2.Name == "Potion" && back2.Seconds == 600 && back2.X == -40 && back2.Y == 77, "timer basics survive a round trip");
        Check(back2.Size == 96 && back2.OpacityPercent == 80 && !back2.Sound && !back2.Visible && back2.Hotkey == (int)(Keys.Control | Keys.F5), "timer options survive a round trip");
        Store.Delete("Timers");
        Check(Store.Parse("<Profile><Name>Old file</Name></Profile>").Timers.Count == 0, "a file from before timers existed loads with none");
        Throws<InvalidDataException>(delegate { Store.Parse("<Profile><Name>ok</Name><Timers><CooldownTimer><Name>t</Name><Seconds>0</Seconds></CooldownTimer></Timers></Profile>"); }, "invalid timer inside a file");
    }

    static void LayoutChecks()
    {
        // ---- arrow-key fine tuning
        MirrorRegion region = Region(100, 50, 200, 100, 1000, 500);
        Geometry.Nudge(region, 1, -1, false);
        Check(region.CropX == 101 && region.CropY == 49 && region.CropWidth == 200 && region.CropHeight == 100, "nudging moves the crop and keeps its size");
        Geometry.Nudge(region, 1, 2, true);
        Check(region.CropX == 101 && region.CropWidth == 201 && region.CropHeight == 102, "nudging with resize changes only the size");
        region = Region(0, 0, 10, 10, 1000, 500);
        Geometry.Nudge(region, -5, -5, false);
        Check(region.CropX == 0 && region.CropY == 0, "the crop cannot leave the source at the top left");
        region = Region(990, 490, 10, 10, 1000, 500);
        Geometry.Nudge(region, 5, 5, false);
        Check(region.CropX == 990 && region.CropY == 490, "the crop cannot leave the source at the bottom right");
        Geometry.Nudge(region, 5, 5, true);
        Check(region.CropWidth == 10 && region.CropHeight == 10, "the crop cannot grow past the source");
        Geometry.Nudge(region, -50, -50, true);
        Check(region.CropWidth == 1 && region.CropHeight == 1, "the crop cannot shrink below one pixel");
        region.Validate();
        passed++;

        // ---- repeating the spacing of selected areas
        Size source = new Size(400, 300);
        Rectangle first = new Rectangle(10, 20, 100, 8), second = new Rectangle(10, 46, 100, 8);
        Check(Geometry.NextInSeries(first, second, source) == new Rectangle(10, 72, 100, 8), "the next area keeps the spacing of the last two");
        Check(Geometry.NextInSeries(null, first, source) == new Rectangle(10, 28, 100, 8), "with one area the next sits directly below");
        Check(Geometry.NextInSeries(new Rectangle(10, 10, 50, 8), new Rectangle(80, 10, 50, 8), source) == new Rectangle(150, 10, 50, 8), "a series can run sideways");
        Check(Geometry.NextInSeries(new Rectangle(10, 200, 100, 8), new Rectangle(10, 280, 100, 8), source).IsEmpty, "no area is added past the bottom edge");
        Check(Geometry.NextInSeries(second, first, source).IsEmpty, "no area is added past the top edge");
        Check(Geometry.NextInSeries(first, first, source).IsEmpty, "two identical areas define no direction");

        // ---- snapping
        List<Rectangle> others = new List<Rectangle> { new Rectangle(100, 100, 200, 20) };
        Check(Geometry.Snap(new Rectangle(103, 123, 200, 20), others, 8) == new Point(100, 120), "snaps under a neighbour and lines up its left edge");
        Check(Geometry.Snap(new Rectangle(305, 96, 50, 20), others, 8) == new Point(300, 100), "snaps to the right of a neighbour and lines up its top");
        Check(Geometry.Snap(new Rectangle(140, 160, 50, 20), others, 8) == new Point(140, 160), "a mirror that is not near anything stays put");
        Check(Geometry.Snap(new Rectangle(103, 400, 200, 20), others, 8) == new Point(103, 400), "a far-away neighbour does not pull");
        Check(Geometry.Snap(new Rectangle(109, 129, 200, 20), others, 8) == new Point(109, 129), "outside the threshold nothing snaps");
        Check(Geometry.Snap(new Rectangle(103, 123, 200, 20), new List<Rectangle>(), 8) == new Point(103, 123), "nothing to snap to");
        List<Rectangle> screen = new List<Rectangle> { new Rectangle(0, 0, 1920, 1040) };
        Check(Geometry.Snap(new Rectangle(5, 500, 100, 50), screen, 8) == new Point(0, 500), "snaps to the left screen edge");
        Check(Geometry.Snap(new Rectangle(1816, 987, 100, 50), screen, 8) == new Point(1820, 990), "snaps into the bottom-right screen corner");
        List<Rectangle> two = new List<Rectangle> { new Rectangle(100, 100, 50, 20), new Rectangle(157, 100, 50, 20) };
        Check(Geometry.Snap(new Rectangle(155, 121, 50, 20), two, 8).X == 157, "the closest of several edges wins");

        // ---- groups
        Check(Groups.IsValidName("") && Groups.IsValidName("Party list") && Groups.IsValidName("HP & mana"), "ordinary group names are accepted");
        Check(!Groups.IsValidName(null) && !Groups.IsValidName(" padded ") && !Groups.IsValidName("two\nlines") && !Groups.IsValidName(new string('x', 41)), "odd group names are refused");
        Throws<InvalidDataException>(delegate { MirrorRegion bad = Region(0, 0, 10, 10, 100, 100); bad.Group = null; bad.Validate(); }, "region without a group value");
        Throws<InvalidDataException>(delegate { new CooldownTimer { Name = "t", Group = new string('x', 41) }.Validate(); }, "timer with an overlong group");

        Profile profile = new Profile { Name = "Groups" };
        MirrorRegion a = Region(0, 0, 10, 10, 100, 100), b = Region(0, 20, 10, 10, 100, 100), c = Region(0, 40, 10, 10, 100, 100);
        a.Group = b.Group = "Party";
        profile.Regions.AddRange(new[] { a, b, c });
        profile.Timers.Add(new CooldownTimer { Name = "t", Group = "Cooldowns" });
        List<string> names = profile.GroupNames();
        Check(names.Count == 2 && names[0] == "Cooldowns" && names[1] == "Party", "group names are listed once each, sorted");
        Store.Save(profile);
        Profile loaded = Store.Load("Groups");
        Check(loaded.Regions[0].Group == "Party" && loaded.Regions[2].Group == "" && loaded.Timers[0].Group == "Cooldowns", "groups survive a round trip");
        Store.Delete("Groups");
        Check(Store.Parse("<Profile><Name>Old</Name><Regions><MirrorRegion><Name>r</Name></MirrorRegion></Regions></Profile>").Regions[0].Group == "", "a region from before groups existed has none");
    }

    static void SettingsChecks()
    {
        // ---- timer options added in 1.2
        CooldownTimer fresh = new CooldownTimer { Name = "t" };
        Check(fresh.Shape == TimerShape.Circle && fresh.Colour == 0 && fresh.CountUp && fresh.SoundFile == "", "new timers start as a counting-up circle with the standard sound");
        Throws<InvalidDataException>(delegate { new CooldownTimer { Name = "t", Colour = CooldownTimer.ColourCount }.Validate(); }, "unknown timer colour");
        Throws<InvalidDataException>(delegate { new CooldownTimer { Name = "t", Colour = -1 }.Validate(); }, "negative timer colour");
        Throws<InvalidDataException>(delegate { new CooldownTimer { Name = "t", Shape = (TimerShape)7 }.Validate(); }, "unknown timer shape");
        Throws<InvalidDataException>(delegate { new CooldownTimer { Name = "t", SoundFile = null }.Validate(); }, "timer without a sound file value");
        Check(Theme.TimerColours.Length == CooldownTimer.ColourCount, "there is a colour for every colour number");

        // ---- only local .wav files may be played
        Check(CooldownTimer.IsPlayableSoundPath(@"C:\Sounds\ding.wav") && CooldownTimer.IsPlayableSoundPath(@"d:\a\B.WAV"), "a .wav on a local drive is accepted");
        foreach (string bad in new[] { "", "ding.wav", @"\\server\share\ding.wav", "http://example.com/ding.wav", "https://example.com/a.wav", "file:///C:/ding.wav",
            @"C:\Sounds\ding.mp3", @"C:\Sounds\ding.wav:hidden.wav", @"C:ding.wav", @"\\?\C:\ding.wav", @"C:\Sounds\run.exe" })
            Check(!CooldownTimer.IsPlayableSoundPath(bad), "refused as a sound file: \"" + bad + "\"");
        Check(!CooldownTimer.IsPlayableSoundPath(null) && !CooldownTimer.IsPlayableSoundPath(@"C:\" + new string('a', 300) + ".wav"), "null and overlong sound paths are refused");

        // ---- export and import
        string folder = Path.Combine(Store.DataRoot, "shared");
        Directory.CreateDirectory(folder);
        Profile profile = new Profile { Name = "Mine" };
        profile.Regions.Add(Region(1, 2, 30, 40, 800, 600));
        profile.Timers.Add(new CooldownTimer { Name = "Potion", Shape = TimerShape.Bar, Colour = 3, CountUp = false, SoundFile = @"C:\Users\someone\ding.wav" });
        Store.Save(profile);
        string file = Path.Combine(folder, "Party layout.xml");
        Store.Export(profile, file);
        Check(!File.ReadAllText(file).Contains("someone"), "an exported profile does not contain the sound file path");
        Check(profile.Timers[0].SoundFile.Length > 0, "exporting leaves the profile in use untouched");
        Profile imported = Store.Import(file);
        Check(imported.Name == "Party layout", "an imported profile is named after its file");
        Check(imported.Regions.Count == 1 && imported.Regions[0].CropWidth == 30, "imported regions arrive intact");
        Check(imported.Timers[0].Shape == TimerShape.Bar && imported.Timers[0].Colour == 3 && !imported.Timers[0].CountUp, "imported timer options arrive intact");
        Store.Save(imported);
        Check(Store.Import(file).Name == "Party layout 2", "importing the same file again does not overwrite the first");

        string hostile = Path.Combine(folder, "bad name!.xml");
        File.WriteAllText(hostile, Store.Serialize(profile).Replace("ding.wav", "evil.wav"));
        Profile renamed = Store.Import(hostile);
        Check(renamed.Name == "Mine 2", "a file with an unusable name falls back to the name inside it, made unique");
        Check(renamed.Timers[0].SoundFile == "", "an imported profile never brings a sound file path with it");
        File.WriteAllText(hostile, "<Profile><Name>x</Name><Regions><MirrorRegion><Name>r</Name><CropWidth>0</CropWidth></MirrorRegion></Regions></Profile>");
        Throws<InvalidDataException>(delegate { Store.Import(hostile); }, "an invalid file is refused on import");
        File.WriteAllText(hostile, "this is not a profile");
        Throws<InvalidDataException>(delegate { Store.Import(hostile); }, "a file that is not XML is refused on import");
        Store.Delete("Mine");
        Store.Delete("Party layout");

        // ---- settings
        Settings defaults = Store.LoadSettings();
        Check(!defaults.OnlyWithGame && defaults.Snap && defaults.MinimizeToTray && defaults.LockHotkey == 0 && defaults.Language == "", "defaults when there is no settings file");
        Settings changed = new Settings { OnlyWithGame = true, Snap = false, MinimizeToTray = false, LockHotkey = (int)(Keys.Control | Keys.F6), ShowHotkey = (int)Keys.F7, Language = "pl" };
        Store.SaveSettings(changed);
        Settings back = Store.LoadSettings();
        Check(back.OnlyWithGame && !back.Snap && !back.MinimizeToTray && back.LockHotkey == (int)(Keys.Control | Keys.F6) && back.ShowHotkey == (int)Keys.F7 && back.Language == "pl", "settings survive a round trip");
        Store.SaveSettings(new Settings());
        Check(!Store.LoadSettings().OnlyWithGame, "saving again replaces the previous settings");
        Throws<InvalidDataException>(delegate { Store.SaveSettings(new Settings { Language = "xx" }); }, "unknown language");
        Throws<InvalidDataException>(delegate { Store.SaveSettings(new Settings { LockHotkey = -1 }); }, "negative hotkey setting");
        File.WriteAllText(Path.Combine(Store.DataRoot, "settings.xml"), "<Settings><Language>klingon</Language></Settings>");
        Check(Store.LoadSettings().Language == "", "a damaged settings file gives the defaults instead of an error");
        File.WriteAllText(Path.Combine(Store.DataRoot, "settings.xml"), "garbage");
        Check(Store.LoadSettings().Snap, "an unreadable settings file gives the defaults instead of an error");
        File.Delete(Path.Combine(Store.DataRoot, "settings.xml"));

        // ---- start with Windows (only the text is checked; the registry is not touched by tests)
        Check(Autostart.CommandFor(@"C:\Program Files\Echo HUD\EchoHUD.exe") == "\"C:\\Program Files\\Echo HUD\\EchoHUD.exe\" --minimized", "the start-up command quotes the path and asks for a hidden start");

        // ---- an empty language code follows Windows instead of meaning English
        Strings.Use("");
        Check(Strings.Get("common.ok").Length > 0, "an empty language code still gives a language");
        Strings.Use("en");
    }
}
