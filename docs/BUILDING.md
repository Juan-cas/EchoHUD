# Building Echo HUD yourself

You do not have to trust a downloaded `.exe`. This page shows how to turn the
source code in this repository into the program on your own computer. It takes
about a minute and needs no programming knowledge.

## What you need

- A PC with **64-bit Windows 10 or Windows 11**.
- That is all. The tool that turns the code into a program (the C# compiler) is
  already part of Windows. Nothing is downloaded or installed during the build.

## The short version

1. Download the code and unpack it.
2. Double-click **`build.bat`**.
3. Run **`artifacts\bin\EchoHUD.exe`**.

The rest of this page explains each step and what to do if something goes wrong.

## Step 1 - Get the code

**Without Git (easiest):**

1. Open the project's page on GitHub.
2. Click the green **Code** button, then **Download ZIP**.
3. Open your Downloads folder, right-click the ZIP file and choose
   **Extract All...**. Pick a simple location such as `C:\EchoHUD`.

> Do not run anything from inside the ZIP preview window. Extract it first -
> the build needs real files on disk.

**With Git or GitHub Desktop:** clone the repository as usual. In GitHub Desktop
that is **File -> Clone repository**.

## Step 2 - Build

Open the folder you extracted. You should see `build.bat`, `README.md` and
folders named `src`, `scripts` and `docs`.

**Double-click `build.bat`.**

A black window opens, prints `Building Echo HUD...` and, a few seconds later,
`Done.` It then opens the folder that contains your freshly built program.

If Windows shows a blue **"Windows protected your PC"** box or an
**"Open File - Security Warning"**, that is Windows being careful with a script
that came from the internet. Click **More info -> Run anyway** (or **Run**).
You can read `build.bat` and `scripts\build.ps1` in Notepad first if you like -
they are short and commented.

### Prefer to type the command?

Open PowerShell in the project folder (in File Explorer: hold Shift,
right-click an empty area, **Open PowerShell window here**) and run:

```powershell
powershell -ExecutionPolicy Bypass -File scripts/build.ps1
```

`-ExecutionPolicy Bypass` applies to this one command only. It does not change
any setting on your computer.

## Step 3 - Run it

Your program is at:

```text
artifacts\bin\EchoHUD.exe
```

Double-click it. You can copy `EchoHUD.exe` anywhere you like - the Desktop, a
USB stick - it is a single file and needs nothing beside it. (The `LICENSE`
file next to it is the legal text; keep it with the program if you pass the
program on to someone else.)

The first time you start it, Windows SmartScreen may warn about an
"unrecognized app". That is normal for a program you built yourself, because it
is not signed by a company. Choose **More info -> Run anyway**.

To start using it, see the "Using it" section of the [README](../README.md).

## Optional - check that your build is healthy

The project comes with automated checks. They open no windows and do not touch
your saved profiles.

```powershell
powershell -ExecutionPolicy Bypass -File scripts/test.ps1
```

The last line should say `0 failed`.

## Updating to a newer version

1. Close Echo HUD.
2. Get the new code (download the ZIP again, or pull in Git / GitHub Desktop).
3. Double-click `build.bat` again.

Your profiles are stored separately in `%LOCALAPPDATA%\EchoHUD` and are kept.

## If something goes wrong

| What you see | What it means and what to do |
|---|---|
| `.NET Framework 4.x (64-bit) was not found` | Very rare on Windows 10/11. Install ".NET Framework 4.8 Runtime" from Microsoft's website, or run Windows Update, then try again. 32-bit Windows is not supported. |
| `Compilation failed` followed by `error CS0016` or "the file is being used by another process" | Echo HUD is still running, so its file cannot be replaced. Close it (and check it is not hidden behind other windows) and build again. |
| `Compilation failed` with other `error CS....` lines | The code itself has a problem. If you did not change anything, download a fresh copy; if it still fails, please open an issue and paste the error lines. |
| `running scripts is disabled on this system` | You ran `scripts\build.ps1` directly. Use `build.bat` or the full command shown above instead. |
| The window flashes and closes immediately | You probably started it from inside the ZIP. Extract the ZIP first (Step 1). |
| "Access is denied" or nothing appears in `artifacts` | The folder is somewhere Windows protects, such as `C:\Program Files`. Move the project to a normal folder like `C:\EchoHUD` or your Documents. |
| Your antivirus removes `EchoHUD.exe` | Freshly built, unsigned programs are sometimes flagged by guesswork. You can inspect every line of the source; if you are satisfied, restore the file or add an exception for the project folder. |
| Windows blocks the program with "an administrator has blocked this app" | Your PC is managed (work or school) and only allows approved programs. Echo HUD cannot and should not bypass that; ask whoever manages the PC. |

Still stuck? Open an issue on GitHub. Say which Windows version you use and
paste the text from the black window. Please do not include character names or
screenshots of your game.

## What the build actually does

For the curious, `scripts/build.ps1` does three things:

1. Finds the C# compiler at
   `%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe`.
2. Compiles every `.cs` file in `src\` into one program,
   `artifacts\bin\EchoHUD.exe`, with the icon from `assets\`.
3. Copies the `LICENSE` file next to it.

It writes only inside the `artifacts` folder, which you can delete at any time.
