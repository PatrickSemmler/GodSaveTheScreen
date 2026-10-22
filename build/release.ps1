<#
.SYNOPSIS
  Baut das Release: Tests -> je Plattform Publish -> ZIP (portabel) -> MSI und Setup-EXE.
.DESCRIPTION
  Ergebnis in dist\ (je Plattform, jeweils mit .sha256):
    GodSaveTheScreen-<Version>-<Runtime>.zip        portabel: EXE, Hilfe und Lizenzseite (DE/EN), README.txt, LICENSE, NOTICE,
                                                    THIRD-PARTY-NOTICES.txt. portable.txt liegt bewusst NICHT bei (siehe README.txt:
                                                    ohne sie liegen die Daten in %APPDATA%).
    GodSaveTheScreen-<Version>-<Runtime>.msi        für Softwareverteilung (pro Benutzer oder pro Computer), siehe build-installer.ps1
    GodSaveTheScreen-<Version>-<Runtime>-Setup.exe  Setup mit Auswahl "nur für mich" / "für alle Benutzer"
.PARAMETER Runtime
  Eine oder mehrere Plattformen: win-x64, win-arm64 (Standard: beide).
.PARAMETER SkipTests
  Überspringt dotnet test (nur für Probeläufe).
.PARAMETER SkipNotices
  Erzeugt THIRD-PARTY-NOTICES.txt nicht neu.
.PARAMETER SkipInstaller
  Baut nur die ZIP-Dateien, kein MSI und keine Setup-EXE.
#>
[CmdletBinding()]
param(
    [ValidateSet('win-x64', 'win-arm64')]
    [string[]]$Runtime = @('win-x64', 'win-arm64'),
    [switch]$SkipTests,
    [switch]$SkipNotices,
    [switch]$SkipInstaller
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
Set-Location $root

function Invoke-Step([string]$Title, [scriptblock]$Action) {
    Write-Host "==> $Title" -ForegroundColor Cyan
    & $Action
    if ($LASTEXITCODE -ne 0) { throw "Schritt fehlgeschlagen: $Title (Exitcode $LASTEXITCODE)" }
}

function Write-Sha256([string]$File) {
    $hash = (Get-FileHash $File -Algorithm SHA256).Hash
    Set-Content -Path "$File.sha256" -Value "$hash  $(Split-Path $File -Leaf)"
    return $hash
}

# Version aus Directory.Build.props (vierstellig, Regel siehe CLAUDE.md)
[xml]$props = Get-Content (Join-Path $root 'Directory.Build.props')
$version = ($props.Project.PropertyGroup | Where-Object { $_.Version } | Select-Object -First 1).Version
if ($version -notmatch '^\d+\.\d+\.\d+\.\d+$') { throw "Ungültige Version in Directory.Build.props: '$version' (erwartet: Major.Minor.Patch.Fix)" }
Write-Host "GodSaveTheScreen $version ($($Runtime -join ', '))" -ForegroundColor Green

if (-not $SkipNotices) {
    Invoke-Step 'Lizenzhinweise erzeugen' { & (Join-Path $PSScriptRoot 'generate-notices.ps1') }
}

if (-not $SkipTests) {
    Invoke-Step 'Tests' { dotnet test --configuration Release }
}

$dist = Join-Path $root 'dist'
New-Item -ItemType Directory -Path $dist -Force | Out-Null
$results = [System.Collections.Generic.List[string]]::new()

foreach ($rid in $Runtime) {
    $publishDir = Join-Path $root "artifacts\publish\$rid"
    if (Test-Path $publishDir) { Remove-Item $publishDir -Recurse -Force }
    Invoke-Step "Publish ($rid)" { dotnet publish src/GodSaveTheScreen.App --configuration Release "-p:PublishProfile=$rid" }

    $exe = Join-Path $publishDir 'GodSaveTheScreen.exe'
    if (-not (Test-Path $exe)) { throw "Die veröffentlichte EXE fehlt: $exe" }

    $name = "GodSaveTheScreen-$version-$rid"
    $staging = Join-Path $root "artifacts\release\$name"
    if (Test-Path $staging) { Remove-Item $staging -Recurse -Force }
    New-Item -ItemType Directory -Path $staging | Out-Null

    Copy-Item $exe $staging
    # Benutzeranleitung und Lizenzseite (deutsch/englisch) liegen neben der EXE, damit F1 bzw. Einstellungen → Info sie finden.
    foreach ($help in 'GodSaveTheScreen-Hilfe.html', 'GodSaveTheScreen-Help.html', 'GodSaveTheScreen-Lizenz.html', 'GodSaveTheScreen-License.html') {
        $helpFile = Join-Path $publishDir $help
        if (-not (Test-Path $helpFile)) { throw "Die Datei fehlt: $helpFile" }
        Copy-Item $helpFile $staging
    }
    (Get-Content (Join-Path $PSScriptRoot 'README.txt') -Raw).Replace('{{VERSION}}', $version) |
        Set-Content (Join-Path $staging 'README.txt') -Encoding UTF8
    # Apache-2.0 §4: Lizenz und NOTICE liegen jeder Weitergabe bei.
    foreach ($file in 'LICENSE', 'NOTICE', 'THIRD-PARTY-NOTICES.txt') {
        Copy-Item (Join-Path $root $file) $staging
    }

    $zip = Join-Path $dist "$name.zip"
    if (Test-Path $zip) { Remove-Item $zip -Force }
    Compress-Archive -Path (Join-Path $staging '*') -DestinationPath $zip
    [void](Write-Sha256 $zip)
    $results.Add($zip)

    if (-not $SkipInstaller) {
        Invoke-Step "Installer ($rid)" { & (Join-Path $PSScriptRoot 'build-installer.ps1') -Runtime $rid -PublishDir $publishDir -OutputDir $dist }
        foreach ($installer in "$name.msi", "$name-Setup.exe") {
            [void](Write-Sha256 (Join-Path $dist $installer))
            $results.Add((Join-Path $dist $installer))
        }
    }
}

Write-Host ''
Write-Host "Fertig ($dist):" -ForegroundColor Green
foreach ($file in $results) { Write-Host "  $(Split-Path $file -Leaf)" }
