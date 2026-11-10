# SPDX-License-Identifier: GPL-3.0-only
#
# Builds EchoHUD.exe using the C# compiler that ships with Windows.
# Nothing to install and nothing is downloaded.
#
#   ./scripts/build.ps1                                  -> artifacts/bin/EchoHUD.exe
#   ./scripts/build.ps1 -OutputDirectory artifacts/try2  -> build elsewhere, e.g. while the app is running
#
# NOTE: that compiler understands C# 5 only. Newer syntax (string
# interpolation, "?.", "nameof", "out var", expression-bodied members) will
# not compile - see CONTRIBUTING.md.
param([string]$OutputDirectory = 'artifacts/bin')
$ErrorActionPreference = 'Stop'

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
