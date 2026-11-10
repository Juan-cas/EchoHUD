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
        foreach (string key in Strings.English.Keys)
            Check(Strings.Spanish.ContainsKey(key), "Spanish is missing \"" + key + "\"");
        foreach (string key in Strings.Spanish.Keys)
            Check(Strings.English.ContainsKey(key), "Spanish has an unknown key \"" + key + "\"");
        foreach (KeyValuePair<string, string> pair in Strings.English)
        {
            string other = Strings.Spanish.ContainsKey(pair.Key) ? Strings.Spanish[pair.Key] : "";
            Check(pair.Value.Contains("{0}") == other.Contains("{0}") && pair.Value.Contains("{1}") == other.Contains("{1}"),
                "placeholders differ between languages for \"" + pair.Key + "\"");
        }
        Strings.Use("es");
        Check(Strings.Get("common.cancel") == "Cancelar", "Spanish can be selected");
        Strings.Use("zz");
        Check(Strings.Get("common.cancel") == "Cancel", "unknown languages fall back to English");
        Check(Strings.Get("no.such.key") == "no.such.key", "an unknown key is shown as itself");
        Check(Strings.Format("list.count", 2, 32) == "2 / 32", "formatting fills placeholders");
    }
}
