# M11 – Feinschliff & Portable Release

← [M10](M10-Projektformat.md) · [Umsetzungsplan](../Umsetzungsplan.md) · Weiter: [M12](M12-Werkzeugvorlagen.md)

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
- [x] Alle UI-Texte, Tooltips, Fehlermeldungen, Dialoge in `Strings.resx`/`Strings.de.resx` (Test aus M0 erweitern: keine hartkodierten Strings in XAML – Analyzer/Regex-Test auf `Content="…"`/`Text="…"`) – `LocalizationCompletenessTests` scannt alle XAML-Dateien (Literale, `{l:Loc}`-Schlüssel, DE/EN, Platzhalter)
- [x] Zahlen-/Datumsformate kulturabhängig; Hotkey-Anzeige lokalisiert („Strg“ vs. „Ctrl“) – Formatierung über `CurrentUICulture`, Kürzel über `ShortcutText`/`HotkeyGesture.ToDisplayString`
- [x] Fenster „Tastenkürzel“ (`F1` bzw. `Strg+#`) mit allen Kürzeln aus `ToolRegistry` und Befehlen – `F1` bzw. `Strg+#`, `ShortcutCatalog` (Werkzeuge aus `ToolRegistry`, Befehle, globale Hotkeys)
- [x] Erststart-Hinweis (einmalig): Hotkeys, Tray, Hinweis auf `Druck`-Taste – `FirstRunHint`

### 2. Barrierefreiheit
- [ ] Alle Bedienelemente per Tastatur erreichbar, sichtbarer Fokus, logische Tab-Reihenfolge – nicht manuell geprüft (Tab-Reihenfolge, Fokusdarstellung)
- [x] `AutomationProperties.Name`/`HelpText` für Icon-Buttons (lokalisiert) – `AccessibilityTests` erzwingen Namen für Symbolschaltflächen; `HelpText` wird nicht gesetzt
- [x] Objekte auf der Zeichenfläche per `Tab` durchschaltbar (Auswahl), Pfeiltasten verschieben, `Umschalt+Pfeil` 10 px, `Alt+Pfeil` Größe ändern – `Tab`/`Umschalt+Tab`, Pfeile, `Umschalt+Pfeil`, `Alt+Pfeil` (`SelectTool`)
- [ ] Hochkontrast-Theme: WPF-UI-Verhalten prüfen, Overlay-Farben aus `SystemColors` – Overlay-Farben der Zeichenfläche folgen `SystemColors`; das Verhalten von WPF-UI im Hochkontrast ist nicht geprüft
- [ ] Prüfung mit Narrator und „Accessibility Insights for Windows“ – steht aus (manuell)

### 3. Performance & Stabilität
- [ ] Startzeit messen (Ziel < 1 s bis Tray-Icon); ReadyToRun beim Publish (`PublishReadyToRun=true`) – gemessen ≈ 1,1 s bis zum ersten Fenster (ReadyToRun ist aktiv); Ziel < 1 s bis Tray-Symbol nicht belegt
- [ ] Aufnahme-Latenz Hotkey → Overlay sichtbar (Ziel < 250 ms bei 2× 4K) – nicht gemessen
- [ ] Speicher: Editor schließen gibt Bitmaps frei (Memory-Profiler; Event-Handler-Lecks bei `PropertyChanged` → schwache Events/abmelden) – teilweise umgesetzt (Zeichenfläche gibt Bitmap frei, `EffectCache.RemoveImage`, Handler werden in `Dispose` abgemeldet); Memory-Profiler nicht eingesetzt
- [x] Virtualisierung der Ebenenliste bei vielen Objekten (> 500) – `VirtualizingPanel` mit Recycling
- [ ] Randfälle: Monitor wird während Aufnahme getrennt, RDP-Sitzung, gesperrter Bildschirm (Hotkey ignorieren), sehr große Bilder (16k × 16k → Warnung), Zwischenablage gesperrt, Zielordner nicht beschreibbar, OneDrive-Pfade mit Sonderzeichen – umgesetzt/getestet: sehr große Bilder (`LargeImageGuard`), gesperrte/getrennte Sitzung (`SessionState`), OneDrive-Sonderzeichen (Test), Zwischenablage (Retry), nicht beschreibbare Ziele (Fehlerdialog); nicht gesondert behandelt: Monitor wird während der Aufnahme getrennt (nur allgemeiner Fehlerdialog)
- [x] Crash-Report-Dialog mit „Log-Ordner öffnen“ – `IDialogService.ShowCrash`

