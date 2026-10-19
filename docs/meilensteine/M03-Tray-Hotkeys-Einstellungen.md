# M3 – Tray, globale Hotkeys & Einstellungen

← [M2](M02-Fenster-Freiform-Timer.md) · [Umsetzungsplan](../Umsetzungsplan.md) · Weiter: [M4](M04-Editor-Dateien.md)

## Ziel

Die Anwendung läuft im Hintergrund im Infobereich, Aufnahmen werden per globalem Hotkey ausgelöst, und alle Verhaltensweisen sind über ein Einstellungsfenster konfigurierbar – inklusive der optionalen Nachaufnahme-Aktionen (Zwischenablage, Auto-Speichern) und Autostart.

## Voraussetzungen

- M1 abgeschlossen (M2 empfohlen, damit alle Modi per Hotkey belegt werden können)

## Umfang

**Enthalten**
- Tray-Icon mit Kontextmenü und Doppelklick-Aktion
- Konfigurierbare globale Hotkeys je Aufnahmemodus
- Einstellungsfenster (WPF-UI `NavigationView`)
- Nachaufnahme-Aktionen: Editor (immer), Zwischenablage (optional), Auto-Speichern mit Dateinamensmuster (optional)
- Autostart (HKCU `Run`), Start minimiert im Tray
- Kommandozeile `--capture …` funktioniert (für eigene Tastenkürzel/Verknüpfungen)

**Nicht enthalten**
- Editor-Funktionen (M4+)

## Aufgaben

### 1. Tray
- [x] `TrayService : IHostedService` mit `H.NotifyIcon.Wpf` (`TaskbarIcon`), Icon passend zum Taskleisten-Theme
- [x] Kontextmenü: Rechteck / Fenster / Freiform / Vollbild / Mit Timer ▸ (3, 5, 10 s, letzter Wert) / Bild öffnen… / Startfenster / Einstellungen / Beenden *(Eintrag vorhanden, bis M4 deaktiviert)*
- [x] Linksklick/Doppelklick: konfigurierbar (Standard: Rechteckaufnahme)
- [x] Benachrichtigung „Gespeichert unter …“ bei Auto-Speichern (klickbar → Ordner öffnen)
- [x] Schließen des Startfensters beendet die App nicht, solange Tray aktiv ist; „Beenden“ fragt bei ungespeicherten Editoren nach

### 2. Globale Hotkeys
- [x] `IHotkeyService` (Register/Unregister/Re-Register nach Einstellungsänderung) auf Basis `NHotkey.Wpf` (`HotkeyManager.Current.AddOrReplace`)
- [x] `HotkeyGesture`-Modell im Core (Modifiers + Key, serialisierbar, Anzeige-String lokalisiert)
- [x] `HotkeyAlreadyRegisteredException` abfangen → Status „belegt“ in den Einstellungen, Snackbar-Hinweis *(Hinweis als Status in der Hotkey-Zeile statt Snackbar)*
- [x] Sonderfall `Druck`-Taste: bei Belegung durch Windows Hinweis + Button „Windows-Einstellung öffnen“ (`ms-settings:easeofaccess-keyboard`)
- [x] `HotkeyRecorder`-Control: fokussieren, Tastenkombination drücken, Anzeige; `Entf` = löschen; Konflikterkennung innerhalb der App

### 3. Einstellungsfenster
- [x] `SettingsWindow` mit `NavigationView`-Seiten:
  - **Allgemein:** Sprache, Theme, Autostart, Start im Tray, Tray-Klickaktion
  - **Aufnahme:** Standardmodus, Standard-Timer, Mauszeiger aufnehmen, Fensteraufnahme-Quelle, abgerundete Ecken, eigene Fenster ausschließen, Lupe im Overlay an/aus, Countdown-Ton
  - **Nach der Aufnahme:** In Zwischenablage kopieren (an/aus), Automatisch speichern (an/aus), Zielordner, Dateinamensmuster, Format (PNG/JPEG …), Editor öffnen (immer an, ausgegraut – Anforderung)
  - **Tastenkürzel:** Liste der globalen Hotkeys mit Recorder
  - **Info:** Version, Pfade (Einstellungen/Logs mit „Öffnen“-Button), Lizenzen der NuGet-Pakete
- [x] Änderungen wirken sofort (außer Sprache → Hinweis „Neustart erforderlich“)

### 4. Nachaufnahme-Pipeline
- [x] `IPostCaptureAction`-Kette im `CaptureCoordinator`: `CopyToClipboardAction`, `AutoSaveAction`, `OpenEditorAction` (Reihenfolge fest, Fehler einer Aktion stoppt die anderen nicht)
- [x] `FileNamePattern` (Core): Platzhalter `{date:yyyy-MM-dd}`, `{time:HH-mm-ss}`, `{mode}`, `{window}` (Fenstertitel, bereinigt), `{counter:000}`; ungültige Dateinamenzeichen ersetzen, Kollisionen mit Suffix ` (2)` auflösen
- [x] Standard-Zielordner: `%USERPROFILE%\Pictures\Screenshots\GodSaveTheScreen`

### 5. Autostart & Kommandozeile
- [x] `AutostartService`: `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` mit Pfad der EXE + `--tray`; Pfadänderung (portable verschoben) beim Start erkennen und korrigieren
- [x] Auswertung von `--capture`, `--delay`, `--tray` aus M0-Parser (eigene Instanz und weitergeleitet)

## Technische Details

- **Warum NHotkey:** dünner, erprobter Wrapper um `RegisterHotKey` mit WPF-`Key`/`ModifierKeys`; kein eigener Message-Loop-Code nötig.
- Hotkey-Auslösung während einer laufenden Aufnahme wird ignoriert (Coordinator ist reentrancy-geschützt).

## Tests

- `Core.Tests`: `FileNamePattern` (alle Platzhalter, ungültige Zeichen, Kollisionen über gemocktes Dateisystem), `HotkeyGesture` Serialisierung/Parsing
- `App.Tests`: `SettingsViewModel` speichert über `ISettingsService`, Hotkey-Konflikterkennung, Nachaufnahme-Pipeline (Clipboard-Fehler → Auto-Save läuft trotzdem)

## Abnahmekriterien

- [ ] App startet mit `--tray` nur im Infobereich; alle Menüeinträge funktionieren
- [ ] Hotkeys lösen Aufnahmen aus jeder Anwendung heraus aus; Änderung im Recorder wirkt sofort
- [ ] Belegte Hotkeys werden klar angezeigt
- [ ] Auto-Zwischenablage und Auto-Speichern arbeiten unabhängig voneinander; Dateinamen entsprechen dem Muster
- [ ] Autostart-Haken erzeugt/entfernt den Registry-Eintrag
