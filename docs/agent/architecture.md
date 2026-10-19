# GodSaveTheScreen – Agent Architecture
Verified against working tree on top of commit bd50364 (M10 uncommitted) on 2026-10-19
- STATUS: M0–M10 umgesetzt (Rest ist Soll-Architektur aus `docs/Umsetzungsplan.md`). Nach Commit Kopfzeile auf Commit-Hash setzen.
- Ist-Stand: `Core` (Settings inkl. `ShapeDefaultsMap`/`TextStyle`/`MarkerStyle`, `IAppPaths` inkl. Autosave-/Recent-Pfad, Geometrie inkl. JSON-Konverter, `PixelBuffer`, `ImageDocument` mit `Annotations` + `Undo` + `CropRect`/`ProjectPath`/`DocumentId`, `Imaging/CropGeometry`, `Annotations/` = Modell (Formen, Text, Marker, Strich, Unschärfe, Lupe) + `AnnotationSerializer`, `Undo/` inkl. `CropAction`, `Project/` = `ProjectSerializer`/`ProjectContent`/`IProjectMigration`, `IProjectService`, `RecentFilesService`, Service-Interfaces), `Capture`, `Rendering` (`AnnotationRenderer` partial inkl. `AnnotationOrdering`, `RegionEffectProcessor`/`EffectCache`, `TextLayout`, `LineLayout`, `DocumentExporter`, `ImageCodecService`, `ProjectService`), `App` (`Hosting/`, `Services/` inkl. `EditorService`/`AutoBackupService`/`JumpListService`, `Capture/`, `Editor/` = Kontext/`CropState`/Werkzeuge/Schriften, `Controls/`, `Views/`, `ViewModels/`, `Localization/`, `Resources/`)

## Overview
- Zweck: Screenshot-Tool (Tray + Hotkeys) mit nicht-destruktivem Annotations-Editor
- Stack: .NET 10, C#, WPF + WPF-UI, MVVM (CommunityToolkit.Mvvm), Generic Host, xUnit v3
- Layout: `GodSaveTheScreen.slnx`; `src/` 4 Projekte, `tests/` 2 Projekte; Plan in `docs/Umsetzungsplan.md`, Meilensteine in `docs/meilensteine/M*.md`

## Modules
| Pfad | Verantwortung | darf abhängen von |
|---|---|---|
| `src/GodSaveTheScreen.Core` | Dokumentmodell, Annotationen, Undo, Serialisierung, Settings, Service-Interfaces; `net10.0`, kein WPF/Win32 | – |
| `src/GodSaveTheScreen.Capture` | Monitore, Fenster, Bildschirmabbild (CsWin32) | `Core` |
| `src/GodSaveTheScreen.Rendering` | `AnnotationRenderer`, Textlayout, Export, Codecs (WPF) | `Core` |
| `src/GodSaveTheScreen.App` | Views, ViewModels, Werkzeuge, Overlay, Tray, Hotkeys, DI-Root | `Core`, `Capture`, `Rendering` |
| `tests/GodSaveTheScreen.Core.Tests` | Modell-/Undo-/Serializer-Tests | `Core` |
| `tests/GodSaveTheScreen.App.Tests` | ViewModels, Werkzeuge, Rendering (`[StaFact]`) | alle `src` |
- Verboten: `Core` -> WPF-Typen (`System.Windows.*`), `Capture` <-> `Rendering`, ViewModels -> Win32/P/Invoke direkt

