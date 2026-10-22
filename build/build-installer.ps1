<#
.SYNOPSIS
  Baut MSI und Setup-EXE (WiX 7) aus einer veröffentlichten App.
.DESCRIPTION
  1. MSI je Sprache (en-US, de-DE) bauen (installer/GodSaveTheScreen.Msi)
  2. Zu einem MSI zusammenführen: Englisch als Basis, Deutsch als eingebettete Sprachumwandlung ("1031");
     Windows Installer wählt sie anhand der Anzeigesprache automatisch aus.
  3. Setup-EXE um dieses MSI bauen (installer/GodSaveTheScreen.Setup)
  Ergebnis in -OutputDir: GodSaveTheScreen-<Version>-<Runtime>.msi und GodSaveTheScreen-<Version>-<Runtime>-Setup.exe
  Wird von release.ps1 aufgerufen; einzeln z. B.:
    pwsh build/build-installer.ps1 -Runtime win-x64 -PublishDir artifacts\publish\win-x64 -OutputDir dist
.PARAMETER Runtime
  win-x64 oder win-arm64.
.PARAMETER PublishDir
  Ordner mit GodSaveTheScreen.exe und den Hilfe-/Lizenzseiten (Ergebnis von dotnet publish).
.PARAMETER OutputDir
  Zielordner für MSI und EXE.
