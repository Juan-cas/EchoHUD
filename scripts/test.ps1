# SPDX-License-Identifier: GPL-3.0-only
#
# Compiles the application sources together with tests/*.cs into a console
# program and runs it. Exit code 0 means every check passed.
#
#   ./scripts/test.ps1
#
# The tests use a throw-away data folder; they never read or write your
# real profiles, and they open no windows.
$ErrorActionPreference = 'Stop'

$repo = Split-Path $PSScriptRoot
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path -LiteralPath $compiler)) { throw '.NET Framework 4.x (64-bit) was not found. It is part of Windows 10 and 11.' }

$destination = Join-Path $repo 'artifacts\tests'
New-Item -ItemType Directory -Path $destination -Force | Out-Null

$sources = @(Get-ChildItem -LiteralPath "$repo\src" -Filter '*.cs' | Sort-Object Name | ForEach-Object FullName)
$checks = @(Get-ChildItem -LiteralPath "$repo\tests" -Filter '*.cs' | Sort-Object Name | ForEach-Object FullName)
$references = @('/reference:System.Windows.Forms.dll', '/reference:System.Drawing.dll', '/reference:System.Xml.dll')

# /main:Checks picks the test runner's Main instead of the application's.
& $compiler /nologo /target:exe /platform:x64 /main:Checks /debug- /warnaserror+ /codepage:65001 `
    "/out:$destination\Checks.exe" @references @sources @checks
if ($LASTEXITCODE -ne 0) { throw 'Test compilation failed.' }

& "$destination\Checks.exe"
if ($LASTEXITCODE -ne 0) { throw 'Tests failed.' }
