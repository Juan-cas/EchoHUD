# Echo HUD

**Your interface, repeated where you need it.**

Website: **https://echohud.vercel.app**

Echo HUD shows live copies ("mirrors") of parts of a window in small floating
windows. Draw a box around your cooldowns, your party list or a status bar, and
drag the mirror next to your character so you never have to look away.

It is free, open source (GPL-3.0), has no ads, no accounts and no network code.

> **Not affiliated with CipSoft, BattlEye or Tibia Vision.** Echo HUD only
> displays; it never reads or changes the game. Even so, no third-party tool is
> officially approved, and you use it at your own risk. Read
> [docs/SAFETY.md](docs/SAFETY.md) before using it with a game.

## Project status: complete

Echo HUD does what it was meant to do, and version 1.2.0 is its final planned
version. **The maintainer does not plan any further development** - no new
features, and no schedule for updates.

- The program keeps working as it is; nothing in it expires or phones home.
- The code stays public under the GPL-3.0. You are free to fork it and take it
  further, as long as your version stays under the same license.
- One feature was considered and deliberately **not** built: removing a
  background colour from a mirror. It would require reading the game's pixels,
  which this program never does. See [docs/SAFETY.md](docs/SAFETY.md).

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
   - drag it anywhere - it clicks into place beside other mirrors and at the
     edges of the screen,
   - drag its bottom-right corner to resize it,
   - fine-tune it with the keyboard (click the mirror first; its frame turns
     white): **arrow keys** move the mirrored area one pixel, **Shift+arrows**
     make the area larger or smaller, **Ctrl+arrows** move the mirror itself,
   - right-click it for size, opacity, a highlight border, groups,
     re-cropping, renaming and deleting.
4. Press the **lock** button. Mirrors lose their frame, stay where they are, and
   mouse clicks pass straight through them to whatever is underneath.

Each mirror also has a row in the panel with rename, show/hide, re-crop, delete,
and sliders for size and opacity. The eye button next to the lock hides or shows
all mirrors at once.

Everything is saved automatically. **Profiles** (bottom of the panel) are
separate layouts - one per character or screen setup, for example.

While you draw a box, a **magnifier** next to the mouse shows the pixels under
it enlarged, which makes thin things such as health bars much easier to hit.

### Settings (the gear button)

| Setting | What it does |
|---|---|
| Show mirrors only while the game is in front | Mirrors and timers disappear when you switch to your browser or another program, and come back with the game. Off by default. |
| Snap mirrors to each other | Turn off to place mirrors freely. |
| Minimize to the notification area | Minimizing hides the panel next to the clock instead of on the taskbar. Click the icon to bring it back; right-click it to lock, show/hide or exit. |
| Start with Windows | Starts Echo HUD hidden in the notification area when you sign in. |
| Hotkey to lock / unlock, hotkey to show / hide | Key combinations that work while you are in the game. Like timer hotkeys, Windows gives them to Echo HUD *instead of* the game, so pick ones you do not use in game. |
| Language | English, Español, Português or Polski. Follows Windows by default. |
| Import / export a profile | Save a layout as a file to back it up or share it, and load one you were given. |
| Look for a new version | Opens the project's download page in your web browser. Echo HUD itself never goes online. |

Starting Echo HUD a second time does not open a second copy: it brings the
panel of the running one back on screen.

### Showing only the bars (hiding a panel's background)

Echo HUD cannot erase a background colour - that would mean reading the
picture, which it never does (see [docs/SAFETY.md](docs/SAFETY.md)). What you
can do is mirror only the parts you care about:

1. Press **Add region** and drag a tight box around the first health bar.
2. **Hold Shift while you release the mouse.** The box stays lit and the
   selector stays open. Drag the next bar, again with Shift, and so on.
3. Release the last one without Shift, or press Enter.

**Shortcut for lists:** after boxing the first two rows this way, press **R**.
Echo HUD adds a third box the same distance below the second, and another for
each further press - five party members are two drags and three key presses.
(It repeats the spacing *you* drew; it does not look at the picture.)

You get one slim mirror per bar, arranged exactly as they were in the game,
with nothing but your screen showing between them. Areas picked together form
a **group**:

- **hold Shift while dragging** one of them to move the whole group,
- **hold Ctrl while dragging** to move every mirror and timer at once,
- right-click a mirror or timer and use **Group** to add it to a group, start
  a new one, or take it out.

If a box is a pixel off, click its mirror and use the arrow keys (see above) -
you see the result immediately in the mirror itself.

### Cooldown timers

For anything with a long cooldown - a potion, a ring, a spell - add a timer:

1. Press the **stopwatch** button next to the lock. A circle appears beside the
   panel, with a row for it in the list.
2. Set the **duration** (clock button): pick one from the list, or choose
   *Other...* and type `10` for ten minutes, or `10:30`.
3. Choose a **hotkey** (keyboard button), for example Ctrl+F5.
4. Drag the circle where you want it and lock.

When you use the item, press the timer's hotkey. The ring empties as time
passes and shows the time left; when it is over it turns solid, says *Ready*
and plays a short sound (optional). You can also start a timer with the play
button in its row, or by double-clicking the circle while unlocked.

Right-click a timer (while unlocked) for more:

- **Shape:** a circle, or a slim bar that fits under an action bar.
- **Colour:** five colours, to tell timers apart at a glance.
- **Sound when ready:** none, the standard Windows sound, or your own `.wav`
  file - a different sound per timer lets you tell them apart by ear.
