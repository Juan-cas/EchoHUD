# SPDX-License-Identifier: GPL-3.0-only
#
# Builds EchoHUD.exe using the C# compiler that ships with Windows.
# Nothing to install and nothing is downloaded.
#
#   ./scripts/build.ps1                                  -> artifacts/bin/EchoHUD.exe
#   ./scripts/build.ps1 -OutputDirectory artifacts/try2  -> build elsewhere, e.g. while the app is running
#   ./scripts/build.ps1 -CopyTo Desktop                  -> also put a copy on your Desktop
#   ./scripts/build.ps1 -CopyTo "D:\Games\Tools"         -> also put a copy in that folder
#   ./scripts/build.ps1 -Ask                             -> ask where to put the copy (what build.bat does)
#
# The program is always built inside artifacts/ first. -CopyTo and -Ask then
# copy the finished EchoHUD.exe to a place that is easier to find.
#
# NOTE: that compiler understands C# 5 only. Newer syntax (string
# interpolation, "?.", "nameof", "out var", expression-bodied members) will
# not compile - see CONTRIBUTING.md.
param(
    [string]$OutputDirectory = 'artifacts/bin',
    [string]$CopyTo = '',
    [switch]$Ask
)
$ErrorActionPreference = 'Stop'

# Ask first, so the answer is not requested after a wait.
if ($Ask) {
    $desktop = [Environment]::GetFolderPath('Desktop')
    Write-Host 'Where should EchoHUD.exe be placed?'
    Write-Host "  - Press Enter to put it on your Desktop ($desktop)"
    Write-Host '  - or type or paste a folder, for example D:\Games\Tools'
    $answer = (Read-Host 'Folder').Trim().Trim('"')
    $CopyTo = if ($answer) { $answer } else { 'Desktop' }
    Write-Host ''
}
if ($CopyTo -eq 'Desktop') { $CopyTo = [Environment]::GetFolderPath('Desktop') }
if ($CopyTo) {
    $CopyTo = [System.IO.Path]::GetFullPath([Environment]::ExpandEnvironmentVariables($CopyTo))
}

$repo = Split-Path $PSScriptRoot
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path -LiteralPath $compiler)) { throw '.NET Framework 4.x (64-bit) was not found. It is part of Windows 10 and 11.' }

# Refuse to write outside artifacts/, which is the only folder Git ignores for build output.
$destination = [System.IO.Path]::GetFullPath((Join-Path $repo $OutputDirectory))
$artifactRoot = [System.IO.Path]::GetFullPath((Join-Path $repo 'artifacts')) + [System.IO.Path]::DirectorySeparatorChar
if (-not $destination.StartsWith($artifactRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw 'Build output must be inside the artifacts directory.'
}
New-Item -ItemType Directory -Path $destination -Force | Out-Null

$sources = @(Get-ChildItem -LiteralPath "$repo\src" -Filter '*.cs' | Sort-Object Name | ForEach-Object FullName)
$references = @('/reference:System.Windows.Forms.dll', '/reference:System.Drawing.dll', '/reference:System.Xml.dll')

& $compiler /nologo /target:winexe /platform:x64 /optimize+ /debug- /warnaserror+ /codepage:65001 `
    "/win32icon:$repo\assets\EchoHUD.ico" "/out:$destination\EchoHUD.exe" @references @sources
if ($LASTEXITCODE -ne 0) { throw 'Compilation failed.' }

Copy-Item -LiteralPath "$repo\LICENSE" -Destination $destination
Write-Output "Built $OutputDirectory/EchoHUD.exe"

if ($CopyTo) {
    New-Item -ItemType Directory -Path $CopyTo -Force | Out-Null
    $copy = Join-Path $CopyTo 'EchoHUD.exe'
    try {
        Copy-Item -LiteralPath "$destination\EchoHUD.exe" -Destination $copy -Force
    }
    catch {
        throw "Could not write $copy. If Echo HUD is running from there, close it (notification area icon -> Exit) and build again. Your new build is still in $destination."
    }
    Write-Output "Copied to $copy"
    # Show the file in File Explorer when a person is watching.
    if ($Ask) { Start-Process explorer.exe "/select,`"$copy`"" }
}
