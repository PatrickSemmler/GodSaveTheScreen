# GodSaveTheScreen – Umsetzungsplan

Stand: 2026-10-19 · Status: Planung (noch kein Code)

## 1. Zielbild

GodSaveTheScreen ist ein Screenshot- und Bildannotationswerkzeug für Windows. Es läuft im Infobereich (Tray), wird per globalem Hotkey ausgelöst, nimmt Bildschirmbereiche auf und öffnet das Ergebnis in einem Editor, in dem nicht-destruktive Objekte (Formen, Text, Freihand, Unschärfe, Marker, Lupe …) über dem Bild platziert werden. Das Ergebnis wird gespeichert, in die Zwischenablage kopiert oder als Projekt mit weiterhin bearbeitbaren Objekten abgelegt.

### 1.1 Funktionsumfang (Anforderungen)

| Bereich | Anforderung | Herkunft | Meilenstein |
|---|---|---|---|
| Aufnahme | Auswahl-Rechteck | Vorgabe | M1 |
| Aufnahme | Ganzes Fenster | Vorgabe | M2 |
| Aufnahme | Vollbild (aktueller Monitor / alle Monitore) | Klärung | M1 |
| Aufnahme | Freiform (Lasso) | Klärung | M2 |
| Aufnahme | Frei einstellbarer Timer (Verzögerung) | Vorgabe | M2 |
| Bedienung | Tray-Icon, globale Hotkeys, Startfenster | Klärung | M1 (Startfenster), M3 (Tray/Hotkeys) |
| Nach Aufnahme | Editor öffnen; optional Auto-Zwischenablage, optional Auto-Speichern | Klärung | M1, M3 |
| Editor | Anzeige, Zoom/Pan, Speichern, Zwischenablage | Vorgabe | M1 (einfach), M4 (vollständig) |
| Editor | Vorhandene Bilder laden | Vorgabe | M4 |
| Editor | Speichern in allen WPF-Formaten (PNG, JPEG, BMP, GIF, TIFF, JPEG XR) | Vorgabe + Klärung | M4 |
| Objekte | Rechteck, Ellipse/Kreis, Linie, Pfeil, Dreieck (+ Polygon) mit Linien-/Füllfarbe und Transparenz | Vorgabe | M5 |
| Objekte | Alle Objekte bleiben bis zum Schließen bearbeitbar | Vorgabe | M5 ff. |
| Objekte | Undo/Redo, Z-Reihenfolge (Ebenen) | Klärung | M5 |
| Text | Schriftart, Schnitt, Größe, Linien- und Füllfarbe | Vorgabe | M6 |
| Marker | Nummerierte Schritt-Marker | Klärung | M6 |
| Freihand | Stift (Dicke/Farbe), Textmarker (Transparenz), virtuelles Lineal | Vorgabe | M7 |
| Maskieren | Bereiche unscharf maskieren (Weichzeichnen/Verpixeln) | Vorgabe | M8 |
| Lupe | Vergrößerungsobjekt | Klärung | M8 |
| Zuschneiden | Beschneiden wie beim Snipping Tool | Vorgabe | M9 |
| Projekt | Eigenes Projektformat `.gsts` (Objekte bleiben bearbeitbar) | Klärung | M10 |
| Sprache | Deutsch + Englisch (.resx) | Klärung | M0 (Infrastruktur), M11 (Vollständigkeit) |
| Qualität | Unit-Tests (xUnit) | Klärung | ab M0 |
| Auslieferung | Portable Single-File-EXE als ZIP | Klärung | M11 |

### 1.2 Bewusst nicht im Umfang (v1)

- Videoaufnahme / GIF-Aufnahme
- Moderne Speicherformate außerhalb von WPF (WebP, AVIF, HEIC) – **Laden** funktioniert trotzdem, sofern der passende WIC-Codec in Windows installiert ist (z. B. „WebP-Bilderweiterungen“).
- Installer, Auto-Update, Store-Paket
- Cloud-Upload, OCR, Teilen-Dialoge
- Rotation einzelner Objekte (Datenmodell ist vorbereitet, siehe M5 „Ausblick“)

## 2. Technologie-Stack

