# M7 – Freihand, Textmarker & virtuelles Lineal

← [M6](M06-Text-Marker.md) · [Umsetzungsplan](../Umsetzungsplan.md) · Weiter: [M8](M08-Unschaerfe-Lupe.md)

## Ziel

Freihandzeichnen mit Stift (Dicke/Farbe frei) und Textmarker (transparenter, breiter Stift) sowie ein virtuelles, frei verschieb- und drehbares Lineal, an dessen Kante Freihandstriche einrasten. Jeder Strich ist ein eigenständiges, bearbeitbares Objekt.

## Voraussetzungen

- M5 abgeschlossen

## Umfang

**Enthalten**
- `StrokeAnnotation` (Stift + Textmarker)
- Glättung, Druckempfindlichkeit (Stift-Eingabe), `Umschalt` für gerade Linie
- Radierer (objektbasiert: löscht ganze Striche)
- Lineal mit Drehung, Winkelanzeige, Pixelskala und Einrasten

**Nicht enthalten**
- Pixelbasiertes Radieren (Striche bleiben Objekte)

## Aufgaben

### 1. Datenmodell (Core)
- [x] `StrokeAnnotation : Annotation`: `Points` (`IReadOnlyList<StrokePoint>` mit X, Y, Pressure), `Color`, `Thickness`, `IsHighlighter`, `Smoothing` (0–1), `UsePressure`
- [x] Punktreduktion beim Abschluss (Ramer-Douglas-Peucker, Toleranz 0,5 px) – reduziert Speicher und Projektgröße
- [x] `Bounds` aus Punkten + halber Dicke; Hit-Test: Abstand zu Segmenten ≤ Dicke/2 + Toleranz
- [x] `Translate`/`Scale` für Verschieben und Skalieren über Griffe

### 2. Rendering
- [x] Geglätteter Pfad: Catmull-Rom → kubische Bézier-Segmente in `StreamGeometry` (eingefroren, gecacht bis zur nächsten Änderung) – *eingefroren, aber nicht gecacht (wird je Render neu aufgebaut; bei normalen Strichen ausreichend schnell)*
- [x] Ohne Druck: ein `Pen` mit runden Kappen/Verbindungen
- [x] Mit Druck: Umriss-Polygon aus variabler Breite (Normale je Punkt) gefüllt
- [x] Textmarker: Standard-Alpha 40 %, flache Kappen (`PenLineCap.Square`), Standardbreite 16 px, Standardfarbe Gelb; Überlappungen **innerhalb eines Strichs** dürfen nicht dunkler werden → Strich als ein Pfad mit einheitlicher Deckkraft zeichnen (`PushOpacity` auf deckender Farbe statt halbtransparenter Pen-Farbe)
- [x] Option „Textmarker hinter Formen/Text zeichnen“ (Standard: an) → Renderer zeichnet Textmarker-Striche vor anderen Annotationen, Z-Reihenfolge innerhalb der Gruppe bleibt

### 3. Werkzeuge
- [x] `PenTool` / `HighlighterTool` (gemeinsame Basis `FreehandToolBase`):
  - Live-Vorschau als leichter `DrawingVisual` im `InteractionLayer` (inkrementell, keine Neuberechnung des ganzen Pfads je Mausbewegung) – *umgesetzt als Vorschau-Objekt im `InteractionLayer` (Punkteliste wird je Mausbewegung neu gesetzt), kein inkrementeller `DrawingVisual`*
  - `StylusPoint`-Druck nutzen, wenn Stift-Eingabe vorhanden (`Stylus`-Events), sonst Maus
  - `Umschalt` gedrückt: gerade Linie vom Startpunkt (15°-Raster mit zusätzlich `Strg`)
  - Abschluss → `AddAnnotationAction`
