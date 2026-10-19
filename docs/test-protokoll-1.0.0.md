# Testprotokoll 1.0.0

Release-Kandidat: `GodSaveTheScreen-1.0.0-win-x64.zip` (Git-Hash siehe Dateiinformationen der EXE).
Dieses Protokoll trennt **automatisch geprüfte** Punkte (Stand 2026-10-19) von **manuellen** Prüfungen, die noch ausstehen.

## 1. Automatisch (dotnet test, 573 Tests)

| Bereich | Abdeckung |
|---|---|
| Modell, Undo/Redo, Geometrie | `Core.Tests` (u. a. `UndoManagerTests`, `CropTests`) |
| Projektformat `.gsts` | `ProjectSerializerTests` (alle Annotationstypen, Golden-File `formatVersion=1`, Migration, ZIP-Bombe, unbekannte Typen), `RecentFilesServiceTests` |
| Aufnahme-Logik, Einstellungen, Hotkeys | `CaptureTests`, `M2Tests`, `M3Tests`, `JsonSettingsServiceTests`, `HotkeyGestureTests` |
| Editor, Werkzeuge, Export (Pixeltests) | `M4Tests`, `M6Tests`, `M7M8Tests`, `M9Tests` (Zuschnitt), `M10Tests` (Projekt-Flow, Sicherung, Sprungliste), `M11Tests` |
| Lokalisierung/Barrierefreiheit | `LocalizationCompletenessTests`, `AccessibilityTests` |
| Fenster lassen sich mit echten Ressourcen erzeugen | `WindowSmokeTests` |

Zusätzlich wurde eine Probe-Veröffentlichung (`win-x64`, ReadyToRun, Single-File, 74 MB EXE) lokal gebaut und im portablen Modus gestartet (Start bis zum ersten Fenster ≈ 1,1 s).

## 2. Manuelle Testmatrix (offen)

Konfigurationen: 1 Monitor 100 % · 2 Monitore 100 % + 150 % · Monitor links/oberhalb des primären (negative Koordinaten) · Remote-Desktop-Sitzung · Hell/Dunkel · Hochkontrast.

| # | Prüfung | Ergebnis |
|---|---|---|
| 1 | Rechteck-, Fenster-, Freiform-, Vollbildaufnahme (alle Matrixzeilen) | ☐ |
| 2 | Timer-Aufnahme, Abbruch mit `Esc` | ☐ |
| 3 | Hotkeys, Tray-Menü, Tray-Linksklick, Startfenster | ☐ |
| 4 | Editor: alle Werkzeuge zeichnen, bearbeiten, löschen, Undo/Redo | ☐ |
| 5 | Zuschneiden: Griffe, Abdunklung, Seitenverhältnisse, Übernehmen/Abbrechen, später erweitern | ☐ |
| 6 | Speichern/Exportieren in allen Formaten, Zwischenablage in Fremdprogrammen | ☐ |
| 7 | Projekt speichern, App beenden, öffnen: identische Darstellung, Objekte bearbeitbar | ☐ |
| 8 | Erzwungenes Beenden (Task-Manager) → Wiederherstellung beim nächsten Start | ☐ |
| 9 | Zuletzt verwendet in Startfenster, Tray-Menü, Editor „Öffnen ▾“, Sprungliste | ☐ |
| 10 | `F1`-Übersicht, Erststart-Hinweis (frische Daten) | ☐ |
| 11 | Bedienung nur mit Tastatur (Tab-Reihenfolge, Fokus, `Tab`/Pfeile auf der Zeichenfläche) | ☐ |
| 12 | Narrator liest die Werkzeugleiste vor; Accessibility Insights ohne Fehler | ☐ |
| 13 | Hochkontrast: Zeichenfläche, Auswahl, Lineal gut erkennbar | ☐ |
| 14 | Sehr großes Bild (16 384 × 16 384) → Warnung | ☐ |
| 15 | Gesperrter Bildschirm: Hotkey wird ignoriert | ☐ |
| 16 | Aufnahme-Latenz Hotkey → Overlay bei 2 × 4K (Ziel < 250 ms) | ☐ |
| 17 | Startzeit bis Tray-Symbol (Ziel < 1 s) | ☐ |

## 3. Smoke-Test der veröffentlichten EXE (offen)

| System | Ergebnis |
|---|---|
| Frische Windows-10-VM ohne .NET | ☐ |
| Frische Windows-11-VM ohne .NET | ☐ |