| Thema | Entscheidung |
|---|---|
| Runtime | .NET 10 (LTS), `net10.0-windows` für UI-/Win32-Projekte, `net10.0` für den Kern |
| UI | WPF, MVVM, Fluent-Design über **WPF-UI** (lepoco) mit Hell/Dunkel/System-Theme |
| Plattform | Windows 10 21H2+ / Windows 11, x64 (arm64 optional als zusätzliches Publish-Profil) |
| DPI | Per-Monitor-V2 (`app.manifest`), alle Bildkoordinaten in physischen Pixeln |
| Sprache | C# 14, `Nullable` enable, `ImplicitUsings` enable, `TreatWarningsAsErrors` |

### 2.1 NuGet-Pakete

Versionsangaben sind die zum Planungszeitpunkt bekannten Hauptversionen. Bei M0 jeweils die aktuelle stabile Version einsetzen und zentral in `Directory.Packages.props` pflegen (Central Package Management).

| Paket | Zweck | Projekt |
|---|---|---|
| `CommunityToolkit.Mvvm` (8.4+) | `ObservableObject`, `[ObservableProperty]`, `[RelayCommand]`, `WeakReferenceMessenger` (Source Generator) | Core, App |
| `Microsoft.Extensions.Hosting` (10.x) | Generic Host: DI, Konfiguration, Logging, Lebenszyklus | App |
| `WPF-UI` (4.x) | Fluent-Controls, `FluentWindow`, `NavigationView`, Snackbar, Theme-Handling, Fluent-Symbol-Icons | App |
| `H.NotifyIcon.Wpf` (2.x) | Tray-Icon mit Kontextmenü, Balloon/Toast | App |
| `NHotkey.Wpf` (3.x) | Globale Hotkeys (`RegisterHotKey`-Wrapper) | App |
| `Microsoft.Windows.CsWin32` (0.3.x) | Source-generierte, typsichere P/Invokes (GDI, DWM, User32) über `NativeMethods.txt` | Capture, App |
| `PixiEditor.ColorPicker` (3.x) | WPF-Farbwähler mit Alpha-Kanal, HSV/RGB/Hex, Farbpalette | App |
| `Serilog.Extensions.Hosting` + `Serilog.Sinks.File` | Dateilogging (rollierend) in `%LOCALAPPDATA%\GodSaveTheScreen\logs` | App |
| `xunit.v3` + `xunit.runner.visualstudio` + `Microsoft.NET.Test.Sdk` | Unit-Tests | Tests |
| `Xunit.StaFact` | `[StaFact]` für Tests, die WPF-Objekte (Rendering) benötigen | Tests |
| `NSubstitute` | Mocks für Service-Interfaces | Tests |
| `Shouldly` | Lesbare Assertions (Lizenzfrei; bewusst nicht FluentAssertions 8 wegen kommerzieller Lizenz) | Tests |

Bewusst **nicht** verwendet:
- `InkCanvas` – eigenes Strichmodell, damit Freihand-Striche Objekte wie alle anderen sind (Auswahl, Undo, Serialisierung, Lineal-Snapping).
- `Windows.Graphics.Capture` – erfordert WinRT-TFM; der „Freeze-Frame“-Ansatz (siehe 3.3) deckt die Anforderungen ab. Für verdeckte Fenster wird `PrintWindow(PW_RENDERFULLCONTENT)` genutzt.
- `Magick.NET` / `SkiaSharp` – Speicherformate beschränken sich auf WPF-Encoder; Weichzeichnen wird selbst implementiert (kleiner Footprint).

## 3. Architektur

### 3.1 Solution-Struktur

```
GodSaveTheScreen/
├─ GodSaveTheScreen.slnx
├─ Directory.Build.props          # gemeinsame Compiler-Einstellungen
├─ Directory.Packages.props       # zentrale Paketversionen
├─ src/
│  ├─ GodSaveTheScreen.Core/      # net10.0 – Dokumentmodell, Annotationen, Undo, Serialisierung, Einstellungen, Service-Interfaces
│  ├─ GodSaveTheScreen.Capture/   # net10.0-windows – Monitore, Fenster, Bildschirmaufnahme (Win32 via CsWin32)
│  ├─ GodSaveTheScreen.Rendering/ # net10.0-windows (UseWPF) – Zeichnen der Annotationen, Effekte, Export/Encoder
│  └─ GodSaveTheScreen.App/       # WinExe (UseWPF) – Views, ViewModels, Tray, Hotkeys, Overlay, DI-Root
├─ tests/
│  ├─ GodSaveTheScreen.Core.Tests/
│  └─ GodSaveTheScreen.App.Tests/ # ViewModels + Rendering (STA)
└─ docs/
   ├─ Umsetzungsplan.md           # dieses Dokument
   ├─ meilensteine/               # je Meilenstein ein Dokument
   └─ agent/architecture.md       # kompakte Architekturdoku für KI-Agenten
```

