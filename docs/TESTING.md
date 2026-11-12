# Testing

## Automated checks

```powershell
powershell -ExecutionPolicy Bypass -File scripts/test.ps1
```

`tests/Checks.cs` is compiled together with the application and run as a console
program. It opens no windows and uses a temporary data folder. It covers:

| Area | Examples |
|---|---|
| Geometry | drag rectangles, clipping, crop behaviour when the source is resized, size limits, letterboxing |
| Source recognition | which titles and window classes count as the game or an OBS projector, which window a profile picks |
| Model validation | out-of-range crops, scales, opacities, names, too many regions |
| Storage | save/load round trip, atomic replace, name rules, hostile or damaged XML |
| Text | every language has every key and the same placeholders |
| Timers | duration parsing and formatting, hotkey conversion and naming, validation, save/load, which sound files may be played |
| Layout | arrow-key nudging, repeating the spacing of areas, snapping, group names |
| Sharing and settings | export leaves out sound paths, import renames and validates, settings round trip and damaged settings files, the start-up command text |

A change is not ready until this prints `0 failed`.

## What automated checks cannot cover

Anything that depends on Windows actually drawing something. Use the checklist
below with the **Demo window** source - it needs no game.

### Manual checklist

Start from an empty profile (use a scratch data folder:
`$env:ECHOHUD_DATA_DIR = "C:\some\empty\folder"` before starting the app).

1. **First run.** The panel opens, the list shows the "No regions yet" hint.
2. **Demo source.** Choose *Demo window* under Source. The demo opens, the
   status dot turns green, the status line says it is connected.
3. **Add region.** Press Add region. The demo dims, a hint appears at the top.
   Drag a box: the inside lights up and the hint shows its size. Release.
4. **Mirror appears** next to the selected area with a coloured frame, and it
   animates along with the demo.
5. **Cancel paths.** Add region, then Esc. Again, then right-click. Again, then
   a single click without dragging (selector stays open). No region is added.
6. **Move and resize.** Drag the mirror. Drag its bottom-right corner; on
   release it snaps to the crop's proportions and the Size slider follows.
7. **Row controls.** Rename (pencil, and double-click on the name), hide/show,
   re-crop, both sliders. The mirror follows each change immediately.
8. **Right-click menu** on the mirror: size, opacity, highlight border, re-crop,
   rename (focuses the row), hide, delete.
9. **Lock.** Frames disappear without the picture shifting. Clicking a mirror
   now reaches the window underneath. A mirror with *highlight border* keeps an
   amber frame. Unlock restores editing.
10. **Show/hide all.** The eye button hides and shows every mirror.
11. **Source lifecycle.** Minimize the demo: mirrors hide, status explains.
    Restore: they return. Close the demo: mirrors hide. Choose it again: it
    reopens and mirrors return. Resize the demo: mirrors keep their content.
12. **Profiles.** Create a second profile, add a region, switch back and forth.
    Delete a profile. Try an invalid name (`a/b`) and an existing name.
13. **Persistence.** Close and reopen the app: same profile, same mirrors, same
    lock state. Only one copy of the app can run at a time.
14. **Several monitors / high DPI,** if available: mirrors and the selector line
    up on every monitor.

### Several areas and timers

17. **Several areas.** Add region; drag a small box and hold Shift while
    releasing. It stays lit and the hint counts it. Add two more the same way,
    then press Enter. Three mirrors appear in the same arrangement.
18. **Move together.** Hold Shift while dragging one of those mirrors: the
    three move by the same amount. Hold Ctrl instead: every mirror and timer
    moves. Without either key only that one moves.
19. **Add a timer.** Press the stopwatch button. A dashed circle appears next
    to the panel and a row in the list.
20. **Duration.** Clock button: `0:05` works; `abc` and `0` are refused with a
    message.
21. **Hotkey.** Keyboard button: press Ctrl+F5. The row shows it. Press Ctrl+F5
    with another program focused: the timer starts. Assign the same hotkey to
    a second timer: a message says Windows refused it. "Remove" clears it.
22. **Countdown.** The ring empties, the last ten seconds turn amber, then it
    shows *Ready*, solid, and a sound plays if enabled. Starting again
    restarts from the full duration. Reset returns to idle.
23. **Timer menu and lock.** Right-click the circle: size, opacity, sound,
    rename, hide, delete. Lock: the dashed outline disappears and clicks pass
    through; the hotkey and the row's play button still work.

### Version 1.2 additions

