# GodSaveTheScreen

Screenshot- und Annotationstool, .NET 10 / C# / WPF (MVVM). Umsetzungsplan: `docs/Umsetzungsplan.md`, Meilensteine: `docs/meilensteine/`.

- Sprache für Doku, Commits und UI-Texte: Deutsch (UI zusätzlich Englisch über `.resx`)
- Arbeit erfolgt meilensteinweise; vor Beginn das jeweilige Meilenstein-Dokument lesen und Checkboxen beim Abschluss abhaken

## Agent-Architekturdoku
- Pfad: `docs/agent/architecture.md` (Index auf weitere Dateien in `docs/agent/`)
- Lesen vor: Änderungen an Modulstruktur, Abhängigkeiten, übergreifenden Features; bei Unsicherheit über Konventionen
- Pflicht: Bei Änderung von Modulstruktur, Abhängigkeiten oder Konventionen die Doku im selben Commit aktualisieren (Skill `agent-architecture-doc`)
- Stand: M0–M13 gegen den Code verifiziert (Skill bei Änderungen im Update-Modus nutzen)

## Verbesserungsliste (Skill `find-improvements`)
- Datei: `Verbesserungsliste.html` im Projektstamm (eigenständig, `file://`-tauglich)
- Nach jeder umgesetzten Änderung: Status-Badge des Eintrags auf „In Arbeit“ bzw. „Erledigt“ setzen (CSS-Klasse `badge-inprogress` / `badge-erledigt` UND Text), Datum im Kopf und Footer aktualisieren; Zähler und Fortschrittsbalken berechnet das eingebettete JavaScript
- IDs sind stabil, nie neu vergeben; nicht mehr zutreffende Punkte auf „Obsolet“ setzen statt löschen
- Pro Änderungspunkt ein eigener Commit (Code + Liste + Eintrag in der Änderungshistorie zusammen)
- Tests: `dotnet test` im Solution-Root führt hier 0 Tests aus (Exitcode 5, Punkt #7); funktionierend: Testprojekt-`.exe` aus der Ausgabe starten, z. B. `dotnet build -p:ArtifactsPath=C:/Temp/gsts-art` und dann `C:/Temp/gsts-art/bin/GodSaveTheScreen.Core.Tests/debug/GodSaveTheScreen.Core.Tests.exe`
- Baseline 19.10.2026: Core.Tests 266 grün; App.Tests 448, 1 Fehler (inzwischen behoben, #5)

## Änderungshistorie

### Settings-Speichern fängt IO-Fehler ab – 19.10.2026
- **Kategorie**: Stabilität
- **Verbesserungsliste-ID**: #1
- **Geänderte Dateien**: `src/GodSaveTheScreen.Core/Settings/JsonSettingsService.cs`, `tests/GodSaveTheScreen.Core.Tests/JsonSettingsServiceTests.cs`
- **Was wurde gemacht**: `SaveAsync` fängt `IOException`/`UnauthorizedAccessException` und protokolliert sie
- **Warum**: Aufrufer starten `SaveAsync` fire-and-forget (`_ = ...`); Fehler waren unbeobachtet und unsichtbar
- **Seiteneffekte / Hinweise**: `SaveAsync` wirft bei diesen Fehlern nicht mehr; der Benutzer bekommt keine Meldung, nur Log-Eintrag

### Instabilen Cache-Test repariert – 19.10.2026
- **Kategorie**: Technik
- **Verbesserungsliste-ID**: #5
- **Geänderte Dateien**: `src/GodSaveTheScreen.Rendering/RegionEffects.cs`, `tests/GodSaveTheScreen.App.Tests/M11Tests.cs`
- **Was wurde gemacht**: `EffectCache.CountFor(baseId)` ergänzt; der Test prüft nur noch Einträge des eigenen Bildes und ruft `RemoveImage` nicht mehr selbst auf (das hatte die Prüfung von `Dispose` verdeckt)
- **Warum**: Vergleich des globalen Zählers von `EffectCache.Shared` war durch parallele Tests unzuverlässig
- **Seiteneffekte / Hinweise**: App.Tests 448/448 grün in 3 Läufen

### Drag-Temp-Dateien werden aufgeräumt – 19.10.2026
- **Kategorie**: Stabilität
- **Verbesserungsliste-ID**: #2
- **Geänderte Dateien**: `src/GodSaveTheScreen.App/Services/DragTempFiles.cs` (neu), `App.xaml.cs`, `ViewModels/EditorViewModel.cs`, `tests/GodSaveTheScreen.App.Tests/DragTempFilesTests.cs` (neu), `docs/agent/architecture.md`
- **Was wurde gemacht**: Beim Start löscht ein Hintergrund-Task PNGs älter als 1 Tag im Drag-Temp-Ordner
- **Warum**: Die Dateien aus „Bild ziehen“ wurden nie gelöscht
- **Seiteneffekte / Hinweise**: App.Tests 451/451 grün. `EditorViewModel.cs` enthielt fremde, uncommittete Änderungen des Nutzers; nur die eigene Zeile wurde gestaged