Abhängigkeitsrichtung: `App → Rendering → Core`, `App → Capture → Core`. `Core` kennt weder WPF noch Win32. `Capture` und `Rendering` kennen sich nicht.

### 3.2 Schichten und Verantwortungen

| Projekt | Inhalt | Wichtige Typen (geplant) |
|---|---|---|
| Core | Geometrie-Primitive (`PointD`, `RectD`, `SizeD`, `ArgbColor`), `PixelBuffer` (BGRA32), `ImageDocument`, Annotation-Hierarchie, `UndoManager`, Projekt-Serialisierung, `AppSettings`, Dateinamensmuster, Interfaces (`IClipboardService`, `IDialogService`, `ISettingsService`, `IImageCodecService` …) | `ImageDocument`, `Annotation`, `IUndoableAction`, `UndoManager`, `ProjectSerializer` |
| Capture | Monitor-Enumeration (physische Pixel, DPI), Bildschirmabbild via GDI `BitBlt` (`CAPTUREBLT`), Fenster-Enumeration in Z-Order mit DWM-Rahmen, `PrintWindow`, Ausschluss eigener Fenster | `IScreenCaptureService`, `MonitorInfo`, `WindowInfo`, `ScreenSnapshot` |
| Rendering | `AnnotationRenderer` (eine Zeichenroutine für Anzeige **und** Export), Effekte (Weichzeichnen, Verpixeln) auf `PixelBuffer`, `DocumentExporter` (Flatten → `BitmapSource`), Encoder-Optionen, Konvertierung `PixelBuffer ↔ BitmapSource` | `AnnotationRenderer`, `RegionEffectProcessor`, `DocumentExporter`, `ImageCodecService` |
| App | Generic Host, Views (XAML), ViewModels, Overlay-Fenster für Auswahl, Editor-Fenster mit Canvas/Werkzeugen, Tray, Hotkeys, Einstellungen, Lokalisierung, Implementierung der Service-Interfaces | `App`, `TrayService`, `HotkeyService`, `CaptureCoordinator`, `EditorViewModel`, `ToolBase`-Ableitungen |

### 3.3 Zentrale Abläufe

**Aufnahme (Freeze-Frame-Prinzip, wie Snipping Tool):**
1. Auslöser (Hotkey, Tray, Startfenster, zweiter Programmstart) → `CaptureCoordinator.StartAsync(mode, delay)`.
2. Optional: Timer-Countdown (eigenes kleines Countdown-Fenster, per `SetWindowDisplayAffinity(WDA_EXCLUDEFROMCAPTURE)` von der Aufnahme ausgeschlossen; `Esc` bricht ab).
3. `IScreenCaptureService.CaptureVirtualScreen()` friert den gesamten virtuellen Desktop als `ScreenSnapshot` ein (physische Pixel, inkl. Fensterliste in Z-Order).
4. Je Monitor ein randloses Overlay-Fenster zeigt den eingefrorenen Ausschnitt abgedunkelt; der Nutzer wählt Rechteck / Fenster / Freiform / Vollbild (Lupe + Fadenkreuz + Größenanzeige).
5. Ergebnis wird aus dem Snapshot ausgeschnitten → `PixelBuffer` → neues `ImageDocument`.
6. Nachaufnahme-Aktionen gemäß Einstellungen: Editor öffnen (immer), Zwischenablage (optional), Auto-Speichern (optional).

**Bearbeiten:**
- Werkzeug (`ToolBase`) verarbeitet Mausereignisse auf dem Canvas → erzeugt/ändert Annotationen ausschließlich über `IUndoableAction`s im `UndoManager`.
- Canvas zeigt Basisbild + `ItemsControl` mit je einem `AnnotationElement` (eigenes `FrameworkElement`, `OnRender` → `AnnotationRenderer`).
- Auswahl-Adorner (Griffe) und Lineal liegen in einer eigenen Overlay-Ebene und werden nie exportiert.

