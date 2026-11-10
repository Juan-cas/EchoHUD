@echo off
rem SPDX-License-Identifier: GPL-3.0-only
rem
rem Double-click this file to build Echo HUD from source.
rem It runs scripts\build.ps1, which uses the C# compiler that is already part
rem of Windows. Nothing is downloaded or installed, and no settings are changed.
rem
rem The finished program appears in:  artifacts\bin\EchoHUD.exe
rem Full instructions and troubleshooting:  docs\BUILDING.md

setlocal
cd /d "%~dp0"

echo Building Echo HUD...
echo.
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\build.ps1"
set RESULT=%ERRORLEVEL%
echo.

if "%RESULT%"=="0" (
    echo Done. Your program is here:
    echo   %~dp0artifacts\bin\EchoHUD.exe
    if not "%~1"=="nopause" start "" explorer "%~dp0artifacts\bin"
) else (
    echo The build did not finish. Read the message above, then see docs\BUILDING.md
    echo for the most common causes and how to fix them.
)

rem Keep the window open when started by double-click, so the result can be read.
if not "%~1"=="nopause" pause
exit /b %RESULT%
