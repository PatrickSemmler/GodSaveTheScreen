# M17 – Scroll-Capture

← [M16](M16-Texterkennung.md) · [Umsetzungsplan](../Umsetzungsplan.md)

Feature-Vorschlag Nr. 16. Geschätzter Aufwand: 13 Personentage, davon 2 für den Vorversuch.

## Ziel

Inhalte, die über den sichtbaren Bereich hinausgehen, werden als ein langes Bild aufgenommen: Webseiten, Dokumente, lange Listen. Die App scrollt selbst. Reagiert das Programm nicht, scrollt der Benutzer von Hand und die App setzt die Bilder laufend zusammen. Das Ergebnis läuft wie jede Aufnahme weiter in den Editor.

## Voraussetzungen

- M0–M13 abgeschlossen; unabhängig von M14–M16

## Entscheidungen (mit dem Auftraggeber geklärt)

| Thema | Entscheidung |
|---|---|
| Scrollen | Automatisch, mit manuellem Scrollen als Ausweichweg |
| Länge | Höchstens 16.384 px; beim Erreichen stoppt die Aufnahme mit Hinweis. Der Editor wird nicht auf Kacheln umgebaut |
| Richtung | Nur vertikal. Der Algorithmus wird so gebaut, dass horizontal später als eigener Schritt möglich ist |
| Startpunkt | Aktuelle Position; Kontrollkästchen „Zuerst zum Anfang scrollen“, das gespeichert wird |

## Geprüfte Fakten (Testprojekt am 31.10.2026, dieser PC)

| Prüfung | Ergebnis | Folge für den Plan |
|---|---|---|
| `RenderCapability.MaxHardwareTextureSize` | 16384 × 16384 | Andere Grafikkarten können weniger melden, deshalb wird der Wert zur Laufzeit gelesen |
| `WriteableBitmap` 1920 × 16.384 im `Image` | Wird korrekt angezeigt | Obergrenze ist tragfähig |
| `WriteableBitmap` 1920 × 16.385 und mehr | Anzeige weiß, Prozess endet mit Fehlercode; im ersten Lauf `UCEERR_RENDERTHREADFAILURE` | Grenze muss hart eingehalten werden |
| Kacheln bis 8192 px, gesamt bis 32.768 px | Wird korrekt angezeigt | Spätere Erweiterung bleibt möglich |
| `RenderTargetBitmap` bis 1920 × 40.000 | Letzte Zeile korrekt | Export wäre kein Engpass |

**Nicht geprüft:** Verhalten von Scrollbefehlen in echten Programmen. Das klärt der Vorversuch in Aufgabe 1, bevor Oberfläche entsteht.

## Ergebnis des Vorversuchs (31.10.2026, dieser PC, 3840 × 2160, 100 %)

Konsolenprogramme: `artifacts/scroll-probe` (jeder Treiber gegen ein Fenster, Vorher-/Nachher-Bilder, Ende-zu-Ende-Lauf mit Auswahl und Schleife) und `artifacts/scroll-session-probe` (die echte `ScrollCaptureSession` mit echter Steuerleiste). Beide liegen nicht in der Solution und nicht im Repository (`artifacts/` ist ignoriert). Testinhalte: eine eigene HTML-Seite mit fester Kopf- (70 px) und Fußzeile (50 px), 70 Abschnitten und Zebra-Tabellen; eine Textdatei mit 400 Zeilen.

| Programm | UI-Automation | Mausrad | Fensternachricht | Tastatur | Ergebnis |
|---|---|---|---|---|---|
| Chrome 1500 × 1000, leeres Profil | ja, 40 px je Einheit, gleichmäßig | ja, 100 px je Notch | nicht anwendbar | ja, `Bild↓` = 549 px | Ende-zu-Ende: 31 Bilder, 1484 × 16.384 px (Limit), Kopfzeile 70 und Fußzeile 50 genau erkannt, Abschnitte 1–48 lückenlos, ohne Naht. Echte Sitzung mit Oberfläche: 17 s, Leiste und Rahmen nicht im Bild |
| Notepad++ (Scintilla), maximiert | ja, 20 px | ja, 60 px | ja, 20 px, kennt das Ende | unbrauchbar (Cursor springt, keine Überlappung) | 13 Bilder, 1914 × 8029 px, Ende des Inhalts erkannt |
| Edge | nicht aussagekräftig | | | | Ein neues Profil zeigt einen Sync-Dialog zum Microsoft-Konto, der die Seite abdunkelt und blockiert; nicht weggeklickt |
| Firefox | nicht anwendbar | | | | Willkommensdialog mit Nutzungsbedingungen blockiert; nicht bestätigt |
| Explorer, Word, Excel, Outlook, VS Code, Visual Studio, Editor, Einstellungen, Adobe Reader, WPF, Fenster mit Adminrechten | – | – | – | – | **nicht geprüft** |

