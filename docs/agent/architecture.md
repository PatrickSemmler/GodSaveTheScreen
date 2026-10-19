# GodSaveTheScreen – Agent Architecture
Verified against working tree after M0+M1 (uncommitted) on 2026-10-19
- STATUS: M0 + M1 umgesetzt (Rest ist Soll-Architektur aus `docs/Umsetzungsplan.md`). Nach Commit Kopfzeile auf Commit-Hash setzen.
- Ist-Stand M0/M1: `Core` (Settings, Geometrie, `PixelBuffer`, `ImageDocument`, `IDialogService`/`IClipboardService`), `Capture` (`MonitorService`, `ScreenCaptureService`, `ScreenSnapshot`), `Rendering` (`PixelBufferExtensions`), `App` (`Hosting/`, `Services/`, `Capture/` = Koordinator + Overlay, `Views/StartWindow`+`EditorWindow` (vorläufig), `ViewModels/`, `Localization/`, `Resources/`)

## Overview
- Zweck: Screenshot-Tool (Tray + Hotkeys) mit nicht-destruktivem Annotations-Editor
- Stack: .NET 10, C#, WPF + WPF-UI, MVVM (CommunityToolkit.Mvvm), Generic Host, xUnit v3
- Layout: `GodSaveTheScreen.slnx`; `src/` 4 Projekte, `tests/` 2 Projekte; Plan in `docs/Umsetzungsplan.md`, Meilensteine in `docs/meilensteine/M*.md`

## Modules
| Pfad | Verantwortung | darf abhängen von |
|---|---|---|
| `src/GodSaveTheScreen.Core` | Dokumentmodell, Annotationen, Undo, Serialisierung, Settings, Service-Interfaces; `net10.0`, kein WPF/Win32 | – |
| `src/GodSaveTheScreen.Capture` | Monitore, Fenster, Bildschirmabbild (CsWin32) | `Core` |
| `src/GodSaveTheScreen.Rendering` | `AnnotationRenderer`, Effekte, Export, Codecs (WPF) | `Core` |
| `src/GodSaveTheScreen.App` | Views, ViewModels, Werkzeuge, Overlay, Tray, Hotkeys, DI-Root | `Core`, `Capture`, `Rendering` |
| `tests/GodSaveTheScreen.Core.Tests` | Modell-/Undo-/Serializer-Tests | `Core` |
| `tests/GodSaveTheScreen.App.Tests` | ViewModels, Werkzeuge, Rendering (`[StaFact]`) | alle `src` |
- Verboten: `Core` -> WPF-Typen (`System.Windows.*`), `Capture` <-> `Rendering`, ViewModels -> Win32/P/Invoke direkt

## Entry Points
- `src/GodSaveTheScreen.App/App.xaml.cs:OnStartup` – Single-Instance (Mutex+Pipe), Serilog, Host-Aufbau, Theme, StartWindow (Tray ab M3); `ShutdownMode=OnExplicitShutdown`
- Datenfluss Aufnahme: Hotkey/Tray/Pipe -> `CaptureCoordinator` -> `IScreenCaptureService.CaptureVirtualScreen` -> `SelectionOverlayWindow` je Monitor -> `PixelBuffer` -> `ImageDocument` -> Nachaufnahme-Aktionen -> `EditorWindow`
- Datenfluss Bearbeiten: `ToolBase.OnPointer*` -> `IUndoableAction` -> `UndoManager.Execute` -> `Annotation` (ObservableObject) -> `AnnotationElement.OnRender` -> `AnnotationRenderer`
- Datenfluss Export: `DocumentExporter.Render` -> `AnnotationRenderer` (gleicher Pfad wie Anzeige) -> `RenderTargetBitmap` -> `ImageCodecService` / `IClipboardService`
- Projektdatei: `ProjectSerializer` (ZIP: `manifest.json`, `document.json`, `base.png`, `preview.png`)