**Export:** `DocumentExporter.Render(document)` → `DrawingVisual` (gleicher `AnnotationRenderer`) → `RenderTargetBitmap` in Originalauflösung → Zuschnitt anwenden → Encoder / Zwischenablage.

### 3.4 Dokumentmodell (Kern)

```
ImageDocument
 ├─ BaseImage : PixelBuffer (BGRA32, unveränderlich nach Erstellung)
 ├─ CropRect  : RectD?           (nicht-destruktiv, M9)
 ├─ Annotations : ObservableCollection<Annotation>  (Reihenfolge = Z-Order)
 ├─ SourcePath / ProjectPath / IsDirty
 └─ UndoManager

Annotation (abstrakt, ObservableObject)
 ├─ Id : Guid, IsLocked, Opacity
 ├─ ShapeAnnotation (Stroke, Fill, StrokeThickness, DashStyle)
 │   ├─ RectangleAnnotation (CornerRadius)
 │   ├─ EllipseAnnotation
 │   ├─ TriangleAnnotation (Variante: gleichschenklig/rechtwinklig)
 │   ├─ PolygonAnnotation (Punkte)
 │   └─ LineAnnotation (Start, End, StartCap, EndCap) – Pfeil = Linie mit Pfeilspitze(n)
 ├─ TextAnnotation (Text, FontFamily, Weight, Style, Stretch, Size, Fill, Stroke, StrokeThickness, Hintergrund)
 ├─ StrokeAnnotation (Punkte, Dicke, Farbe, IsHighlighter)
 ├─ MarkerAnnotation (Nummer, Stil, Größe)
 ├─ BlurAnnotation (Rect, Modus: Weichzeichnen | Verpixeln, Stärke)
 └─ MagnifierAnnotation (Quellbereich, Zielmittelpunkt, Zoom, Verbindungslinie)
```

Koordinaten aller Annotationen sind **Bildpixel des Basisbilds** (unabhängig von Zoom, Monitor-DPI und Zuschnitt).

### 3.5 Querschnittsthemen

