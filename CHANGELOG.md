# Änderungen

Das Format orientiert sich an [Keep a Changelog](https://keepachangelog.com/de/1.1.0/), die Versionen folgen [SemVer](https://semver.org/lang/de/).

## [1.0.0] – 2026-10-19

Erste Version mit vollem Funktionsumfang (Meilensteine M0–M11).

### Neu
- **Aufnahme:** Rechteck, Fenster, Freiform, Vollbild (aktueller Monitor / alle Monitore), Timer, globale Hotkeys, Tray-Symbol, Startfenster (M1–M3).
- **Editor:** Bilder laden und speichern (PNG, JPEG, BMP, GIF, TIFF, JPEG XR), Zwischenablage, Zoom/Pan, Drag & Drop (M4).
- **Objekte:** Formen, Text, nummerierte Marker, Stift, Textmarker, Radierer, Lineal, Unschärfe/Verpixeln/Schwärzen, Lupe – alle bearbeitbar, mit Undo/Redo, Ebenenliste und Z-Reihenfolge (M5–M8).
- **Zuschneiden** wie im Snipping Tool: nicht-destruktiv, Griffe, Seitenverhältnisse, Pixel-Eingabe, Einrasten, „Auf Objekte zuschneiden“, „Leeren Rand entfernen“ (M9).
- **Projektformat `.gsts`:** Originalbild, Zuschnitt und alle Objekte in einer ZIP-Datei; „Exportieren…“ für Bildformate; Liste „Zuletzt verwendet“ in Startfenster, Tray, Editor und Sprungliste; automatische Sicherung mit Wiederherstellung nach einem Absturz (M10).
- **Feinschliff (M11):**
  - Übersicht aller Tastenkürzel (`F1`), einmaliger Erststart-Hinweis, lokalisierte Tastenkürzel-Anzeige.
  - Barrierefreiheit: Bedienung der Objekte per Tastatur (`Tab`, Pfeiltasten, `Alt+Pfeil`), Automationsnamen für Symbolschaltflächen, Hochkontrast-Farben auf der Zeichenfläche.
  - Härtung: Warnung vor sehr großen Bildern, Hotkeys bei gesperrter Sitzung ignoriert, Absturzmeldung mit „Log-Ordner öffnen“, Effekt-Cache wird beim Schließen des Editors geleert, virtualisierte Ebenenliste.
  - Release: portable Single-File-EXE (ReadyToRun) per `build\release.ps1`, Versionierung mit Git-Hash, `THIRD-PARTY-NOTICES.txt`.

### Bekannte Einschränkungen
- Die EXE ist nicht signiert (SmartScreen-Hinweis beim ersten Start).
- Keine Dateizuordnung für `.gsts` im System; kein Installer.