Erkenntnisse:
- Die Reihenfolge UI-Automation → Mausrad → Nachricht → Tastatur bleibt. Die Tastatur ist die schlechteste Wahl (Fokus, Cursor, Sprünge von einer Seite).
- Browser mit weichem Scrollen beginnen erst nach etwa 100 ms zu bewegen. Ohne **Anlaufzeit** (120 ms nach jedem Scrollbefehl) hielt die Stillstandsprüfung das alte Bild für ruhig und alle Treiber galten als wirkungslos. Die Anlaufzeit steht jetzt in `RegionSettler`.
- Chrome meldet das `ScrollPattern` erst nach der ersten UIA-Anfrage; die erste Suche lieferte nichts. `UiaScrollable.Find` versucht es deshalb dreimal.
- Die **Abbruchbedingung** („reagiert weder Edge noch Chrome zuverlässig“) trifft nicht zu: Chrome reagiert zuverlässig. Edge teilt sich die Engine mit Chrome, wurde aber nicht belegt; erst wenn Edge auf dem Rechner des Auftraggebers nicht reagiert, ist die Entscheidung fällig.

## Festlegungen dieses Plans

| Thema | Festlegung | Begründung |
|---|---|---|
| Auswahl | Vorhandene Fensterauswahl: Klick nimmt das Fenster, Ziehen einen Bereich (`SelectionOverlayViewModel`, Fenstermodus) | Keine neue Bedienung |
| Bereich beim Fensterklick | Größtes scrollbares Element unter dem Klick per UI Automation; sonst Clientbereich | Titelleiste und Menüs bleiben draußen |
| Aufnahmequelle | Bildschirmbereich per `BitBlt`, nicht `PrintWindow` | `PrintWindow` zeigt nicht immer den aktuellen Scrollstand; verdeckte Fenster sind hier kein Ziel |
| Eigene Fenster | Leiste und Rahmen mit `WDA_EXCLUDEFROMCAPTURE`; Editorfenster wie bisher über `HideAll` versteckt | Sie landen nicht im Bild |
| Abbruchtasten | `GetAsyncKeyState` in der Aufnahmeschleife (`Esc`, `Eingabe`). In `App` schon verfügbar | Das Zielprogramm behält den Fokus; keine zusätzlichen globalen Hotkeys |
| Zusammensetzen | Reiner Algorithmus in Core | Ohne Bildschirm testbar |
| UI Automation | COM-Schnittstelle über CsWin32 im Projekt `Capture` | `Capture` soll kein WPF referenzieren (`System.Windows.Automation` hängt daran) |
| Bildlaufleiste | Wird automatisch abgeschnitten, sofern erkannt | Der Balken wäre sonst in jedem Teilstück anders |
| Feststehende Kopf- und Fußzeilen | Kopfzeile aus dem ersten Bild, Fußzeile aus dem letzten | Sie erscheinen genau einmal |
| Fehlschlag mittendrin | Bisheriges Ergebnis wird angeboten, mit Hinweis auf die Bruchstelle | Eine lange Aufnahme geht nicht verloren |
| Standard-Hotkey | `Strg+Umschalt+6` | Setzt die Reihe fort |
| Mauszeiger | Immer ohne Zeiger, Position wird am Ende wiederhergestellt | Ein Zeiger wäre im Bild mehrfach zu sehen |

## Umfang

**Enthalten**
- Bereichsaufnahme ohne vollen Desktop
- Scroll-Treiber mit automatischer Auswahl
- Algorithmus für Überlappung, feststehende Bereiche und Bildlaufleiste
- Steuerleiste, manueller Modus, Teilergebnis, Einstellungen, Hilfe