## Conventions
- Koordinaten: Annotationen + `CropRect` immer in Bildpixeln des Basisbilds; Umrechnung nur in `CoordinateMapper` (Overlay) bzw. `EditorCanvas.ToImagePoint` (Editor)
- Capture-Koordinaten: virtueller Desktop in physischen Pixeln (negativ möglich)
- Annotation ändern nur via `UndoManager` (auch Eigenschaftenpanel); Ziehen = `BeginTransaction`
- Ein Renderpfad: neue Annotation = Modellklasse (Core) + Zweig in `AnnotationRenderer` + `[JsonDerivedType]` mit kurzem stabilem Diskriminator + Roundtrip-Test
- Core-Farben/Geometrie: eigene Structs (`ArgbColor`, `PointD`, `RectD`, `PixelRect`), nie WPF-Typen
- Pixel: `PixelBuffer` BGRA32, nicht vormultipliziert
- MVVM: `[ObservableProperty]`/`[RelayCommand]`; Fenster via DI (`IWindowFactory`); je Editorfenster eigener `IServiceScope`
- UI-Texte nur über `Strings.resx` + `Strings.de.resx`, XAML `{l:Loc Key}`; beide Dateien im selben Commit
- Paketversionen nur in `Directory.Packages.props`
- `Strings.Designer.cs` wird von Hand/Skript synchron zu `Strings.resx` gehalten (kein VS-Generator im CLI-Build); Schlüssel-Test in `App.Tests` prüft DE/EN-Gleichheit
- Analyzer streng (`TreatWarningsAsErrors`, `latest-recommended`); bewusst aus: CA1304/1305/1863/1873/1707/1848 (siehe `.editorconfig`)
- Enum-Namen in Core mit Suffix `Setting` (`ThemeSetting`, `LogLevelSetting`) wegen WPF-Namenskollisionen
- Bis M3 (Tray) endet die App, wenn das letzte Fenster schließt (`WindowFactory.OnWindowClosed`)
- Tests: `<Klasse>Tests.cs`, Methoden `Methode_Fall_Erwartung`; WPF-Objekte nur in `[StaFact]`
- Logging: `ILogger<T>`; keine `Debug.WriteLine`/`Console`

## Decisions
- Freeze-Frame (Snapshot vor Overlay) – Menüs/Tooltips aufnehmbar, Overlay ohne Transparenz; nicht `Windows.Graphics.Capture` (WinRT-TFM)
- Ein Overlay-Fenster je Monitor – WPF-Fenster über Monitore mit Misch-DPI skaliert falsch
- Eigenes Strichmodell statt `InkCanvas` – einheitliches Objektmodell/Undo/Serialisierung/Lineal-Snapping
- Unschärfe selbst auf `PixelBuffer` statt WPF `BlurEffect` – exportstabil, Verpixeln möglich; Quelle immer Basisbild
- Modell-Objekte direkt `ObservableObject` – keine Wrapper-VM-Hierarchie
- Speichern nur WPF-Encoder; Laden über WIC (installierte Codecs inkl. WebP/HEIC)
- Projektformat ZIP+JSON mit `formatVersion` und Migrationen auf `JsonNode`
- Shouldly statt FluentAssertions (Lizenz)
- Lineal = UI-Zustand (`RulerViewModel`), nie im Dokument/Export

## Pitfalls
- `Druck`-Hotkey kann von Windows 11 (Snipping Tool) belegt sein -> `RegisterHotKey` schlägt fehl; Fehler anzeigen, nicht crashen
- CsWin32 gibt es in `Capture` und `App` (je eigene `NativeMethods.txt`, internal); `Capture` nutzt Delegate-/Pointer-Overloads ohne Marshaling
- Overlay: bei `DpiChanged` Bounds erneut per `SetWindowPos` setzen (WPF skaliert sonst auf Vorschlagsgröße)
- Overlay erst nach `SourceInitialized` per `SetWindowPos` (physische Pixel) positionieren
- Eigene Fenster: `SetWindowDisplayAffinity(WDA_EXCLUDEFROMCAPTURE)`; sonst landen sie im Snapshot
- Zwischenablage: `COMException` bei Sperre -> Retry; immer `CF_DIB` + `"PNG"` setzen (Alpha)
- GDI-Handles (`HDC`, `HBITMAP`) in `finally` freigeben
- `RenderTargetBitmap` liefert `Pbgra32` (vormultipliziert) -> beim Konvertieren in `PixelBuffer` beachten
- Text im Export: `PixelsPerDip=1`, `TextRenderingMode=Grayscale` (kein ClearType-Saum)
- WPF nicht trimmbar -> kein `PublishTrimmed`
- JSON-Diskriminatoren nie ändern (bricht `.gsts`-Dateien); Golden-File-Test in `Core.Tests`

## Commands
- Build: `dotnet build GodSaveTheScreen.slnx`
- Test: `dotnet test` (Solution-Root; `global.json` aktiviert Microsoft.Testing.Platform, xunit v3)
- Einzeltest: `dotnet test --project tests/GodSaveTheScreen.Core.Tests --filter-class "*UndoManagerTests"`
- Start: `dotnet run --project src/GodSaveTheScreen.App`
- Release: `dotnet publish src/GodSaveTheScreen.App -p:PublishProfile=win-x64`
