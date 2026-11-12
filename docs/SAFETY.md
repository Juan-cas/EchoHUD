# Safety, game rules and anti-cheat

Read this before using Echo HUD with an online game. It states what the program
does, what it deliberately does not do, and - just as important - what nobody
can promise you.

## The honest summary

- Echo HUD is designed to stay clearly on the "display only" side of the line.
- **It has not been reviewed or approved by CipSoft or BattlEye.** Nobody
  outside those companies can guarantee that any third-party tool is safe.
- You use it at your own risk. If that risk is not acceptable to you, do not
  run it alongside the game.

## What the program does

It asks Windows to draw a live, cropped miniature of a window inside a small
floating window (a DWM thumbnail - see ARCHITECTURE.md). That is all.

To find the window it looks at information Windows gives to every program: the
list of open windows with their title, window class, size and position, and
which window is currently in front.

It can also show countdown timers. A timer is a clock you start yourself; it
has no connection to the game at all (see "Cooldown timers and hotkeys" below).

## What the program never does

| It never... | How you can check |
|---|---|
| reads or writes another program's memory | `src/Native.cs` is the complete list of system calls. There is no `OpenProcess`, `ReadProcessMemory` or `WriteProcessMemory`. |
| injects code, installs hooks or loads drivers | No `SetWindowsHookEx`, `CreateRemoteThread`, or similar, anywhere. |
| sends keystrokes, clicks or messages to the game | No `SendInput`, `keybd_event`, `mouse_event`, and no `SendMessage`/`PostMessage` declarations at all. |
| listens to your keyboard or mouse | No keyboard or mouse hooks and no raw input. The one key-related call is `RegisterHotKey`, which makes Windows notify the program about combinations **you** assigned (to timers, to lock/unlock and to show/hide) - and nothing else you type. |
| captures or analyses pixels | No `BitBlt`, `PrintWindow`, `CopyFromScreen`, no OCR, no image processing. The program never holds an image of the game. |
| reads the game's files or network traffic | The only files touched are its own profiles and settings under `%LOCALAPPDATA%\EchoHUD`, plus files **you** point it at: a profile you import or export, and a `.wav` sound you choose for a timer. |
| connects to the internet | There is no networking code. No telemetry, automatic updates, accounts or ads. "Look for a new version" only asks Windows to open the project's web page in your browser. A timer sound must be a `.wav` file on a local drive; web addresses and network shares are refused, including in profiles received from others. |
| changes your system | One optional exception, off by default: "Start with Windows" writes a single entry named `EchoHUD` to the current user's start-up list (`src/Autostart.cs`). Switching it off removes it. No administrator rights are used. |
| automates anything | It has no notion of game state. It cannot tell a health bar from a wall. |

The game window is identified by its **title and window class only**. The
program does not query the game's process. (For OBS projector windows - never
for the game - it reads the owning program's name from the system process list
to confirm the window belongs to OBS.)

The optional setting "show mirrors only while the game is in front" asks
Windows which window is in front (`GetForegroundWindow`) and compares its
title and class with the same rule used to find the game. It then shows or
hides Echo HUD's **own** windows. Nothing is done to the game window.

The magnifier in the area selector and the "repeat the spacing" key (R) do not
look at the picture either: the magnifier is one more thumbnail drawn by
Windows, and R repeats the distance between two boxes you drew yourself.

Locked mirrors are "click-through": Windows itself delivers your clicks to the
window underneath. Echo HUD does not see, repeat or redirect them.

## Cooldown timers and hotkeys

Timers deserve their own explanation, because "a tool that tracks cooldowns"
can mean two very different things.

- **What Echo HUD does:** you press a key (or a button) and a countdown of a
  length you typed starts. It is a stopwatch on screen. It would behave the
  same with no game running.
- **What it does not do:** detect that you used an item, read a cooldown from
  the game, or react when a timer ends. When a timer finishes it changes
  colour and can play a sound. It never presses anything.
- **Hotkeys:** Windows delivers a registered combination to Echo HUD *instead
  of* the focused program, so that combination does not reach the game while
  Echo HUD is running. This is deliberate. Having one key both use an item and
  start a timer would require watching every keystroke (a keyboard hook) or
  re-sending keys to the game, and both are on the "never" list.
- A registered hotkey fires no matter which program has focus. Choose
  combinations you do not use elsewhere.

## What is known about the rules

This section reports public information; it is not legal advice and may be out
of date. Check the current official rules yourself.

- Tibia's rules prohibit unofficial software *to play the game* - broadly,
  software that plays for you or manipulates the client.
- The makers of a similar commercial overlay publish a reply they received from
  CipSoft customer support about their tool. As published, it says the tool
  does not give an unfair advantage and is **tolerated**, that this is **not an
  official statement of approval**, that any problem arising from using it is
  **at the player's risk**, and that it cannot be ruled out that BattlEye might
  refuse to let the game run while such software is running.
- That reply was about *their* program, not this one. Echo HUD uses the same
  kind of Windows feature, but similarity is not approval.

So: a display-only overlay of this kind appears to be tolerated today, tolerance
can be withdrawn, and an anti-cheat can change what it objects to at any time.

## Rules for people changing the code

These are not style preferences; they are what keeps the summary above true.

1. **Do not add to `Native.cs` casually.** Any new system call needs a written
   reason in the pull request, and nothing from the "never" table is acceptable.
2. **No feature may depend on knowing what the pixels mean.** No reading
   numbers, detecting cooldowns, recognising states, or reacting to them.
3. **Timers stay manual.** Nothing may start, stop or adjust a timer based on
   the game's picture, the keyboard at large, or any other observation.
4. **No input to other programs,** including "harmless" conveniences such as
   focusing or moving the game window.
5. **No hiding.** Do not add anything intended to make the program harder for an
   anti-cheat or the game to see. If a protection blocks a feature, the feature
   is unavailable - document it and stop.
6. **Do not claim approval.** Documentation and UI must not say or imply that
   the program is approved, safe or risk-free.

## If something looks wrong

If you believe the program does something this document says it does not, that
is a bug of the most serious kind. Please open an issue describing what you
observed. Do not include character names, account details or screenshots of
your game.
