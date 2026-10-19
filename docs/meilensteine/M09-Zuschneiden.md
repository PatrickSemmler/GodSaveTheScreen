# M9 – Zuschneiden

← [M8](M08-Unschaerfe-Lupe.md) · [Umsetzungsplan](../Umsetzungsplan.md) · Weiter: [M10](M10-Projektformat.md)

## Ziel

Eine Beschneidungsfunktion wie im Windows Snipping Tool: Zuschnittmodus mit Griffen, abgedunkeltem Außenbereich, optionalem Seitenverhältnis, Übernehmen/Abbrechen. Der Zuschnitt ist nicht-destruktiv und rückgängig machbar; Objekte bleiben erhalten.

## Voraussetzungen

- M5 abgeschlossen

## Umfang

**Enthalten**
- `ImageDocument.CropRect` (nicht-destruktiv)
- Zuschnittmodus mit 8 Griffen, Verschieben, Seitenverhältnissen, Pixel-Eingabe
- Undo/Redo, erneutes Zuschneiden (Bereich wieder vergrößerbar bis zur Originalgröße)
- Export/Zwischenablage berücksichtigen den Zuschnitt

**Nicht enthalten**
- Drehen/Spiegeln des Bildes (möglicher Folgepunkt)
- Leinwand vergrößern über das Original hinaus

## Aufgaben

### 1. Modell (Core)
- [ ] `ImageDocument.CropRect : PixelRect?` (null = kein Zuschnitt), immer innerhalb des Basisbilds
- [ ] `CropAction : IUndoableAction` (alt/neu)
- [ ] `ImageDocument.EffectiveBounds` = `CropRect ?? volle Bildfläche`

### 2. Anzeige & Export
- [ ] Editor-Canvas zeigt nur `EffectiveBounds` an (Clip + Versatz über `TranslateTransform`); Annotationen bleiben in Original-Bildkoordinaten
- [ ] Objekte, die über den Zuschnitt hinausragen, werden abgeschnitten dargestellt und exportiert; Objekte komplett außerhalb bleiben im Modell (Ebenenliste zeigt sie ausgegraut)
- [ ] Statusleiste: Mauskoordinaten relativ zum Zuschnitt
- [ ] `DocumentExporter`: `RenderTargetBitmap` in Zuschnittgröße mit `PushTransform(-x, -y)`
- [ ] Zoom „Einpassen“ bezieht sich auf den Zuschnitt

### 3. Zuschnittmodus (Werkzeug `CropTool`)
- [ ] Aktivierung über Werkzeugleiste/`C`: zeigt **gesamtes** Originalbild, aktueller Zuschnitt als heller Bereich, Außenbereich abgedunkelt (60 %)
- [ ] Griffe: 4 Ecken (L-förmig wie Snipping Tool), 4 Kanten; Ziehen im Inneren verschiebt; Ziehen außerhalb zieht neuen Bereich auf
- [ ] Drittel-Raster während des Ziehens
- [ ] Seitenverhältnis-Dropdown: Frei, Original, 1:1, 4:3, 3:2, 16:9, 9:16, Benutzerdefiniert; `Umschalt` hält aktuelles Verhältnis
- [ ] Eingabefelder X, Y, Breite, Höhe (px) im Eigenschaftenpanel
- [ ] Einrasten an Bildrändern und an Objekt-Bounds (Toleranz 8 DIP)
- [ ] Befehlsleiste im Modus: ✓ Übernehmen (`Enter`), ✕ Abbrechen (`Esc`), Zurücksetzen
- [ ] Andere Werkzeuge sind im Zuschnittmodus deaktiviert; Undo während des Modus bricht den Modus ab
- [ ] Zusätzlich: „Auf Objekte zuschneiden“ (Bounding-Box aller Objekte + Rand) und „Leeren Rand automatisch entfernen“ (Rand einheitlicher Farbe erkennen) als Befehle

## Tests

- `Core.Tests`: `CropAction` Undo/Redo, Klemmen an Bildgrenzen, Seitenverhältnis-Berechnung bei Eck- und Kantengriffen, Auto-Rand-Erkennung
- `App.Tests`: Export-Pixeltest mit Zuschnitt + Objekt über den Rand, `CropTool`-Zustandsmaschine (Übernehmen/Abbrechen)

## Abnahmekriterien

- [ ] Bedienung entspricht dem Snipping Tool (Griffe, Abdunklung, Übernehmen/Abbrechen)
- [ ] Zuschnitt ist rückgängig machbar und später wieder erweiterbar
- [ ] Objekte bleiben nach dem Zuschneiden bearbeitbar und korrekt positioniert
- [ ] Export/Zwischenablage haben exakt die Zuschnittgröße
