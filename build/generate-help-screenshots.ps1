<#
.SYNOPSIS
  Erzeugt die Bildschirmfotos der Benutzeranleitung neu (docs/help/de/images und docs/help/en/images).
.DESCRIPTION
  Baut das Testprojekt (mit kurzem ArtifactsPath, siehe Pitfalls in docs/agent/architecture.md) und startet nur den Test
  HelpScreenshotGenerator mit GSTS_HELP_OUT = docs/help. Die Fenster werden außerhalb des sichtbaren Bildschirms
  gezeigt und mit 144 dpi gerendert (Thema Hell, Sprachen Deutsch und Englisch).
  Danach einen normalen Build ausführen: er bettet die Bilder in die HTML-Dateien neben der EXE ein.
.EXAMPLE
  pwsh build/generate-help-screenshots.ps1
#>
[CmdletBinding()]
param(
    [string]$ArtifactsPath = 'C:/Temp/gsts-help'
)

$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent

# Beim allerersten Lauf fehlen die Bilder noch: der Bundle-Schritt darf sie dann überspringen.
dotnet build (Join-Path $root 'tests/GodSaveTheScreen.App.Tests') "-p:ArtifactsPath=$ArtifactsPath" -p:HelpAllowMissingImages=true -v q
if ($LASTEXITCODE -ne 0) { throw 'Build fehlgeschlagen' }

$env:GSTS_HELP_OUT = Join-Path $root 'docs/help'
try {
    & (Join-Path $ArtifactsPath 'bin/GodSaveTheScreen.App.Tests/debug/GodSaveTheScreen.App.Tests.exe') `
        -method 'GodSaveTheScreen.App.Tests.HelpScreenshotGenerator.Hilfe_Bildschirmfotos_Erzeugen'
    if ($LASTEXITCODE -ne 0) { throw 'Erzeugen der Bildschirmfotos fehlgeschlagen' }
}
finally {
    Remove-Item Env:GSTS_HELP_OUT
}

foreach ($language in 'de', 'en') {
    $images = Get-ChildItem (Join-Path $root "docs/help/$language/images") -Filter *.png
    $size = ($images | Measure-Object Length -Sum).Sum / 1MB
    Write-Host ("{0}: {1} Bilder, {2:N1} MB" -f $language, $images.Count, $size) -ForegroundColor Green
}