## Entry Points
- `src/GodSaveTheScreen.App/App.xaml.cs:OnStartup` – Single-Instance (Mutex+Pipe), Serilog, Host-Aufbau, Theme, StartWindow/Tray; `ShutdownMode=OnExplicitShutdown`
- Datenfluss Aufnahme: Hotkey/Tray/Startfenster/Pipe -> `CaptureRequest` -> `CaptureCoordinator` (Fenster ausblenden -> Countdown -> `CaptureVirtualScreen` -> `ISelectionService` -> `PixelBuffer` -> `ImageDocument`) -> `IPostCaptureAction`-Kette `CopyToClipboardAction` -> `AutoSaveAction` -> `OpenEditorAction` (-> `IEditorService.Open`)
- Editor öffnen: `IEditorService` (`EditorService`) erzeugt je Fenster `EditorContext` + `ToolRegistry.Create` + `EditorViewModel` + `EditorWindow` per `ActivatorUtilities`; Quellen: Aufnahme, Datei (`OpenFilesAsync`, auch Kommandozeile/Drop/Dialog), Zwischenablage
- Datenfluss Bearbeiten: Maus -> `EditorCanvas` (Bildpixel) -> `ToolBase.OnPointer*` -> `IUndoableAction` -> `UndoManager.Execute` -> `Annotation` (ObservableObject) -> `AnnotationElement.OnRender` -> `AnnotationRenderer`
- Freihand: `FreehandToolBase` (Stift/Textmarker) sammelt `StrokePoint`s (mit Stiftdruck), rastet am Lineal ein (`RulerGeometry.Snap`), reduziert per RDP und fügt EINEN `StrokeAnnotation` ein; `EraserTool` markiert (`Interaction.Marked`) und entfernt beim Loslassen in einer Undo-Aktion
- Lupe: `MagnifierAnnotation` rendert Basisbild + Objekte unter sich (`RenderContext.Annotations`, ohne andere Lupen) im Clip; `AnnotationLayer` zeichnet Lupen bei jeder Objektänderung neu; Quelle/Ziel getrennt verschiebbar (`SelectTool`, `MovePart`)
- Textbearbeitung: Werkzeug/Doppelklick -> `EditorContext.BeginTextEdit` -> `TextBox` im `Overlay` von `EditorCanvas` -> `CommitTextEdit` (eine Undo-Aktion)
- Datenfluss Export: `DocumentExporter.Render` -> `AnnotationRenderer` (gleicher Pfad wie Anzeige) -> `PixelBuffer` -> `IImageCodecService.SaveAsync` / `IClipboardService`; ohne sichtbare Objekte wird `BaseImage` selbst geliefert
- Eigenschaften: `SelectionPropertiesViewModel` (Formen + Deckkraft), `TextPropertiesViewModel`, `MarkerPropertiesViewModel`; ohne Auswahl bearbeiten sie die Standardwerte des aktiven Werkzeugs (`IToolDefaultsService`)
- Hintergrundbetrieb: `TrayService` + `HotkeyService` (`IHostedService`); `IAppShell` öffnet Start-/Einstellungsfenster und beendet die App
- Zuschneiden (M9): `CropTool` (Werkzeug `C`) arbeitet auf `EditorContext.Crop` (`CropState`: Arbeitsrahmen, Seitenverhältnis); erst `CropTool.Commit` erzeugt eine `CropAction` (setzt `ImageDocument.CropRect`). `EditorViewModel.ViewRect` = Zuschnitt (im Zuschnittmodus ganzes Bild); `EditorCanvas`: `Frame` (Größe = ViewRect, clippt, Zoom per `LayoutTransform`) enthält `Surface` (volle Bildgröße, `TranslateTransform` = −ViewRect.TopLeft). `CropPanel`/`CropViewModel` ersetzen im Modus das Eigenschaftenpanel; Undo bricht den Modus ab
- Projektdatei (M10): `ProjectSerializer` (ZIP: `manifest.json`, `document.json`, `base.png`, `preview.png`) liest/schreibt `ProjectContent` (PNG als Bytes, kein WPF im Core); `ProjectService` (Rendering) setzt Dokument ⇄ Inhalt um. Speichern (`EditorViewModel.TrySaveAsync`): bekanntes `ProjectPath` → Projekt; sonst bekannte Bilddatei → Bild; sonst Dialog (`PickDocumentSavePath`, Projekt zuerst); „Exportieren…“ schreibt nur Bilder (`SourcePath` = Exportziel) und lässt den Projektzustand unberührt
- Sicherung/Wiederherstellung: `AutoBackupService` (60 s + 10 s nach Änderung) schreibt `{DocumentId}.gsts` nach `IAppPaths.AutosaveDirectory` (Schnappschuss auf UI-Thread, Schreiben im Hintergrund), löscht beim Speichern/Schließen; `RecoverAsync` beim Start. `RecentFilesService` (`recent.json`, max. 15) speist Startfenster, Tray-Untermenü, Editor-Menü „Öffnen ▾“ und `JumpListService`

