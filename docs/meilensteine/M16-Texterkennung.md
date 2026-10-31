# M16 – Texterkennung (OCR)

← [M15](M15-Bild-verschoenern.md) · [Umsetzungsplan](../Umsetzungsplan.md) · Weiter: [M17](M17-Scroll-Capture.md)

Feature-Vorschlag Nr. 13. Geschätzter Aufwand: 5 Personentage.

## Ziel

Text aus einem Bildschirmbereich oder geöffneten Bild wird lokal erkannt, in einem Ergebnisfenster angezeigt und lässt sich korrigieren und kopieren. Die Erkennung ist als eigene Aufnahmeart „Text aufnehmen“ und als Befehl im Editor verfügbar. Wortpositionen stehen für Feature-Vorschlag Nr. 14 (automatisch schwärzen) bereit.

## Voraussetzungen

- M0–M13 abgeschlossen; unabhängig von M14 und M15

## Entscheidungen (mit dem Auftraggeber geklärt)

| Thema | Entscheidung |
|---|---|
| Technik | `Windows.Media.Ocr`, lokal, ohne Modelldateien |
| Aufnahmeart | „Text aufnehmen“ öffnet nach der Bereichsauswahl **immer** das Ergebnisfenster. Der Editor öffnet sich nicht |
| Editor | Befehl „Text erkennen“ für das ganze Bild oder eine vorhandene Bereichsauswahl (`W`); Ergebnis im selben Fenster. Kein Werkzeug zum Markieren von Wörtern im Bild |
| Sprache | Standard sind die Windows-Profilsprachen. Einstellungen und Ergebnisfenster bieten alle installierten Sprachen; im Fenster lässt sich erneut erkennen |

## Geprüfte Fakten (Testprojekt am 31.10.2026, dieser PC)

| Prüfung | Ergebnis | Folge für den Plan |
|---|---|---|
| Zielframework `net10.0-windows10.0.19041.0` mit WPF | Baut und läuft; OCR liefert Text und Wortrechtecke | Machbar |
| Projektverweis von `net10.0-windows` auf dieses Zielframework | Fehler NU1201 | App und App.Tests müssen mit angehoben werden; Capture und Rendering können bleiben |
| Größe des Single-File-Builds (win-x64, kleines Testprogramm) | 66,2 → 74,2 MB, also rund 8 MB mehr | Akzeptiert. Der Versuch, die SDK-DLL von ReadyToRun auszunehmen, brach mit MSB3030 ab; die mögliche Ersparnis ist also nicht gemessen |
| `OcrEngine.MaxImageDimension` | 10.000; bei 10.001 px `ArgumentException` | Lange Bilder müssen in Kacheln erkannt werden |
| Geschwindigkeit | Kleines Testbild 4–18 ms, leeres Bild 1920 × 9999 in 279 ms | Echte Textbilder dauern länger, deshalb trotzdem abbrechbar und im Hintergrund |
| Installierte Sprachen | Hier nur `de-DE` | Fehlende Sprachen müssen in der Oberfläche erklärt werden |
| Schrift ab 13 px | „Ox80070005“ statt „0x80070005“, sonst korrekt | Null und O verwechselt die Engine auch bei guter Größe. Ergebnisfenster zum Prüfen ist nötig |
| Schrift 9–12 px | Viele Fehler ohne Skalierung. Verdopplung half meist, war aber nicht immer besser (12 und 13 px verloren teils Wörter) | Skalierung mit echten Screenshots festlegen, siehe Aufgabe 1 |

**Nicht geprüft:** Start des veröffentlichten Single-File-Builds mit WinRT-Aktivierung, ARM64, Windows 10 2004, andere Schriften als Segoe UI, ClearType.

## Festlegungen dieses Plans

| Thema | Festlegung | Begründung |
|---|---|---|
| Aufbau | Neues Projekt `src/GodSaveTheScreen.Ocr` (`net10.0-windows10.0.19041.0`, hängt nur von Core ab). Schnittstelle und Ergebnis-Typen in Core | WinRT-Typen und das höhere Zielframework bleiben an einer Stelle; Core bleibt frei davon |
| Bildquelle Editor | `IDocumentExporter.RenderContent` (ohne Rahmen aus M14) statt Basisbild. Vor M14 wird `Render` genutzt | Erkennt, was sichtbar ist. Durch Unschärfe verdeckter Text wird **nicht** erkannt, Textobjekte werden erkannt |
| Transparenz | Vor der Erkennung auf Weiß verrechnen | Transparente Pixel sind sonst schwarz und verfälschen das Ergebnis |
| Mauszeiger | „Text aufnehmen“ nimmt ohne Zeiger auf, unabhängig von `CaptureCursor` | Der Zeiger stört die Erkennung |
| Nachaufnahme-Kette | „Text aufnehmen“ überspringt `IPostCaptureAction`-Schritte | Kein Bildergebnis zum Kopieren oder Speichern |
| Tabellen | Schalter „Spalten mit Tabulator trennen“ im Fenster, Standard aus | Macht Tabellen in Excel einfügbar, trifft aber nicht jedes Layout |
| Bild im Ergebnisfenster | Kleine Vorschau; „Im Editor öffnen“ für den aufgenommenen Bereich | Für den Fall, dass doch Bearbeitung gewünscht ist |
| Standard-Hotkey | `Strg+Umschalt+5` | Setzt die Reihe 1–4 fort |
| Tastenkürzel Editor | `Strg+Umschalt+T` | Zeigt noch keine Belegung im `EditorWindow`; beim Umsetzen im `ShortcutCatalog` gegenprüfen |

