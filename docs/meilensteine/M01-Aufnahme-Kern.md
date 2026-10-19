# M1 – Aufnahme-Kern: Rechteck & Vollbild

← [M0](M00-Grundgeruest.md) · [Umsetzungsplan](../Umsetzungsplan.md) · Weiter: [M2](M02-Fenster-Freiform-Timer.md)

## Ziel

Erstes nutzbares Produkt: Über ein Startfenster wird ein Rechteck oder der Vollbildschirm aufgenommen; das Ergebnis erscheint in einem einfachen Ergebnisfenster und kann als PNG gespeichert oder in die Zwischenablage kopiert werden. Multi-Monitor und gemischte DPI funktionieren korrekt.

## Voraussetzungen

- M0 abgeschlossen

## Umfang

**Enthalten**
- Monitor-Erkennung in physischen Pixeln inkl. DPI je Monitor
- Freeze-Frame-Aufnahme des gesamten virtuellen Desktops
- Auswahl-Overlay je Monitor mit Rechteckauswahl, Lupe, Fadenkreuz, Größenanzeige
- Vollbild: aktueller Monitor (unter Mauszeiger) und alle Monitore
- Startfenster (Snipping-Tool-artig): Modus wählen, „Neu“
- Einfaches Ergebnisfenster: Anzeige (Einpassen/100 %), „Speichern als PNG“, „Kopieren“
- `PixelBuffer` und `ImageDocument` (Grundform) im Core

**Nicht enthalten**
- Fenster/Freiform/Timer (M2), Tray/Hotkeys (M3), vollständiger Editor (M4)

## Aufgaben

### 1. Core
- [ ] Geometrie-Primitive `PointD`, `SizeD`, `RectD` (readonly record structs, Hilfsmethoden `Normalize`, `Intersect`, `Contains`, `Inflate`), `PixelRect` (int)
- [ ] `PixelBuffer`: `Width`, `Height`, `Stride`, `byte[] Pixels` (BGRA32, nicht vormultipliziert), `Crop(PixelRect)`, `Clone()`
- [ ] `ImageDocument` (Grundform): `BaseImage`, `DpiX/DpiY`, `SourcePath`, `CreatedAt`, `IsDirty`

### 2. Capture
- [ ] `NativeMethods.txt` (CsWin32): `EnumDisplayMonitors`, `GetMonitorInfo`, `GetDpiForMonitor`, `GetDC`, `ReleaseDC`, `CreateCompatibleDC`, `CreateCompatibleBitmap`, `SelectObject`, `BitBlt`, `GetDIBits`, `DeleteObject`, `DeleteDC`, `GetSystemMetrics`, `GetCursorPos`, `MonitorFromPoint`
- [ ] `MonitorInfo` (Handle, Bounds/WorkArea in physischen Pixeln, DPI, IsPrimary, DeviceName)
- [ ] `IMonitorService.GetMonitors()`
- [ ] `IScreenCaptureService.CaptureVirtualScreen()` → `ScreenSnapshot` (`PixelBuffer` des virtuellen Desktops + Ursprung `VirtualLeft/Top` + Monitore)
  - `BitBlt` mit `SRCCOPY | CAPTUREBLT` vom Desktop-DC, `GetDIBits` als 32-bpp Top-Down-DIB, Alpha auf 255 setzen
  - Optional Mauszeiger mitzeichnen (Einstellung, Standard aus) via `GetCursorInfo`/`DrawIconEx`
- [ ] `ScreenSnapshot.Crop(PixelRect inVirtualCoords)` → `PixelBuffer`
- [ ] Alle Handles in `try/finally` bzw. `SafeHandle` freigeben

### 3. Auswahl-Overlay (App)
- [ ] `CaptureCoordinator`: steuert Ablauf Snapshot → Overlays → Ergebnis; nur eine Aufnahme gleichzeitig
- [ ] `SelectionOverlayWindow` (randlos, `Topmost`, `ShowInTaskbar=false`, `AllowsTransparency=false` – Snapshot ist Hintergrund, daher keine Transparenz nötig)
  - Positionierung je Monitor über `SetWindowPos` in physischen Pixeln nach `SourceInitialized`
  - Hintergrund: Monitorausschnitt des Snapshots als `BitmapSource`, darüber halbtransparente Abdunklung; ausgewählter Bereich wird ausgespart (`CombinedGeometry` Exclude)
  - Eingaben: Ziehen = Rechteck; `Esc` = Abbruch; Rechtsklick = Abbruch; Pfeiltasten verschieben Endpunkt pixelgenau; `Enter` übernimmt
  - Fadenkreuz-Linien, Pixellupe (z. B. 8×, 15×15 px, mit Farbwert-Anzeige `#RRGGBB`), Größenlabel `W × H`
  - Auswahl über Monitorgrenzen hinweg: Overlays teilen ein gemeinsames `SelectionState` in **virtuellen** Pixelkoordinaten; jedes Overlay zeichnet seinen Anteil