## Conventions
- Zuschnitt ist nicht-destruktiv: `ImageDocument.CropRect` (null = voll, immer im Basisbild); Annotationen bleiben in Originalkoordinaten; Export (`DocumentExporter`) rendert `EffectiveBounds` mit `TranslateTransform(-x,-y)`; Statusleiste/Pipette rechnen relativ zum Zuschnitt
- Koordinaten: Annotationen immer in Bildpixeln des Basisbilds; Capture: virtueller Desktop in physischen Pixeln; Umrechnung im Editor nur über `EditorCanvas.ToImagePoint` (Surface-Grid hat Bildpixelgröße, Zoom per `LayoutTransform`)
- `EditorContext.Scale` = DIPs je Bildpixel; Griffe/Toleranzen über `ToImageUnits`/`HitTolerance` zoom-unabhängig
- Annotation ändern nur via `UndoManager` (Panel, Werkzeuge, Ebenenliste); Ziehen = vorher-/nachher-Klone + `ReplaceGeometryAction`; Eigenschaften = `PropertyChangeAction` (merged im 1,5-s-Fenster) in Transaktion
- JSON-Formate (Projekt, Objekt-Zwischenablage): reflektionsbasiertes `System.Text.Json` mit gemeinsamen Optionen in `ProjectSerializer`; Geometrie kompakt per `[JsonConverter]` auf `PointD`/`RectD`/`PixelRect`/`StrokePoint` (Arrays), `ArgbColor` als `#AARRGGBB`; Diskriminator-Eigenschaft heißt `type`; defekte/unbekannte Objekte werden beim Lesen übersprungen (Warnung), Formatänderungen nur mit `formatVersion`-Anhebung + `IProjectMigration` + neuer Golden-Datei (`tests/GodSaveTheScreen.Core.Tests/TestData/golden-v1.gsts`, neu erzeugen mit Umgebungsvariable `GSTS_GOLDEN_OUT`)
- Neue Annotation = Modellklasse (Core, `Clone` läuft über `CopyFrom` per Reflection: nur öffentliche Properties mit Setter, `Id` ausgenommen) + Zweig in `AnnotationRenderer.Render` + `[JsonDerivedType]` mit kurzem stabilem Diskriminator in `Annotation` + Roundtrip-Test
- Berechnete Core-Properties (`Bounds`, `HasFill` …) mit `[JsonIgnore]`; Listen im Modell als `ImmutableArray`, nie mutieren
- Core-Farben/Geometrie: eigene Structs (`ArgbColor`, `PointD`, `RectD`, `PixelRect`), nie WPF-Typen; Schriftwerte als string/int
- Text-Core braucht Messung: `TextAnnotation.MeasureProvider` setzt `TextLayout.Install()` (in `AddRendering`, `DocumentExporter`, `AnnotationRenderer`)
- Pixel: `PixelBuffer` BGRA32, nicht vormultipliziert
- Neues Werkzeug = `ToolBase`-Ableitung (`Id`, `NameKey`, `Symbol`/`IconPath`, `ShortcutKey`) + Eintrag in `ToolRegistry.Create` + Strings `Tool_*`; `UsesShapeStyle` nur bei Formwerkzeugen (`ShapeStyle`), Text/Marker haben `TextStyle`/`MarkerStyle`
- Zeichenreihenfolge nur über `AnnotationOrdering.RenderOrder` (Textmarker-Striche zuerst, wenn `ImageDocument.HighlightersBehind`); Anzeige (`AnnotationLayer`) und Export (`DocumentExporter`) nutzen sie beide
- `StrokeAnnotation` erbt von `ShapeAnnotation` (Farbe/Dicke/Deckkraft über das Formpanel); Glättung, Druck, Unschärfe- und Lupenoptionen im `ToolOptionsViewModel`, das ohne Auswahl die Fenster-Vorlagen (`EditorContext.Freehand`/`BlurTemplate`/`MagnifierTemplate`) bearbeitet (nicht persistiert)
- Objekte, die das Basisbild lesen (`BlurAnnotation`, `MagnifierAnnotation`), brauchen `RenderContext.BasePixels`/`BaseImage`/`Annotations` – ohne diese zeichnen sie nichts (z. B. `RenderObjects` für die Objekt-Zwischenablage)
- Tastenkürzel zentral in `EditorWindow.OnPreviewKeyDown` (ignoriert Eingabefelder); Werkzeuge bekommen Tasten über `ToolBase.OnKeyDown`
- MVVM: `[ObservableProperty]`/`[RelayCommand]`; Fenster via DI (`IWindowFactory`, Editor via `IEditorService`)
- UI-Texte nur über `Strings.resx` + `Strings.de.resx`, XAML `{l:Loc Key}`; beide Dateien im selben Commit
- `Strings.Designer.cs` von Hand/Skript synchron zu `Strings.resx`; Schlüssel-Test in `App.Tests` prüft DE/EN-Gleichheit
- Neue Einstellung = Feld in `AppSettings` (Default!) + Eigenschaft im ViewModel + Strings DE/EN; Dictionary/Listen in `AppSettings` nur als `ShapeDefaultsMap`/`StringList` (Wertgleichheit für Record-Vergleich)
- Paketversionen nur in `Directory.Packages.props`; Analyzer streng (`TreatWarningsAsErrors`); bewusst aus: CA1304/1305/1716/1863/1873/1707/1848 (siehe `.editorconfig`)
- Enum-Namen in Core mit Suffix `Setting`/`Kind` bei WPF-Kollision (`ThemeSetting`, `FontStyleKind`); in Dateien mit `using System.Windows.Input` Alias für `CaptureMode`
- Alle Fenster über `IWindowFactory` bzw. `EditorService` erzeugen (Registrierung beim `IWindowExclusionService`)
- Neue Nachaufnahme-Schritte = `IPostCaptureAction` in `AppServiceCollectionExtensions` in fester Reihenfolge
- Tests: `<Klasse>Tests.cs`, `Methode_Fall_Erwartung`; WPF-Objekte nur in `[StaFact]`; Editor-Tests über `EditorHarness` (`M4Tests.cs`)
- Logging: `ILogger<T>`; keine `Debug.WriteLine`/`Console`