| Thema | Lösung |
|---|---|
| DI / Lebenszyklus | Generic Host in `App.xaml.cs`; Singletons für Services; je Editor-Fenster ein `IServiceScope` |
| Einstellungen | `AppSettings` (JSON, `System.Text.Json` Source-Generator). Ablage `%APPDATA%\GodSaveTheScreen\settings.json`; liegt eine Datei `portable.txt` neben der EXE, wird `.\data\` neben der EXE verwendet |
| Logging | `ILogger<T>` überall; Serilog-Datei-Sink; unbehandelte Ausnahmen (`DispatcherUnhandledException`, `TaskScheduler.UnobservedTaskException`) werden geloggt und als Dialog gemeldet |
| Lokalisierung | `Strings.resx` (Englisch, neutral) + `Strings.de.resx`; XAML über Markup-Extension `{l:Loc Key}`; Sprache: System / Deutsch / Englisch, Umschalten wirkt nach Neustart |
| Single Instance | Benannter `Mutex` + Named Pipe; zweiter Start übergibt Kommandozeile (`--capture rect`, Dateipfade) an die laufende Instanz |
| Theme | WPF-UI `ApplicationThemeManager` (System/Hell/Dunkel), Akzentfarbe des Systems |
| Fehlerbehandlung | Services werfen fachliche Ausnahmen; ViewModels fangen sie und melden über `IDialogService`/Snackbar; Zwischenablage mit Wiederholungslogik |

## 4. Meilensteine

Jeder Meilenstein liefert eine lauffähige, getestete Anwendung mit abgeschlossenem Funktionsumfang. Details, Aufgaben und Abnahmekriterien stehen im jeweiligen Dokument.

| # | Meilenstein | Ergebnis (nutzbar nach Abschluss) | Dokument |
|---|---|---|---|
| M0 | Grundgerüst & Infrastruktur | Solution, Host, DI, Theme, Logging, Lokalisierung, Einstellungen, Tests laufen | [M00](meilensteine/M00-Grundgeruest.md) |
| M1 | Aufnahme-Kern: Rechteck & Vollbild | Startfenster → Rechteck/Vollbild aufnehmen → Ergebnisfenster → PNG speichern / kopieren | [M01](meilensteine/M01-Aufnahme-Kern.md) |
| M2 | Fenster, Freiform & Timer | Alle Aufnahmemodi inkl. Timer mit freier Zeit | [M02](meilensteine/M02-Fenster-Freiform-Timer.md) |
| M3 | Tray, Hotkeys & Einstellungen | Hintergrundbetrieb, globale Hotkeys, Auto-Zwischenablage/-Speichern, Autostart | [M03](meilensteine/M03-Tray-Hotkeys-Einstellungen.md) |
| M4 | Editor-Grundgerüst & Datei-I/O | Editor mit Zoom/Pan, Bilder laden, alle Formate speichern, Zwischenablage, Drag & Drop | [M04](meilensteine/M04-Editor-Dateien.md) |
| M5 | Objekt-Framework & Formen | Formen platzieren/auswählen/ändern, Farben+Transparenz, Undo/Redo, Ebenen | [M05](meilensteine/M05-Objekte-Formen.md) |
| M6 | Text & nummerierte Marker | Textobjekte mit voller Typografie, Schritt-Marker | [M06](meilensteine/M06-Text-Marker.md) |
| M7 | Freihand, Textmarker & Lineal | Stift, Textmarker, drehbares Lineal mit Einrasten | [M07](meilensteine/M07-Freihand-Lineal.md) |
| M8 | Unschärfe & Lupe | Weichzeichnen/Verpixeln von Bereichen, Lupenobjekt | [M08](meilensteine/M08-Unschaerfe-Lupe.md) |
| M9 | Zuschneiden | Snipping-Tool-artiges, nicht-destruktives Zuschneiden | [M09](meilensteine/M09-Zuschneiden.md) |
| M10 | Projektformat `.gsts` | Speichern/Laden mit bearbeitbaren Objekten, zuletzt verwendete Dateien | [M10](meilensteine/M10-Projektformat.md) |
| M11 | Feinschliff & Portable Release | Vollständige Lokalisierung, Barrierefreiheit, Performance, Single-File-ZIP | [M11](meilensteine/M11-Feinschliff-Release.md) |
| M12 | Werkzeugvorlagen & letzte Konfiguration | Eigene Vorlagen für alle Zeichenwerkzeuge (Liste, Speichern, Umbenennen, Löschen), letzte Konfiguration je Werkzeug gespeichert | [M12](meilensteine/M12-Werkzeugvorlagen.md) |

### 4.1 Abhängigkeiten der Meilensteine

```
M0 ─► M1 ─► M2
       │
       ├──► M3
       │
       └──► M4 ─► M5 ─┬─► M6
                      ├─► M7
                      ├─► M8
                      ├─► M9
                      └─► M10 (sinnvoll nach M6–M9, technisch ab M5 möglich)
