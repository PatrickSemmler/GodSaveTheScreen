# GodSaveTheScreen

Screenshot- und Annotationstool, .NET 10 / C# / WPF (MVVM). Umsetzungsplan: `docs/Umsetzungsplan.md`, Meilensteine: `docs/meilensteine/`.

- Sprache für Doku, Commits und UI-Texte: Deutsch (UI zusätzlich Englisch über `.resx`)
- Arbeit erfolgt meilensteinweise; vor Beginn das jeweilige Meilenstein-Dokument lesen und Checkboxen beim Abschluss abhaken

## Versionierung
- Version vierstellig (`Major.Minor.Patch.Fix`) in `Directory.Build.props` (`<Version>`); `build/release.ps1` akzeptiert nur dieses Format
- Bei jedem Commit mit Code-/Funktionsänderung im selben Commit erhöhen:
  - Sehr umfangreiche Änderungen: 1. Stelle erhöhen
  - Neues Feature: 2. Stelle erhöhen
  - Bestehendes Feature erweitert / geändert: 3. Stelle erhöhen
  - Bugfix: 4. Stelle erhöhen
- Alle Stellen hinter der erhöhten Stelle auf 0 setzen (z. B. 1.2.3.4 + neues Feature → 1.3.0.0)
- Neue Version im Eintrag der Änderungshistorie angeben (Zeile **Version**)

## Agent-Architekturdoku
- Pfad: `docs/agent/architecture.md` (Index auf weitere Dateien in `docs/agent/`)
- Lesen vor: Änderungen an Modulstruktur, Abhängigkeiten, übergreifenden Features; bei Unsicherheit über Konventionen
- Pflicht: Bei Änderung von Modulstruktur, Abhängigkeiten oder Konventionen die Doku im selben Commit aktualisieren (Skill `agent-architecture-doc`)
- Stand: M0–M13 gegen den Code verifiziert (Skill bei Änderungen im Update-Modus nutzen)

