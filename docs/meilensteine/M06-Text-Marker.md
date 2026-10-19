# M6 – Text & nummerierte Marker

← [M5](M05-Objekte-Formen.md) · [Umsetzungsplan](../Umsetzungsplan.md) · Weiter: [M7](M07-Freihand-Lineal.md)

## Ziel

Textobjekte mit freier Wahl von Schriftart, Schnitt, Größe sowie Linien- (Kontur) und Füllfarbe; zusätzlich nummerierte Schritt-Marker für Anleitungs-Screenshots. Beide sind vollwertige, nachträglich bearbeitbare Objekte.

## Voraussetzungen

- M5 abgeschlossen

## Umfang

**Enthalten**
- `TextAnnotation` mit mehrzeiligem Text, Ausrichtung, optionalem Hintergrundkasten
- In-Place-Bearbeitung direkt auf der Zeichenfläche
- Schriftauswahl mit Vorschau, Schnitte je Schriftfamilie
- `MarkerAnnotation` mit automatischer Nummerierung

## Aufgaben

### 1. Datenmodell (Core)
- [x] `TextAnnotation : Annotation`: `Text`, `Position` (oben links), `FontFamilyName`, `FontWeight` (int 100–900), `FontStyle` (Normal/Italic/Oblique), `FontStretch` (int 1–9), `FontSize` (px), `Fill` (`ArgbColor`, Textfarbe), `Stroke` (`StrokeStyle`, Kontur; Stärke 0 = keine), `TextAlignment`, `Background` (`ArgbColor`), `BackgroundPadding`, `BackgroundCornerRadius`, optional `Decorations` (Unterstrichen/Durchgestrichen), `WrapWidth` (null = keine Umbrüche)
- [x] Schriftwerte als einfache Typen (string/int), damit der Core WPF-frei bleibt
- [x] `MarkerAnnotation : Annotation`: `Center`, `Number`, `Shape` (Kreis/Quadrat/abgerundet), `Size`, `Fill`, `Stroke`, `TextColor`, `FontFamilyName`, optional `PointerTarget` (Sprechblasen-Spitze zu einem Punkt)

### 2. Rendering
- [x] Text über `FormattedText` (Kultur, `FlowDirection`, `PixelsPerDip` = 1 für Bildpixel-Treue)
- [x] Mit Kontur: `FormattedText.BuildGeometry()` → erst Kontur (`Pen` mit `LineJoin.Round`, doppelte Stärke), dann Füllung → saubere Außenkontur
- [x] Ohne Kontur: `DrawText` (besseres Hinting)
- [x] `TextOptions.TextFormattingMode = Ideal`, `TextRenderingMode = Grayscale` im Export (kein ClearType-Farbsaum in Bilddateien)
- [x] `Bounds` aus `FormattedText.Width/Height` (+ Padding/Kontur), Cache für Layout invalidiert bei Text-/Schriftänderung
- [x] Marker: Form + zentrierte Zahl (Schriftgröße relativ zu `Size`), optionale Spitze als Geometrie-Vereinigung

### 3. Werkzeuge
- [x] `TextTool`: Klick = neuer Text an Position (oder Ziehen = Textbox mit Umbruchbreite) → sofort Bearbeitungsmodus
- [x] `TextEditAdorner`: transparenter `TextBox` exakt über dem Objekt (gleiche Schrift, Größe × Zoom, `AcceptsReturn`), Objekt währenddessen ausgeblendet; `Esc`/Klick außerhalb beendet; leerer Text → Objekt wird verworfen (keine Undo-Leiche)
- [x] Gesamte Bearbeitungssitzung = eine Undo-Aktion
- [x] Doppelklick auf Text mit Auswahlwerkzeug startet Bearbeitung
- [x] Griffe: Verschieben; seitliche Griffe ändern `WrapWidth`; Eckgriffe skalieren `FontSize` proportional
- [x] `MarkerTool`: Klick setzt Marker mit nächster freier Nummer (`max + 1` im Dokument); Ziehen beim Setzen erzeugt Spitze zu Zielpunkt
- [x] Befehl „Marker neu nummerieren“ (nach Z-Reihenfolge oder Erstellreihenfolge); Nummer im Eigenschaftenpanel manuell änderbar; Startwert einstellbar

