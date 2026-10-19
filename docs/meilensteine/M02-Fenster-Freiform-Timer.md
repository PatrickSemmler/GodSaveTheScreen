# M2 – Fensteraufnahme, Freiform & Timer

← [M1](M01-Aufnahme-Kern.md) · [Umsetzungsplan](../Umsetzungsplan.md) · Weiter: [M3](M03-Tray-Hotkeys-Einstellungen.md)

## Ziel

Alle Aufnahmemodi sind verfügbar: Rechteck, Fenster, Freiform, Vollbild – jeweils optional mit einem frei einstellbaren Timer.

## Voraussetzungen

- M1 abgeschlossen

## Umfang

**Enthalten**
- Fenstererkennung unter dem Mauszeiger mit Hervorhebung (inkl. untergeordneter Elemente optional)
- Fensteraufnahme aus dem Snapshot oder per `PrintWindow` (auch verdeckt)
- Freiform-Auswahl (Lasso) mit transparentem Außenbereich
- Timer: Voreinstellungen + freie Eingabe in Sekunden, Countdown-Anzeige, Abbruch
- Eigene Fenster werden zuverlässig von Aufnahmen ausgeschlossen

**Nicht enthalten**
- Hotkeys/Tray (M3)

## Aufgaben

### 1. Fenster-Enumeration (Capture)
- [x] `IWindowEnumerator.GetTopLevelWindows()` in Z-Order (`EnumWindows`), gefiltert:
  - sichtbar (`IsWindowVisible`), nicht minimiert (`IsIconic`), nicht „cloaked“ (`DwmGetWindowAttribute(DWMWA_CLOAKED)` – virtuelle Desktops/UWP-Hintergrund)
  - Mindestgröße, kein Tool-Window ohne Titel, eigene Prozess-ID ausgeschlossen
- [x] Rahmen über `DWMWA_EXTENDED_FRAME_BOUNDS` (ohne unsichtbare Resize-Ränder), Fallback `GetWindowRect`
- [x] `WindowInfo` (Handle, Titel, Prozessname, Bounds, IsMaximized, Z-Index)
- [x] Fensterliste wird **zusammen mit dem Snapshot** erstellt (gleicher Zeitpunkt)
- [ ] Optional: Kind-Elemente via `ChildWindowFromPointEx` / UI-Automation-Rechteck bei gedrückter `Strg`-Taste (Steuerelement statt Fenster) *(nicht umgesetzt)*

### 2. Fenster-Modus im Overlay
- [x] Hover hebt das oberste Fenster unter dem Cursor hervor (Rahmen + Titel-Badge), Klick übernimmt
- [x] Ziehen im Fenster-Modus wechselt automatisch zur Rechteckauswahl (wie bei vielen Tools)
- [x] Ergebnisquelle (Einstellung „Fensteraufnahme“):
  - **Snapshot** (Standard): Ausschnitt aus dem eingefrorenen Bild – exakt was sichtbar war
  - **Fensterinhalt**: `PrintWindow(hwnd, PW_RENDERFULLCONTENT)` → erfasst auch verdeckte Teile; Fallback auf Snapshot, wenn Ergebnis leer/schwarz
- [x] Option „Abgerundete Ecken transparent“ (Windows 11): Maske mit `DWMWA_WINDOW_CORNER_PREFERENCE`-Radius (8 px × DPI-Faktor)
- [x] Maximierte Fenster: auf Monitorarbeitsbereich klemmen

### 3. Freiform (Lasso)
- [x] Punkte beim Ziehen sammeln (Mindestabstand 2 px), Pfad live zeichnen; Loslassen schließt den Pfad
- [x] Ergebnis: Bounding-Box ausschneiden, außerhalb des Polygons Alpha = 0 (Scanline-Polygon-Füllung im Core: `PolygonMask.Apply(PixelBuffer, points)`)
- [x] Optional Kantenglättung (Supersampling 4× am Rand) – Einstellung
- [x] Hinweis: Export als JPEG/BMP füllt Transparenz mit einstellbarer Hintergrundfarbe (Umsetzung in M4)

### 4. Timer
- [x] `CaptureRequest(Mode, Delay)`; Timer ist Teil des Startfensters: Dropdown „Kein / 3 s / 5 s / 10 s / Benutzerdefiniert…“ + `NumberBox` (0–3600 s, Schrittweite 1)
- [x] Letzter benutzerdefinierter Wert wird gespeichert
- [x] `CountdownWindow`: kleines, rundes, halbtransparentes Fenster unten rechts (oder am Cursor), große Ziffer, Klick/`Esc` bricht ab; `SetWindowDisplayAffinity(WDA_EXCLUDEFROMCAPTURE)` (Fallback: vor Snapshot ausblenden)
- [x] Optionaler Ton bei 3-2-1 (Einstellung)
- [x] Timer läuft **vor** dem Einfrieren; danach wird normal ausgewählt (Verhalten wie Snipping Tool)
- [x] `CaptureCoordinator` unterstützt `CancellationToken` für Abbruch

### 5. Ausschluss eigener Fenster
- [x] Alle eigenen Fenster (Start, Editor, Countdown) mit `WDA_EXCLUDEFROMCAPTURE` versehen (Einstellung „Eigene Fenster nicht aufnehmen“, Standard an) – Startfenster wird zusätzlich ausgeblendet
- [x] Prüfung der Windows-Version (≥ 10 2004), sonst Ausblenden + Wartezeit

## Technische Details

- **Warum Snapshot als Standardquelle:** Entspricht exakt dem, was der Nutzer sah (inkl. überlagernder Menüs/Tooltips); `PrintWindow` liefert bei manchen Apps (Hardware-beschleunigt, Spiele) schwarze Flächen.
- **Z-Order-Hit-Test:** Ermittlung des Fensters unter dem Cursor erfolgt gegen die gespeicherte Liste (nicht live), damit Overlay-Fenster selbst nicht getroffen werden.

## Tests

- `Core.Tests`: `PolygonMask` (konvex, konkav, selbstüberschneidend → Even-Odd), Bounding-Box-Berechnung
- `App.Tests`: Hit-Test gegen `WindowInfo`-Liste (Überlappung, Z-Order), Timer-ViewModel (Grenzen, Persistenz), `CaptureCoordinator` mit gemocktem `IScreenCaptureService` (Abbruch während Countdown → kein Snapshot)

## Abnahmekriterien

- [ ] Fenster werden beim Überfahren korrekt hervorgehoben, auch bei Monitorgrenzen und Mischskalierung
- [ ] Fensteraufnahme enthält keine unsichtbaren Ränder/Schatten
- [ ] Teilweise verdecktes Fenster wird im Modus „Fensterinhalt“ vollständig aufgenommen
- [ ] Freiform-PNG hat transparenten Außenbereich
- [ ] Timer mit 7 s (frei eingegeben) zählt sichtbar herunter, Countdown erscheint nicht im Bild, `Esc` bricht ab
- [ ] Ein geöffnetes Kontextmenü einer Fremd-App ist nach Timer-Ablauf aufnehmbar
