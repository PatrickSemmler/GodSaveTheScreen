# Verbesserungen – Projektgedächtnis

## Technische Bestandsaufnahme

Stand der Analyse: 22.10.2026, Version **1.10.2.0**, Ausgangscommit `6c06501`, Branch `main`. Der getrackte Arbeitsbaum war zu Beginn sauber. Keine übergeordneten oder projektspezifischen AGENTS.md vorhanden; der Verbesserungsworkflow wurde bei dieser Analyse ergänzt. `CLAUDE.md` wurde als zusätzliche Projektinformation und historische Änderungschronik gelesen.

- **Architektur:** `GodSaveTheScreen.slnx`, vier Module: `Core` (plattformneutrales Dokumentmodell, Undo, Geometrie, JSON, Einstellungen), `Capture` (Windows-Aufnahme), `Rendering` (WPF-Bildcodecs und Rendering), `App` (WPF/MVVM, Fenster, Werkzeuge, Dienste). Zwei xUnit-v3-Testprojekte. Keine Datenbank und kein Webbackend.
- **Technik:** .NET 10, WPF, CommunityToolkit.Mvvm 8.4.2, WPF-UI 4.3.0, Microsoft.Extensions.Hosting 10.0.13, Svg.Skia 5.2.3, Vortice.Direct2D1 3.8.3, Serilog.Extensions.Hosting 10.0.0, xunit.v3 4.0.2. Paketversionen zentral in `Directory.Packages.props`; vollständige Abhängigkeitsliste dort. Diese Versionsangaben sind Bestandsdaten, keine Aussage über aktuelle Upgrades oder Sicherheitsfreiheit.
- **Einstieg:** `App.OnStartup` erstellt Host/DI, Single-Instance-Pipe, Tray, Hotkeys und Wiederherstellung. Aufnahme: `CaptureCoordinator` → Zwischenablage/Autosave → `EditorService`. Editoränderungen über `EditorContext` und `UndoManager`; Darstellung und Export teilen Renderer.
- **Persistenz:** `settings.json`, `recent.json`, ZIP-Projekte `.gsts` mit JSON, Originalbild, optionaler Maske und eingebetteten Bilddateien. Projekt-, Bildexport- und Einstellungsspeicherung verwenden temporäre Dateien; Aufnahme-Autosave noch nicht (#3). Kein Versions-/Datenbankmigrationslauf nötig für diese Analyse.
- **UI:** Deutsch/Englisch über `Strings.resx` und `Strings.de.resx`, Hell/Dunkel/Hochkontrast, Werkzeugkürzel, Ebenen, aufklappbare Eigenschaften, Hilfeseiten. WPF-Tests prüfen u. a. Lokalisierung, Accessibility-Namen, Rendering und Fensteraufbau.
- **Build:** `dotnet build GodSaveTheScreen.slnx`. Zum Vermeiden gesperrter App-Ausgaben ein eigenes `ArtifactsPath` setzen. Start: `dotnet run --project src/GodSaveTheScreen.App` (startet echte Tray-/Hotkey-/Settings-Dienste; für isolierte Prüfungen Testdienste verwenden).

### Baseline vor Dokumentationsänderungen

Geprüft mit SDK **10.0.401**, Runtime **10.0.13** auf Windows:

```powershell
$reviewArtifacts = Join-Path (Get-Location) 'artifacts\review-20261022'
dotnet build GodSaveTheScreen.slnx -p:ArtifactsPath="$reviewArtifacts"
& "$reviewArtifacts\bin\GodSaveTheScreen.Core.Tests\debug\GodSaveTheScreen.Core.Tests.exe"
& "$reviewArtifacts\bin\GodSaveTheScreen.App.Tests\debug\GodSaveTheScreen.App.Tests.exe"
dotnet test GodSaveTheScreen.slnx --no-build --no-restore -p:ArtifactsPath="$reviewArtifacts"
```

- Build: **0 Warnungen, 0 Fehler**, Exitcode 0.
- Core: **388/388 bestanden**, keine übersprungenen Tests.
- App: **621/621 bestanden**, keine übersprungenen Tests.
- Solution-Testlauf: **1.009/1.009 bestanden**, Exitcode 0. Der historische Befund #7 ist damit obsolet; die entsprechende gegenteilige Aussage in README/CLAUDE.md ist überholt.
- Erste Sandbox-Versuche scheiterten an Dateizugriffen und WPF-Registry-Zugriffen (Core 6, App 147 Fehler). Erst die erlaubten Läufe außerhalb der Sandbox liefern die oben dokumentierte aussagekräftige Baseline. Diese Zugriffsfehler sind keine Produktfehler.
- `dotnet --info` diagnostiziert unabhängig davon die ungültige SDK-Auswahl in `global.json` (#12). `latestFeature` benötigt `sdk.version`; siehe [Microsoft-Dokumentation](https://learn.microsoft.com/en-us/dotnet/core/tools/global-json).

### Tatsächlich geprüfte Abdeckung

Vertieft gelesen: Aufnahme-/Nachaufnahmeablauf, Editor öffnen/speichern/schließen, Undo und Dirty-Zustand, automatische Sicherung/Wiederherstellung, Projektserialisierung, Einstellungen, Dateinamensmuster, Bild-/SVG-/GIF-Import, zentrale XAML-Ansichten, DI/Start und globale Fehlerbehandlung. Dazu bestehende Tests dieser Pfade und die gesamte automatisierte Testsuite. Stichproben: Ressourcen, Tastatur-/Accessibility-Verankerung, IPC und Größenbegrenzungen beim Projektlesen. Suche nach TODO/FIXME/HACK und typischen Secret-Bezeichnern im Anwendungscode ergab keine zusätzlichen konkreten Funde.

Nicht vollständig geprüft: jede Zeichen-/Geometrievariante, reale Mehrmonitor-/DPI-/Narrator-Bedienung, Installer/Updates/UAC, ARM64-Hardware, vollständiges NuGet-Sicherheitsaudit und Leistungsvermessung mit großen Bildern. Keine echte Aufnahme oder Änderung von Benutzereinstellungen gestartet. UI-Bewertung beruht auf gelesenem XAML/Code und vorhandenen WPF-Smoke-Tests, nicht auf einem vollständigen manuellen Desktop-Durchlauf.

## Fachlicher Kontext

GodSaveTheScreen ist ein lokales Screenshot- und Annotationstool für Windows. Benutzer nehmen per Hotkey, Tray oder Startfenster auf, bearbeiten das Bild und exportieren es oder speichern ein weiterbearbeitbares Projekt. Ein Projekt enthält das unveränderte Original; Unschärfe, Transparenzmaske und Zuschnitt entfernen Inhalte darin nicht endgültig. Die automatische Sicherung soll ungespeicherte Arbeit nach einem Absturz wiederherstellen.

## Zentrale Anwendungsfälle

1. Bildschirmbereich/Fenster/Vollbild mit optionalem Countdown aufnehmen; Zwischenablage und Aufnahme-Autosave optional nutzen.
2. Formen, Texte, Sprechblasen, Stempel, Emojis und Bildobjekte bearbeiten; Undo/Redo, Maskieren und Zuschneiden verwenden.
3. Ein gerendertes Bild weitergeben oder ein `.gsts`-Projekt zur späteren Bearbeitung speichern.
4. Ungespeicherte Änderungen beim Schließen erkennen und nach einem Absturz wiederherstellen.
5. Hotkeys, Werkzeugvorgaben, Vorlagen, Dateinamensmuster und Erscheinungsbild konfigurieren.

## Bekannte Besonderheiten & Fallstricke

- **Bestandsliste:** IDs #1–#7 wurden übernommen. #1 (Settings-IO-Logging), #2 (Drag-Dateien aufräumen), #5 (Cache-Test isolieren) bleiben erledigt. Frühere Änderungen vom 19.–22.10.2026 stehen ausführlich in `CLAUDE.md`; deren bestehende Historie wurde nicht verändert.
- #3, #4 und #6 sind weiterhin offen. #7 ist nach neuem Testlauf obsolet. Neue IDs #8–#16 dürfen nicht neu vergeben werden.
- **Bestätigte Fehler:** #8 löscht Sicherungen auch nach fehlgeschlagenem Lesen; #9 verliert den Dirty-Zustand neuer Dokumente nach Undo; #10 markiert Änderungen während eines asynchronen Speichervorgangs als gespeichert; #11 lässt einen laufenden Backup-Schreibvorgang eine bereits entfernte Sicherung neu anlegen.
- **Sicherungsstart:** Der Idle-Timer wird erst nach dem ersten `Track` wirksam; Track erfolgt aktuell erst im regelmäßigen Sicherungslauf (#14).
- **Import:** GIF-Vorschauen erzeugen Vollbildkopien pro Frame, bevor verkleinert wird (#13). Die 3,86-GiB-Angabe in der Liste ist eine Rechnung für 500 × 1920 × 1080 × 4 Byte, keine Messung.
- **Dateiformat:** ZIP-Einträge werden anhand fester Namen gelesen und begrenzt; eingebettete Assets besitzen Größenlimits. SVG unterbindet externe Ressourcen. Daraus folgt keine vollständige Sicherheitsfreigabe.
- **Projekt/Export:** Der Originalinhalt-Hinweis beim Projektspeichern prüft bisher nur `BlurAnnotation`, nicht Maske/Zuschnitt (#15).
- **Threading:** WPF-Dokument-Schnappschüsse auf dem UI-Thread erstellen, Hintergrundkodierung und Dateioperationen mit dem Dokumentlebenszyklus koordinieren. WPF-Tests brauchen STA; existierende gemeinsame Application-Testcollection respektieren.
- **Fenster:** Editor startet mit fester Größe auf dem Monitor am Mauszeiger; #6 betrifft das Merken von Größe/Position/Zustand. Zuschneiden ist bewusst auf das Basisbild begrenzt, während die Zeichenfläche um überstehende Objekte wachsen kann; dies wurde nicht als neuer Bug eingetragen.
- **Git:** Die vorhandene `.gitignore` ignoriert `Verbesserungsliste.html`. Für den vom ausdrücklich aufgerufenen Skill verlangten Dokumentationscommit wird genau diese Datei gezielt mit `git add -f` aufgenommen; keine allgemeine Ignore-Regel geändert. Lokale Diagnosehilfen liegen im ohnehin ignorierten `artifacts/` und gehören nicht zum Commit.

### Gezielte lokale Reproduktionen

Diagnoseprogramm: `artifacts/review-probes/ReviewProbes.csproj` und `Program.cs` (lokale, ignorierte Analysehilfen; kein Anwendungscode geändert). Nur künstliche Pixelpuffer, Ersatzdienste und Dateien im Build-Ausgabeordner. Ausgeführt mit dem gleichen `ArtifactsPath` wie die Baseline:

```text
UNDO: never saved, IsDirty=False, SourcePath=null, ProjectPath=null
SAVE: edited during pending save, IsDirty=False, Annotations=1
RECOVERY: failed read, Opened=0, BackupExists=False
BACKUP RACE: saved during backup, IsDirty=False, BackupExists=True
```

Der Save-Probe verzögert den Projektdienst kontrolliert und führt währenddessen eine echte Undo-Aktion aus. Der Recovery-Probe simuliert einen vorübergehenden `IOException`. Der Backup-Probe lässt den Schreibabschluss erst nach `MarkSaved` zu. Diese Resultate bestätigen die Zustands-/Fehlerpfade; keine Aussage über ihre Häufigkeit im Alltag.

## Offene Fragen & Annahmen

- Noch keine IDs zur Umsetzung ausgewählt. „Fahre fort“ während der laufenden Analyse wird als Fortsetzung der Analyse verstanden. Anwendungscode bleibt bis zur Auswahl unverändert.
- #16 (Dateinamensvorschau) ist ausdrücklich eine Erweiterungsidee, kein Defekt.
- Aufwand und Risiko sind relative Einschätzungen. #9/#10 erfordern Regressionstests für Laden, erstmaliges Speichern, Undo/Redo und Schließen. #8 muss bewusstes Verwerfen von fehlgeschlagener Wiederherstellung unterscheiden.
- Keine pauschalen Paketupdates empfohlen: es liegt kein belegter Befund zu veralteten/verwundbaren Abhängigkeiten vor. Ein vollständiges Paket-Audit ist nicht Bestandteil der behaupteten Abdeckung.
- SDK-Mindestversion bei Umsetzung von #12 anhand der tatsächlich benötigten Compiler-/SDK-Funktionen festlegen; 10.0.401 ist die geprüfte Umgebung.

## Arbeitsregeln für Verbesserungen

- Nach jeder umgesetzten Änderung `Verbesserungsliste.html` aktualisieren: Status-Badge von „Offen“ auf „In Arbeit“ bzw. „Erledigt“ setzen (CSS-Klasse `badge-offen` / `badge-inprogress` / `badge-erledigt` **und** Text), Datum im Kopfbereich und Footer aktualisieren. Zähler und Fortschrittsbalken berechnet das eingebettete JavaScript; nicht manuell pflegen.
- IDs stabil halten. Bereits erledigte Punkte nicht zurücksetzen; nicht mehr zutreffende Einträge mit Begründung als „Obsolet“ markieren.
- Pro ausgewähltem Änderungspunkt ein eigener Commit aus Code, Liste und zugehörigem Dokumentationseintrag. Fremde Änderungen erhalten und nicht mitstagen.
- `docs/verbesserungen.md` als letzten Dokumentationsschritt jeder Umsetzung aktualisieren: Änderungshistorie ergänzen und Erkenntnisse in die passenden Abschnitte aufnehmen.
- Gegen die dokumentierte Baseline bauen/testen; keine selbst verursachte Regression als abgeschlossen markieren. UI-Texte in beiden vorhandenen Sprachressourcen pflegen.

## Änderungshistorie

### Verbesserungsliste fortgeschrieben und Projektgedächtnis angelegt – 22.10.2026

- **Kategorie:** Technik / Dokumentation (Analyse)
- **Verbesserungsliste-ID:** #8–#16 neu; #7 obsolet; #1–#6 erhalten
- **Version:** 1.10.2.0 (unverändert)
- **Geänderte Dateien:** `Verbesserungsliste.html`, `AGENTS.md`, `docs/verbesserungen.md`
- **Was wurde gemacht:** Bestehende Liste um neun belegte bzw. ausdrücklich als Idee benannte Punkte ergänzt. Historische Statuswerte bewahrt, überholten Testbefund mit Prüfergebnis versehen. Projektgedächtnis und kurzen Verbesserungsworkflow angelegt.
- **Warum:** Zuerst konkrete Datenverlust- und Speicherfehler beheben können; jedem Punkt Nutzen, Fundstelle, Umsetzung und Akzeptanzkriterien zuordnen.
- **Prüfung:** Baseline-Build ohne Fehler/Warnungen; 1.009 Tests erfolgreich, auch über `dotnet test`. Vier zusätzliche lokale Diagnosefälle reproduzieren #8–#11. HTML-Struktur mit 16 Einträgen und 69 eindeutigen IDs geprüft; native Buttons mit ARIA-Zustand und vollständige Detail-/Chevron-/Status-Paare vorhanden. Eingebettetes JavaScript ohne Laufzeitfehler in einem lokalen DOM-Testmodell ausgeführt: Initialzustand, alle 16 Auf-/Zuklapp-Paare, alle 16 Filterkombinationen, Leerzustände und In-Arbeit-Fortschritt geprüft. Keine externen Ressourcen erforderlich. Diagnosehilfen: `artifacts/review-probes/check_html.py` und `check_html.cjs`.
- **Prüfgrenze HTML:** Der eingebettete Browser verweigert `file://` aus Protokoll-Sicherheitsgründen. Deshalb keine visuelle Browser-/Layoutprüfung und kein tatsächlicher Enter-/Leertasten-Test; native Button-Semantik und vorhandene Fokus-CSS statisch geprüft. Es wurde kein anderer Browserweg zur Umgehung dieser Sperre verwendet.
- **Seiteneffekte / Hinweise:** Keine Anwendungsänderungen, kein Versionsbump, kein Push/Release/Deployment. Neue Punkte stehen auf „Offen“; Umsetzung folgt erst nach Auswahl.

---
> **Hinweis für Codex:** Diese Datei bei Verbesserungsanalysen und der Umsetzung ausgewählter Punkte lesen.
> Nach jeder umgesetzten Änderung die Änderungshistorie ergänzen, `Verbesserungsliste.html` aktualisieren und neue Erkenntnisse in die jeweiligen Abschnitte einpflegen.