## Umfang

**Enthalten**
- OCR-Dienst mit Kacheln, Skalierung, Sprachwahl, Wortpositionen
- Aufnahmeart mit Hotkey, Tray, Startfenster, Editor-Menü und Kommandozeile
- Ergebnisfenster, Editor-Befehl, Einstellungen, Hilfe

**Nicht enthalten**
- Markieren von Text direkt im Bild
- Sprachpakete aus der App heraus installieren (braucht Adminrechte); es gibt einen Verweis auf die Windows-Einstellungen
- Mitgelieferte Erkennung (Tesseract)
- Automatisches Schwärzen (Feature-Vorschlag Nr. 14), Suche im Bildverlauf

## Aufgaben

### 1. Vorversuch und Projektumstellung
- [x] Projekt `src/GodSaveTheScreen.Ocr` anlegen, in `GodSaveTheScreen.slnx` eintragen; `App` und `App.Tests` auf `net10.0-windows10.0.19041.0` anheben
- [x] `dotnet publish -p:PublishProfile=win-x64` und `win-arm64`; veröffentlichte EXE auf x64 starten und erkennen lassen. Bricht das, Plan anhalten und klären
- [x] Größe des echten Builds notieren; bei mehr als 15 MB Zuwachs ReadyToRun-Ausnahme erneut untersuchen (Zuwachs 17,7 MB x64 / 16,5 MB arm64; die Ausnahme ändert nichts, siehe Stand)
- [x] Mustersammlung (abweichend vom Plan nicht als Dateien, sondern selbst gerendert in `OcrTests.cs` – Segoe UI, Consolas, Arial von 8 bis 16 px, hell und dunkel; keine persönlichen Daten, keine Binärdateien im Repository)
- [x] Skalierungsregel aus der Sammlung ableiten. Startannahme: erste Erkennung in Originalgröße; ist die mittlere Höhe der Wortrechtecke unter etwa 10 px, zweiter Lauf mit Faktor 2 und hochwertiger Skalierung. Die Rechtecke sind kleiner als die Schriftgröße (im Test 8 px bei 11-px-Schrift, 12 px bei 16-px-Schrift). Geprüft werden auch dunkler Hintergrund und Faktor 3

### 2. Schnittstelle und Text-Aufbereitung (Core)
- [x] `Core/Ocr/IOcrService`: `AvailableLanguages`, `RecognizeAsync(PixelBuffer, string? languageTag, CancellationToken)`
- [x] `OcrResult` (Sprache, Zeilen, Wörter mit `RectD` in Pixeln des übergebenen Bilds), `OcrLanguage` (Kennung, Anzeigename)
- [x] `OcrUnavailableException`, wenn keine Sprache installiert ist
- [x] `OcrTextBuilder` (rein, ohne Windows): Zeilen, Leerzeile bei großem Abstand, optional Tabulator bei breiten Wortabständen
- [x] `OcrTiling` (rein): Kacheln bis 10.000 px mit Überlappung; doppelte Zeilen im Überlappungsbereich entfernen; Koordinaten zurückrechnen

### 3. Dienst (`GodSaveTheScreen.Ocr`)
- [x] `WindowsOcrService : IOcrService`, Registrierung in `AppServiceCollectionExtensions`
- [x] `PixelBuffer` auf Weiß verrechnen und per `SoftwareBitmap.CreateCopyFromBuffer` übergeben (Bgra8)
- [x] Skalierung nach Aufgabe 1, Wortrechtecke zurückrechnen
- [x] Engine je Sprache cachen; `TryCreateFromUserProfileLanguages` für Automatik; gespeicherte, inzwischen deinstallierte Sprache fällt auf Automatik zurück
- [x] Abbruch über `AsTask(token)`; zwischen Kacheln prüfen