**Nicht enthalten**
- Horizontales oder zweidimensionales Scrollen
- Bilder über 16.384 px, Kachelanzeige im Editor
- Video-Inhalte sauber herausrechnen (bleiben sichtbar als Bruchstücke, dokumentiert)
- Aufnahme verdeckter oder minimierter Fenster
- Browser-Erweiterungen oder DevTools-Protokoll

## Aufgaben

### 1. Vorversuch (2 Tage, Abbruchpunkt)
- [x] Konsolenprogramm unter `artifacts/` (nicht in der Solution), das jeden Treiber gegen jedes Programm versucht und Vorher/Nachher-Bilder ablegt
- [ ] Testmatrix: Edge, Chrome, Firefox, Explorer, Word, Excel, Outlook, VS Code, Visual Studio, Editor, Einstellungen, Adobe Reader, ein WPF-Programm, ein mit Adminrechten laufendes Fenster – **teilweise** (31.10.2026): Chrome und Notepad++ geprüft, Edge und Firefox blockierten Willkommens-/Sync-Dialoge frischer Profile (Zustimmung nicht im Auftrag), übrige Programme nicht geprüft
- [x] Je Treiber festhalten: Bewegt sich der Inhalt? Springt er gleichmäßig? Braucht er Fokus? Stört er die Sitzung des Benutzers?
- [x] Reihenfolge der Treiber aus den Ergebnissen festlegen; Startannahme unten in Aufgabe 3
- [ ] 10 echte Bildfolgen als Testdaten sichern (nur eigene, unkritische Inhalte), auch Skalierung 125 % und 150 % – offen (Vorversuch lieferte zwei vollständige Läufe, Prüfsummen-Tests stehen aus)
- [x] Ergebnis in diesem Dokument ergänzen. **Reagiert weder Edge noch Chrome zuverlässig, wird vor dem Weiterbauen mit dem Auftraggeber entschieden**

### 2. Algorithmus (Core, `Core/Imaging/ScrollStitcher.cs`)
- [x] API: `Add(PixelBuffer frame)` liefert `StitchStep` (`Moved(dy)`, `NoMovement`, `NoOverlap`, `LimitReached`); `Build()` liefert das Ergebnis; `FrameHeightHint` für den Treiber
- [x] Laufrichtung über eine schmale Zugriffsschicht kapseln, damit Horizontal später die Achsen tauschen kann, ohne die Logik zu kopieren
- [x] **Feststehende Ränder:** Zeilen, die zwischen zwei Bildern an gleicher Stelle gleich bleiben, von oben und unten zählen; bei Bewegung als Kopf- und Fußzeile merken; mehrere Bilder bestätigen den Wert, damit gleichförmige Inhalte nicht als Kopfzeile gelten
- [x] **Aktive Spalten:** Spalten, die sich nie ändern (Seitenleisten), aus dem Vergleich nehmen
- [x] **Versatz suchen, Stufe 1:** Hash je Zeile im aktiven Bereich; einfarbige Zeilen auslassen; Abstimmung über `dy = alteZeile − neueZeile`
- [x] **Versatz suchen, Stufe 2** (für unscharf abweichende Zeilen, z. B. Bruchteil-Scrollen im Browser): Kreuzkorrelation der Zeilen-Helligkeit
- [x] **Prüfung:** Kandidat pixelweise mit Toleranz vergleichen; verlangt mindestens 12 nicht einfarbige Zeilen und 98 % Übereinstimmung
- [x] Bei mehreren Kandidaten (Zebra-Tabellen, wiederholte Zeilen) den wählen, der dem bisherigen Schritt am nächsten kommt
- [x] Vergleich gegen das Ende des bisherigen Ergebnisses, nicht nur das letzte Bild, damit Zurückscrollen im manuellen Modus nichts kaputt macht
- [x] **Bildlaufleiste:** Spalten am rechten Rand (höchstens 32 px × Skalierung), die in Überlappungen dauerhaft abweichen, abschneiden
- [x] Speicher: Nur neue Zeilen als Abschnitte halten, erst in `Build` zusammensetzen; hartes Limit `MaxHeight` als Parameter
- [x] Rechenzeit je Bild bei 1920 × 1080 unter 50 ms, sonst läuft der Vergleich im Hintergrund

