# Safety, game rules and anti-cheat

Read this before using Echo HUD with an online game. It states what the program
does, what it deliberately does not do, and - just as important - what nobody
can promise you.

## The honest summary

- Echo HUD is designed to stay clearly on the "display only" side of the line.
- **It is not approved or endorsed by CipSoft or BattlEye.** Tibia Customer
  Support has said its use is *tolerated* - which is not approval, and which
  they later declined to extend to individual features (details under "What
  is known about the rules"). BattlEye has not commented at all. Nobody
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

The maintainer of Echo HUD wrote to Tibia Customer Support twice. Both answers
are summarised here, not quoted, and neither is an endorsement.

- **First answer, about the program as first described to them (the window
  mirrors).** Customer Support said they had looked at the software and found
  that it does **not provide an unfair advantage**, that they are **not
  opposed to its use**, and that its use is **tolerated**. They added that
  this is **not an official endorsement or approval**, that any problem
  arising from using it is **entirely at the player's own risk**, and that
  they cannot give support for problems that occur while using it.
- **Second answer, about the later additions.** A follow-up described the
  cooldown timers, the hotkeys and the other features added afterwards, and
  asked whether a display-only colour filter (see below) would be acceptable.
  This time the answer was that Customer Support **does not assess or approve
  individual third-party programs, their features or their source code**;
  that a feature being purely visual **does not make it approved**; that
  players who use third-party software do so **at their own risk**; and that
  no guarantee can be given that any program is permissible or compatible
  with Tibia and BattlEye.

Read together: the mirroring tool was called tolerated; the features added
later were neither accepted nor objected to, because Customer Support does not
judge features one by one; and nothing was said on behalf of BattlEye.

So: a display-only overlay of this kind is tolerated today, tolerance is not
approval and can be withdrawn, and an anti-cheat can change what it objects to
at any time.

## The colour filter that was not built

Players asked for a way to hide the grey background of a panel such as the
party list, leaving only names and bars. Doing that properly needs a colour
filter: capture the picture of the selected area, make one colour transparent,
and show the result.

This was **decided against, permanently**. It would mean reading the game's
pixels, and "the program never holds an image of the game" is the most
important promise in this document and the easiest one to verify. No official
answer was available to weigh against that (see above), so the promise stays.
The supported alternative is to mirror only the parts you want - see "Showing
only the bars" in the README.

## Project status

Echo HUD has reached what it set out to do, and its maintainer plans no
further development. The rules in the next section still apply to anyone who
forks the project and wants to keep the claims in this document true.

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