### 4. Eigenschaftenpanel
- [x] `FontPicker`: ComboBox mit `Fonts.SystemFontFamilies` (alphabetisch, lokalisierte Namen über `FamilyNames[XmlLanguage]`), jeder Eintrag in eigener Schrift dargestellt (virtualisiert!), Suche per Tastatur, zuletzt verwendete Schriften oben
- [x] `TypefacePicker`: Schnitte aus `FontFamily.FamilyTypefaces` (Weight/Style/Stretch) mit lokalisierten Namen („Fett Kursiv“), Fallback auf synthetisch Fett/Kursiv
- [x] Größe: editierbare ComboBox (8 … 144) + freie Eingabe
- [x] Schnellknöpfe **F**/*K*/U, Ausrichtung links/zentriert/rechts
- [x] Farben: Füllung (Textfarbe), Kontur (Farbe + Stärke), Hintergrund
- [x] Vorlagen: 3–4 Textstile („Beschriftung“, „Warnung rot mit weißer Kontur“, „Notiz gelb hinterlegt“) als Schnellauswahl

## Technische Details

- **Bildpixel-Treue:** `PixelsPerDip = 1` und `FontSize` in Bildpixeln sorgen dafür, dass 24-px-Text im Export genau 24 px hoch ist, unabhängig vom Monitor-DPI.
- **Fehlende Schrift beim Öffnen eines Projekts (M10):** WPF fällt auf eine Ersatzschrift zurück; Eigenschaftenpanel zeigt den gespeicherten Namen mit Warnsymbol.

## Tests

- `Core.Tests`: Nummerierungslogik (Lücken, Neu-Nummerierung), `Clone` von Text/Marker
- `App.Tests` (`[StaFact]`): Bounds bei mehrzeiligem Text und Umbruch, Kontur vergrößert Bounds um halbe Stärke, Bearbeitung mit leerem Ergebnis entfernt Objekt ohne Undo-Eintrag, `TypefacePicker` liefert Schnitte für „Segoe UI“

## Abnahmekriterien

- [ ] Text in beliebiger installierter Schrift, jedem verfügbaren Schnitt und Größe; Kontur- und Füllfarbe frei inkl. Transparenz
- [x] Text bleibt nach dem Platzieren per Doppelklick editierbar, Eigenschaften nachträglich änderbar
- [x] Export zeigt Text ohne Farbsäume und in korrekter Pixelgröße
- [x] Marker nummerieren sich automatisch fortlaufend; Neu-Nummerierung nach Löschen funktioniert

## Umsetzungsstand und Abweichungen

- Offen zur **manuellen Prüfung:** Text in beliebigen installierten Schriften/Schnitten (die Auswahl-Logik ist getestet, nicht jede Schrift).
- `TextAnnotation.Bounds` benötigt Textmessung, der Core bleibt aber WPF-frei: `TextAnnotation.MeasureProvider` wird vom Rendering-Projekt (`TextLayout.Install()`, aufgerufen in `AddRendering`, `DocumentExporter` und `AnnotationRenderer`) gesetzt; ohne Renderer greift eine grobe Schätzung. Das Layout wird je Objekt gecacht und bei Text-/Schriftänderung verworfen.
- Bearbeitung direkt auf der Fläche: `EditorContext.BeginTextEdit/CommitTextEdit/CancelTextEdit` (testbar ohne UI) plus ein `TextBox` im `Overlay`-Canvas von `EditorCanvas`. `Esc`, `Strg+Enter` oder Fokusverlust übernehmen; leerer Text verwirft neue Objekte ohne Undo-Eintrag, löscht bestehende (ein Undo-Schritt).
- Die Kontur wird mit doppelter Strichstärke unter die Füllung gezeichnet (`BuildGeometry`); `Bounds` wachsen um die halbe Stärke, wie in den Tests gefordert.
- Schriftauswahl: virtualisierte `ComboBox` (zuletzt verwendete Schriften oben), Schnitte aus `FamilyTypefaces`, Größe als editierbare `ComboBox`; fehlende Schriften werden mit einem Hinweis im Panel markiert. Vier Textvorlagen (Beschriftung, Warnung, Notiz, Hinweis).
- Marker: Startnummer und Neunummerierung (nach Ebenen- oder Erstellreihenfolge) im Marker-Abschnitt des Panels; `MarkerAnnotation.Created` hält die Erstellreihenfolge.
