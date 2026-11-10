# Contributing

Thanks for wanting to help. The project aims to stay **small, readable and
display-only**. A change that makes it harder to understand or audit needs a
strong reason.

## Before you start

- Read [docs/SAFETY.md](docs/SAFETY.md). Its "rules for people changing the
  code" are non-negotiable: no memory access, no injection, no input to other
  programs, no pixel capture or analysis, no networking, no evasion.
- Read [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) for how things fit together.

## Build and test

```powershell
powershell -ExecutionPolicy Bypass -File scripts/build.ps1
```

```powershell
powershell -ExecutionPolicy Bypass -File scripts/test.ps1
```

Double-clicking `build.bat` does the same as the first command;
[docs/BUILDING.md](docs/BUILDING.md) has the walkthrough for newcomers.

No SDK, package manager or internet access is involved. Build output goes to
`artifacts/`, which Git ignores. If the app is running, build somewhere else:
`scripts/build.ps1 -OutputDirectory artifacts/try`.

## The C# 5 rule

The build uses the compiler that ships with Windows, which stops at **C# 5**.
These will not compile:

| Do not write | Write instead |
|---|---|
| `$"Hello {name}"` | `"Hello " + name` or `String.Format` |
| `obj?.Member` | an explicit `null` check |
| `nameof(x)` | the string `"x"` |
| `int Size => 4;` | `int Size { get { return 4; } }` |
| `out var value` | declare the variable first |
| `public int X { get; } = 1;` | a field, or a property set in the constructor |

Warnings are treated as errors.

## Code style

- One topic per file, with a header comment that explains *what the file is for*
  in plain language. Someone should be able to read the headers alone and
  understand the program.
- Comment the **why**, especially around Windows quirks. Do not narrate what
  the next line obviously does.
- Keep maths in `Geometry.cs` as pure functions and add a check for it in
  `tests/Checks.cs`.
- Every user-visible string goes in `Strings.cs`, in every language. The tests
  fail if a key is missing.
- Every new system call goes in `Native.cs`, with a comment, and must be
  justified in the pull request.
- Every icon-only button needs a tooltip and an `AccessibleName`.
- Sizes in UI code are design pixels wrapped in `Theme.Px(...)`.
- 4 spaces, braces on their own line (`.editorconfig` has the details).

## What to check before opening a pull request

1. `scripts/test.ps1` prints `0 failed`.
2. You walked through the relevant part of the manual checklist in
   [docs/TESTING.md](docs/TESTING.md) with the Demo window.
3. Documentation still matches the behaviour - including the "never does"
   table in SAFETY.md and the verification table in TESTING.md.
4. No personal data: no profiles, character names, screenshots of a real game,
   or local paths in the commit.

## Testing with a game

Never automate anything involving a game client, not even for testing. Use the
Demo window for development. When checking against a real game, interact with
the game yourself, as a player.
