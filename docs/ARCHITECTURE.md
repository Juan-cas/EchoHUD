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
| `TimerWindow`, `TimerRow` | A floating countdown circle and its list row. |
| `Hotkeys` | Converts and asks for the key combinations that start timers. |
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

### Picking several areas

Holding Shift while releasing the mouse keeps the selector open. Each accepted
rectangle keeps its own lit thumbnail, and a new one is registered for the next
drag; Enter finishes. All mirrors created from one session start at the same
offset from their source position, so their arrangement is preserved. This is
how a panel's background is "removed" without touching pixels: only the wanted
parts are mirrored.

Pressing R adds `Geometry.NextInSeries(previous, last)`: a rectangle as far
from the last one as that is from the one before. A `LoupeWindow` (a small
click-through window holding one more thumbnail, enlarged five times) follows
the mouse as a magnifier.

What happens when one of the resulting mirrors is dragged is described under
"Fine-tuning, snapping and groups" below.

## Cooldown timers

A `TimerWindow` is the one place where the program draws a picture itself.

- **State** is three fields: `running`, `finished` and `endsAt`. It is not
  saved; closing the program forgets running countdowns.
- **Drawing:** the circle is painted into a bitmap with transparency and
  handed to Windows with `UpdateLayeredWindow` (`Native.PresentOwnBitmap`),
  which gives smooth round edges. Mirrors cannot use this technique and
  timers cannot use thumbnails, which is why the two window types differ.
  The picture is redrawn only when the text or the ring would visibly change.
- **Starting:** by hotkey, by double-click (unlocked), or from the row.
  Starting a running timer restarts it.
- **Hotkeys:** `MainForm.RegisterHotkeys` calls `RegisterHotKey` once per timer
  with an id equal to the timer's list position plus one. Windows then sends
  `WM_HOTKEY` with that id to the main window. Registrations are dropped and
  redone whenever the list changes, and released while the "press a key"
  dialog is open so the current combination can be typed into it.

There is no code path from anything observed on screen to a timer. See
SAFETY.md for why that matters.

### Shapes, colours and sounds

`CooldownTimer.Shape` selects `DrawCircle` or `DrawBar`; `Colour` is an index
into `Theme.TimerColours`. With `CountUp`, a finished timer keeps its clock
running and shows the time since it finished.

A custom sound is a path in `CooldownTimer.SoundFile`. Because profiles are
shared between players, that path is untrusted: `IsPlayableSoundPath` accepts
only a `.wav` on a local drive letter (no UNC shares, no URLs - `SoundPlayer`
would happily download one), `Store.Import` drops the path altogether, and
`Store.Export` leaves it out because it usually contains a user name.

## Fine-tuning, snapping and groups

- **Arrow keys** (`MirrorWindow.ProcessCmdKey`) call `Geometry.Nudge`, which
  moves or resizes the saved crop by one source pixel and keeps it inside the
  source. Ctrl+arrows move the window instead. The mirror that has keyboard
  focus draws a white frame.
- **Snapping** (`MirrorWindow.SnapWhileMoving`): while Windows drags the
  window it sends `WM_MOVING` with the proposed rectangle. We replace it with
  `Geometry.Snap(...)` of the position implied by the mouse. Two details
  matter: the position is recomputed from the mouse every time (otherwise a
  snapped window never lets go), and the rectangle that snaps is the
  *picture*, not the editing frame, so mirrors end up flush once locked.
  `MainForm.SnapTargetsFor` supplies the other mirrors, the timers and the
  screen's working area.
- **Groups** are just a name stored on each mirror and timer (`Group`).
  Areas picked in one selector session get a fresh name. On drop,
  `MainForm.MoveCompanions` moves the other members (Shift) or everything
  (Ctrl) by the same distance.

## Settings, the tray icon and starting hidden

`Settings` (Model.cs) holds what belongs to the computer rather than to a
profile; `Store.LoadSettings` falls back to defaults on any problem.

- **Only while the game is in front:** `MainForm.GameInFront`, evaluated on
  every tick, checks whether the foreground window is one of ours, the
  current source, or matches `SourcePolicy.IsTibiaWindow`.
- **Lock and show/hide hotkeys** are registered beside the timers' with fixed
  ids (100 and 101).
- **Tray icon:** a `NotifyIcon`. With "minimize to the notification area",
  `OnResize` hides the panel when it is minimized. Mirrors and timers are
  separate top-level windows and are not affected.
- **Start with Windows:** `Autostart` writes one value under the current
  user's `Run` key with the argument `--minimized`; `MainForm.SetVisibleCore`
  then swallows the first "show".
- **Second start:** `Program` holds a named mutex. A second copy signals a
  named event and exits; the first copy waits on that event and shows its
  panel. No window messages are sent between processes.
- **Language:** `Strings.Use(settings.Language)` runs before any window is
  built, which is why a change applies at the next start.

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

## Ideas that were not built

The project is complete and no further development is planned (see the
README). For anyone continuing it in a fork, these were the open ideas:

- Remembering a running countdown across a restart.
- Snapping for timers themselves (mirrors snap to timers, not the reverse).
- Showing the group name in the panel's rows.
- Changing language without a restart.
- More languages in `Strings.cs`, and review of the Portuguese and Polish texts.

Deliberately rejected, and not to be revived under this project's safety
rules: removing a background colour from a mirror, or anything else that
reads the source's pixels. See SAFETY.md.
