# M15 – Bild verschönern

← [M14](M14-Rahmen-Kanteneffekte.md) · [Umsetzungsplan](../Umsetzungsplan.md) · Weiter: [M16](M16-Texterkennung.md)

Feature-Vorschlag Nr. 10. Geschätzter Aufwand: 4 Personentage. Die Vorschlagsliste nannte Aufwand S; eigenes Hintergrundbild und festes Seitenverhältnis erhöhen ihn.

## Ziel

Der Screenshot liegt mit Innenabstand auf einem einfarbigen, verlaufenden oder eigenen Bildhintergrund, mit runden Ecken und Schatten. Auf Wunsch füllt das Ergebnis ein festes Seitenverhältnis. Fertige Vorlagen liefern das mit einem Klick, zum Beispiel für Folien und soziale Medien.

## Voraussetzungen

- [M14](M14-Rahmen-Kanteneffekte.md) abgeschlossen (`FrameStyle`, `FrameLayout`, `FrameRenderer`, Werkzeug „Rahmen“)

## Entscheidungen (mit dem Auftraggeber geklärt)

| Thema | Entscheidung |
|---|---|
| Hintergründe | Einfarbig, linearer Verlauf und eigenes Bild |
| Seitenverhältnis | Festes Verhältnis wählbar, die Ausgabe wird aufgefüllt |
| Bedienung | Im Werkzeug „Rahmen“ (M14), kein eigenes Werkzeug |
| Neue Aufnahmen | Kein automatisches Anwenden |

## Festlegungen dieses Plans

| Thema | Festlegung | Begründung |
|---|---|---|
| Verlauf | Zwei Farben und Winkel | Reicht für die Vorlagen; mehr Stopps lassen sich später ergänzen |
| Bildeinpassung | Immer „Füllen“: Seitenverhältnis bleibt, Überstand wird beschnitten und zentriert | Kein Verzerren und keine Lücken, kein zusätzliches Bedienelement |
| Bildquelle | Vorhandenes `ImageInsert.Load`, `FramePickerDialog` und `ImageAsset` | Formate, SVG, Mehrbilddateien und Sicherheitsgrenzen gibt es bereits |
| Seitenverhältnis | Frei, 16:9, 4:3, 1:1, 9:16, benutzerdefiniert (wie beim Zuschneiden) | Bekannte Bedienung |
| Auffüllen | Nur vergrößern, der Inhalt wird nie beschnitten oder skaliert; er steht mittig | Bleibt nicht-destruktiv |
| Mindestabstand | Innenabstand gilt immer, das Seitenverhältnis kommt danach obendrauf | Einstellung bleibt vorhersehbar |
| Ohne Hintergrund | Innenabstand ist transparent | Passt zum Export als PNG |

## Umfang

**Enthalten**
- Hintergrundart mit Farbe, Verlauf und Bild, Innenabstand, Seitenverhältnis
- Vorlagen mit Vorschaubildern
- Einbettung des Hintergrundbilds ins Projekt

**Nicht enthalten**
- Mitgelieferte Hintergrundbilder
- Verläufe mit mehr als zwei Farben, radiale Verläufe
- Weichzeichnen des Hintergrundbilds
- Festlegung von Zielgrößen in Pixeln (dafür später Feature-Vorschlag Nr. 11)

## Aufgaben

### 1. Modell (Core)
- [x] `FrameBackground` (Record): `Kind` (`None`, `Solid`, `Gradient`, `Image`), `ColorA`, `ColorB`, `AngleDegrees`, `ImageAsset? Image`
- [x] `FrameStyle` um `Background`, `Padding` und `AspectRatio` (`FrameAspect`: Art plus Breite und Höhe) ergänzen; alle optional mit Standard „aus“
- [x] `FrameLayout.Compute`: Inhalt → Rand → Innenabstand → Auffüllen auf Verhältnis. Schatten bleibt im Innenabstand sichtbar; ohne Hintergrund wächst die Ausgabe weiter um den Schattenüberstand
- [x] Obergrenze: Die Ausgabe wird auf `MaxPixels` und die Anzeige-Obergrenze geprüft. Überschreitung verhindert das Verhältnis und zeigt einen Hinweis im Panel

### 2. Zeichnen (Rendering)
- [x] `FrameRenderer.DrawBehind` zeichnet zuerst den Hintergrund über die ganze Ausgabe
- [x] Verlauf als `LinearGradientBrush`, Winkel über Rechteckgröße umgerechnet, damit die Farben genau in den Ecken enden
- [x] Bild über `ImageAssetCache`, berechnet aus Ausgabe- und Bildgröße; SVG bei der Zielgröße neu gerastert (wie Bildobjekte)
- [x] Bei großen Hintergrundbildern dekodiert die Vorschau verkleinert; Export in voller Größe
- [x] Regel aus `architecture.md` beachten: Bitmaps im gemeinsamen Cache als eigenständige, eingefrorene Kopie