### 4. Aufnahmeart
- [x] `CaptureMode.Text` am **Ende** des Enums ergänzen, damit gespeicherte Werte gültig bleiben
- [x] `AppSettings.HotkeyText` und `GetHotkey`/`WithHotkey`: Beide Methoden haben einen `_`-Zweig auf Vollbild (`AppSettings.cs:283,291`), der den neuen Modus sonst stillschweigend falsch zuordnet
- [x] `HotkeyService.Modes`, Hotkey-Liste in `SettingsViewModel.cs:143`, `CaptureModeNames`, `CommandLine.ParseMode` (`text`), `TrayService`-Menü, `StartViewModel`, Editor-Menü (`EditorWindow.xaml:51-54`), `ShortcutCatalog`
- [x] Bewusst nicht erweitern: Liste `Modes` im Einstellungsdialog (`SettingsViewModel.cs:116-122`) und Linksklick-Aktion im Tray (`TrayClickActionSetting`, `TrayService.cs:230-247`), weil beide von Bildaufnahmen ausgehen
- [x] `CaptureCoordinator`: Bei `Text` Snapshot ohne Zeiger, Bereich zuschneiden, Kette überspringen, `IOcrResultPresenter.Show(image)` aufrufen. Fehler landen wie bisher in `Error_CaptureFailed`
- [x] `ISelectionService.SelectAsync` bekommt bei `Text` den Modus `Rectangle`: `SelectionOverlayViewModel.IsRectangleSelection` prüft genau auf diesen Wert und würde bei einem neuen Modus keinen Auswahlrahmen zeigen. Den Hinweistext im Overlay passend setzen

### 5. Ergebnisfenster und Editor
- [x] `IClipboardService.SetText` ergänzen, mit derselben Wiederholung wie `SetImage`
- [x] `OcrResultWindow` und `OcrResultViewModel` über `IWindowFactory`, also auch von Aufnahmen ausgeschlossen
- [x] Inhalt: Bildvorschau, bearbeitbares Textfeld, Sprachauswahl, „Spalten mit Tabulator trennen“, „Erneut erkennen“, „Kopieren“, „Im Editor öffnen“, Ladeanzeige, Abbrechen
- [x] `Strg+C` ohne Markierung kopiert alles; `Esc` schließt; `F1` springt zum Abschnitt `ocr`
- [x] Dauerhafter Hinweis: „Erkannten Text prüfen“, weil Verwechslungen wie O und 0 vorkommen
- [x] Leeres Ergebnis: eigene Meldung mit Tipp (andere Sprache, größerer Ausschnitt)
- [x] Keine Sprache installiert: Meldung mit Schaltfläche auf `ms-settings:regionlanguage`
- [x] Editor: `RecognizeTextCommand` in Menü, Kontextmenü und `ShortcutCatalog`. Mit nicht leerer `AreaSelection` wird `Bounds` ausgeschnitten und alles außerhalb der Deckung weiß gefüllt
- [x] Einstellung `OcrLanguage` in `AppSettings` (leer = automatisch) mit Auswahl in den Einstellungen; Wechsel im Fenster speichert mit

### 6. Abschluss
- [x] `build/generate-notices.ps1` erfasst nur NuGet-Pakete. Lizenz von `Microsoft.Windows.SDK.NET` und `WinRT.Runtime` aus dem Targeting Pack prüfen und als festen Abschnitt ergänzen; Format aus `architecture.md` beachten
- [x] Umsetzungsplan: OCR aus 1.2 „nicht im Umfang“ streichen, Zielframework in Abschnitt 2 und Modul-Tabelle ergänzen
- [x] Hilfe: Abschnitt `ocr` in beiden Sprachen, Hotkey-Tabelle; Bildschirmfotos neu erzeugen
- [x] README: Funktionsliste, Kürzel; Hinweis zu Sprachpaketen
- [x] `docs/agent/architecture.md` (neues Modul, erlaubte Abhängigkeit), `docs/agent/installer.md` prüfen, `CHANGELOG.md`, `CLAUDE.md`, Version 2. Stelle erhöhen

## Tests

- `Core.Tests`
  - `OcrTextBuilder`: Zeilen, Absätze, Tabulatoren, Reihenfolge
  - `OcrTiling`: Größen um 10.000, Überlappung, Entfernen doppelter Zeilen, Koordinaten
  - `GetHotkey`/`WithHotkey` für `Text`; alte Einstellungsdatei ohne `HotkeyText` bekommt den Standard