#>
[CmdletBinding()]
param(
    [ValidateSet('win-x64', 'win-arm64')]
    [string]$Runtime = 'win-x64',
    [Parameter(Mandatory)]
    [string]$PublishDir,
    [Parameter(Mandatory)]
    [string]$OutputDir
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot

[xml]$props = Get-Content (Join-Path $root 'Directory.Build.props')
$version = ($props.Project.PropertyGroup | Where-Object { $_.Version } | Select-Object -First 1).Version
$platform = @{ 'win-x64' = 'x64'; 'win-arm64' = 'ARM64' }[$Runtime]
$PublishDir = (Resolve-Path $PublishDir).Path.TrimEnd('\') + '\'
New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null
$OutputDir = (Resolve-Path $OutputDir).Path

$work = Join-Path $root "artifacts\installer\$Runtime"
if (Test-Path $work) { Remove-Item $work -Recurse -Force }
New-Item -ItemType Directory -Path $work | Out-Null

function Invoke-Build([string]$Title, [string[]]$Arguments) {
    Write-Host "==> $Title" -ForegroundColor Cyan
    & dotnet build @Arguments
    if ($LASTEXITCODE -ne 0) { throw "Schritt fehlgeschlagen: $Title (Exitcode $LASTEXITCODE)" }
}

# LICENSE als RTF für die Setup-EXE (Burn zeigt nur RTF an).
function New-LicenseRtf([string]$Source, [string]$Target) {
    $sb = [System.Text.StringBuilder]::new('{\rtf1\ansi\deff0{\fonttbl{\f0\fmodern Consolas;}}\fs16 ')
    foreach ($line in (Get-Content $Source -Encoding UTF8)) {
        foreach ($ch in $line.ToCharArray()) {
            if ($ch -eq '\' -or $ch -eq '{' -or $ch -eq '}') { [void]$sb.Append('\').Append($ch) }
            elseif ([int]$ch -gt 127) { [void]$sb.Append("\u$([int]$ch)?") }
            else { [void]$sb.Append($ch) }
        }
        [void]$sb.Append("\par`n")
    }
    [void]$sb.Append('}')
    [System.IO.File]::WriteAllText($Target, $sb.ToString(), [System.Text.Encoding]::ASCII)
}

# 64×64-Bild aus dem App-Symbol für den Kopf der Setup-EXE.
function New-LogoPng([string]$Icon, [string]$Target) {
    Add-Type -AssemblyName System.Drawing
    $ico = [System.Drawing.Icon]::new($Icon, 64, 64)
    try { $ico.ToBitmap().Save($Target, [System.Drawing.Imaging.ImageFormat]::Png) } finally { $ico.Dispose() }
}

# Bilder der MSI-Oberfläche statt der WiX-Standardbilder: Banner (493×58) und Willkommen/Fertig (493×312), Fläche links in einem
# hellen Ton aus dem Rand des App-Symbols.
function New-MsiBitmaps([string]$Icon, [string]$Banner, [string]$Dialog) {
    Add-Type -AssemblyName System.Drawing
    $ico = [System.Drawing.Icon]::new($Icon, 64, 64)
    $logo = $ico.ToBitmap()
    $accent = $logo.GetPixel(32, 4)
    try {
        $bmp = [System.Drawing.Bitmap]::new(493, 58)
        $g = [System.Drawing.Graphics]::FromImage($bmp)
        $g.InterpolationMode = 'HighQualityBicubic'
        $g.Clear([System.Drawing.Color]::White)
        $g.DrawImage($logo, 431, 6, 46, 46)
        $bmp.Save($Banner, [System.Drawing.Imaging.ImageFormat]::Bmp)
        $g.Dispose(); $bmp.Dispose()

        $bmp = [System.Drawing.Bitmap]::new(493, 312)
        $g = [System.Drawing.Graphics]::FromImage($bmp)
        $g.InterpolationMode = 'HighQualityBicubic'
        $g.Clear([System.Drawing.Color]::White)
        $brush = [System.Drawing.SolidBrush]::new($accent)
        $g.FillRectangle($brush, 0, 0, 164, 312)
        $g.DrawImage($logo, 42, 38, 80, 80)
        $bmp.Save($Dialog, [System.Drawing.Imaging.ImageFormat]::Bmp)
        $brush.Dispose(); $g.Dispose(); $bmp.Dispose()
    } finally { $logo.Dispose(); $ico.Dispose() }
}

# Windows-Installer-Automatisierung (COM, nur späte Bindung). Argumente auspacken: Cmdlet-Ausgaben (z. B. Join-Path) sind
# PSObject-Hüllen und kämen sonst als IDispatch an (DISP_E_TYPEMISMATCH).
function Invoke-Com($Object, [string]$Name, [object[]]$Arguments = @(), [System.Reflection.BindingFlags]$Kind = 'InvokeMethod') {
    $plain = [object[]]@($Arguments | ForEach-Object { if ($_ -is [psobject]) { $_.PSObject.BaseObject } else { $_ } })
    $Object.GetType().InvokeMember($Name, $Kind, $null, $Object, $plain)
}

function Merge-MsiLanguages([string]$BaseMsi, [string]$LanguageMsi, [int]$LanguageId, [string]$Target) {
    Copy-Item $BaseMsi $Target -Force
    $mst = Join-Path $work "$LanguageId.mst"
    $installer = New-Object -ComObject WindowsInstaller.Installer
    $db = Invoke-Com $installer 'OpenDatabase' @($Target, 1)              # msiOpenDatabaseModeTransact
    $langDb = Invoke-Com $installer 'OpenDatabase' @($LanguageMsi, 0)     # msiOpenDatabaseModeReadOnly
    # Die Umwandlung darf nur Texte ändern; ein anderer ProductCode würde die Erkennung durch die Setup-EXE brechen.
    $codes = foreach ($d in $db, $langDb) {
        $v = Invoke-Com $d 'OpenView' @("SELECT ``Value`` FROM ``Property`` WHERE ``Property`` = 'ProductCode'")
        [void](Invoke-Com $v 'Execute')
        $r = Invoke-Com $v 'Fetch'
        Invoke-Com $r 'StringData' @(1) 'GetProperty'
        [void](Invoke-Com $v 'Close')
        foreach ($o in $r, $v) { [void][System.Runtime.InteropServices.Marshal]::ReleaseComObject($o) }
    }
    if ($codes[0] -ne $codes[1]) { throw "ProductCode der Sprachvarianten unterscheidet sich: $($codes -join ' / ')" }
    [void](Invoke-Com $langDb 'GenerateTransform' @($db, $mst))
    [void](Invoke-Com $langDb 'CreateTransformSummaryInfo' @($db, $mst, 0, 0))
    [void][System.Runtime.InteropServices.Marshal]::ReleaseComObject($langDb)

    $view = Invoke-Com $db 'OpenView' @('SELECT `Name`, `Data` FROM `_Storages`')
    $record = Invoke-Com $installer 'CreateRecord' @(2)
    [void](Invoke-Com $record 'StringData' @(1, "$LanguageId") 'SetProperty')
    [void](Invoke-Com $record 'SetStream' @(2, $mst))
    [void](Invoke-Com $view 'Execute' @($record))
    [void](Invoke-Com $view 'Modify' @(3, $record))                       # msiViewModifyAssign
    [void](Invoke-Com $view 'Close')

    # Template "<Plattform>;1033" -> "<Plattform>;1033,1031": Liste der Sprachen, die das Paket unterstützt.
    $summary = Invoke-Com $db 'SummaryInformation' @(1) 'GetProperty'
    $template = Invoke-Com $summary 'Property' @(7) 'GetProperty'
    [void](Invoke-Com $summary 'Property' @(7, "$template,$LanguageId") 'SetProperty')
    [void](Invoke-Com $summary 'Persist')
    [void](Invoke-Com $db 'Commit')
    foreach ($o in $summary, $record, $view, $db, $installer) { [void][System.Runtime.InteropServices.Marshal]::ReleaseComObject($o) }
    # Erst nach der Freigabe aller COM-Hüllen ist das MSI für den Bundle-Build wieder lesbar.
    [GC]::Collect(); [GC]::WaitForPendingFinalizers()
    Write-Host "    $template -> $template,$LanguageId"
}

$licenseRtf = Join-Path $work 'License.rtf'
$logoPng = Join-Path $work 'logo.png'
New-LicenseRtf (Join-Path $root 'LICENSE') $licenseRtf
New-LogoPng (Join-Path $root 'src\GodSaveTheScreen.App\Assets\app.ico') $logoPng
$bannerBmp = Join-Path $work 'banner.bmp'
$dialogBmp = Join-Path $work 'dialog.bmp'
New-MsiBitmaps (Join-Path $root 'src\GodSaveTheScreen.App\Assets\app.ico') $bannerBmp $dialogBmp

$msiProject = Join-Path $root 'installer\GodSaveTheScreen.Msi\GodSaveTheScreen.Msi.wixproj'
$msiOut = Join-Path $work 'msi\'
$productCode = [guid]::NewGuid().ToString('B').ToUpperInvariant()
Invoke-Build "MSI ($platform)" @($msiProject, '-c', 'Release', "-p:Platform=$platform", "-p:PublishDir=$PublishDir", "-p:OutputPath=$msiOut", "-p:ProductCode=$productCode", "-p:BannerBmp=$bannerBmp", "-p:DialogBmp=$dialogBmp")

$name = "GodSaveTheScreen-$version-$Runtime"
$msi = Join-Path $OutputDir "$name.msi"
Write-Host '==> Sprachen zusammenführen' -ForegroundColor Cyan
Merge-MsiLanguages (Join-Path $msiOut 'en-US\GodSaveTheScreen.msi') (Join-Path $msiOut 'de-DE\GodSaveTheScreen.msi') 1031 $msi

$bundleProject = Join-Path $root 'installer\GodSaveTheScreen.Setup\GodSaveTheScreen.Setup.wixproj'
$bundleOut = Join-Path $work 'setup\'
Invoke-Build "Setup-EXE ($platform)" @($bundleProject, '-c', 'Release', "-p:Platform=$platform", "-p:MsiPath=$msi", "-p:LicenseRtf=$licenseRtf", "-p:LogoPng=$logoPng", "-p:OutputPath=$bundleOut")
$exe = Join-Path $OutputDir "$name-Setup.exe"
Copy-Item (Join-Path $bundleOut 'GodSaveTheScreen-Setup.exe') $exe -Force

Write-Host "Installer: $msi" -ForegroundColor Green
Write-Host "           $exe" -ForegroundColor Green