M11 nach allen anderen
M12 nach M11 (Erweiterung; baut auf M5–M8 und M10 auf)
```

- M2 und M3 sind unabhängig von M4+ und können parallel zum Editor entwickelt werden.
- M6, M7, M8, M9 bauen nur auf M5 auf und sind untereinander unabhängig.
- M10 muss jeden Annotationstyp kennen; neue Typen nach M10 erfordern nur einen `[JsonDerivedType]`-Eintrag + Test.

### 4.2 Grobe Aufwandsschätzung

| Meilenstein | Aufwand (Personentage) |
|---|---|
| M0 | 2 |
| M1 | 4 |
| M2 | 3 |
| M3 | 3 |
| M4 | 4 |
| M5 | 7 |
| M6 | 3 |
| M7 | 4 |
| M8 | 3 |
| M9 | 2 |
| M10 | 2 |
| M11 | 4 |
| M12 | 4 |
| **Summe** | **≈ 45** |

## 5. Bedienkonzept (Übersicht)

### 5.1 Globale Hotkeys (Standard, konfigurierbar)

| Aktion | Standard |
|---|---|
| Rechteck aufnehmen | `Druck` (siehe Risiko R1) |
| Fenster aufnehmen | `Alt+Druck` |
| Vollbild aufnehmen | `Strg+Druck` |
| Freiform aufnehmen | `Strg+Umschalt+Druck` |
| Letzten Modus mit Timer | `Umschalt+Druck` |

### 5.2 Editor-Tastenkürzel

| Taste | Funktion |
|---|---|
| `V` | Auswahl |
| `R` / `E` / `L` / `A` / `D` | Rechteck / Ellipse / Linie / Pfeil / Dreieck |
| `T` | Text |
| `N` | Nummerierter Marker |
| `P` / `H` | Stift / Textmarker |
| `B` | Unschärfe |
| `M` | Lupe |
| `C` | Zuschneiden |
| `Strg+R` | Lineal ein/aus |
| `Strg+Z` / `Strg+Y` | Rückgängig / Wiederholen |
| `Strg+C` | Ausgewählte Objekte kopieren; ohne Auswahl: Bild kopieren |
| `Strg+Umschalt+C` | Bild (gerendert) immer in die Zwischenablage |
| `Strg+V` | Objekte einfügen bzw. Bild als neues Dokument |
| `Strg+D` / `Entf` | Duplizieren / Löschen |
| `Strg+S` / `Strg+Umschalt+S` | Speichern / Speichern unter |
| `Strg+O` / `Strg+N` | Öffnen / Neue Aufnahme |
| `Strg+Mausrad`, `Strg+0`, `Strg+1` | Zoom, Einpassen, 100 % |
| `Leertaste` (gedrückt) | Verschieben (Pan) |
| `Umschalt` beim Zeichnen | Seitenverhältnis 1:1 bzw. Winkel in 15°-Schritten |
| Pfeiltasten (+`Umschalt`) | Auswahl um 1 px (10 px) verschieben |

## 6. Qualitätssicherung

- **Unit-Tests** je Meilenstein (siehe Dokumente): Core zu ≥ 80 % abgedeckt (Modell, Undo, Serialisierung, Geometrie), ViewModels über gemockte Services, Rendering per `[StaFact]` mit Pixelvergleich an Stichpunkten.
- **Manuelle Testmatrix** (ab M1, fortgeschrieben): 1 Monitor 100 %; 2 Monitore 100 % + 150 %; Monitor links/oberhalb des primären (negative Koordinaten); Remote-Desktop-Sitzung; Hell/Dunkel.
- **Definition of Done** für jeden Meilenstein: Build ohne Warnungen, alle Tests grün, Abnahmekriterien manuell geprüft, neue UI-Texte in DE und EN, `docs/agent/architecture.md` bei Struktur-/Konventionsänderungen aktualisiert, Commit je abgeschlossenem Teilschritt.

## 7. Risiken

| # | Risiko | Gegenmaßnahme |
|---|---|---|
| R1 | Windows 11 belegt `Druck` für das Snipping Tool („Druck-Taste zum Öffnen des Bildschirmausschnitts verwenden“) → `RegisterHotKey` schlägt fehl | Fehlschlag erkennen, Hinweis mit Link zu `ms-settings:easeofaccess-keyboard` anzeigen, alternativen Hotkey vorschlagen |
| R2 | Gemischte DPI über mehrere Monitore; WPF-Fenster über Monitorgrenzen skaliert falsch | Ein Overlay-Fenster **je Monitor**, Positionierung in physischen Pixeln per `SetWindowPos` |
| R3 | HDR-Monitore liefern per `BitBlt` ausgewaschene Farben | Dokumentieren; optional später `Windows.Graphics.Capture` mit SDR-Konvertierung |
| R4 | Fenster mit abgerundeten Ecken (Win 11) / verdeckte Fenster | DWM-Rahmen (`DWMWA_EXTENDED_FRAME_BOUNDS`); Option „verdeckte Fenster mit PrintWindow erfassen“; Ecken optional transparent |
| R5 | Zwischenablage blockiert durch andere Prozesse (`COMException`) | Wiederholung mit Backoff (5× 50 ms), danach Meldung |
| R6 | Große Bilder (8K, mehrere Monitore) → Speicher/Performance beim Weichzeichnen | Effekte nur für den Bereich berechnen, Ergebnis cachen, beim Ziehen entprellen |
| R7 | Weichzeichnen ist bei kleinen Radien rekonstruierbar | Standardmodus „Verpixeln“ mit großer Blockgröße, Hinweis im Tooltip |
| R8 | WPF unterstützt kein Trimming | Single-File ohne `PublishTrimmed`, ZIP-Größe ~70 MB akzeptiert |