24. **Magnifier.** Add region: a small enlarged view follows the mouse and
    moves to the other side near the right and bottom edges.
25. **Repeat spacing.** Shift-select two boxes one above the other, press R
    twice: two more boxes light up at the same spacing. Near the edge, R shows
    "no room". Enter creates the mirrors; right-click one: it is in a group.
26. **Fine-tune.** Click an unlocked mirror: white frame. Arrows shift what it
    shows by one pixel; Shift+arrows change its size; Ctrl+arrows move it.
27. **Snap.** Drag a mirror near another: it clicks to its edge, and lets go
    again when pulled further. Lock: no gap between the two pictures. Turn
    "Snap mirrors" off in the settings: no more clicking into place.
28. **Groups.** Shift+drag a grouped mirror: only its group follows.
    Ctrl+drag: everything follows. Group menu: none / existing / new.
29. **Only with the game in front.** Enable it with the demo source, click
    another program: mirrors and timers vanish within a moment; click the
    demo or the panel: they return.
30. **Lock and show/hide hotkeys.** Assign both in the settings menu and press
    them with another program focused.
31. **Tray.** Minimize: the panel leaves the taskbar, a bubble explains once.
    Left-click the icon: panel returns. Right-click: lock, show/hide, exit.
    Start the program a second time while hidden: the panel appears.
32. **Start with Windows.** Enable, check Task Manager -> Startup apps lists
    Echo HUD; sign out and in: it runs hidden. Disable: the entry is gone.
33. **Timer options.** Duration menu presets and *Other...*; shape bar/circle;
    each colour; sound off / standard / a `.wav` file (it plays when chosen);
    count up shows `+0:05` five seconds after Ready.
34. **Import/export.** Export, delete the profile, import the file: the
    layout is back. Import the same file twice: the second gets " 2".
35. **Languages.** Choose each language, restart, skim every label for
    clipped text.
36. **build.bat.** Enter puts `EchoHUD.exe` on the Desktop and shows it; a
    typed folder is created and used.

### With the real applications

15. **OBS:** open a Windowed Projector; it appears under Source. Repeat steps
    3-9. Cover the projector with another window: mirrors keep working.
16. **Game client:** only follow steps 3-9, and only interact with the game
    yourself as you normally would. Check windowed and DirectX fullscreen.

## Verification status of version 1.2.0

Being explicit about what has and has not been checked:

| Item | Status |
|---|---|
| Automated checks | Pass (1708 checks) on Windows 11, 96 DPI. |
| Panel layout, list rows, status line | Checked by screenshot with the demo source. |
| Mirrors render live, scaled, with opacity | Checked by screenshot with the demo source. |
| Region selector: dim copy, lit selection, size hint, region created at the right crop and position | Checked with the demo source. |
| Lock: frame removed without shifting, click-through and no-activate styles set, highlight border kept, state saved | Checked (window styles read back; screenshots). |
| Auto-save and reload | Checked. |
| Timer circle: idle, running and Ready pictures; rows in the panel | Checked by screenshot. |
| Timer hotkey is registered with Windows, and a hotkey message starts the right timer | Checked (a second registration of the same combination is refused; message delivered directly). |
| 1.2: region added through the selector with the magnifier present; arrow keys change the saved crop; lock and show/hide hotkey messages toggle the saved state and the click-through style; a timer finishing with count-up; second start leaves one copy running and exits; clean exit; `build.ps1 -CopyTo`, `build.bat nopause` | Checked by driving the program's own windows and reading the saved profile. **No screenshots were possible for 1.2**, so nothing new has been checked by eye. |
| 1.2: how the bar timer, colours, magnifier, settings menu, tray menu and the Portuguese/Polish texts look; snapping and group dragging with a real mouse; "only with the game in front"; Start with Windows; custom sound; import/export dialogs; the `build.bat` question | **Not yet checked by a person.** |
| Pressing a real hotkey on the keyboard, the "press a key" dialog, duration dialog, sound | **Not yet checked by a person.** |
| Picking several areas with Shift, and moving everything with Shift | **Not yet checked by a person.** |
| Dragging/resizing a mirror with a real mouse, right-click menu, rename editing | **Not yet checked by a person.** |
| OBS projector as source | **Not yet checked.** |
| The real game client as source (window recognition, whether its picture can be mirrored, fullscreen) | **Not yet checked.** |
| Mixed-DPI multi-monitor setups | **Not yet checked.** |

Update this table when you verify something, and say how.
