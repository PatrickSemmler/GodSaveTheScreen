# M4 – Editor-Grundgerüst & Datei-I/O

← [M3](M03-Tray-Hotkeys-Einstellungen.md) · [Umsetzungsplan](../Umsetzungsplan.md) · Weiter: [M5](M05-Objekte-Formen.md)

## Ziel

Das vorläufige Ergebnisfenster wird zum Editor-Rahmen: Werkzeugleiste, Eigenschaftenbereich, Zeichenfläche mit Zoom/Pan, Laden vorhandener Bilder, Speichern in allen WPF-Formaten mit Formatoptionen, Zwischenablage (Kopieren/Einfügen) sowie Drag & Drop. Noch ohne Annotationswerkzeuge – aber mit allen Andockpunkten dafür.

## Voraussetzungen

- M1 abgeschlossen

## Umfang

**Enthalten**
- Editor-Layout und Werkzeug-Infrastruktur (`ToolBase`, aktives Werkzeug, Eigenschaftenpanel)
- Zoom/Pan, Statusleiste (Zoom, Bildgröße, Mausposition in Bildpixeln)
- Öffnen: alle über WIC dekodierbaren Formate (PNG, JPEG, BMP, GIF, TIFF, ICO, JPEG XR + installierte Codecs wie WebP/HEIC)
- Speichern: PNG, JPEG (Qualität), BMP, GIF, TIFF (Kompression), JPEG XR / WDP (Qualität, verlustfrei)
- Zwischenablage: Bild kopieren, Bild einfügen (neues Dokument)
- Drag & Drop von Bilddateien auf Editor/Startfenster; Drag aus dem Editor heraus (als PNG-Datei)
- Ungespeicherte Änderungen beim Schließen abfragen
- Mehrere Editorfenster gleichzeitig

**Nicht enthalten**
- Annotationswerkzeuge (M5+)

## Aufgaben

### 1. Editor-Layout
- [ ] `EditorWindow` (`ui:FluentWindow`): oben Befehlsleiste (Neu ▾, Öffnen, Speichern, Speichern unter, Kopieren, Undo/Redo [deaktiviert bis M5], Zoom), links vertikale Werkzeugleiste (`ToolPalette`, nur „Auswahl“ in M4), rechts einklappbares Eigenschaftenpanel (`PropertyPanel`, kontextabhängig über `DataTemplate` je Werkzeug/Auswahl), unten Statusleiste
- [ ] `EditorViewModel` (pro Fenster eigener DI-Scope): `Document`, `ActiveTool`, `Zoom`, Befehle
- [ ] Werkzeug-Infrastruktur:
  - `ToolBase` (abstrakt): `OnPointerDown/Move/Up(EditorPointerArgs)`, `OnKeyDown`, `Cursor`, `PropertiesViewModel`, `Activate/Deactivate`
  - `EditorPointerArgs`: Position in **Bildpixeln**, Modifier-Tasten, Druck (Stift), Klickanzahl
  - `ToolRegistry`: Werkzeuge per DI registriert, Tastenkürzel + Icon + lokalisierter Name
- [ ] Fenstergröße beim Öffnen: Bildgröße bei 100 % + Ränder, begrenzt auf 85 % des Arbeitsbereichs, Monitor = Monitor der Aufnahme

### 2. Zeichenfläche
- [ ] `EditorCanvas` (UserControl): `ScrollViewer` → `ZoomPanel` (eigenes Panel mit `LayoutTransform`-Skalierung) → `Grid` mit Ebenen:
  1. `Image` (Basisbild, `RenderOptions.BitmapScalingMode` = `NearestNeighbor` ab 200 %, sonst `HighQuality`)
  2. `AnnotationLayer` (`ItemsControl` mit `Canvas`-Panel, ab M5 befüllt)
  3. `InteractionLayer` (Werkzeug-Vorschau, Auswahl-Adorner, Lineal – nicht exportiert)
- [ ] Zoom: `Strg+Mausrad` um Mauspunkt, Stufen 10 %–3200 %, `Strg+0` einpassen, `Strg+1` 100 %; Zoom-Dropdown in Statusleiste
- [ ] Pan: Leertaste gedrückt + Ziehen, mittlere Maustaste, Scrollbalken
- [ ] Transparenz-Schachbrett hinter dem Bild (für Freiform-Aufnahmen)
- [ ] Umrechnung Bildschirm ↔ Bildpixel zentral in `EditorCanvas.ToImagePoint()` inkl. Monitor-DPI-Kompensation