- [x] `EraserTool`: überstrichene Freihand-Objekte werden markiert und beim Loslassen in einer Undo-Aktion entfernt (nur `StrokeAnnotation`, Option „alle Objekte“ im Eigenschaftenpanel)
- [x] Eigenschaften: Farbe (mit Alpha), Dicke (1–100 px), Glättung, Druck an/aus; Textmarker eigene Standardwerte
- [x] Größenvorschau-Cursor (Kreis in Strichbreite × Zoom)

### 4. Virtuelles Lineal
- [x] `RulerViewModel` (UI-Zustand je Editor, **nicht Teil des Dokuments**, nicht exportiert): `Center` (Bildpixel), `Angle` (Grad), `Length`, `IsVisible`
- [x] `RulerVisual` im `InteractionLayer`: halbtransparentes Band (Breite 80 DIP, zoom-unabhängig), Skala in Bildpixeln (kleine Striche 10 px, große 50/100 px mit Zahl), Winkelanzeige in der Mitte
- [x] Bedienung (Touch und Winkeleingabe offen, siehe unten):
  - Ziehen am Lineal = verschieben
  - Mausrad über dem Lineal = drehen (1°; mit `Umschalt` 15°; `Strg` 0,1°)
  - Zwei-Finger-Touch: Verschieben + Drehen (`Manipulation`-Events) – *nicht umgesetzt*
  - Doppelklick auf die Winkelanzeige → Eingabe eines exakten Winkels; Kontextmenü 0°/45°/90° – *nur Kontextmenü (0°/45°/90°/135°, Lineal ausblenden); keine Winkeleingabe*
  - `Strg+R` / Werkzeugleistenknopf blendet ein/aus; beim Einblenden zentriert im sichtbaren Bereich
- [x] Einrasten (Linie/Pfeil offen, siehe unten):
  - Beginnt ein Freihandstrich innerhalb von 24 DIP einer Linealkante, werden alle Punkte des Strichs auf die Gerade dieser Kante (versetzt um halbe Strichbreite) projiziert
  - Projektion als reine Funktion `RulerGeometry.Snap(point, ruler, side, strokeThickness)` im Core
  - Gilt für Stift, Textmarker; optional auch für Linie/Pfeil (Einstellung) – *nur Stift und Textmarker*
- [x] Lineal schließt sich nicht beim Werkzeugwechsel; Position bleibt je Editorfenster erhalten

## Technische Details

- **Warum kein `InkCanvas`:** Striche müssen dasselbe Objektmodell nutzen (Auswahl, Eigenschaftenpanel, Undo, Projektformat, Lineal-Snapping). `InkCanvas` hat eigene `StrokeCollection`-Semantik und eigene Auswahl, was zwei parallele Welten erzeugen würde.
- **Highlighter ohne Blend-Modes:** WPF kennt kein „Multiplizieren“. Gleichmäßige Deckkraft + Zeichnen unterhalb von Text kommt dem realen Textmarker-Effekt am nächsten. Ein Pixel-Shader (`ShaderEffect`) für echtes Multiply ist als spätere Option denkbar.

## Tests

- `Core.Tests`: RDP-Reduktion, Hit-Test, `RulerGeometry.Snap` (beide Kanten, 0°/37°/90°, Strichbreiten-Versatz), Bounds mit Druck
- `App.Tests`: `FreehandToolBase` mit simulierten Punkten → genau eine Annotation, `Umschalt`-Linie, `EraserTool` als eine Undo-Aktion; Renderer-Pixeltest: Textmarker-Überlappung im selben Strich gleichmäßig hell

- [ ] Stift und Textmarker mit frei wählbarer Dicke und Farbe; Striche sind auswählbar, verschiebbar, farblich nachträglich änderbar
- [ ] Textmarker lässt darunterliegenden Text lesbar
- [ ] Lineal ist frei drehbar, Winkel wird angezeigt, Striche an der Kante sind exakt gerade
- [ ] Lineal erscheint weder im Export noch in der Zwischenablage
- [ ] Zeichnen bleibt bei 4K-Bild und 400 % Zoom flüssig (≥ 60 FPS Vorschau)
