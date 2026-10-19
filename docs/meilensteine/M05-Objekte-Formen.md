# M5 – Objekt-Framework, Formen, Undo/Redo & Ebenen

← [M4](M04-Editor-Dateien.md) · [Umsetzungsplan](../Umsetzungsplan.md) · Weiter: [M6](M06-Text-Marker.md)

## Ziel

Das Herzstück des Editors: ein nicht-destruktives Objektmodell, in dem Formen (Rechteck, Ellipse/Kreis, Linie, Pfeil, Dreieck, Polygon) mit frei konfigurierbarer Linien- und Füllfarbe inkl. Transparenz platziert werden. Alle Objekte bleiben auswählbar, verschiebbar, skalierbar und in ihren Eigenschaften änderbar, bis das Bild geschlossen wird. Jede Änderung ist rückgängig machbar; die Z-Reihenfolge ist steuerbar. Export und Zwischenablage enthalten die Objekte.

## Voraussetzungen

- M4 abgeschlossen

## Umfang

**Enthalten**
- Annotation-Basismodell + Formtypen
- Gemeinsamer `AnnotationRenderer` (Anzeige = Export)
- Auswahlwerkzeug: Klick, Mehrfachauswahl (`Strg`/`Umschalt`), Rahmenauswahl, Verschieben, Größe ändern über Griffe, Endpunkt-Griffe für Linien/Pfeile, Polygon-Eckpunkte
- Formwerkzeuge mit Vorschau; `Umschalt` = Quadrat/Kreis bzw. 15°-Winkelraster; `Alt` = vom Mittelpunkt aus
- Eigenschaftenpanel: Linienfarbe, Füllfarbe (je mit Alpha), Linienstärke, Strichart, Eckenradius, Pfeilspitzen, Deckkraft
- Farbwähler mit Alpha, zuletzt verwendete Farben, Pipette aus dem Bild
- Undo/Redo mit Verlauf, Transaktionen für Ziehvorgänge
- Z-Reihenfolge: ganz nach vorne / eine Ebene vor / eine Ebene zurück / ganz nach hinten; Ebenenliste (optional einblendbar) mit Sichtbarkeit und Sperre
- Kopieren/Einfügen/Duplizieren/Löschen von Objekten (auch zwischen Editorfenstern)
- Werkzeug-Standardwerte werden je Werkzeug gespeichert

**Nicht enthalten**
- Text, Marker (M6), Freihand (M7), Unschärfe/Lupe (M8), Zuschnitt (M9)

## Aufgaben

### 1. Datenmodell (Core)
- [ ] `ArgbColor` (readonly record struct, `A,R,G,B`, Hex-Parsing/-Formatierung `#AARRGGBB`)
- [ ] `StrokeStyle` (Color, Thickness, DashStyle: Solid/Dash/Dot/DashDot, LineJoin, LineCap)
- [ ] `Annotation` (abstrakt, `ObservableObject`): `Id`, `Opacity`, `IsVisible`, `IsLocked`, `Name`, abstrakt `RectD Bounds`, `Annotation Clone()`, `void Translate(VectorD)`, `HitTest(PointD, tolerance)`
- [ ] `ShapeAnnotation`: `Stroke`, `Fill` (`ArgbColor`, Alpha 0 = keine Füllung)
- [ ] `BoxAnnotation : ShapeAnnotation` (`Rect`) → `RectangleAnnotation` (`CornerRadius`), `EllipseAnnotation`, `TriangleAnnotation` (`TriangleKind`: Isosceles, Right, + `IsFlipped`)
- [ ] `LineAnnotation : ShapeAnnotation` (`Start`, `End`, `StartCap`/`EndCap`: None, Arrow, OpenArrow, Circle, Square; `CapSize`-Faktor)
- [ ] `PolygonAnnotation : ShapeAnnotation` (`Points`, `IsClosed`)
- [ ] Geometrie-Hilfen: Hit-Test für Linien (Abstand zum Segment), Ellipse (normierte Gleichung), Polygon (Even-Odd), Füllung vs. nur Rand

### 2. Undo/Redo (Core)
- [ ] `IUndoableAction { string Description; void Do(); void Undo(); bool TryMerge(IUndoableAction next); }`
- [ ] Standardaktionen: `AddAnnotationAction`, `RemoveAnnotationsAction`, `PropertyChangeAction<T>` (Ziel, Getter/Setter, alt/neu), `MoveAnnotationsAction`, `ReorderAction`, `ReplaceGeometryAction`
- [ ] `UndoManager`: `Execute(action)`, `Undo()`, `Redo()`, `CanUndo/CanRedo`, `BeginTransaction(description)` → `IDisposable` (fasst Ziehvorgänge zusammen), Limit (Standard 200), Ereignis `Changed`, Kopplung an `IsDirty` (Save-Point-Marker)
- [ ] **Invariante:** Annotationen werden nach dem Hinzufügen ausschließlich über den `UndoManager` geändert

### 3. Rendering
- [ ] `AnnotationRenderer.Render(DrawingContext dc, Annotation a, RenderContext ctx)` mit `switch` über Typ; `ctx` enthält Basisbild-Zugriff (für M8) und Zoom (nur für UI-Hilfslinien)
- [ ] `BrushCache`/`PenCache`: eingefrorene `SolidColorBrush`/`Pen` je Stil
- [ ] Pfeilspitzen als gefüllte Geometrie; Linie endet an der Spitzenbasis (kein Überstand bei dicken Linien)
- [ ] `AnnotationElement : FrameworkElement` – `OnRender` ruft Renderer; `InvalidateVisual` bei `PropertyChanged`; Position über `Canvas.Left/Top` = `Bounds` (inkl. Stroke-Überstand)
- [ ] `AnnotationLayer` bindet `Document.Annotations`
- [ ] `DocumentExporter.Render` zeichnet Basisbild + alle sichtbaren Annotationen in Reihenfolge in ein `DrawingVisual` → `RenderTargetBitmap(w, h, 96, 96, Pbgra32)` → DPI-Metadaten setzen