### 3. Bereichsaufnahme und Treiber (Capture)
- [x] `IScreenCaptureService.CaptureRegion(PixelRect)` und gemeinsamen GDI-Teil aus `CaptureVirtualScreen` (`ScreenCaptureService.cs:35-86`) herausziehen; `CaptureVirtualScreen` wäre mit Monitoren und Fensterliste je Bild zu teuer
- [x] `IScrollDriver`: `TryPrepare(target)`, `ScrollStep(amount)`, `ScrollToStart()`, `IsAtEnd` (falls bekannt)
- [x] Startreihenfolge, im Vorversuch zu bestätigen:
  1. `UiaScrollDriver`: `ScrollPattern`, keine Zeigerbewegung, kennt den Endpunkt
  2. `WheelScrollDriver`: `SendInput`-Mausrad über dem Bereich, Zeiger danach zurück
  3. `MessageScrollDriver`: `WM_VSCROLL` für klassische Steuerelemente
  4. `KeyboardScrollDriver`: `Bild↓` (braucht Fokus)
- [x] `ScrollDriverSelector`: Treiber der Reihe nach probieren; „wirkt“ heißt, der Inhalt hat sich bewegt. Keiner wirkt → manueller Modus
- [x] `NativeMethods.txt` um benötigte Funktionen ergänzen (`SendInput`, `SetCursorPos`, `PostMessage`, UI-Automation-Schnittstellen)
- [x] Fenster mit höheren Rechten: UIPI blockiert Eingaben still; wird als „wirkt nicht“ erkannt und führt zum manuellen Modus mit passender Meldung

### 4. Ablauf (App, `Capture/ScrollCaptureSession.cs`)
- [x] `CaptureMode.Scrolling` am Ende des Enums; dieselben Stellen wie bei `CaptureMode.Text` in [M16](M16-Texterkennung.md), auch `GetHotkey`/`WithHotkey`-Fallback
- [x] `ISelectionService.SelectAsync` bekommt bei `Scrolling` den Modus `Window`, denn `SelectionOverlayViewModel` und `SelectionOverlayWindow` prüfen an mehreren Stellen genau auf diesen Wert. Der Hinweistext im Overlay wird passend gesetzt
- [x] `CaptureCoordinator`: Nach der Auswahl statt `BuildImage` die Sitzung starten. Das Ergebnis läuft als `ImageDocument` durch die normale Kette (Zwischenablage, Speichern, Editor)
- [x] `IsCapturing` bleibt während der gesamten Sitzung gesetzt; `HideAll` bleibt aktiv; nach `Dispose` alles wiederhergestellt, auch bei Ausnahmen
- [x] Schleife: Scrollen → warten, bis der Bereich stillsteht → aufnehmen → `Add` → Schrittgröße anpassen
- [x] Stillstand: alle 40 ms vergleichen; zwei gleiche Bilder hintereinander oder 1 s Obergrenze; danach einstellbare Zusatzwartezeit für nachladende Seiten (Standard 150 ms, 0–2 s)
- [x] Schrittgröße: Ziel 70 % der Bewegungshöhe; aus erstem Schritt je Einheit berechnen
- [x] `NoOverlap`: halbe Schrittgröße und zurückscrollen, wenn der Treiber es kann; nach 3 Fehlschlägen beenden und Teilergebnis anbieten
- [x] Ende: `IsAtEnd`, zweimal keine Bewegung, Limit, `Eingabe`, Zeitlimit 5 Minuten
- [x] `Esc` verwirft nach Rückfrage, falls schon mehr als zwei Bildschirme aufgenommen wurden
- [x] Manueller Modus: alle 100 ms aufnehmen; bei `NoOverlap` Warnung „Langsamer scrollen“; Zurückscrollen bis zur Bruchstelle macht weiter
- [x] `MaxHeight` = kleinster Wert aus 16.384 und `RenderCapability.MaxHardwareTextureSize`; dieselbe Prüfung für die Breite beim Start