## Verbesserungsliste (Skill `find-improvements`)
- Datei: `Verbesserungsliste.html` im Projektstamm (eigenständig, `file://`-tauglich)
- Nach jeder umgesetzten Änderung: Status-Badge des Eintrags auf „In Arbeit“ bzw. „Erledigt“ setzen (CSS-Klasse `badge-inprogress` / `badge-erledigt` UND Text), Datum im Kopf und Footer aktualisieren; Zähler und Fortschrittsbalken berechnet das eingebettete JavaScript
- IDs sind stabil, nie neu vergeben; nicht mehr zutreffende Punkte auf „Obsolet“ setzen statt löschen
- Pro Änderungspunkt ein eigener Commit (Code + Liste + Eintrag in der Änderungshistorie zusammen)
- Tests: `dotnet test` im Solution-Root führt hier 0 Tests aus (Exitcode 5, Punkt #7); funktionierend: Testprojekt-`.exe` aus der Ausgabe starten, z. B. `dotnet build -p:ArtifactsPath=C:/Temp/gsts-art` und dann `C:/Temp/gsts-art/bin/GodSaveTheScreen.Core.Tests/debug/GodSaveTheScreen.Core.Tests.exe`
- Baseline 19.10.2026: Core.Tests 266 grün; App.Tests 448, 1 Fehler (inzwischen behoben, #5)

## Änderungshistorie

### Settings-Speichern fängt IO-Fehler ab – 19.10.2026
- **Kategorie**: Stabilität
- **Verbesserungsliste-ID**: #1
- **Geänderte Dateien**: `src/GodSaveTheScreen.Core/Settings/JsonSettingsService.cs`, `tests/GodSaveTheScreen.Core.Tests/JsonSettingsServiceTests.cs`
- **Was wurde gemacht**: `SaveAsync` fängt `IOException`/`UnauthorizedAccessException` und protokolliert sie
- **Warum**: Aufrufer starten `SaveAsync` fire-and-forget (`_ = ...`); Fehler waren unbeobachtet und unsichtbar
- **Seiteneffekte / Hinweise**: `SaveAsync` wirft bei diesen Fehlern nicht mehr; der Benutzer bekommt keine Meldung, nur Log-Eintrag

### Instabilen Cache-Test repariert – 19.10.2026
- **Kategorie**: Technik
- **Verbesserungsliste-ID**: #5
- **Geänderte Dateien**: `src/GodSaveTheScreen.Rendering/RegionEffects.cs`, `tests/GodSaveTheScreen.App.Tests/M11Tests.cs`
- **Was wurde gemacht**: `EffectCache.CountFor(baseId)` ergänzt; der Test prüft nur noch Einträge des eigenen Bildes und ruft `RemoveImage` nicht mehr selbst auf (das hatte die Prüfung von `Dispose` verdeckt)
- **Warum**: Vergleich des globalen Zählers von `EffectCache.Shared` war durch parallele Tests unzuverlässig
- **Seiteneffekte / Hinweise**: App.Tests 448/448 grün in 3 Läufen

### Drag-Temp-Dateien werden aufgeräumt – 19.10.2026
- **Kategorie**: Stabilität
- **Verbesserungsliste-ID**: #2
- **Geänderte Dateien**: `src/GodSaveTheScreen.App/Services/DragTempFiles.cs` (neu), `App.xaml.cs`, `ViewModels/EditorViewModel.cs`, `tests/GodSaveTheScreen.App.Tests/DragTempFilesTests.cs` (neu), `docs/agent/architecture.md`
- **Was wurde gemacht**: Beim Start löscht ein Hintergrund-Task PNGs älter als 1 Tag im Drag-Temp-Ordner
- **Warum**: Die Dateien aus „Bild ziehen“ wurden nie gelöscht
- **Seiteneffekte / Hinweise**: App.Tests 451/451 grün. `EditorViewModel.cs` enthielt fremde, uncommittete Änderungen des Nutzers; nur die eigene Zeile wurde gestaged

### Zeichenfläche wächst um überstehende Objekte – 20.10.2026
- **Kategorie**: Funktionserweiterung
- **Verbesserungsliste-ID**: – (Nutzerwunsch, Verhalten wie Screenpresso)
- **Geänderte Dateien**: `src/GodSaveTheScreen.Core/Imaging/ImageDocument.cs`, `src/GodSaveTheScreen.App/Controls/EditorCanvas.xaml(.cs)`, `Controls/AnnotationLayer.cs`, `Editor/CropState.cs`, `Services/ClipboardService.cs`, `ViewModels/EditorViewModel.cs`, `tests/…/CanvasBoundsTests.cs` (neu), `tests/…/CanvasExpansionTests.cs` (neu), `docs/agent/architecture.md`
- **Was wurde gemacht**: `ImageDocument.CanvasBounds` (Bild ∪ sichtbare Objekte); `EffectiveBounds` nutzt sie ohne Zuschnitt. Editor zeigt den erweiterten Bereich als Schachbrett, Export liefert ihn transparent
- **Warum**: Objekte über den Bildrand hinaus wurden abgeschnitten
- **Seiteneffekte / Hinweise**: Anpassung erst nach Loslassen/Undo (Ziehen zeigt das Objekt ungeschnitten als Vorschau). Mit Zuschnitt keine Erweiterung. JPG/BMP/`CF_DIB` füllen den Bereich weiß. Max. 8192 px Überstand je Seite. Tests: Core 275, App 454 grün; manuelle Prüfung im laufenden Editor steht noch aus

### Schatten und weiche Textkontur – 20.10.2026
- **Kategorie**: Funktionserweiterung
- **Verbesserungsliste-ID**: – (Nutzerwunsch)
- **Geänderte Dateien**: `src/GodSaveTheScreen.Core/Annotations/ShadowStyle.cs` (neu), `Annotation.cs`, `Shapes.cs`, `TextAnnotation.cs`, `MarkerAnnotation.cs`, `EmojiAnnotation.cs`, `Settings/ToolDefaults.cs`, `Settings/ToolPresets.cs`, `Settings/AppSettings.cs`, `src/GodSaveTheScreen.Rendering/SoftLayer.cs` (neu), `AnnotationRenderer.cs`, `RegionEffects.cs`, `src/GodSaveTheScreen.App/ViewModels/ShadowPropertiesViewModel.cs` (neu), `TextPropertiesViewModel.cs`, `EditorViewModel.cs`, `Controls/PropertyPanel.xaml`, `Controls/AnnotationLayer.cs`, `Editor/StyleConversions.cs`, `Editor/PresetStyles.cs`, `Editor/EmojiToolState.cs`, `Editor/Tools/TextTools.cs`, `Resources/Strings*.resx`, `Strings.Designer.cs`, Tests (`ShadowTests.cs` in Core und App, Golden-Dateien), `docs/agent/architecture.md`
- **Was wurde gemacht**: Schattenfarbe, -größe (Versatz nach rechts unten) und -unschärfe für Formen, Linien/Pfeile, Text, Marker und Emoji; Kontur-Unschärfe für Text (nur die Kontur wird weich). Panel-Abschnitt „Schatten“, Standardwerte je Werkzeug, Vorlagen übernehmen die Werte
- **Warum**: Nutzerwunsch
- **Seiteneffekte / Hinweise**: Schatten ist aus, solange die Farbe transparent ist (Standard). Freihand-Stift/Textmarker haben keinen Schatten. Projektformat bleibt v1 (optionale Felder), Golden-Dateien neu erzeugt. Effekte werden gerastert (Auflösung = Zoom bzw. 1 im Export) und je Objekt gecacht. Tests: Core 284, App 467 grün; manuelle Prüfung im laufenden Editor steht noch aus

### Vierstellige Versionsnummer und Erhöhungsregel – 20.10.2026
- **Kategorie**: Technik
- **Verbesserungsliste-ID**: – (Nutzerwunsch)
- **Version**: 1.2.0.0
- **Geänderte Dateien**: `Directory.Build.props`, `Directory.Build.targets` (Kommentar), `build/release.ps1`, `src/GodSaveTheScreen.App/ViewModels/SettingsViewModel.cs`, `docs/agent/architecture.md`, `CLAUDE.md`
- **Was wurde gemacht**: Version auf 1.2.0.0 gesetzt, Regel zur Erhöhung im Abschnitt „Versionierung“ festgehalten; Einstellungsdialog zeigt vier Stellen, Release-Skript akzeptiert nur noch vierstellige Versionen
- **Warum**: Nutzerwunsch, einheitliches Versionsschema
- **Seiteneffekte / Hinweise**: ZIP-Name jetzt `GodSaveTheScreen-1.2.0.0-win-x64.zip`; SemVer-Suffixe (`-beta`) werden vom Release-Skript nicht mehr akzeptiert

### Bildbereiche entfernen (Transparenz) – 20.10.2026
- **Kategorie**: Funktionserweiterung
- **Verbesserungsliste-ID**: – (Nutzerwunsch)
- **Version**: 1.3.0.0
- **Geänderte Dateien**: `src/GodSaveTheScreen.Core/Imaging/AlphaMask.cs`, `AreaSelection.cs`, `MaskBrush.cs` (alle neu), `PolygonMask.cs`, `ImageDocument.cs`, `Undo/MaskEditAction.cs` (neu), `Project/ProjectSerializer.cs`, `Settings/AppSettings.cs`, `Settings/ToolPresets.cs`, `src/GodSaveTheScreen.Rendering/DocumentExporter.cs`, `ProjectService.cs`, `src/GodSaveTheScreen.App/Editor/Tools/MaskTools.cs` (neu), `ToolRegistry.cs`, `Editor/EditorContext.cs`, `Editor/ShortcutCatalog.cs`, `Controls/EditorCanvas.xaml.cs`, `AnnotationLayer.cs`, `InteractionLayer.cs`, `PropertyPanel.xaml`, `ViewModels/MaskToolsViewModel.cs` (neu), `EditorViewModel.cs`, `CropViewModel.cs`, `Resources/Strings*.resx`, `Strings.Designer.cs`, Tests (`AlphaMaskTests.cs`, `MaskToolTests.cs`), `Directory.Build.props`, `docs/agent/architecture.md`
- **Was wurde gemacht**: Werkzeug „Bereich auswählen und entfernen“ (W: Rechteck, Ellipse, Lasso, Zauberstab; Umschalt/Alt kombinieren; Entf macht transparent; weiche Kante, Auswahl umkehren) und „Hintergrundradierer“ (D: Größe, Härte, Alt = wiederherstellen, `[`/`]`). Dazu „Ganzen Screenshot wiederherstellen“ und „Auf sichtbaren Bereich zuschneiden“. Umsetzung als nicht-destruktive Transparenzmaske des Basisbilds
- **Warum**: Nutzerwunsch
- **Seiteneffekte / Hinweise**: Nur der Screenshot wird transparent, Objekte bleiben darüber; Unschärfe und Lupe sehen den entfernten Bereich ebenfalls nicht. Projektdatei bekommt optional `mask.bin` (Format bleibt v1; ältere Programmversionen ignorieren die Maske und zeigen das volle Bild). Die Bereichsauswahl ist nicht im Undo-Verlauf und wird beim Werkzeugwechsel aufgehoben. JPG/BMP/`CF_DIB` füllen transparente Stellen weiß. Test `Version_IstSemVer` vorher separat auf vierstellige Versionen korrigiert. Tests: Core 314, App 481 grün; manuelle Prüfung im laufenden Editor steht noch aus

### Stempel-Werkzeug – 20.10.2026
- **Kategorie**: Funktionserweiterung
- **Verbesserungsliste-ID**: – (Nutzerwunsch)
- **Version**: 1.4.0.0
- **Geänderte Dateien**: `src/GodSaveTheScreen.Core/Annotations/StampAnnotation.cs` (neu), `Annotation.cs`, `TextAnnotation.cs`, `EmojiAnnotation.cs`, `Settings/ToolDefaults.cs`, `Settings/AppSettings.cs`, `src/GodSaveTheScreen.Rendering/StampRenderer.cs` (neu), `AnnotationRenderer.cs`, `TextLayout.cs`, `src/GodSaveTheScreen.App/Editor/Tools/StampTool.cs` (neu), `Editor/StampToolState.cs` (neu), `ViewModels/StampPropertiesViewModel.cs` (neu), `Editor/EditorContext.cs`, `Editor/Tools/SelectTool.cs`, `ShapeTools.cs`, `ToolRegistry.cs`, `Controls/EditorCanvas.xaml.cs`, `AnnotationLayer.cs`, `PropertyPanel.xaml`, `ViewModels/EditorViewModel.cs`, `Resources/Strings*.resx`, `Strings.Designer.cs`, Tests (`StampTests.cs`, `StampToolTests.cs`, `M6Tests.cs`), `Directory.Build.props`, `docs/agent/architecture.md`
- **Was wurde gemacht**: Werkzeug „Stempel“ (S): einfarbiger Rahmen (Rechteck, abgerundet, Doppelrahmen, Oval) mit mehrzeiligem Text, der sich an den Text anpasst. Regler für Stempel-Look (ausgefranste Kanten, Fehlstellen, Sprenkel, Kratzer, Ausreißer am Rahmen) mit festem Muster je Stempel und „Neu würfeln“, Rahmenstärke, Neigung (Standard −8°), Schrift/Schnitt/Größe, Tinten-Look (Multiplizieren), Deckkraft, Schnellauswahl von sechs Texten. Text wird direkt im Objekt bearbeitet; Eckgriffe skalieren Rahmen und Schrift proportional
- **Warum**: Nutzerwunsch
- **Seiteneffekte / Hinweise**: Tinten-Look multipliziert nur mit dem Screenshot, nicht mit Objekten darunter. Stempel haben keine Vorlagen und keinen Schatten. Auswahländerungen im Panel werden auch Vorgabe neuer Stempel; der zuletzt gestempelte Text wird Standardtext. Projektformat bleibt v1 (neuer Typ `stamp`; ältere Programmversionen überspringen Stempel mit Warnung). Textbearbeitung (`TextEditSession`) und `IRotatableAnnotation.Rotation` (jetzt settable) verallgemeinert. Tests: Core 324, App 496 grün; manuelle Prüfung im laufenden Editor steht noch aus

### Stempel: Schriftauszeichnungen und Standardschrift – 21.10.2026
- **Kategorie**: Funktionserweiterung
- **Verbesserungsliste-ID**: – (Nutzerwunsch)
- **Version**: 1.4.1.0
- **Geänderte Dateien**: `src/GodSaveTheScreen.Core/Annotations/StampAnnotation.cs`, `Settings/ToolDefaults.cs`, `src/GodSaveTheScreen.Rendering/StampRenderer.cs`, `src/GodSaveTheScreen.App/Editor/StampToolState.cs`, `ViewModels/StampPropertiesViewModel.cs`, `Controls/PropertyPanel.xaml`, `Controls/EditorCanvas.xaml.cs`, Tests (`StampTests.cs`, `StampToolTests.cs`), `Directory.Build.props`
- **Was wurde gemacht**: Schaltflächen Fett, Kursiv, Unterstrichen, Durchgestrichen im Stempel-Panel (wie beim Text). Neue Eigenschaft `Decorations` am Stempel; Unter-/Durchstreichung werden kräftiger gezeichnet als die Linien der Schrift (7,5 % der Schriftgröße), damit sie den Stempel-Look überstehen. Standardschrift jetzt Bahnschrift Bold (bei jedem Windows 10/11 vorhanden, DIN-artig, auch abgenutzt gut lesbar) statt Arial Black
- **Warum**: Nutzerwunsch
- **Seiteneffekte / Hinweise**: Wer schon Einstellungen mit dem bisherigen Standard gespeichert hat, behält Arial Black (gespeicherte Vorgaben werden nicht überschrieben). Rockwell/Stencil sähen ebenfalls gut aus, kommen aber nur mit Office. Projektformat bleibt v1 (optionales Feld). Tests: Core 325, App 498 grün; manuelle Prüfung im laufenden Editor steht noch aus

### Werkzeug-Einstellungen zurücksetzen – 21.10.2026
- **Kategorie**: Funktionserweiterung
- **Verbesserungsliste-ID**: – (Nutzerwunsch)
- **Version**: 1.5.0.0
- **Geänderte Dateien**: `src/GodSaveTheScreen.App/Editor/ToolDefaultsReset.cs` (neu), `Editor/ToolDefaultsService.cs`, `Editor/EmojiToolState.cs`, `ViewModels/EditorViewModel.cs`, `Controls/PropertyPanel.xaml`, `Resources/Strings*.resx`, `Strings.Designer.cs`, `tests/GodSaveTheScreen.App.Tests/ToolDefaultsResetTests.cs` (neu), `Directory.Build.props`, `docs/agent/architecture.md`
- **Was wurde gemacht**: Schaltfläche „Standard wiederherstellen“ im Eigenschaftenpanel (über der Vorlagenliste) für alle Werkzeuge mit Einstellungen: Formen, Linie/Pfeil, Stift, Textmarker, Radierer, Text, Marker (inkl. Startnummer), Unschärfe, Lupe, Bereichsauswahl, Hintergrundradierer, Emoji, Stempel. Nach Rückfrage werden die gespeicherten Werte durch die eingebauten Standardwerte ersetzt
- **Warum**: Nutzerwunsch
- **Seiteneffekte / Hinweise**: Nur sichtbar, wenn nichts ausgewählt ist (dann zeigt das Panel die Werkzeug-Vorgaben); kein Undo, daher Rückfrage. Stift und Textmarker teilen sich Glättung/Druck – Zurücksetzen des einen setzt diese auch beim anderen zurück. Emoji behält den Hautton, Stempel bekommt wieder den lokalisierten Standardtext. Vorlagen, zuletzt verwendete Farben/Schriften/Emojis und „Textmarker hinter Objekten“ (Dokumentoption) bleiben unverändert. Auswahl, Zuschneiden und Pipette haben keinen Button. Tests: Core 325, App 505 grün; manuelle Prüfung im laufenden Editor steht noch aus

### Sprechblasen-Werkzeug – 21.10.2026
- **Kategorie**: Funktionserweiterung
- **Verbesserungsliste-ID**: – (Nutzerwunsch, Verhalten wie Screenpresso)
- **Version**: 1.6.0.0
- **Geänderte Dateien**: `src/GodSaveTheScreen.Core/Annotations/CalloutAnnotation.cs`, `CalloutGeometry.cs` (beide neu), `Annotation.cs`, `Settings/ToolDefaults.cs`, `Settings/AppSettings.cs`, `Settings/ToolPresets.cs`, `src/GodSaveTheScreen.Rendering/CalloutRenderer.cs` (neu), `AnnotationRenderer.cs`, `src/GodSaveTheScreen.App/Editor/Tools/CalloutTool.cs` (neu), `Editor/CalloutToolState.cs` (neu), `ViewModels/CalloutPropertiesViewModel.cs` (neu), `Editor/EditorContext.cs`, `Editor/PresetStyles.cs`, `Editor/ToolDefaultsReset.cs`, `Editor/Tools/SelectTool.cs`, `ShapeTools.cs`, `TextTools.cs`, `ToolRegistry.cs`, `Controls/EditorCanvas.xaml.cs`, `AnnotationLayer.cs`, `InteractionLayer.cs`, `PropertyPanel.xaml(.cs)`, `ViewModels/EditorViewModel.cs`, `PresetListViewModel.cs`, `ShadowPropertiesViewModel.cs`, `Resources/Strings*.resx`, `Strings.Designer.cs`, Tests (`CalloutTests.cs`, `CalloutToolTests.cs`, `CalloutRenderTests.cs`), `Directory.Build.props`, `docs/agent/architecture.md`
- **Was wurde gemacht**: Werkzeug „Sprechblase“ (K): Körper aufziehen (Klick = Standardgröße), Text direkt in der Blase eingeben (umbrochen, mittig; Blase wächst nach unten, wenn der Text nicht passt). Die Spitze wird im Auswahlwerkzeug am runden Griff frei gezogen und wandert dabei auf die Seite, zu der sie zeigt. Formen: abgerundetes Rechteck (Eckenradius), Ellipse, Gedankenblase (Wolke mit Kreisen als Spitze); Breite der Spitze, gebogene Spitze. Schrift wie beim Text (Schriftart, Schnitt, Größe, Fett/Kursiv/Unter-/Durchgestrichen, Ausrichtung), Textfarbe, Hintergrund, Leuchtfarbe (Rahmen außen) mit Breite und Unschärfe, Innenabstand, Schatten, Deckkraft, Vorlagen und „Standard wiederherstellen“
- **Warum**: Nutzerwunsch
- **Seiteneffekte / Hinweise**: Eckgriffe skalieren nur den Körper (Schrift bleibt, Text wird neu umbrochen; Spitze bleibt stehen); Verschieben bewegt Körper und Spitze. Liegt die Spitze im Körper, wird keine gezeichnet. Gebogene Spitze gilt nicht für die Gedankenblase. Projektformat bleibt v1 (neuer Typ `callout`; ältere Programmversionen überspringen Sprechblasen mit Warnung). Tests: Core 344, App 530 grün; manuelle Prüfung im laufenden Editor steht noch aus

### Sprechblase: Drehen und Biegung umkehren – 21.10.2026
- **Kategorie**: Funktionserweiterung
- **Verbesserungsliste-ID**: – (Nutzerwunsch)
- **Version**: 1.6.0.0 (gemeinsamer Commit mit dem Sprechblasen-Werkzeug)
- **Geänderte Dateien**: `src/GodSaveTheScreen.Core/Annotations/CalloutAnnotation.cs`, `CalloutGeometry.cs`, `Settings/ToolDefaults.cs`, `src/GodSaveTheScreen.Rendering/CalloutRenderer.cs`, `src/GodSaveTheScreen.App/Editor/Tools/SelectTool.cs`, `TextTools.cs`, `Editor/PresetStyles.cs`, `Controls/EditorCanvas.xaml.cs`, `PropertyPanel.xaml`, `ViewModels/CalloutPropertiesViewModel.cs`, `Resources/Strings*.resx`, `Strings.Designer.cs`, Tests (`CalloutTests.cs`, `CalloutToolTests.cs`, `CalloutRenderTests.cs`), `Directory.Build.props`, `docs/agent/architecture.md`
- **Was wurde gemacht**: Sprechblasen lassen sich drehen: Körper und Text drehen sich um die Körpermitte, der Zielpunkt der Spitze bleibt im Bild stehen. Bedienung über Drehgriff an der Auswahl (Umschalt = 15°-Schritte), Regler/Zahlenfeld „Drehung“ mit „Zurücksetzen“ im Panel sowie `[`/`]`. Eck- und Kantengriffe skalieren im gedrehten System, die Texteingabe wird mitgedreht. Statt „Spiegeln“ gibt es den Schalter „Biegung umkehren“ für die gebogene Spitze (Werkzeug-Vorgabe und Vorlagen übernehmen ihn)
- **Warum**: Nutzerwunsch. Spiegeln wurde nach Rückfrage durch „Biegung umkehren“ ersetzt: Alle Körperformen sind symmetrisch, bei festem Zielpunkt hätte horizontales und vertikales Spiegeln nur dieselbe Krümmung umgekehrt
- **Seiteneffekte / Hinweise**: Drehung ist keine Werkzeug-Vorgabe (neue Blasen entstehen ungedreht; Regler nur bei Auswahl sichtbar). Wächst eine gedrehte Blase beim Tippen, bleibt ihre Oberkante stehen. Gruppen-Skalierung mehrerer Objekte skaliert eine gedrehte Blase achsparallel (Drehung bleibt). Projektformat bleibt v1 (optionale Felder Drehung und Biegungsumkehr; ältere Programmversionen zeigen die Blase ungedreht). Tests: Core 352, App 537 grün; manuelle Prüfung im laufenden Editor steht noch aus

### Formen, Polygone, Freihandstriche und Text drehen – 21.10.2026
- **Kategorie**: Funktionserweiterung
- **Verbesserungsliste-ID**: – (Nutzerwunsch)
- **Version**: 1.6.1.0
- **Geänderte Dateien**: `src/GodSaveTheScreen.Core/Annotations/EmojiAnnotation.cs` (`RotationMath`), `Shapes.cs`, `StrokeAnnotation.cs`, `TextAnnotation.cs`, `src/GodSaveTheScreen.Rendering/AnnotationRenderer.cs`, `src/GodSaveTheScreen.App/Editor/Tools/SelectTool.cs`, `Editor/EditorContext.cs`, `Editor/StyleEditing.cs`, `Editor/PresetStyles.cs`, `Controls/EditorCanvas.xaml.cs`, `Controls/PropertyPanel.xaml`, `ViewModels/RotationPropertiesViewModel.cs` (neu), `ViewModels/EditorViewModel.cs`, `ViewModels/SelectionPropertiesViewModel.cs`, `Resources/Strings*.resx`, `Strings.Designer.cs`, Tests (`RotationTests.cs` in Core, `RotationToolTests.cs` in App), `Directory.Build.props`, `docs/agent/architecture.md`
- **Was wurde gemacht**: Rechteck, Ellipse, Dreieck, Polygon, Stift/Textmarker und Text lassen sich drehen: Drehgriff an der Auswahl (Umschalt = 15°-Schritte; beim Polygon zusätzlich zu den Eckpunkt-Griffen), `[`/`]` und Abschnitt „Drehung“ im Panel mit Regler, Zahlenfeld und „Zurücksetzen“. Die Drehung ist eine gespeicherte Eigenschaft (Rahmen bleibt gedreht, Winkel jederzeit änderbar); Griffe skalieren im gedrehten System, Polygon-Eckpunkte werden direkt im Bild gezogen bzw. per Doppelklick eingefügt, Text wird gedreht bearbeitet
- **Warum**: Nutzerwunsch. Entscheidungen nach Rückfrage: Drehung als Eigenschaft statt in die Punkte eingerechnet; Polygon bekommt zusätzlich einen Drehgriff; bei Mehrfachauswahl dreht sich jedes Objekt um die eigene Mitte; Schatten fällt immer nach rechts unten; Textmarker ist mit drehbar (gleicher Objekttyp wie der Stift); Linie/Pfeil, Marker, Unschärfe und Lupe bleiben ungedreht
- **Seiteneffekte / Hinweise**: Gedrehter Text wächst beim Tippen und bei Schriftänderungen von seiner linken oberen Ecke aus (die Ecke bleibt im Bild stehen). Keine Werkzeug-Vorgabe (neue Objekte entstehen ungedreht). Gruppen-Skalieren mehrerer Objekte bzw. `Alt+Pfeil` skaliert gedrehte Objekte achsparallel (Drehung bleibt). `Umschalt+H/V` reagiert nur noch, wenn ein Emoji ausgewählt ist. Projektformat bleibt v1 (optionales Feld, nur bei Drehung ≠ 0 geschrieben; ältere Programmversionen zeigen die Objekte ungedreht). Tests: Core 366, App 554 grün; manuelle Prüfung im laufenden Editor steht noch aus

### Kontur für Pfeil und Linie – 21.10.2026
- **Kategorie**: Funktionserweiterung
- **Verbesserungsliste-ID**: – (Nutzerwunsch)
- **Version**: 1.6.2.0
- **Geänderte Dateien**: `src/GodSaveTheScreen.Core/Annotations/Shapes.cs`, `Settings/ToolDefaults.cs`, `src/GodSaveTheScreen.Rendering/AnnotationRenderer.cs`, `src/GodSaveTheScreen.App/ViewModels/SelectionPropertiesViewModel.cs`, `Controls/PropertyPanel.xaml`, `Controls/AnnotationLayer.cs`, `Editor/StyleConversions.cs`, `Editor/PresetStyles.cs`, `Editor/Tools/ShapeTools.cs`, Tests (`LineOutlineTests.cs` in Core und App), `Directory.Build.props`, `docs/agent/architecture.md`
- **Was wurde gemacht**: Pfeil und Linie bekommen im Panel (unter den Linienenden) „Kontur“ mit Farbe und Stärke sowie „Kontur-Unschärfe“, wie beim Text. Die Kontur umschließt Linie und Spitzen, folgt bei gestrichelten Linien den Strichen und wird vom Schatten mit erfasst. Werkzeug-Vorgaben (je Werkzeug Linie/Pfeil getrennt), Vorlagen und „Standard wiederherstellen“ übernehmen die Werte
- **Warum**: Nutzerwunsch. Entscheidungen nach Rückfrage: auch für die Linie (gleicher Objekttyp); Kontur folgt der Strichelung; Schatten schließt die Kontur ein
- **Seiteneffekte / Hinweise**: Standard ohne Kontur (Weiß, Stärke 0). Wie beim Text liegt die innere Hälfte der Kontur unter der Linie – bei halbtransparenter Linienfarbe scheint sie durch. Projektformat bleibt v1 (Felder nur geschrieben, wenn gesetzt; ältere Programmversionen zeigen die Linie ohne Kontur; Golden-Dateien unverändert). Tests: Core 372, App 563 grün; manuelle Prüfung im laufenden Editor steht noch aus