## Decisions
- Freeze-Frame (Snapshot vor Overlay); ein Overlay-Fenster je Monitor (Misch-DPI)
- Eigenes Strichmodell statt `InkCanvas`; Modell-Objekte direkt `ObservableObject` (keine Wrapper-VMs)
- Anzeige: je Objekt ein bildgroßes `AnnotationElement` (Clip auf Bild = wie Export), `AnnotationLayer` baut bei Listenänderung neu auf
- Interaktionsebene (`InteractionLayer`): Vorschau, Auswahl, Griffe, Aufziehrahmen, Radierer-Markierung, Pinselvorschau und Lineal (`RulerViewModel` in `EditorContext.Ruler`, nur UI-Zustand); nie exportiert
- Unschärfe selbst auf `PixelBuffer` statt `BlurEffect` (M8): Quelle immer das Basisbild (`RenderContext.BasePixels`), Ergebnis gecacht (`EffectCache.Shared`, Schlüssel inkl. `BaseImageIdentity`), Form/weicher Rand im Alphakanal; Speichern nur WPF-Encoder, Laden über WIC
- Objekt-Zwischenablage: Format `GodSaveTheScreen.Annotations` (JSON) + PNG-Fallback
- Shouldly statt FluentAssertions (Lizenz); Farbwahl `PixiEditor.ColorPicker` (`StandardColorPicker` im `ColorButton`-Popup)
- Projektformat ZIP+JSON mit `formatVersion` (M10); Abweichung vom Plan: kein Source-Generator-Kontext (Annotationen sind `ObservableObject`s mit privaten Settern → Reflection), Diskriminator `type` statt `$type` (Clipboard-Format bleibt kompatibel)