- **Count up after it is ready:** instead of just *Ready*, the timer shows how
  long it has been ready, for example `+0:42`.

> **Why a separate key?** Echo HUD does not watch the game or your keyboard, so
> it cannot know when you used an item. And Windows gives a registered hotkey
> to Echo HUD *instead of* the game, so the timer's key cannot be the same key
> that uses the item. Use a combination you do not need in game and press it
> right after the item's key.

### Good to know

- **Fullscreen:** use the game's DirectX 11 or DirectX 12 engine. OpenGL uses
  exclusive fullscreen, which Windows cannot mirror, and mirrors cannot float
  above it.
- **Minimized sources cannot be mirrored.** The source may be behind other
  windows or on another monitor, but not minimized.
- **Resizing the game window** can move interface elements; if a mirror shows
  the wrong spot afterwards, use *Choose a different area*.
- Your data lives in `%LOCALAPPDATA%\EchoHUD`. Profiles are plain XML files
  you can back up or share (gear button, *Export*). They contain region
  positions and names you typed - never a character name. The location of a
  custom timer sound is left out of exported files.
- The Portuguese and Polish texts were not written by native speakers.
  Corrections are welcome: everything is in [src/Strings.cs](src/Strings.cs).

## Building it yourself

You need 64-bit Windows 10 or 11 and nothing else: the C# compiler used here
ships with Windows, and the build downloads nothing.

1. Download this repository (green **Code** button, **Download ZIP**) and
   extract it.
2. Double-click **`build.bat`**. It asks where to put the program; press
   Enter for your Desktop.
3. Run **`EchoHUD.exe`** from there.

The full walkthrough, with troubleshooting, is in
[docs/BUILDING.md](docs/BUILDING.md).

## Finding your way around the code

About 5,200 lines of C#, one topic per file. Start with `Native.cs`: it is the
complete list of what the program can do to your system.

| File | What it is |
|---|---|
| [src/Native.cs](src/Native.cs) | Every Windows API the program calls, and the list of those it must never call. |
| [src/Sources.cs](src/Sources.cs) | Finding the game window, OBS projectors and the demo window. |
| [src/Model.cs](src/Model.cs) | `MirrorRegion`, `CooldownTimer`, `Profile` and `Settings` - the data that is saved. |
| [src/Geometry.cs](src/Geometry.cs) | Rectangle maths, free of side effects and fully unit tested. |
| [src/Store.cs](src/Store.cs) | Loading, saving, importing and exporting profiles safely; the settings file. |
| [src/MirrorWindow.cs](src/MirrorWindow.cs) | One floating mirror: thumbnail, drag, resize, lock, menu. |
| [src/RegionSelector.cs](src/RegionSelector.cs) | The "draw a box" overlay. |
| [src/MainForm.cs](src/MainForm.cs) | The control panel; ties everything together. |
| [src/RegionRow.cs](src/RegionRow.cs) | One row of the region list. |
| [src/TimerWindow.cs](src/TimerWindow.cs) | One floating countdown, as a circle or a bar. |
| [src/TimerRow.cs](src/TimerRow.cs) | One row of the timer list. |
| [src/Hotkeys.cs](src/Hotkeys.cs) | Hotkeys: storing, describing and asking for a key combination. |
| [src/Autostart.cs](src/Autostart.cs) | The optional "Start with Windows" entry. |
| [src/Theme.cs](src/Theme.cs) | Colours, fonts, icons and small custom controls. |
| [src/Strings.cs](src/Strings.cs) | All user-visible text (English, Spanish, Portuguese, Polish). |
| [src/DemoWindow.cs](src/DemoWindow.cs) | The practice source. |
| [src/Program.cs](src/Program.cs) | Start-up. |
| [tests/Checks.cs](tests/Checks.cs) | Automated tests. |
| [site/](site/) | The project website: one static page, no build step. |

More detail:

- [docs/BUILDING.md](docs/BUILDING.md) - build the program from source, step by step.
- [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) - how the pieces fit, and why.
- [docs/SAFETY.md](docs/SAFETY.md) - what the program does and does not do, and
  what is known about game rules.
- [docs/TESTING.md](docs/TESTING.md) - what is tested automatically, what was
  checked by hand, and what still needs checking.
- [CONTRIBUTING.md](CONTRIBUTING.md) - rules for changes.

## Code signing policy

**Current status: downloads are not signed yet.** Windows will show an
"unknown publisher" warning when you start a downloaded `EchoHUD.exe`. You can
avoid trusting a download altogether by
[building the program yourself](docs/BUILDING.md).

The project may still apply for free code signing from the
[SignPath Foundation](https://signpath.org/) for its final version. If an
application is accepted, this section will be updated and releases will carry
this notice: *Free code
signing provided by [SignPath.io](https://about.signpath.io/), certificate by
[SignPath Foundation](https://signpath.org/).*

How releases are produced, signed or not:

- **Built from this repository only.** Every release file is built by GitHub
  Actions from the tagged source, using
  [.github/workflows/build.yml](.github/workflows/build.yml). Nothing is built
  on a maintainer's computer, and the build log shows the file's SHA-256
  fingerprint.
- **Published by hand.** The automated build only prepares a draft; a
  maintainer reviews and publishes each release.
- **Team roles.** Committers, reviewers and approvers:
  [Juan-cas](https://github.com/Juan-cas). Changes from anyone else are
  reviewed before they are merged.
- **Privacy.** This program will not transfer any information to other
  networked systems unless specifically requested by the user or the person
  installing or operating it. It contains no networking code at all.

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
