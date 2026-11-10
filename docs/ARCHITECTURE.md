# Architecture

This document explains how Echo HUD is put together and why. It assumes you can
read C# but know nothing about Windows internals.

## The one idea everything rests on

Windows draws every window into its own off-screen picture and then a system
component, the **Desktop Window Manager (DWM)**, combines those pictures into
what you see. Because DWM already holds a live picture of every window, it
offers programs a service: *"draw a miniature of window A inside my window B"*.
That is a **DWM thumbnail**. Alt+Tab and the taskbar previews use it.

A thumbnail can be cropped to a rectangle of the source and scaled to any size.
Echo HUD is a friendly way to create and arrange such thumbnails.

Consequences worth knowing:

- **The program never sees the pixels.** DWM draws straight to the screen. There
  is no capture loop in this code base, and nothing to OCR or analyse.
- **It is smooth and cheap.** Mirrors update at the compositor's frame rate on
  the GPU; the program's own timer only runs four times a second for
  housekeeping.
- **Limits come from DWM**, not from us: a minimized window has no live picture,
  and an exclusive-fullscreen game bypasses DWM entirely.
- **Thumbnails are drawn on top of whatever the destination window paints.**
  That is why hints and frames in this program are either separate windows or
  drawn *around* the thumbnail, never over it.

## The pieces

```text
                 +------------------+
                 |    MainForm      |  control panel; owns the Profile
                 +---------+--------+
        discovers          | creates one per region
   +-----------+           |
   |  Sources  |     +-----v--------+        +----------------+
   +-----+-----+     | MirrorWindow |<------>|   RegionRow    |
         |           +-----+--------+ edits  +----------------+
   SourceWindow            |           the same MirrorRegion
   (a window handle)       | DwmRegisterThumbnail
         |                 v
         +---------> [ Windows DWM ] ----> pixels on screen
```

| Piece | Role |
|---|---|
| `Profile`, `MirrorRegion` (Model.cs) | Plain data. What is saved to disk. |
| `Sources`, `SourcePolicy` (Sources.cs) | Finds candidate source windows by title and window class. |
| `MirrorWindow` | A borderless, always-on-top window holding one thumbnail. |
| `RegionSelector` | Modal overlay for drawing a crop rectangle. |
| `MainForm` | Wires the above together and runs the housekeeping timer. |
| `RegionRow` | The list row that edits one `MirrorRegion`. |
| `Store` | Profile files. |
| `Geometry` | All coordinate maths, as pure functions. |
| `Native` | Every P/Invoke declaration. |

## Two coordinate spaces

Mixing these up is the classic bug in this kind of program.

| Name | Unit | Used for |
|---|---|---|
| **Source space** | pixels inside the source window's *client area* (content only, no title bar) | `MirrorRegion.CropX/Y/Width/Height` |
| **Desktop space** | pixels on the virtual desktop | `MirrorRegion.X/Y`, window positions |

The process declares itself *per-monitor DPI aware* at start-up
(`Native.EnablePixelAccurateCoordinates`), so both spaces are real pixels on
every monitor and no scaling factors appear in the maths. UI sizes are written
in 96-DPI "design pixels" and converted once with `Theme.Px`.

### When the source changes size

A region remembers the client size of the source it was drawn on
(`SourceWidth/Height`). `Geometry.CurrentCrop` decides what to show if that size
later differs:

- **OBS projector / demo:** the picture is simply stretched, so the crop is
  scaled proportionally.
- **Game client:** the interface is re-flowed at the same pixel size, so the
  crop keeps its pixel position and is only clamped back inside. This is a
  best effort - the game may have moved the element, and the user may need to
  re-crop.

## Mirror windows in detail

A `MirrorWindow` has two modes, switched by `SetLocked`:

| | Unlocked | Locked |
|---|---|---|
| Frame | 2 px accent colour | none (or 2 px highlight if the region asks for it) |
| Mouse | drag to move, corner to resize, right-click menu | clicks pass through (`WS_EX_TRANSPARENT`) |
| Focus | may take focus when clicked | never (`WS_EX_NOACTIVATE`) |

Details that are easy to get wrong:

- **`MirrorRegion.X/Y` is the corner of the picture, not of the window**, so the
  picture does not shift by the frame width when locking.
- **Click-through needs the window to be recreated.** Extended styles are
  applied in `CreateParams`; `SetLocked` calls `RecreateHandle`, and
  `OnHandleCreated` registers the thumbnail again because a thumbnail belongs
  to a window handle.
- **Moving and resizing are done by Windows.** `WndProc` answers
  `WM_NCHITTEST` with "this is the title bar" or "this is the resize corner".
  When the user lets go, `OnResizeEnd` converts the new width into
  `ScalePercent` and snaps the window to the crop's exact proportions.
- **Opacity is a whole-window property** (`SetLayeredWindowAttributes`), which
  makes the mirror genuinely see-through instead of merely darker.

## The region selector

`RegionSelector` is a borderless window placed exactly over the source. It
contains two thumbnails of the same source:

1. the whole source at low opacity over black - the dimmed look;
2. only the selected rectangle at full opacity, in the same place - the lit
   selection.

Dragging just updates the second thumbnail's rectangle. Because both are 1:1
copies positioned at the origin, a mouse position in the selector *is* a source
coordinate; there is no transform to get wrong. The text bubble is a separate
click-through window (`HintWindow`) because nothing can be painted over a
thumbnail.

The selector receives the mouse clicks itself. Nothing is forwarded to the
source window.

## The housekeeping timer

`MainForm.Tick` runs every 250 ms:

1. Once a second, `Sources.Discover` lists candidate windows and
   `SourcePolicy.Choose` picks the one the profile wants. If the game was
   restarted its window handle is new; mirrors are simply re-bound.
2. `UpdateMirrors` shows or hides each mirror (source available, not selecting,
   master switch on, region visible) and lets it follow source resizes.
3. If anything changed, the profile is saved.
4. The status line is refreshed.

There is no Save button and no background thread. Everything runs on the UI
thread, and none of it is heavy.

## Storage

One XML file per profile under `%LOCALAPPDATA%\EchoHUD\profiles`. See the
header of `Store.cs`. Three rules:

- **Atomic writes:** write `name.xml.tmp`, then swap it in.
- **Untrusted input:** DTDs and external references are disabled and every
  number is range-checked by `Validate()` before use, because players share
  profile files.
- **Names are file names**, restricted to a safe character set
  (`Store.IsValidName`).

## Why C# on .NET Framework, and why WinForms

- The job is a handful of Win32 calls plus a small panel. C# does both with
  little ceremony, and the result is readable by many contributors.
- **.NET Framework 4.8 and its C# compiler are part of Windows 10 and 11.**
  Players need no runtime, contributors need no SDK, and the build downloads
  nothing. A newer .NET, Rust or C++ would add an install step for one side or
  the other without making the program better at what it does.
- The cost is the old C# 5 language level (see CONTRIBUTING.md). For a code
  base this small that is a fair trade.

Deliberately out of scope: effects that need the picture itself, such as
removing a background colour or drawing outlines around text. They would
require reading pixels, which conflicts with the "never captures pixels"
promise in SAFETY.md.

## Ideas not built yet

- Hide mirrors automatically while the game is not the foreground window.
- Snap mirrors to each other and move them as a group.
- Global hotkeys for lock and show/hide.
- Import/export buttons for profiles (the files can already be copied by hand).
- More languages in `Strings.cs`.