### 3. Dateien öffnen
- [ ] `IImageCodecService.LoadAsync(path)`: `BitmapDecoder.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad)`, erster Frame, Konvertierung nach `Bgra32` → `PixelBuffer`, DPI übernehmen
- [ ] EXIF-Orientierung bei JPEG berücksichtigen (`BitmapMetadata` `System.Photo.Orientation` → `TransformedBitmap`)
- [ ] Mehrseitige TIFF/animierte GIF: erster Frame + Hinweis
- [ ] Öffnen-Dialog (`Microsoft.Win32.OpenFileDialog`) mit Filter „Alle Bilder“ inkl. dynamisch ermittelter WIC-Decoder-Endungen (Registry `HKLM\SOFTWARE\Microsoft\Windows Imaging Component\...` bzw. bekannte Liste + „Alle Dateien“)
- [ ] Kommandozeile/Weiterleitung: Dateipfade öffnen je einen Editor; „Öffnen mit…“ funktioniert dadurch automatisch
- [ ] Startfenster-Button „Öffnen“ aktivieren; „Aus Zwischenablage“ als zusätzlicher Eintrag

### 4. Speichern
- [ ] `IImageCodecService.SaveAsync(BitmapSource, path, ImageFormat, EncoderOptions)`
- [ ] Formate und Encoder:

| Format | Endungen | Encoder | Optionen |
|---|---|---|---|
| PNG | `.png` | `PngBitmapEncoder` | Interlace |
| JPEG | `.jpg`, `.jpeg` | `JpegBitmapEncoder` | Qualität 1–100 (Std. 90) |
| BMP | `.bmp` | `BmpBitmapEncoder` | – |
| GIF | `.gif` | `GifBitmapEncoder` | – (Palette automatisch) |
| TIFF | `.tif`, `.tiff` | `TiffBitmapEncoder` | Kompression (Keine, LZW, ZIP, …) |
| JPEG XR | `.jxr`, `.wdp` | `WmpBitmapEncoder` | Qualität, verlustfrei |

- [ ] Formate ohne Alpha (JPEG, BMP): Transparenz auf einstellbare Hintergrundfarbe (Standard Weiß) flach rechnen
- [ ] DPI-Metadaten schreiben (Original-DPI)
- [ ] „Speichern“: bekanntes Ziel → direkt; sonst „Speichern unter“. Vorgeschlagener Name aus `FileNamePattern`, Ordner = zuletzt verwendet
- [ ] Optionsdialog (kompaktes Flyout) für JPEG/TIFF/JPEG XR, Werte werden gemerkt
- [ ] Atomisches Schreiben (temp-Datei, dann ersetzen)
- [ ] `DocumentExporter.Render(document)` als Einstiegspunkt (in M4: nur Basisbild; ab M5 inkl. Annotationen, ab M9 Zuschnitt)

### 5. Zwischenablage & Drag & Drop
- [ ] Kopieren (`Strg+Umschalt+C`, in M4 auch `Strg+C`): gerendertes Bild
- [ ] Einfügen (`Strg+V`) im Start-/Editorfenster: Bild aus `CF_DIB`/`PNG`/Dateiliste → neues Editorfenster (ab M5 im Editor: Objekte, s. dort)
- [ ] Drop von Bilddateien: Startfenster → neuer Editor; Editor → neuer Editor (ab M5 optional als Bildobjekt – Ausblick)
- [ ] Drag aus dem Editor (Ziehgriff in der Befehlsleiste): temporäre PNG + `DataObject` mit `FileDrop` und Bild

### 6. Schließen
- [ ] `IsDirty`-Verfolgung; Schließen fragt „Speichern / Nicht speichern / Abbrechen“ (`ui:MessageBox`)
- [ ] Hinweis-Infozeile, dass Objekte nach dem Schließen nur im Projektformat bearbeitbar bleiben (Link auf M10-Funktion, ab M10)

## Tests

- `App.Tests` (`[StaFact]`): Lade-/Speicher-Roundtrip je Format (Pixel identisch bei verlustfreien Formaten, Toleranz bei JPEG), Alpha-Flatten, EXIF-Orientierung, Koordinatenumrechnung `ToImagePoint` bei Zoom/DPI, `EditorViewModel` Dirty-Logik und Schließen-Dialog über gemockten `IDialogService`

## Abnahmekriterien

- [ ] Bilder aller Formate (inkl. WebP, falls Codec installiert) lassen sich öffnen
- [ ] Speichern in allen 6 Formaten erzeugt in anderen Programmen lesbare Dateien
- [ ] Zoom um Mausposition flüssig, Pan per Leertaste
- [ ] Kopieren/Einfügen zwischen GodSaveTheScreen, Paint und Browser funktioniert
- [ ] Drei Editorfenster gleichzeitig unabhängig nutzbar
