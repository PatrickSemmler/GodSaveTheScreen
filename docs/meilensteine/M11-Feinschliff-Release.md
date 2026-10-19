# M11 – Feinschliff & Portable Release

← [M10](M10-Projektformat.md) · [Umsetzungsplan](../Umsetzungsplan.md)

## Ziel

Produktreife: vollständige Lokalisierung, Barrierefreiheit, Performance-Optimierung, Stabilität, Dokumentation und ein reproduzierbarer Build als portable Single-File-EXE im ZIP.

## Voraussetzungen

- M0–M10 abgeschlossen

## Umfang

**Enthalten**
- Lokalisierungs-Review DE/EN, Tastenkürzel-Übersicht
- Barrierefreiheit (Tastatur, Screenreader, Hochkontrast)
- Performance- und Speicherprofilierung
- Fehler- und Randfall-Härtung
- Portable Release (x64, optional arm64), Versionierung, Release-Skript

## Aufgaben

### 1. Lokalisierung & Hilfe
- [ ] Alle UI-Texte, Tooltips, Fehlermeldungen, Dialoge in `Strings.resx`/`Strings.de.resx` (Test aus M0 erweitern: keine hartkodierten Strings in XAML – Analyzer/Regex-Test auf `Content="…"`/`Text="…"`)
- [ ] Zahlen-/Datumsformate kulturabhängig; Hotkey-Anzeige lokalisiert („Strg“ vs. „Ctrl“)
- [ ] Fenster „Tastenkürzel“ (`F1` bzw. `Strg+#`) mit allen Kürzeln aus `ToolRegistry` und Befehlen
- [ ] Erststart-Hinweis (einmalig): Hotkeys, Tray, Hinweis auf `Druck`-Taste

### 2. Barrierefreiheit
- [ ] Alle Bedienelemente per Tastatur erreichbar, sichtbarer Fokus, logische Tab-Reihenfolge
- [ ] `AutomationProperties.Name`/`HelpText` für Icon-Buttons (lokalisiert)
- [ ] Objekte auf der Zeichenfläche per `Tab` durchschaltbar (Auswahl), Pfeiltasten verschieben, `Umschalt+Pfeil` 10 px, `Alt+Pfeil` Größe ändern
- [ ] Hochkontrast-Theme: WPF-UI-Verhalten prüfen, Overlay-Farben aus `SystemColors`
- [ ] Prüfung mit Narrator und „Accessibility Insights for Windows“

### 3. Performance & Stabilität
- [ ] Startzeit messen (Ziel < 1 s bis Tray-Icon); ReadyToRun beim Publish (`PublishReadyToRun=true`)
- [ ] Aufnahme-Latenz Hotkey → Overlay sichtbar (Ziel < 250 ms bei 2× 4K)
- [ ] Speicher: Editor schließen gibt Bitmaps frei (Memory-Profiler; Event-Handler-Lecks bei `PropertyChanged` → schwache Events/abmelden)
- [ ] Virtualisierung der Ebenenliste bei vielen Objekten (> 500)
- [ ] Randfälle: Monitor wird während Aufnahme getrennt, RDP-Sitzung, gesperrter Bildschirm (Hotkey ignorieren), sehr große Bilder (16k × 16k → Warnung), Zwischenablage gesperrt, Zielordner nicht beschreibbar, OneDrive-Pfade mit Sonderzeichen
- [ ] Crash-Report-Dialog mit „Log-Ordner öffnen“

### 4. Release
- [ ] Versionierung: `Version` in `Directory.Build.props` (SemVer), `InformationalVersion` mit Git-Hash (`SourceRevisionId`)
- [ ] Publish-Profil `Properties/PublishProfiles/win-x64.pubxml`:
  - `RuntimeIdentifier=win-x64`, `SelfContained=true`, `PublishSingleFile=true`, `IncludeNativeLibrariesForSelfExtract=true`, `EnableCompressionInSingleFile=true`, `PublishReadyToRun=true`
  - **kein** `PublishTrimmed` (WPF nicht trimming-kompatibel)
  - optional `win-arm64.pubxml`
- [ ] `build/release.ps1`: Tests ausführen → Publish → ZIP `GodSaveTheScreen-<Version>-win-x64.zip` mit EXE, `README.txt`, `LICENSE`, `THIRD-PARTY-NOTICES.txt`, leere `portable.txt` als **optional** beschriebene Datei (nicht enthalten → Daten in `%APPDATA%`)
- [ ] `THIRD-PARTY-NOTICES.txt` aus den NuGet-Lizenzen generieren
- [ ] Optional: GitHub-Actions-Workflow (Build + Test + Artefakt), sobald ein Remote existiert
- [ ] Hinweis zu SmartScreen bei unsignierter EXE in README; Code-Signing als Folgepunkt

### 5. Dokumentation
- [ ] `README.md` (Funktionen, Screenshots, Tastenkürzel, Portable-Modus, Build-Anleitung)
- [ ] `docs/agent/architecture.md` final verifizieren (Skill `agent-architecture-doc`, Update-Modus)
- [ ] `CHANGELOG.md`

## Tests

- Vollständiger manueller Regressionstest nach Testmatrix (Umsetzungsplan Kap. 6) – Protokoll in `docs/test-protokoll-<version>.md`
- Smoke-Test der veröffentlichten EXE auf frischer Windows-10- und Windows-11-VM ohne installiertes .NET

## Abnahmekriterien

- [ ] Release-ZIP läuft auf sauberer VM ohne Installation
- [ ] Keine fehlenden Übersetzungen in DE/EN
- [ ] Alle Funktionen per Tastatur bedienbar; Narrator liest Werkzeugleiste vor
- [ ] Zielwerte für Start- und Aufnahmezeit erreicht
- [ ] Alle Tests grün, Build ohne Warnungen