### 5. Oberfläche (App)
- [x] `ScrollCaptureBar`: kleines, randloses Fenster außerhalb des Bereichs, auf dem richtigen Monitor; Status, aktuelle Höhe, Limit-Balken, „Fertig“, „Abbrechen“, Umschalter Automatisch/Manuell, Kontrollkästchen „Zuerst zum Anfang scrollen“
- [x] Kann die Leiste nirgends neben dem Bereich stehen (Vollbildfenster), liegt sie in einer Ecke darüber. Sie landet durch den Aufnahme-Ausschluss trotzdem nicht im Bild
- [x] `ScrollCaptureFrame`: Rahmen um den Bereich, klick-durchlässig (`WS_EX_TRANSPARENT`), außerhalb des Bereichs gezeichnet
- [x] Mini-Vorschau der bisherigen Aufnahme in der Leiste, verkleinert, höchstens zweimal pro Sekunde
- [x] Beide Fenster über `IWindowFactory`, aber immer von der Aufnahme ausgeschlossen: `IWindowExclusionService.Register` richtet sich nach der Einstellung `ExcludeOwnWindows` (`WindowExclusionService.cs:74`), deshalb neue Methode `RegisterAlwaysExcluded`. Schlägt `SetWindowDisplayAffinity` fehl, bricht die Aufnahme mit Meldung ab, statt die Leiste mit aufzunehmen
- [x] Eintrag in Start, Tray, Editor-Menü, Einstellungen (Hotkey, Zusatzwartezeit) und `--capture scroll`
- [x] Nach Abschluss: Hinweis bei Limit, Teilergebnis oder wenn Kopfzeile nicht sicher erkannt wurde

### 6. Abschluss
- [x] Hilfe: Abschnitt `capture-scroll` in beiden Sprachen, mit Grenzen (Limit, animierte Inhalte, Programme mit höheren Rechten, nachladende Seiten)
- [x] README: Funktionsliste, Hotkeys
- [x] `docs/agent/architecture.md`: Datenfluss, Limit, neue Fallstricke
- [x] `Umsetzungsplan.md` Abschnitt 3.3: Freeze-Frame gilt für diesen Modus nicht
- [x] `CHANGELOG.md`, Änderungshistorie in `CLAUDE.md`, Version 2. Stelle erhöhen
- [ ] Skill `testkatalog`: Testfälle für Programme und Mehrmonitor ergänzen – offen (Testkatalog.html neu erzeugen: `/testkatalog`)

## Tests

- `Core.Tests` (`ScrollStitcherTests`, erzeugte Seiten aus Zufallsblöcken)
  - Alle Versätze von 1 px bis Bildhöhe − 12
  - Kopf- und Fußzeile, beide zusammen, Seitenleiste, Bildlaufleiste
  - Einfarbige Abschnitte, Zebra-Tabelle, sich wiederholende Zeilen
  - Rauschen ±4 je Kanal; kleine animierte Fläche
  - `NoOverlap`, Stillstand am Seitenende, Limit, Zurückscrollen
  - Zeitlimit je Bild als Leistungs-Test
  - Echte Bildfolgen aus dem Vorversuch: Ergebnis gegen gespeicherte Prüfsumme
- `App.Tests`
  - `ScrollCaptureSession` mit Fake-Treiber und Fake-Aufnahme: Treiberwechsel, Stillstand, Abbruch, Teilergebnis, Fehlschlag, Wiederherstellung des Zeigers und `HideAll`
  - `ScrollDriverSelector`: keiner wirkt → manuell
  - Dokument mit 16.384 px öffnet und zeigt sich im Editor (`WindowSmokeTests`)
  - `AccessibilityTests`, Lokalisierung, `CommandLine`

## Manuelle Prüfung (nicht automatisierbar)

- Testmatrix aus Aufgabe 1 mit dem fertigen Ablauf
- Skalierung 100 %, 125 %, 150 %; Monitor links oberhalb (negative Koordinaten); zwei Monitore mit verschiedener Skalierung
- Lange Webseite mit feststehender Kopfzeile, nachladende Seite, Seite mit Video
- Windows-Einstellung „Inaktive Fenster beim Daraufzeigen scrollen“ aus
- Fenster mit Adminrechten führt zur Meldung und in den manuellen Modus

## Risiken

| Risiko | Gegenmaßnahme |
|---|---|
| Programme reagieren nicht oder springen ungleichmäßig | Mehrere Treiber, Schrittregelung, manueller Modus, Vorversuch als Abbruchpunkt |
| Falsche Überlappung erzeugt unbemerkt doppelte oder fehlende Inhalte | Harte Prüfung mit Mindestwerten, lieber abbrechen als raten; Hinweis an der Bruchstelle |
| Animierte Inhalte, Videos | Toleranz, dokumentierte Grenze |
| Nachladende Seiten ändern die Höhe | Zusatzwartezeit, Vergleich gegen das Ende des Ergebnisses |
| Sehr breite Bereiche brauchen viel Speicher (3840 × 16.384 ≈ 250 MB) | Abschnitte statt Einzelbilder, Prüfung beim Start |
| Eingaben treffen ein falsches Fenster, wenn der Benutzer während der Aufnahme wechselt | Vordergrundfenster und Bereich je Schritt prüfen; bei Änderung pausieren |
| Sicherheitssoftware beanstandet `SendInput` | Reihenfolge beginnt mit UI Automation; manueller Modus kommt ganz ohne Eingaben aus |

