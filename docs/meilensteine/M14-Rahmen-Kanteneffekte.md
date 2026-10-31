# M14 – Rahmen und Kanteneffekte

← [M13](M13-Emojis.md) · [Umsetzungsplan](../Umsetzungsplan.md) · Weiter: [M15](M15-Bild-verschoenern.md)

Feature-Vorschlag Nr. 9. Geschätzter Aufwand: 6 Personentage.

## Ziel

Das ganze Bild bekommt mit einem Klick Rand, Schatten, abgerundete Ecken, abgerissene Kanten, Vignette oder Graustufen. Die Effekte sind nicht-destruktiv und rückgängig machbar. Sie werden im Projekt gespeichert und wirken in Editor, Export, Zwischenablage und „Bild ziehen“ gleich. M14 legt die technische Grundlage für [M15](M15-Bild-verschoenern.md).

## Voraussetzungen

- M0–M13 abgeschlossen
- `docs/agent/architecture.md` gelesen (Abschnitte Erweiterte Zeichenfläche, Zuschnitt, Transparenzmaske, Gerasterte Effekte)

## Entscheidungen (mit dem Auftraggeber geklärt)

| Thema | Entscheidung |
|---|---|
| Bedienung | Neues Werkzeug **Rahmen** (Taste `F`) mit Karten im Eigenschaftenpanel; jede Änderung wirkt sofort auf der Zeichenfläche |
| Umfang | Rand, Schatten, abgerundete Ecken, abgerissene Kante, Vignette, Graustufen, Vorlage Polaroid |
| Graustufen | Betrifft nur den Screenshot. Objekte bleiben farbig. Unschärfe und Lupe zeigen den Screenshot ebenfalls grau |
| Polaroid | Vorlage ohne Text und ohne Neigung: weißer Rand, unten breiter, mit Schatten. Dafür hat der Rand je Seite eine eigene Breite |
| Neue Aufnahmen | Kein automatischer Rahmen. Vorlagen und zuletzt genutzte Werte stehen mit einem Klick bereit |

## Festlegungen dieses Plans

| Thema | Festlegung | Begründung |
|---|---|---|
| Bezugsfläche | Der Rahmen liegt um `EffectiveBounds`, also um den Zuschnitt oder die um Objekte erweiterte Zeichenfläche | Das Ergebnis enthält immer alles, was sonst exportiert würde |
| Koordinaten | Objekte bleiben in Basisbild-Koordinaten. `EffectiveBounds` ändert seine Bedeutung nicht, neu ist `OutputBounds` | Pipette, Statusleiste und Zuschnitt rechnen unverändert weiter |
| Objekte im Rahmenbereich | Zeichnet man dort ein Objekt, wächst die Zeichenfläche wie bisher und der Rahmen rückt nach außen | Es entsteht kein Sonderfall im Werkzeug-Code |
| Zuschneiden | Im Zuschnittmodus sind Rahmen und Clip ausgeblendet | Der Modus arbeitet auf dem Basisbild |
| Schatten | Folgt der Deckung des Inhalts (auch Maske, Freiform, runde Ecken, Riss) | Sonst schiene der Schatten durch transparente Stellen |
| Undo | Vorhandene `PropertyChangeAction<FrameStyle?>` (Ziel Dokument, Schlüssel `Frame`) | Regler fassen sich im vorhandenen 1,5-s-Fenster zusammen; keine neue Aktionsklasse |
| Stapelfolge | Von außen nach innen: Schatten, Rand, Inhalt mit Clip, Vignette. Die Vignette liegt über den Objekten | Die Abdunklung wirkt gleichmäßig |
| Aus-Zustand | Sind alle Effekte aus, wird `Frame` zu `null` normalisiert | Dokument ohne Rahmen verhält sich exakt wie heute, auch beim verlustfreien Export |

## Umfang

**Enthalten**
- Modell, Layout und Geometrie rein in Core
- Eine gemeinsame Zeichenroutine für Anzeige und Export
- Werkzeug, Panel, Vorlagen, Werkzeug-Vorgaben, Projektformat, Hilfe, Tests

**Nicht enthalten**
- Hintergrund, Innenabstand, Seitenverhältnis ([M15](M15-Bild-verschoenern.md))
- Bildunterschrift und Neigung beim Polaroid
- Automatischer Rahmen für neue Aufnahmen

## Aufgaben

### 1. Modell (Core)
- [x] `Core/Imaging/FrameStyle.cs`: unveränderlicher Record mit optionalen Feldern
  - Rand: `BorderColor`, `BorderLeft/Top/Right/Bottom` (ganze Bildpixel)
  - Schatten: vorhandener `ShadowStyle`
  - Ecken: `CornerRadius`
  - Riss: `TornSides` (Flags je Seite), `TornHeight`, `TornWidth`, `TornSeed`
  - `VignetteStrength`, `VignetteSize`, `Grayscale`
  - `IsEmpty` und `Normalized()` (leer → `null`, Werte geklemmt)