- `App.Tests`
  - `CaptureCoordinator` mit Fake-Dienst: Text-Modus überspringt die Kette, Abbruch, Fehler
  - `OcrResultViewModel`: Kopieren, Sprachwechsel startet neu, leeres Ergebnis, keine Sprache
  - Bereichsauswahl: Außerhalb der Deckung weiß; Unschärfe verdeckt den Text
  - Echte Erkennung gegen Musterbilder, übersprungen ohne installierte passende Sprache (wie `EmojiTestSupport.RequireFont`)
  - `CommandLine` versteht `--capture text`; `WindowSmokeTests` um das neue Fenster erweitern
  - Bestehende `AccessibilityTests` und `LocalizationCompletenessTests` decken das neue Fenster ab

## Abnahmekriterien

- [x] Ein Hotkey, Bereich ziehen, und Text steht im Fenster, ohne dass der Editor aufgeht
- [x] Bei Musterbildern mit Schrift ab 13 px sind mindestens 98 % der Zeichen korrekt, abgesehen von O/0-Verwechslungen
- [x] Bild über 10.000 px wird vollständig erkannt, ohne doppelte Zeilen
- [x] Ohne Sprachpaket erscheint eine verständliche Meldung, kein Absturz
- [x] Nachweislich kein Netzwerkzugriff (`GodSaveTheScreen.Ocr` verweist auf keine `System.Net*`-Assembly, per Test geprüft; `Windows.Media.Ocr` arbeitet lokal)
- [x] Veröffentlichter Single-File-Build funktioniert auf x64 (mit einer eigenen Single-File-Probe mit gleichen Publish-Einstellungen geprüft; das veröffentlichte Programm selbst wurde nicht gestartet); ARM64-Paket baut (Start auf ARM-Hardware bleibt offen wie bisher)

## Stand

Umgesetzt am 31.10.2026 (Version 1.13.0.0). Tests: Core 490, App 744 grün (die Erkennungstests laufen hier mit `de-DE` und werden ohne Sprachpaket übersprungen).

**Vorversuch:** Single-File-Probe (win-x64, selbstenthalten, R2R, Kompression, WPF + `Windows.Media.Ocr`) erkennt Text ohne Probleme. Das veröffentlichte Programm ist 17,7 MB (x64: 84,8 → 102,5 MB) bzw. 16,5 MB (arm64: 79,6 → 96,1 MB) größer; die Windows-SDK-Projektion (`Microsoft.Windows.SDK.NET.dll`, 25 MB IL) ist der Hauptgrund. Der Ausschluss von ReadyToRun (`PublishReadyToRunExclude`) ändert nichts, weil die DLL fast nur Metadaten enthält. Möglicher späterer Hebel: eine gefilterte C#/WinRT-Projektion (nur `Windows.Media.Ocr`, `Windows.Graphics.Imaging`, `Windows.Storage.Streams`) statt des ganzen SDK-Pakets. Ein Veröffentlichen aus sehr langen Pfaden erzeugt eine EXE, die nicht startet; die Probe lag deshalb unter `C:\Temp`.

**Skalierungsregel (gemessen):** Selbst gerenderte Texte, drei Schriften, 8–16 px, hell/dunkel, Ähnlichkeit 1 − Levenshtein/Länge. Ab Schrift 13 px liefert der erste Lauf ≥ 97 %; bei 11–12 px 95–100 %; bei 9–10 px nur 13–58 % (Wortrechteck-Höhe unter 8 px), dort bringt 2× 85–100 %, 3× 98–100 %; bei 8 px 3× ≥ 49 % gegenüber 2× ≥ 21 %. Regel daher dreistufig: mittlere Wortrechteck-Höhe ≥ 10 px = ein Lauf, < 10 px = zweiter Lauf 2×, < 7,5 px = 3× (`OcrImagePrep.RetryFactor`); der vergrößerte Lauf ersetzt den ersten nur, wenn er mehr Zeichen liefert. Verwechslungen von Null und O treten wie beschrieben auf (die Messung wertet sie als Fehler); die Windows-Erkennung ließ in einem Versuch die Hexzahl „0x80070005“ einer Zeile ganz aus.

**Abweichungen/Festlegungen:** Bilder breiter als 10.000 px werden gleichmäßig verkleinert (Kacheln gibt es nur senkrecht). Die Leiste des Editors hat eine Schaltfläche „Text erkennen“. Das Einstellungsfeld liegt unter Aufnahme. „Neu“ im Editor und Startfenster enthalten „Text aufnehmen“; die Linksklick-Aktion des Trays und die Standardmodus-Liste bleiben ohne Text (siehe Plan). `docs/agent/installer.md` geprüft: keine Änderung nötig (der Installer packt das Publish-Verzeichnis).

**Offen:** manuelle Prüfung im laufenden Programm (Hotkey `Strg+Umschalt+5`, Auswahl, Ergebnisfenster, Editor-Befehl mit `W`-Auswahl, Meldung ohne Sprachpaket), Start des veröffentlichten Programms mit Texterkennung, ARM64-Hardware, Windows 10 2004.
