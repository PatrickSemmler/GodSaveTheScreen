# M8 – Unschärfe-Maskierung & Lupe

← [M7](M07-Freihand-Lineal.md) · [Umsetzungsplan](../Umsetzungsplan.md) · Weiter: [M9](M09-Zuschneiden.md)

## Ziel

Bereiche des Bildes lassen sich unscharf maskieren (Weichzeichnen oder Verpixeln) – als nicht-destruktive, nachträglich veränderbare Objekte. Zusätzlich gibt es ein Lupenobjekt, das einen Bildausschnitt vergrößert an anderer Stelle darstellt.

## Voraussetzungen

- M5 abgeschlossen

## Umfang

**Enthalten**
- `BlurAnnotation` mit Modi Weichzeichnen (Gauß), Verpixeln, Schwärzen (deckend)
- Rechteckig und elliptisch
- Performante, gecachte Effektberechnung
- `MagnifierAnnotation` (rund/rechteckig) mit Verbindungslinie

## Aufgaben

### 1. Datenmodell (Core)
- [x] `BlurAnnotation : Annotation`: `Rect`, `Mode` (`GaussianBlur` | `Pixelate` | `Redact`), `Strength` (Radius bzw. Blockgröße in px, 2–100), `Shape` (Rechteck/Ellipse), `FeatherEdge` (weicher Rand, px), `RedactColor`
- [x] `MagnifierAnnotation : Annotation`: `SourceRect` (Quellbereich), `TargetCenter`, `Zoom` (1,5–8), `Shape` (Kreis/Rechteck), `Border` (`StrokeStyle`), `ShowConnector` (Linie/Kegel zwischen Quelle und Ziel), `ShowSourceFrame`
- [x] Lupengröße ergibt sich aus `SourceRect.Size × Zoom`

### 2. Effektverarbeitung (Rendering)
- [x] `RegionEffectProcessor` arbeitet auf `PixelBuffer` (BGRA, verwaltete Arrays ohne `unsafe`):
  - **Gauß:** 3 Durchläufe Box-Blur horizontal/vertikal (≈ Gauß), Radius = `Strength`; Eingangsbereich um Radius erweitert (Randbehandlung: Klemmen an Bildgrenzen)
  - **Verpixeln:** Blockmittelwert; Blockraster am Bildursprung ausgerichtet (stabil beim Verschieben)
  - **Schwärzen:** Vollfläche in `RedactColor`
  - Parallelisierung zeilenweise (`Parallel.For`) ab Bereichsgröße > 256×256
- [x] **Quelle ist immer das Basisbild** (nicht darunterliegende Annotationen) – deterministisch und sicher
- [x] `EffectCache`: Schlüssel (Rect, Mode, Strength, Basisbild-Version) → eingefrorene `BitmapSource`; LRU-Begrenzung (z. B. 64 MB)
- [ ] Während Ziehen/Skalieren: Berechnung auf Hintergrund-Task mit Entprellung (50 ms) und Abbruch veralteter Aufträge; bis dahin vorheriges Ergebnis gestreckt anzeigen – *offen: Berechnung läuft synchron im Renderpfad; jede neue Größe berechnet das Rechteck neu (`EffectCache` hilft nur bei Wiederholung); die Flüssigkeit bei großen Bereichen ist nicht gemessen*
- [x] Renderer: Ergebnis-Bitmap im Bereich zeichnen, bei Ellipse/Feather mit `OpacityMask`/Clip-Geometrie
- [x] Export: synchroner Pfad (`EffectCache` vorwärmen), damit die Datei nie ohne Effekt geschrieben wird

### 3. Lupe (Rendering)
- [x] Ausschnitt `SourceRect` aus dem **gerenderten** Bild (Basisbild + Annotationen unterhalb der Lupe in Z-Order) – für Anleitungen sinnvoll, da Markierungen mitvergrößert werden; Option „nur Basisbild“
- [x] Skalierung mit `BitmapScalingMode` wählbar (Pixelig/Glatt)
- [x] Clip auf Kreis/Rechteck, Rahmen, optional Schatten (`DropShadow` per Geometrie, kein `Effect` – exportstabil) – *ohne Schatten (optional, nicht umgesetzt)*
- [x] Verbinder: zwei Tangenten zwischen Quellkreis und Zielkreis bzw. einfache Linie
- [x] Zyklenschutz: Lupen sehen keine anderen Lupen über ihnen

### 4. Werkzeuge
- [x] `BlurTool`: Rechteck aufziehen (`Umschalt` = Quadrat), Modus-Umschalter im Eigenschaftenpanel; Standardmodus Verpixeln
- [x] `MagnifierTool`: 1. Ziehen = Quellbereich; danach erscheint die Lupe versetzt (rechts unten, im Bild geklemmt); Ziel separat verschiebbar (Ziehen am Lupenkörper statt eigenem Griff), Quelle über Rahmengriffe (Ziehen am Quellrahmen verschiebt nur die Quelle, die Eckgriffe ändern deren Größe)
- [x] Eigenschaften: Modus, Stärke (Slider + Zahl), Form, weicher Rand; Lupe: Zoom, Form, Rahmen, Verbinder

## Technische Details

- **Sicherheit:** Ein kleiner Gauß-Radius kann rekonstruierbar sein. Daher Standard „Verpixeln“ mit Blockgröße ≥ 12 px und Tooltip-Hinweis, für sensible Daten „Schwärzen“ zu nutzen. Beim Export wird der Effekt in das Bild gerendert; das Projektformat (M10) enthält das Originalbild – Hinweis beim Speichern als `.gsts`, wenn Unschärfe-Objekte vorhanden sind.
- **Warum nicht `BlurEffect` von WPF:** GPU-Effekt, der bei `RenderTargetBitmap` software-gerendert wird und an Rändern Artefakte erzeugt; zudem keine Verpixelung.

## Tests

- `App.Tests`/Rendering: Box-Blur auf Schachbrett (Mittelwert korrekt, Ränder geklemmt), Verpixeln (Blockmittel exakt, Ausrichtung stabil bei Verschiebung um 1 px), Schwärzen, Cache-Treffer/LRU-Verdrängung, Export enthält Effekt (Pixelprüfung), Lupe: Pixel in Lupenmitte = Quellmittelpunkt
- `Core.Tests`: Lupengeometrie (Größe, Klemmung, Verbinder-Tangenten)

- [ ] Bereiche lassen sich weichzeichnen, verpixeln und schwärzen; Stärke und Größe nachträglich änderbar
- [ ] Verschieben eines 1000×600-Unschärfebereichs auf einem 4K-Bild bleibt flüssig
- [ ] Export und Zwischenablage enthalten die Maskierung zuverlässig
- [ ] Lupe vergrößert inklusive darunterliegender Markierungen, Quelle und Ziel unabhängig verschiebbar