- [x] `Core/Imaging/FrameLayout.cs`: `Compute(content, style)` liefert `OutputBounds`, Inhalts- und Randrechteck, Schattenüberstand; je Seite höchstens `MaxCanvasOverhang`
- [x] `Core/Imaging/FrameGeometry.cs`: Risskante als Punktliste, deterministisch aus Größe, Seiten, Zahnmaß und Seed; abgerundetes Rechteck als Parameter
- [x] `ImageDocument.Frame` (`FrameStyle?`) und `OutputBounds` (benachrichtigt bei Änderungen von `Frame` und `EffectiveBounds`)
- [x] `ImageDocument.EffectiveImage` = `MaskedImage` plus Graustufen, gecacht je Maskenstand und Graustufen-Wert. `MaskedImage` bleibt farbig, damit Zauberstab (`MaskTools.cs:133`), `AlphaMask.VisibleBounds` (`MaskTools.cs:50`) und `CropGeometry.DetectContent` (`CropViewModel.cs:135`) sich nicht ändern
- [x] Graustufen-Umrechnung auf `PixelBuffer` in Core (Rec. 709, Alpha bleibt)
- [x] `AppSettings.FrameDefaults` als Referenztyp mit Standardwert, Normalisierung `null` → Standard

### 2. Zeichnen und Export (Rendering)
- [x] `FrameRenderer.DrawBehind(dc, layout, style, shadow)` zeichnet Schatten und Rand; Rand als Ring aus verbreitertem Umriss minus Fläche, wie bei `CalloutRenderer`
- [x] `FrameRenderer.DrawOver(dc, layout, style)` zeichnet die Vignette als Radialverlauf im Inhaltsclip
- [x] `FrameRenderer.CreateContentClip(layout, style)` liefert das `Geometry` für runde Ecken und Riss. Bei Riss hat der Rand eine einheitliche Breite (größter Wert), das Panel sperrt dann die Felder je Seite
- [x] Schatten über vorhandenes `SoftLayer.Create`: Quelle ist die Deckung des gerenderten Inhalts, Vorschau mit längster Kante ≤ 2048, Export mit Maßstab 1
- [x] `IDocumentExporter.RenderContent(document)` = bisheriger `Render`-Inhalt; `Render` setzt danach den Rahmen drauf. Der Pfad ohne Rahmen bleibt unverändert byte-genau
- [x] Unschärfe und Lupe bekommen `EffectiveImage` statt `MaskedImage` (`DocumentExporter.cs:23,27`, `EditorCanvas.xaml.cs:95,162`); `EffectCache.RemoveImage` beim Schließen und beim Wechsel berücksichtigt beide Stände (`EditorViewModel.cs:1088`)

### 3. Anzeige (`EditorCanvas`)
- [x] `EditorViewModel.ViewRect` = `IsCropping ? BaseImage.Bounds : Document.OutputBounds`
- [x] Neues `FrameBackLayer` (`IsHitTestVisible=False`) im `SurfaceHost` unter `Surface`, Größe = `ViewRect`
- [x] Neues `FrameOverLayer` im `Surface` zwischen `Annotations` und `Interaction`
- [x] Clip direkt auf `BaseImage` und `Annotations`, nicht auf `Surface`, weil Griffe und Texteingabe sonst mit abgeschnitten würden
- [x] Bei Zuschnitt plus Rahmen übernimmt derselbe Clip das Abschneiden auf `EffectiveBounds`, denn `Frame.ClipToBounds` schneidet dann erst am Rahmen ab (`EditorCanvas.xaml.cs:189`)
- [x] Schatten neu berechnen bei `Undo.Changed` und Änderung von `Frame`, nicht während des Ziehens (gleicher Zeitpunkt wie `CanvasBounds`)
- [x] `UpdateDisplay` (`EditorCanvas.xaml.cs:141`) wendet Graustufen auf den kopierten Bereich an, damit Live-Radieren weiter bereichsweise läuft; `OnUndoChanged` prüft zusätzlich den Graustufen-Stand
- [x] Zuschnittmodus blendet beide Ebenen und den Clip aus

### 4. Stellen, die `EffectiveBounds` meinen und bleiben oder wechseln
- [x] Bleibt bei `EffectiveBounds`: `SetPointer` (`EditorViewModel.cs:494`), `PipetteTool` (`ToolRegistry.cs:45`)
- [x] `PipetteTool.Activate` (`ToolRegistry.cs:33`) nutzt `RenderContent`, sonst stimmt der Bezugspunkt nicht mehr
- [x] Wechselt zu `OutputBounds`: `ImageSizeText` (`EditorViewModel.cs:274`), `PlaceOn` (`EditorWindow.xaml.cs:54`)
- [x] `DropPoint` und `VisibleCenter` (`EditorCanvas.xaml.cs:70,880`) klemmen auf `EffectiveBounds` statt `ViewRect`, damit neue Objekte nicht im Rahmen landen

