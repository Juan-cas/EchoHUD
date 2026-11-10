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

### With the real applications

15. **OBS:** open a Windowed Projector; it appears under Source. Repeat steps
    3-9. Cover the projector with another window: mirrors keep working.
16. **Game client:** only follow steps 3-9, and only interact with the game
    yourself as you normally would. Check windowed and DirectX fullscreen.

## Verification status of version 1.0.0

Being explicit about what has and has not been checked:

| Item | Status |
|---|---|
| Automated checks | Pass (251 checks) on Windows 11, 96 DPI. |
| Panel layout, list rows, status line | Checked by screenshot with the demo source. |
| Mirrors render live, scaled, with opacity | Checked by screenshot with the demo source. |
| Region selector: dim copy, lit selection, size hint, region created at the right crop and position | Checked with the demo source. |
| Lock: frame removed without shifting, click-through and no-activate styles set, highlight border kept, state saved | Checked (window styles read back; screenshots). |
| Auto-save and reload | Checked. |
| Dragging/resizing a mirror with a real mouse, right-click menu, rename editing | **Not yet checked by a person.** |
| OBS projector as source | **Not yet checked.** |
| The real game client as source (window recognition, whether its picture can be mirrored, fullscreen) | **Not yet checked.** |
| Mixed-DPI multi-monitor setups | **Not yet checked.** |

Update this table when you verify something, and say how.
