# M10 – Projektformat `.gsts`

← [M9](M09-Zuschneiden.md) · [Umsetzungsplan](../Umsetzungsplan.md) · Weiter: [M11](M11-Feinschliff-Release.md)

## Ziel

Ein eigenes Projektformat speichert Originalbild, Zuschnitt und alle Objekte, sodass die Bearbeitung auch nach dem Schließen fortgesetzt werden kann. Dazu „Zuletzt verwendet“ und Wiederherstellung nach Absturz.

## Voraussetzungen

- M5 abgeschlossen; sinnvoll nach M6–M9, damit alle Objekttypen enthalten sind

## Umfang

**Enthalten**
- Speichern/Öffnen `.gsts` (ZIP-Container)
- Versionierung des Formats, tolerantes Lesen
- Zuletzt verwendete Dateien (Startfenster, Tray, Editor)
- Automatische Sicherung offener Dokumente + Wiederherstellung nach Absturz
- Objekt-Zwischenablage nutzt denselben Serializer

**Nicht enthalten**
- Dateizuordnung im System (portable App; optional als Einstellung „`.gsts` mit GodSaveTheScreen öffnen“ unter HKCU – Folgepunkt)

## Aufgaben

### 1. Format
- [ ] ZIP (`System.IO.Compression.ZipArchive`) mit:
  - `manifest.json` – `{ "format": "GodSaveTheScreen.Project", "formatVersion": 1, "appVersion": "…", "createdAt": "…", "modifiedAt": "…" }`
  - `document.json` – Bildgröße, DPI, `CropRect`, Annotationen, Metadaten (Aufnahmemodus, Fenstertitel)
  - `base.png` – Originalbild verlustfrei (`CompressionLevel.NoCompression` für den ZIP-Eintrag, PNG komprimiert bereits)
  - `preview.png` – gerenderte Vorschau (max. 512 px) für künftige Explorer-Vorschau / Zuletzt-verwendet-Liste
- [ ] `ProjectSerializer` (Core): `System.Text.Json` mit Source-Generator-Kontext `ProjectJsonContext`
  - Polymorphie: `[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]` an `Annotation`, `[JsonDerivedType(typeof(RectangleAnnotation), "rect")]` usw. – **stabile, kurze Diskriminatoren**, niemals Klassennamen
  - Konverter für `ArgbColor` (`"#AARRGGBB"`), `PointD`/`RectD` (kompakte Arrays)
  - Unbekannter `$type` → Objekt wird übersprungen + Warnung (vorwärtskompatibel)
- [ ] Migrationen: `IProjectMigration` (from → to) auf `JsonNode`-Ebene vor dem Deserialisieren
- [ ] Schutz: max. Eintragsgröße / Bildgröße prüfen (ZIP-Bomben), Pfade in ZIP ignorieren (nur feste Namen lesen)

### 2. Integration
- [ ] „Speichern unter“-Dialog bietet `.gsts` als erstes Format „GodSaveTheScreen-Projekt (Objekte bearbeitbar)“
- [ ] Geöffnetes Projekt merkt sich `ProjectPath`; „Speichern“ schreibt wieder ins Projekt; zusätzlicher Befehl „Exportieren…“ (Bildformate) mit eigenem zuletzt verwendeten Ziel
- [ ] Hinweis beim Schließen mit Objekten (aus M4) bietet „Als Projekt speichern“ als Option
- [ ] Hinweis, wenn Unschärfe-Objekte enthalten sind (Original bleibt im Projekt lesbar – siehe M8)
- [ ] Öffnen per Dialog, Drag & Drop, Kommandozeile erkennt `.gsts`
- [ ] Objekt-Zwischenablage (M5) auf `ProjectSerializer` umstellen

### 3. Zuletzt verwendet
- [ ] `RecentFilesService`: max. 15 Einträge (Pfad, Typ Bild/Projekt, Zeitpunkt), nicht mehr existierende Dateien ausgegraut + entfernbar
- [ ] Anzeige: Startfenster (Liste mit Vorschaubild aus `preview.png` bzw. verkleinertem Bild), Tray-Untermenü, Editor „Öffnen ▾“
- [ ] Windows-Sprungliste (`JumpList`) mit zuletzt verwendeten Dateien und Aufgaben „Rechteck aufnehmen“ etc. (`--capture …`)

### 4. Automatische Sicherung
- [ ] Alle 60 s (und bei Leerlauf nach Änderung) Sicherung ungespeicherter Dokumente als `.gsts` in `%LOCALAPPDATA%\GodSaveTheScreen\autosave\{DocumentId}.gsts` (Hintergrundthread, Snapshot des Modells auf UI-Thread)
- [ ] Beim sauberen Schließen/Speichern: Sicherung löschen
- [ ] Beim Start: verwaiste Sicherungen → Dialog „Wiederherstellen / Verwerfen“

## Tests

- `Core.Tests`: Roundtrip **jedes** Annotationstyps mit allen Eigenschaften (Gleichheit per Werte-Vergleich), Golden-File-Test mit eingechecktem `formatVersion=1`-Projekt (verhindert unbeabsichtigte Formatänderungen), unbekannter Typ wird übersprungen, Migration v0→v1 (Test-Dummy), ZIP-Bomben-Schutz
- `App.Tests`: Speichern/Öffnen-Flow im `EditorViewModel` (Dirty-Status, `ProjectPath` vs. Export-Ziel), `RecentFilesService`

## Abnahmekriterien

- [ ] Projekt mit allen Objekttypen speichern, App beenden, öffnen → identische Darstellung, alle Objekte bearbeitbar, Undo-Verlauf startet leer
- [ ] Projektdatei ist mit einem ZIP-Programm lesbar (`base.png` sichtbar)
- [ ] Nach erzwungenem Beenden (Task-Manager) bietet der nächste Start die Wiederherstellung an
- [ ] Zuletzt verwendete Dateien erscheinen in Startfenster, Tray und Sprungliste