### 3. Projektformat
- [x] Bilddatei des Hintergrunds als normaler Eintrag `image-<sha256>.*`; `ProjectSerializer.Write` und `Read` serialisieren `frame` deshalb innerhalb desselben `ImageAssetScope` wie die Objekte (heute umschließt der Scope nur `annotations`, `ProjectSerializer.cs:197-201` und `292-312`)
- [x] Gleiche Datei als Objekt und Hintergrund wird nur einmal gespeichert und nur einmal auf `MaxImageAssetBytes` angerechnet
- [x] Fehlt die Datei: Warnung, Hintergrund entfällt, restlicher Rahmen bleibt
- [x] `AppSettings.FrameDefaults` und `ToolPreset.Frame` speichern kein Bild: `Image` vor dem Speichern auf `null` setzen, denn der `ImageAsset`-Konverter schreibt ohne Scope Base64 inline und die Einstellungsdatei würde groß. Vorlagen merken nur die Art; bei einer Vorlage mit Bild bleibt das aktuelle Bild erhalten, sonst fällt sie auf Einfarbig zurück

### 4. Panel (App)
- [x] Karte `FrameBackground` mit Schalter: Auswahl der Art, Farbe(n), Winkel-Regler, „Bild wählen…“ mit kleiner Vorschau
- [x] Karte `FrameLayout`: Innenabstand, Seitenverhältnis (Auswahlliste plus Felder für benutzerdefiniert)
- [x] Kurzfassung im eingeklappten Kopf: Farbfeld bzw. Verlauf, „32 px · 16:9“
- [x] `FramePresets.BuiltIn` ergänzen: Präsentation Blau, Sonnenuntergang, Neutral Grau, Dunkel, Social 1:1, Folie 16:9. Vorlagen enthalten Hintergrund, Innenabstand, Ecken und Schatten
- [x] Vorlagen als Raster mit Vorschaubild; Vorschau entsteht einmal pro Sitzung mit `FrameRenderer` auf einem kleinen Musterbild
- [x] Drop einer Bilddatei auf die Karte ist nicht vorgesehen; Drop auf die Zeichenfläche fügt weiter ein Bildobjekt ein
- [x] Texte in `Strings.resx`, `Strings.de.resx`, `Strings.Designer.cs`; `AutomationProperties.Name` für Vorlagenschaltflächen

### 5. Dokumentation
- [x] Hilfe: Abschnitt `tool-frame` in beiden Sprachen um „Bild verschönern“ erweitern, Bildschirmfoto neu erzeugen
- [x] README: Funktionsliste in beiden Sprachen
- [x] `docs/agent/architecture.md`, `CHANGELOG.md`, Änderungshistorie in `CLAUDE.md`, Version 2. Stelle erhöhen

## Tests

- `Core.Tests`
  - `FrameLayout`: Innenabstand, jedes Verhältnis bei Hoch- und Querformat, Inhalt mittig, keine Verkleinerung, ungerade Restpixel, Obergrenze
  - JSON-Roundtrip je Hintergrundart; Asset-Dedupe mit Bildobjekt; fehlende Datei ergibt Warnung
  - Zustand ohne die neuen Felder: Golden-Dateien unverändert
- `App.Tests`
  - Export-Pixeltests: Einfarbig an allen vier Ecken; Verlauf an den Enden beider Achsen und bei 45°; Bild füllt ohne Verzerrung; Bild wird zentriert beschnitten
  - Innenabstand transparent ohne Hintergrund; JPG füllt weiß
  - Runde Ecken zeigen den Hintergrund statt Transparenz
  - Objekte über den Bildrand hinaus: Ausgabe wächst und Verhältnis stimmt weiter
  - Vorlage anwenden ist ein Undo-Schritt; Bild-Vorlage ohne vorhandenes Bild fällt zurück

## Abnahmekriterien

- [x] Eine Vorlage ergibt mit einem Klick ein fertiges Bild, das sich wie jedes andere speichern und kopieren lässt (per Test geprüft; manuell im laufenden Editor noch zu prüfen)
- [x] Bei gewähltem Verhältnis hat das exportierte Bild genau dieses Verhältnis (± 1 px durch Rundung)
- [x] Projekt mit eigenem Hintergrundbild lässt sich auf einem anderen Rechner ohne die Originaldatei öffnen
- [x] Der Inhalt behält in jeder Kombination seine Pixelgröße

## Stand

Umgesetzt am 31.10.2026 (Version 1.12.0.0). Tests: Core 444, App 688 grün. Festlegungen bei der Umsetzung: Der Schatten gehört zur Ausgabe, soweit er über den Innenabstand hinausreicht (die Ausgabe wird dann um den Rest größer); `FrameLayout.AspectLimited` meldet, dass das Seitenverhältnis wegen der Obergrenzen (16.384 Pixel je Seite, 100 Mio. Pixel; `MaxAspectSide`/`MaxAspectPixels`) nicht angewendet wurde. Eine Bild-Vorlage ohne vorhandenes Bild wird einfarbig (`FrameStyle.FromPreset`). Beim Einschalten des Hintergrunds ohne Innenabstand setzt das Panel 48 px (oder den gemerkten Wert), sonst wäre er hinter dem Bild unsichtbar. Offen: manuelle Prüfung der Bedienung im laufenden Editor (Dateiauswahl, Vorlagenraster, große Hintergrundbilder).