### 5. Werkzeug und Panel (App)
- [x] `FrameTool` (`Id = "frame"`, Taste `F`, ohne Zeigeraktion), Eintrag in `ToolRegistry.Create`, Strings `Tool_Frame`
- [x] `FramePropertiesViewModel`: bindet an `Document.Frame`; ändert über `PropertyChangeAction`; speichert jede Änderung auch in `FrameDefaults`
- [x] Karten mit `PanelSection` und Schalter (`HasSwitch`): `Border`, `FrameShadow`, `Corners`, `TornEdge`, `Vignette`; Graustufen als einfacher Schalter
- [x] Einschalten stellt den zuletzt genutzten Wert aus `FrameDefaults` wieder her. Der vorhandene `EffectSwitch<T>` deckt das nur bis zum Schließen des Editors ab, deshalb hier zusätzlich `FrameDefaults`
- [x] Rand: gekoppelte Breite mit Umschalter „je Seite“; Riss: Kontrollkästchen je Seite und „Neu würfeln“ (wie beim Stempel)
- [x] Schaltfläche „Rahmen entfernen“ (eine Undo-Aktion)
- [x] Eingebaute Schnellvorlagen in `FramePresets.BuiltIn` (Code, nicht löschbar, Namen über Strings): Dezenter Schatten, Einfacher Rand, Polaroid, Abgerissen unten, Vignette
- [x] Eigene Vorlagen über `PresetKind.Frame` und `ToolPreset.Frame` in der vorhandenen `PresetList`
- [x] `ToolDefaultsReset`: Zweig `FrameTool` setzt `FrameDefaults` zurück. Die Schaltfläche ist nur sichtbar, solange `Document.Frame` leer ist, weil der Reset kein Undo hat
- [x] Tastenkürzel im `ShortcutCatalog`, Prüfung auf Konflikt mit `F`; alle Texte in `Strings.resx`, `Strings.de.resx`, `Strings.Designer.cs`

### 6. Projektformat
- [x] `ProjectContent.Frame`; in `document.json` nur schreiben, wenn gesetzt, damit Golden-Dateien gleich bleiben
- [x] Lesen tolerant: defekter Eintrag ergibt Warnung, kein Abbruch; `formatVersion` bleibt 1
- [x] `ProjectService.Snapshot` und `LoadAsync` übernehmen den Wert; automatische Sicherung läuft darüber mit
- [x] Ältere Programmversionen ignorieren das Feld und zeigen das Bild ohne Rahmen und in Farbe

### 7. Dokumentation
- [x] Hilfe: Abschnitt `tool-frame` in `docs/help/de` und `docs/help/en`, Bildschirmfoto über `build/generate-help-screenshots.ps1`
- [x] `docs/agent/architecture.md`: Datenfluss, `OutputBounds` gegen `EffectiveBounds`, `EffectiveImage` gegen `MaskedImage`
- [x] `CHANGELOG.md`, Änderungshistorie in `CLAUDE.md`, Version 2. Stelle erhöhen

## Tests

- `Core.Tests`
  - `FrameLayout` für alle Effekte, negative `EffectiveBounds`, Zuschnitt, Obergrenzen
  - `FrameGeometry` deterministisch bei gleichem Seed, verschieden bei anderem Seed
  - `FrameStyle.Normalized`, JSON-Roundtrip, Golden-Datei unverändert ohne Rahmen
  - `EffectiveImage`: Cache, Maskenstand, Alpha bleibt, `MaskedImage` bleibt farbig
  - Undo und Redo fassen Reglerfolgen zusammen
- `App.Tests`
  - Export-Pixeltests: Rand je Seite, runde Ecken transparent, Riss transparent, Schatten rechts unten, Vignette dunkelt die Ecke ab
  - Ohne Rahmen bleibt `Render` byte-gleich zum heutigen Ergebnis
  - Graustufen: Basisbild grau, Objekt farbig, Unschärfe und Lupe grau
  - Pipette und Mausposition stimmen mit Rahmen; Zuschnitt plus Rahmen hat die erwartete Größe
  - `FramePropertiesViewModel`: Schalter, Wiederherstellen, Vorlagen, „Rahmen entfernen“ als ein Undo-Schritt
  - JPG füllt Transparenz weiß (Schatten auf Weiß)

## Abnahmekriterien

- [ ] Anzeige und Export stimmen bei allen Effekten optisch überein, auch nach Zoom (manuell im laufenden Editor noch zu prüfen; Anzeige per Bildschirmfoto geprüft)
- [x] Jede Kombination lässt sich mit einem Undo-Schritt je Änderung zurücknehmen und bleibt nach Speichern und Laden des Projekts erhalten
- [ ] Griffe und Texteingabe werden vom Clip nicht abgeschnitten (manuell im laufenden Editor noch zu prüfen; Anzeige per Bildschirmfoto geprüft)
- [x] Objekte lassen sich mit aktivem Rahmen weiter normal verschieben, drehen und skalieren
- [x] Regler bleiben bei einem Full-HD-Bild flüssig; neu berechneter Schatten dauert höchstens 300 ms
- [x] Bild ohne Rahmen verhält sich exakt wie vor M14

## Stand

Umgesetzt am 31.10.2026 (Version 1.11.0.0). Tests: Core 414, App 660 grün. Schatten bei Full HD gemessen: ca. 110–220 ms. Offen: manuelle Prüfung der Bedienung (Zoom, Griffe, Texteingabe mit Clip, Drag-and-drop).
