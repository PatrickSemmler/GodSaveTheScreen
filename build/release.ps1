<#
.SYNOPSIS
  Baut das portable Release: Tests -> Publish -> ZIP.
.DESCRIPTION
  Ergebnis: artifacts\release\GodSaveTheScreen-<Version>-<Runtime>.zip mit der EXE, README.txt, LICENSE (falls vorhanden)
  und THIRD-PARTY-NOTICES.txt. Die Datei portable.txt liegt bewusst NICHT bei (siehe README.txt: ohne sie liegen die Daten in %APPDATA%).
.PARAMETER Runtime
  win-x64 (Standard) oder win-arm64.
.PARAMETER SkipTests
  Überspringt dotnet test (nur für Probeläufe).
.PARAMETER SkipNotices
  Erzeugt THIRD-PARTY-NOTICES.txt nicht neu.
#>
[CmdletBinding()]
param(
    [ValidateSet('win-x64', 'win-arm64')]
    [string]$Runtime = 'win-x64',
    [switch]$SkipTests,
    [switch]$SkipNotices
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
Set-Location $root

function Invoke-Step([string]$Title, [scriptblock]$Action) {
    Write-Host "==> $Title" -ForegroundColor Cyan
    & $Action
    if ($LASTEXITCODE -ne 0) { throw "Schritt fehlgeschlagen: $Title (Exitcode $LASTEXITCODE)" }
}

# Version aus Directory.Build.props (SemVer)
[xml]$props = Get-Content (Join-Path $root 'Directory.Build.props')
$version = ($props.Project.PropertyGroup | Where-Object { $_.Version } | Select-Object -First 1).Version
if ($version -notmatch '^\d+\.\d+\.\d+(-[0-9A-Za-z.-]+)?$') { throw "Ungültige Version in Directory.Build.props: '$version'" }
Write-Host "GodSaveTheScreen $version ($Runtime)" -ForegroundColor Green

if (-not $SkipNotices) {
    Invoke-Step 'Lizenzhinweise erzeugen' { & (Join-Path $PSScriptRoot 'generate-notices.ps1') }
}

if (-not $SkipTests) {
    Invoke-Step 'Tests' { dotnet test --configuration Release }
}

$publishDir = Join-Path $root "artifacts\publish\$Runtime"
if (Test-Path $publishDir) { Remove-Item $publishDir -Recurse -Force }
Invoke-Step 'Publish' { dotnet publish src/GodSaveTheScreen.App --configuration Release "-p:PublishProfile=$Runtime" }

$exe = Join-Path $publishDir 'GodSaveTheScreen.exe'
if (-not (Test-Path $exe)) { throw "Die veröffentlichte EXE fehlt: $exe" }

$name = "GodSaveTheScreen-$version-$Runtime"
$staging = Join-Path $root "artifacts\release\$name"
if (Test-Path $staging) { Remove-Item $staging -Recurse -Force }
New-Item -ItemType Directory -Path $staging | Out-Null

Copy-Item $exe $staging
(Get-Content (Join-Path $PSScriptRoot 'README.txt') -Raw).Replace('{{VERSION}}', $version) |
    Set-Content (Join-Path $staging 'README.txt') -Encoding UTF8
Copy-Item (Join-Path $root 'THIRD-PARTY-NOTICES.txt') $staging
if (Test-Path (Join-Path $root 'LICENSE')) {
    Copy-Item (Join-Path $root 'LICENSE') $staging
} else {
    Write-Warning 'Es gibt keine LICENSE-Datei im Repository – das ZIP enthält sie nicht.'
}

$zip = Join-Path $root "artifacts\release\$name.zip"
if (Test-Path $zip) { Remove-Item $zip -Force }
Compress-Archive -Path (Join-Path $staging '*') -DestinationPath $zip
$hash = (Get-FileHash $zip -Algorithm SHA256).Hash
Set-Content -Path "$zip.sha256" -Value "$hash  $name.zip"

Write-Host ''
Write-Host "Fertig: $zip" -ForegroundColor Green
Write-Host "SHA256: $hash"