### 4. Release
- [x] Versionierung: `Version` in `Directory.Build.props` (SemVer), `InformationalVersion` mit Git-Hash (`SourceRevisionId`) – `Version` 1.0.0, Git-Hash über `Directory.Build.targets`
- [x] Publish-Profil `Properties/PublishProfiles/win-x64.pubxml`: – `win-x64.pubxml` und `win-arm64.pubxml`
  - `RuntimeIdentifier=win-x64`, `SelfContained=true`, `PublishSingleFile=true`, `IncludeNativeLibrariesForSelfExtract=true`, `EnableCompressionInSingleFile=true`, `PublishReadyToRun=true`
  - **kein** `PublishTrimmed` (WPF nicht trimming-kompatibel)
  - optional `win-arm64.pubxml`
- [x] `build/release.ps1`: Tests ausführen → Publish → ZIP `GodSaveTheScreen-<Version>-win-x64.zip` mit EXE, `README.txt`, `LICENSE`, `THIRD-PARTY-NOTICES.txt`, leere `portable.txt` als **optional** beschriebene Datei (nicht enthalten → Daten in `%APPDATA%`) – Tests → Publish → ZIP (+ SHA256); ohne `LICENSE` im Repository warnt das Skript
- [x] `THIRD-PARTY-NOTICES.txt` aus den NuGet-Lizenzen generieren – `build/generate-notices.ps1`
- [ ] Optional: GitHub-Actions-Workflow (Build + Test + Artefakt), sobald ein Remote existiert – kein Remote vorhanden
- [x] Hinweis zu SmartScreen bei unsignierter EXE in README; Code-Signing als Folgepunkt – `build/README.txt` und `README.md` (`README.de.md`)

### 5. Dokumentation
- [x] `README.md` (Funktionen, Screenshots, Tastenkürzel, Portable-Modus, Build-Anleitung) – Screenshots ergänzt 22.10.2026, englisch (`README.md`) und deutsch (`README.de.md`)
- [x] `docs/agent/architecture.md` final verifizieren (Skill `agent-architecture-doc`, Update-Modus) – gegen den Code abgeglichen (M0–M11)
- [x] `CHANGELOG.md`

## Tests

- Vollständiger manueller Regressionstest nach Testmatrix (Umsetzungsplan Kap. 6) – Protokoll in `docs/test-protokoll-<version>.md`
- Smoke-Test der veröffentlichten EXE auf frischer Windows-10- und Windows-11-VM ohne installiertes .NET

## Abnahmekriterien

- [ ] Release-ZIP läuft auf sauberer VM ohne Installation – steht aus (manuell); ZIP und EXE wurden lokal gebaut und gestartet
- [x] Keine fehlenden Übersetzungen in DE/EN – durch Tests abgesichert
- [ ] Alle Funktionen per Tastatur bedienbar; Narrator liest Werkzeugleiste vor – steht aus (manuell, Narrator)
- [ ] Zielwerte für Start- und Aufnahmezeit erreicht – nicht erreicht bzw. nicht gemessen (siehe oben)
- [x] Alle Tests grün, Build ohne Warnungen – 573 Tests, Build ohne Warnungen

## Stand der manuellen Prüfungen

Offen und nur von Hand möglich: Regressionstest nach Testmatrix (Protokoll-Vorlage `docs/test-protokoll-1.0.0.md`), Smoke-Test der EXE auf frischen Windows-10-/11-VMs ohne .NET, Narrator und Accessibility Insights, Hochkontrast, Messung der Aufnahme-Latenz, Memory-Profiler. Eine `LICENSE`-Datei fehlt im Repository (Lizenzwahl steht beim Eigentümer).
