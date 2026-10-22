# GodSaveTheScreen – Installer (Agent)
Verified against working tree on top of commit e7168c5 (Installer uncommitted) on 2026-10-21

## Overview
- WiX Toolset 7 (`WixToolset.Sdk/7.0.0` im SDK-Attribut, Extensions in `Directory.Packages.props`); beide Projekte NICHT in `GodSaveTheScreen.slnx`
- Ausgabe je Runtime in `dist/`: `GodSaveTheScreen-<Version>-<rid>.msi`, `…-Setup.exe` (+ ZIP und `.sha256` aus `release.ps1`)

## Modules
| Pfad | Verantwortung | darf abhängen von |
|---|---|---|
| `installer/GodSaveTheScreen.Msi` | MSI (`Package.wxs`, eigener Dialogablauf `UI.wxs`, Texte `Package.<Kultur>.wxl`), Kulturen `en-US;de-DE` | veröffentlichte App (`PublishDir`), `LICENSE`/`NOTICE`/`THIRD-PARTY-NOTICES.txt`, `app.ico` |
| `installer/GodSaveTheScreen.Setup` | Burn-Bundle mit WixStdBA, eigenes `Theme.xml` + `Theme.en-US.wxl`/`Theme.de-DE.wxl` (Payload `1031\thm.wxl`) | fertiges MSI (`MsiPath`), `LicenseRtf`, `LogoPng` |
| `build/build-installer.ps1` | MSI bauen -> Sprachen zusammenführen -> Bundle bauen; erzeugt `License.rtf`, `logo.png`, `banner.bmp`/`dialog.bmp` aus `LICENSE`/`app.ico` in `artifacts/installer/<rid>` | – |
| `build/release.ps1` | Tests -> je Runtime (Standard `win-x64`,`win-arm64`) Publish, ZIP, `build-installer.ps1`; `-SkipInstaller` | – |

## Conventions
- MSI `Scope="perUserOrMachine"`: Standard pro Benutzer (`%LOCALAPPDATA%\Programs\GodSaveTheScreen`), `ALLUSERS=1` = pro Computer; Registry immer `Root="HKMU"` (folgt der Installationsart)
- Öffentliche MSI-Properties: `ALLUSERS`, `MSIINSTALLPERUSER`, `INSTALLFOLDER`; Bundle-Variable (`bal:Overridable`): `InstallScope=PerMachine|PerUser`
- Registry des MSI: `Software\GodSaveTheScreen\Installer` (`InstallFolder`, `Version`), App Paths `GodSaveTheScreen.exe`, `.gsts` -> ProgId `GodSaveTheScreen.Project`
- Neue Dateien neben der EXE: eigene `Component` in `Package.wxs` ergänzen (keine Harvesting-Automatik)
- Neue UI-Texte in beiden `.wxl` je Projekt (IDs müssen übereinstimmen)

## Decisions
- WiX 7 statt 6: nur v7 kann Bundles mit wählbarer Installationsart (configurable-scope bundle). EULA per `<AcceptEula>wix7</AcceptEula>` in beiden `.wixproj` (Entscheidung des Projektinhabers)
- Ein mehrsprachiges MSI: Basis en-US, de-DE als eingebettete Transform `1031` in `_Storages`, Summary-Template `<Plattform>;1033,1031`; Windows Installer wählt nach UI-Sprache
- Eigener Scope-Dialog statt `WixUI_Advanced` (laut WiX-Quelltext experimentell, Benutzerordner `%LOCALAPPDATA%\Apps`); kein Ordner-Dialog
- `MajorUpgrade AllowSameVersionUpgrades="yes"`: Windows Installer ignoriert die 4. Versionsstelle (Bugfix-Releases) -> ICE61 unterdrückt
- `LaunchTarget="GodSaveTheScreen.exe"` (über App Paths), weil der Installationsordner im Bundle von der Laufzeitwahl abhängt

## Pitfalls
- ProductCode muss in beiden Kulturen gleich sein (`-p:ProductCode=` aus `build-installer.ps1`, Prüfung beim Zusammenführen); sonst ändert die Transform ihn und das Bundle erkennt das installierte MSI nicht (Deinstallation über die Setup-EXE tut dann nichts)
- WixStdBA setzt `WixStdBAScope` beim Start immer selbst -> Burn-Schalter `/permachine`/`/peruser` werden ignoriert; deshalb `SetVariable` aus `InstallScope` (läuft bei Detect, nach dem BA-Start)
- `Theme="none"` führt zu „manifest is missing wixstdba information“; eigenes Theme über `Theme="rtfLicense"` + `ThemeFile`
- Bundle-Projekt: `EnableDefaultEmbeddedResourceItems=false`, sonst werden die Theme-`.wxl` als Bundle-Lokalisierung doppelt geladen
- Scope-Wechsel im MSI-Dialog: Ordner wird dort per `SetTargetPath` neu gesetzt (Verzeichnisse sind vor den Dialogen aufgelöst; `INSTALLFOLDER` ist öffentlich und geht an den Server)
- COM-Aufrufe (WindowsInstaller.Installer) in PowerShell: Argumente aus Cmdlets sind `PSObject` -> auspacken (`Invoke-Com`), sonst `DISP_E_TYPEMISMATCH`; alle COM-Objekte freigeben, sonst ist das MSI für den Bundle-Build gesperrt
- Lange Ausgabepfade (Scratchpad) lassen `wix.exe` mit WIX0216 (0x57) scheitern -> kurze Pfade verwenden
- Kein Autostart im Setup (Nutzerentscheidung): „Mit Windows starten“ nur über die App (`AutostartService`, HKCU); das MSI entfernt diesen Wert beim Deinstallieren (CA `RemoveUserAutostart`, nicht beim Update)
- Wechsel Benutzer <-> alle Benutzer bei Updates ergibt zwei Installationen (FindRelatedProducts sucht nur im eigenen Kontext)

## Commands
- Komplett: `pwsh build/release.ps1` (Optionen `-Runtime win-x64`, `-SkipTests`, `-SkipNotices`, `-SkipInstaller`)
- Nur Installer: `pwsh build/build-installer.ps1 -Runtime win-x64 -PublishDir artifacts\publish\win-x64 -OutputDir dist`
- Stiller Test pro Benutzer: `msiexec /i dist\<msi> /qn /l*v <log>`; Setup-EXE: `<exe> /quiet /log <log>`, Deinstallation `<exe> /uninstall /quiet`
