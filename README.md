# Echo HUD

**Your interface, repeated where you need it.**

Echo HUD shows live copies ("mirrors") of parts of a window in small floating
windows. Draw a box around your cooldowns, your party list or a status bar, and
drag the mirror next to your character so you never have to look away.

It is free, open source (GPL-3.0), has no ads, no accounts and no network code.

> **Not affiliated with CipSoft, BattlEye or Tibia Vision.** Echo HUD only
> displays; it never reads or changes the game. Even so, no third-party tool is
> officially approved, and you use it at your own risk. Read
> [docs/SAFETY.md](docs/SAFETY.md) before using it with a game.

## How it works, in one paragraph

Windows already knows how to draw a live miniature of any window - that is what
you see when you press Alt+Tab or hover the taskbar. Echo HUD asks Windows for
the same thing, but cropped to the rectangle you chose and placed in a floating
window. Windows does all the drawing on the graphics card. Echo HUD never
receives the picture, so there is nothing to capture, analyse or lag.

## Using it

1. **Pick a source** at the top of the panel:
   - **Tibia** - the game window itself. Found automatically when the game runs.
   - **OBS projector** - a *Windowed Projector* from OBS Studio (right-click the
     OBS preview). Use this if the game cannot be mirrored directly.
   - **Demo window** - a fake game screen for trying things out.
2. Press **Add region**. The source dims; **click and drag** a box over what you
   want. Release to accept, Esc or right-click to cancel.
3. A mirror appears. While **unlocked** you can:
   - drag it anywhere,
   - drag its bottom-right corner to resize it,
   - right-click it for size, opacity, a highlight border, re-cropping,
     renaming and deleting.
4. Press the **lock** button. Mirrors lose their frame, stay where they are, and
   mouse clicks pass straight through them to whatever is underneath.

Each mirror also has a row in the panel with rename, show/hide, re-crop, delete,
and sliders for size and opacity. The eye button next to the lock hides or shows
all mirrors at once.

Everything is saved automatically. **Profiles** (bottom of the panel) are
separate layouts - one per character or screen setup, for example.

### Good to know

- **Fullscreen:** use the game's DirectX 11 or DirectX 12 engine. OpenGL uses
  exclusive fullscreen, which Windows cannot mirror, and mirrors cannot float
  above it.
- **Minimized sources cannot be mirrored.** The source may be behind other
  windows or on another monitor, but not minimized.
- **Resizing the game window** can move interface elements; if a mirror shows
  the wrong spot afterwards, use *Choose a different area*.
- Your data lives in `%LOCALAPPDATA%\EchoHUD`. Profiles are plain XML files
  you can back up or share. They contain region positions and names you typed -
  never a character name.

## Building it yourself

You need 64-bit Windows 10 or 11 and nothing else: the C# compiler used here
ships with Windows, and the build downloads nothing.

1. Download this repository (green **Code** button, **Download ZIP**) and
   extract it.
2. Double-click **`build.bat`**.
3. Run **`artifacts\bin\EchoHUD.exe`**.

The full walkthrough, with troubleshooting, is in
[docs/BUILDING.md](docs/BUILDING.md).

## Finding your way around the code

About 2,400 lines of C#, one topic per file. Start with `Native.cs`: it is the
complete list of what the program can do to your system.

| File | What it is |
|---|---|
| [src/Native.cs](src/Native.cs) | Every Windows API the program calls, and the list of those it must never call. |
| [src/Sources.cs](src/Sources.cs) | Finding the game window, OBS projectors and the demo window. |
| [src/Model.cs](src/Model.cs) | `MirrorRegion` and `Profile` - the data that is saved. |
| [src/Geometry.cs](src/Geometry.cs) | Rectangle maths, free of side effects and fully unit tested. |
| [src/Store.cs](src/Store.cs) | Loading and saving profiles safely. |
| [src/MirrorWindow.cs](src/MirrorWindow.cs) | One floating mirror: thumbnail, drag, resize, lock, menu. |
| [src/RegionSelector.cs](src/RegionSelector.cs) | The "draw a box" overlay. |
| [src/MainForm.cs](src/MainForm.cs) | The control panel; ties everything together. |
| [src/RegionRow.cs](src/RegionRow.cs) | One row of the region list. |
| [src/Theme.cs](src/Theme.cs) | Colours, fonts, icons and small custom controls. |
| [src/Strings.cs](src/Strings.cs) | All user-visible text (English and Spanish). |
| [src/DemoWindow.cs](src/DemoWindow.cs) | The practice source. |
| [src/Program.cs](src/Program.cs) | Start-up. |
| [tests/Checks.cs](tests/Checks.cs) | Automated tests. |

More detail:

- [docs/BUILDING.md](docs/BUILDING.md) - build the program from source, step by step.
- [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) - how the pieces fit, and why.
- [docs/SAFETY.md](docs/SAFETY.md) - what the program does and does not do, and
  what is known about game rules.
- [docs/TESTING.md](docs/TESTING.md) - what is tested automatically, what was
  checked by hand, and what still needs checking.
- [CONTRIBUTING.md](CONTRIBUTING.md) - rules for changes.

## License

Copyright (C) 2026 the Echo HUD contributors.

This program is free software: you can redistribute it and/or modify it under
the terms of the GNU General Public License, version 3, as published by the
Free Software Foundation. It is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY
or FITNESS FOR A PARTICULAR PURPOSE. See [LICENSE](LICENSE) for the full text.

In short: you may use, study, share and change it, and anything you distribute
that is built on it must stay under the same license with its source available.

Tibia is a trademark of CipSoft GmbH. BattlEye, OBS Studio, Windows and other
product names belong to their owners and are mentioned only to describe
compatibility. This project is not affiliated with or endorsed by any of them.
The project contains no game artwork or third-party code.
