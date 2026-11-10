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
list of open windows with their title, window class, size and position.

## What the program never does

| It never... | How you can check |
|---|---|
| reads or writes another program's memory | `src/Native.cs` is the complete list of system calls. There is no `OpenProcess`, `ReadProcessMemory` or `WriteProcessMemory`. |
| injects code, installs hooks or loads drivers | No `SetWindowsHookEx`, `CreateRemoteThread`, or similar, anywhere. |
| sends keystrokes, clicks or messages to the game | No `SendInput`, `keybd_event`, `mouse_event`, and no `SendMessage`/`PostMessage` declarations at all. |
| captures or analyses pixels | No `BitBlt`, `PrintWindow`, `CopyFromScreen`, no OCR, no image processing. The program never holds an image of the game. |
| reads the game's files or network traffic | The only files touched are its own profiles under `%LOCALAPPDATA%\EchoHUD`. |
| connects to the internet | There is no networking code. No telemetry, updates, accounts or ads. |
| automates anything | It has no notion of game state. It cannot tell a health bar from a wall. |

The game window is identified by its **title and window class only**. The
program does not query the game's process. (For OBS projector windows - never
for the game - it reads the owning program's name from the system process list
to confirm the window belongs to OBS.)

Locked mirrors are "click-through": Windows itself delivers your clicks to the
window underneath. Echo HUD does not see, repeat or redirect them.

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
3. **No input to other programs,** including "harmless" conveniences such as
   focusing or moving the game window.
4. **No hiding.** Do not add anything intended to make the program harder for an
   anti-cheat or the game to see. If a protection blocks a feature, the feature
   is unavailable - document it and stop.
5. **Do not claim approval.** Documentation and UI must not say or imply that
   the program is approved, safe or risk-free.

## If something looks wrong

If you believe the program does something this document says it does not, that
is a bug of the most serious kind. Please open an issue describing what you
observed. Do not include character names, account details or screenshots of
your game.