## Abnahmekriterien

- [ ] Lange Seite in Edge und Chrome wird ohne Eingriff und ohne sichtbare Naht aufgenommen – Chrome ✓ (31.10.2026, 31 Bilder, 16.384 px, keine Naht erkennbar), Edge offen
- [x] Feststehende Kopfzeile erscheint im Ergebnis genau einmal
- [ ] Programm ohne Scroll-Reaktion lässt sich im manuellen Modus aufnehmen – automatisiert mit Attrappen geprüft, im echten Programm offen
- [x] Das Limit stoppt sauber mit Hinweis; das Bild öffnet im Editor ohne Fehler
- [ ] `Esc` beendet jederzeit, Mauszeiger und ausgeblendete Fenster sind danach wiederhergestellt – mit Attrappen geprüft (Zeiger/Fenster-Wiederherstellung), `Esc`-Taste im echten Ablauf offen
- [ ] Ergebnis läuft durch die normale Kette (Zwischenablage, Speichern, Editor) – Koordinator-Test mit Attrappen ✓, Durchlauf im laufenden Programm (Hotkey → Editor) offen

## Umsetzungsstand (31.10.2026, Version 1.14.0.0)

Umgesetzt wie geplant, mit diesen Abweichungen und Ergänzungen:
- **Ablauf-Schleife in Capture:** Die Schleife steht als `Capture/Scrolling/ScrollCaptureEngine` ohne Oberfläche (damit der Vorversuch sie nutzen und sie ohne WPF getestet werden kann); `App/Capture/ScrollCaptureSession` ist die Hülle mit Ziel, Oberfläche, Tasten, Rückfragen und Ergebnis.
- **Oberfläche hinter Schnittstellen:** `IScrollCaptureView`/`IScrollCaptureViewFactory`, `IScrollCaptureKeys`, `IScrollTargetResolver`, `IScrollDriverSelector` – die Sitzung ist mit Attrappen getestet. Die UI-Automation läuft über die `ComImport`-Typen von CsWin32 (kein WPF in `Capture`).
- **Anlaufzeit** nach Scrollbefehlen und **Mehrfachsuche** der UI-Automation (siehe Vorversuch).
- **Zusätzlich:** Pause, solange das Zielfenster nicht vorn liegt (`IScrollTargetResolver.IsInFront`); der erwartete Schritt wird bei mehreren Kandidaten immer mitgeprüft; Vorschau über `ScrollStitcher.BuildPreview` (kostet nur so viel wie die Vorschau groß ist).
- **Rahmen** liegt außerhalb des Bereichs (`WS_EX_TRANSPARENT`); die Leiste steht rechts, sonst links, sonst in einer Ecke des Monitors.
- Hotkey: `Strg+Umschalt+6`; Einstellungen: `HotkeyScroll`, `ScrollCaptureToStart`, `ScrollExtraWaitMs`.

Tests: Core 516 (davon `ScrollStitcherTests` 26: alle Versätze 1 … Höhe − 12, Kopf/Fuß, Seitenleiste, Bildlaufleiste, einfarbige Abschnitte, sich wiederholende Zeilen, Rauschen ±4, kleine Animation, `NoOverlap`, Limit, Zurückscrollen, Bruchteil-Scrollen, Rechenzeit bei Full HD), App 769 (Sitzung, Treiberauswahl, Koordinator, Smoke-Test von Leiste und Rahmen samt Ausschluss von der Aufnahme, 16.384-px-Dokument im Editor).

Noch offen: Testmatrix der übrigen Programme, Bildfolgen mit Prüfsummen, Skalierung 125/150 %, Mehrmonitor (negative Koordinaten, gemischte Skalierung), Fenster mit Adminrechten, Windows-Einstellung „Inaktive Fenster beim Daraufzeigen scrollen“ aus, Testkatalog (siehe oben).