- [ ] `SelectionOverlayViewModel` + `SelectionState` (testbar ohne Fenster)
- [ ] Vollbild-Modi: „Aktueller Monitor“ ohne Overlay direkt; „Alle Monitore“ = gesamter virtueller Desktop (leere Bereiche bei ungleichen Monitorgrößen schwarz)

### 4. Startfenster
- [ ] `StartWindow` (kompakt, `ui:FluentWindow`): Split-Button „Neu“ + Modus-Auswahl (Rechteck, Vollbild-Monitor, Vollbild-Alle; weitere Modi in M2 vorgesehen), Button „Öffnen“ (deaktiviert bis M4), Einstellungen (deaktiviert bis M3)
- [ ] Startfenster blendet sich vor der Aufnahme aus (Hide + kurze Wartezeit ~150 ms für Fensteranimation) und danach wieder ein

### 5. Ergebnisfenster (vorläufig, wird in M4 zum Editor)
- [ ] `EditorWindow` (Grundform) mit `Image` in `ScrollViewer`, Buttons „Einpassen“, „100 %“
- [ ] 100 % = 1 Bildpixel pro physischem Bildschirmpixel (Skalierung mit `1 / (monitorDpi/96)` kompensieren)
- [ ] „Speichern unter…“ (nur PNG in M1) und „Kopieren“
- [ ] `IClipboardService.SetImage(PixelBuffer)`: setzt `CF_DIB` (`BitmapSource`) **und** Format `"PNG"` (Stream) für Alpha-fähige Ziele; Wiederholung bei `COMException` (5× 50 ms)
- [ ] `PixelBufferExtensions.ToBitmapSource()` / `FromBitmapSource()` in Rendering (`BitmapSource.Create` + `Freeze()`)

## Technische Details

- **Freeze-Frame statt Live-Overlay:** Der Bildschirm wird vor dem Anzeigen der Overlays eingefroren. Dadurch sind Tooltips, Menüs und Hover-Zustände aufnehmbar, und das Overlay braucht keine echte Transparenz (performant, kein DWM-Flackern).
- **Koordinatensysteme:** Virtuelle Desktop-Koordinaten (physische Pixel, können negativ sein) → Snapshot-Pixel (`x - VirtualLeft`) → Overlay-lokale DIPs (`/ monitorScale`). Umrechnungen ausschließlich in `CoordinateMapper` (Unit-getestet).
- **DPI:** Durch `PerMonitorV2` erhält jedes Overlay-Fenster den DPI seines Monitors. Das Fenster wird erst nach `SourceInitialized` positioniert, damit WPF nicht zwischen DPIs springt.

## Tests

- `Core.Tests`: `RectD`/`PixelRect`-Operationen, `PixelBuffer.Crop` (Ränder, leere Auswahl)
- `App.Tests`: `CoordinateMapper` (negative Ursprünge, 150 %-Monitor rechts vom 100 %-Monitor), `SelectionState` (Normalisierung bei Rückwärtsziehen, Klemmen an Desktopgrenzen, Mindestgröße), `PixelBuffer ↔ BitmapSource` Roundtrip (`[StaFact]`)

## Abnahmekriterien

- [ ] Rechteckauswahl auf jedem Monitor und über Monitorgrenzen liefert pixelgenaues Ergebnis (Vergleich mit bekannter Testgrafik)
- [ ] Funktioniert bei 100 %/150 %-Mischkonfiguration und Monitor links vom primären
- [ ] Lupe zeigt korrekte Pixel und Farbwert
- [ ] `Esc` bricht ohne Ergebnisfenster ab, alle Overlays schließen
- [ ] PNG-Datei und Zwischenablage (Paint, Word, Browser) enthalten das korrekte Bild
- [ ] Kein GDI-Handle-Leck nach 50 Aufnahmen (Task-Manager-Spalte „GDI-Objekte“)
