@echo off
rem SPDX-License-Identifier: GPL-3.0-only
rem
rem Double-click this file to build Echo HUD from source.
rem It runs scripts\build.ps1, which uses the C# compiler that is already part
rem of Windows. Nothing is downloaded or installed, and no settings are changed.
rem
rem You are asked where the finished EchoHUD.exe should go; pressing Enter
rem puts it on your Desktop. (A copy also stays in artifacts\bin.)
rem
rem   build.bat            ask, build, show the file
rem   build.bat nopause    build into artifacts\bin only, ask nothing (for scripts)
rem
rem Full instructions and troubleshooting:  docs\BUILDING.md

setlocal
cd /d "%~dp0"

echo Building Echo HUD from source.
echo.
if "%~1"=="nopause" (
    powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\build.ps1"
) else (
    powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\build.ps1" -Ask
)
set RESULT=%ERRORLEVEL%
echo.

if "%RESULT%"=="0" (
    echo Done. Double-click EchoHUD.exe to start it.
) else (
    echo The build did not finish. Read the message above, then see docs\BUILDING.md
    echo for the most common causes and how to fix them.
)

rem Keep the window open when started by double-click, so the result can be read.
if not "%~1"=="nopause" pause
exit /b %RESULT%