## Pitfalls
- `Druck`-Hotkey kann von Windows 11 belegt sein -> Fehler anzeigen, nicht crashen
- CsWin32 in `Capture` und `App` (je eigene `NativeMethods.txt`); GDI-Handles in `finally` freigeben
- Overlay: nach `SourceInitialized` per `SetWindowPos` positionieren; bei `DpiChanged` Bounds neu setzen
- Eigene Fenster: `WDA_EXCLUDEFROMCAPTURE` (Build 19041+), sonst im Snapshot; `CountdownWindow` nie aktivieren
- Tray-Symbol braucht `System.Drawing.Icon`; Hotkeys beim `HotkeyRecorder` pausieren
- WPF-`Application` ist threadgebunden: alle Smoke-Tests mit echten Fenstern in EINEM `[StaFact]` (`WindowSmokeTests`)
- Tests/Builds nicht aus sehr langen Pfaden: Prozess stürzt beim Anzeigen eines `FluentWindow` ab (0xC0000409); kurzen `-p:ArtifactsPath=C:/Temp/...` nutzen; Resx-Tests brauchen Ausgabe unter dem Repo
- Läuft die App, sperrt sie `bin\`-DLLs: Build/Test mit abweichendem `ArtifactsPath`
- `RenderTargetBitmap` liefert `Pbgra32`; Text im Export `PixelsPerDip=1`, `TextRenderingMode=Grayscale`
- Symbole `Triangle24`/`Hexagon24` der WPF-UI-Symbolschrift rendern kaputt -> `ToolBase.IconPath`
- WPF-UI `TextBox` im Editor-`Overlay` deshalb mit `Style = null` (sonst Padding/Hintergrund des Themes)
- Mausereignisse aus dem `Overlay` (Textfeld) in `EditorCanvas` ignorieren (`IsFromOverlay`)
- Zwischenablage: `COMException` bei Sperre -> Retry; Bild immer `CF_DIB` + `"PNG"`
- WPF nicht trimmbar -> kein `PublishTrimmed`
- Lupe ohne Rekursion: sieht nur Objekte unter sich und keine anderen Lupen; ändert sich irgendein Objekt, zeichnet `AnnotationLayer` alle Lupen neu
- Lineal-Ziehen und Freihand-Einrasten konkurrieren um dieselbe Fläche: Freihandwerkzeuge starten nahe der Kante (24 DIP) einen Strich, sonst verschiebt `EditorCanvas` das Lineal
- JSON-Diskriminatoren nie ändern (bricht `.gsts`-Dateien)
- Projekt-Leser: nur feste Eintragsnamen, harte Größengrenzen (`ProjectSerializer.Max*`), PNG-Kopf wird vor dem Dekodieren geprüft (ZIP-/PNG-Bomben)
- `AskSaveChanges` mit `offerProject` und `PickDocumentSavePath` sind neue `IDialogService`-Mitglieder: Test-Mocks nutzen `ReturnsForAnyArgs`
- Windows-Sprungliste: „Zuletzt verwendet“ als eigene Kategorie aus `JumpTask`s (Dateizuordnung nicht nötig)

## Commands
- Build: `dotnet build GodSaveTheScreen.slnx`
- Test: `dotnet test` (Solution-Root; `global.json` aktiviert Microsoft.Testing.Platform, xunit v3)
- Einzeltest: `dotnet test --project tests/GodSaveTheScreen.Core.Tests --filter-class "*UndoManagerTests"`
- Start: `dotnet run --project src/GodSaveTheScreen.App`
- Release: `dotnet publish src/GodSaveTheScreen.App -p:PublishProfile=win-x64`
