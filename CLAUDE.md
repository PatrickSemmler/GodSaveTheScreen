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
- Baseline 19.10.2026: Core.Tests 266 grün; App.Tests 448, 1 Fehler (`SchliessenDesEditors_LeertDenEffektCache`, Punkt #5)

## Änderungshistorie