### 4. Werkzeuge (App)
- [ ] `SelectTool`: Hit-Test von oben nach unten; Rahmenauswahl (ganz enthalten); Verschieben mit Transaktion; Pfeiltasten-Nudge; `Entf`; Doppelklick → Bearbeitungsmodus (Polygonpunkte, ab M6 Text)
- [ ] `SelectionAdorner` im `InteractionLayer`: 8 Griffe für Box-Formen, 2 Endpunkt-Griffe für Linien, Punkt-Griffe für Polygone; Griffgröße zoom-unabhängig; Gruppen-Bounding-Box bei Mehrfachauswahl (proportionales Skalieren)
- [ ] `ShapeToolBase<T>` mit Vorschau während des Ziehens; `RectangleTool`, `EllipseTool`, `LineTool`, `ArrowTool` (Linie mit `EndCap=Arrow`), `TriangleTool`, `PolygonTool` (Klick je Punkt, Doppelklick/`Enter` schließt, `Esc` bricht ab)
- [ ] Nach dem Zeichnen: Werkzeug bleibt aktiv (Einstellung: „Nach dem Zeichnen zur Auswahl wechseln“)
- [ ] Neues Objekt wird ausgewählt; Klick auf vorhandenes Objekt mit Formwerkzeug + `Strg` wählt aus

### 5. Eigenschaften & Farben
- [ ] `SelectionPropertiesViewModel`: zeigt gemeinsame Eigenschaften der Auswahl (gemischte Werte = leer), Änderungen → `PropertyChangeAction` je Objekt in einer Transaktion
- [ ] Ohne Auswahl bearbeitet das Panel die **Werkzeug-Standardwerte** (`ToolDefaults` in den Einstellungen)
- [ ] `ColorButton`-Control: Popup mit `PixiEditor.ColorPicker` (`StandardColorPicker` inkl. Alpha), Palette mit 16 Standardfarben, 10 zuletzt verwendete Farben, Hex-Eingabe, „Keine“ (Alpha 0)
- [ ] Pipette: aktiviert temporäres Werkzeug, Klick ins Bild übernimmt Pixel des **gerenderten** Bilds
- [ ] Schieberegler + `NumberBox` für Linienstärke (0,5–100 px) und Deckkraft (0–100 %)

### 6. Ebenen & Zwischenablage
- [ ] Befehle „Nach vorne/hinten“ (Kontextmenü, `Strg+Umschalt+]`/`[`, `Strg+]`/`[`)
- [ ] `LayersPanel` (Tab im Eigenschaftenbereich): Liste in umgekehrter Reihenfolge, Drag & Drop zum Umsortieren, Augen-/Schloss-Symbol, Umbenennen
- [ ] Objekt-Zwischenablage: eigenes Format `GodSaveTheScreen.Annotations` (JSON aus M10-Serializer – bis M10 vorläufig derselbe `System.Text.Json`-Kontext) + gerendertes PNG der Auswahl als Fallback; Einfügen versetzt um +10 px
- [ ] Kontextmenü auf Objekten: Ausschneiden, Kopieren, Einfügen, Duplizieren, Löschen, Reihenfolge, Sperren

## Technische Details

- **Ein Renderpfad:** Anzeige (`AnnotationElement.OnRender`) und Export (`DocumentExporter`) nutzen denselben `AnnotationRenderer` – Abweichungen zwischen Bildschirm und Datei sind damit ausgeschlossen.
- **Warum Modell-Objekte direkt als `ObservableObject`:** Keine doppelte Wrapper-ViewModel-Hierarchie; die Undo-Invariante wird über das Eigenschaften-ViewModel und die Werkzeuge erzwungen.
- **Koordinaten:** Bildpixel; Linienstärken ebenfalls in Bildpixeln (skalieren mit dem Zoom, wie in Paint/Snipping Tool).
- **Ausblick Rotation:** `Annotation` erhält später optional `Rotation` + `Center`; Renderer wendet `PushTransform` an, Hit-Test transformiert den Punkt invers. Nicht Teil von v1.

## Tests

- `Core.Tests`: Hit-Tests aller Formen, `UndoManager` (Do/Undo/Redo, Transaktionen, Merge, Limit, Save-Point/Dirty), `Clone` erzeugt neue `Id`, `ArgbColor`-Parsing
- `App.Tests`: `SelectTool`-Szenarien mit simulierten Pointer-Events (Klick, Rahmen, Verschieben → genau eine Undo-Aktion), `SelectionPropertiesViewModel` mit gemischten Werten, Renderer-Pixeltest (`[StaFact]`: rotes 50 %-Rechteck auf Weiß → Pixelwert ≈ `#FF7F7F`), Pfeilspitzen-Geometrie

## Abnahmekriterien

- [ ] Alle Formtypen lassen sich zeichnen, auswählen, verschieben, skalieren und nachträglich in Farbe/Stärke/Transparenz ändern
- [ ] Halbtransparente Füllung zeigt das Bild darunter
- [ ] 30 gemischte Änderungen lassen sich vollständig rückgängig machen und wiederherstellen
- [ ] Z-Reihenfolge sichtbar korrekt, Ebenenliste synchron
- [ ] Gespeicherte Datei / Zwischenablage zeigen exakt die Bildschirmdarstellung bei 100 %
- [ ] Objekte lassen sich zwischen zwei Editorfenstern kopieren
