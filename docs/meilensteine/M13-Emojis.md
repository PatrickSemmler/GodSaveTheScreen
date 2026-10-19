# M13 – Farbige Emojis

← [M12](M12-Werkzeugvorlagen.md) · [Umsetzungsplan](../Umsetzungsplan.md)

## Ziel

Farbige, frei skalierbare Emojis lassen sich als bearbeitbare Objekte einfügen. Unterstützt werden alle Emojis, die auch der Windows-Emoji-Dialog (`Win+.`) anbietet (außer Länderflaggen, siehe Entscheidungen). Eine Auswahl im Eigenschaftenpanel bietet Suche (deutsch und englisch), Kategorien, „Zuletzt verwendet“, Favoriten und Hauttöne. Emoji-Objekte lassen sich drehen und spiegeln, außerdem gibt es eine Deckkraft. Die Drehung ist das erste drehbare Objekt im Editor.

## Voraussetzungen

- M0–M12 abgeschlossen

## Entscheidungen (mit dem Auftraggeber geklärt)

| Thema | Entscheidung |
|---|---|
| Grafikquelle | Systemschrift **Segoe UI Emoji**, farbig gerendert über DirectWrite/Direct2D (Color-Font-Unterstützung, COLRv0/COLRv1). Es werden keine Grafiken mitgeliefert. Das Aussehen folgt der Windows-Version (Windows 10 flach, Windows 11 Fluent) |
| Umfang | Alle vollständig qualifizierten Emojis aus Unicode `emoji-test.txt`, inkl. Hauttönen, ZWJ-Kombinationen, Tastenkappen und Text-Emojis in Farbdarstellung (© ® ™ ‼ …). **Nicht:** Länderflaggen (Regional-Indicator-Paare) und Unterregionsflaggen (Tag-Sequenzen), weil Segoe UI Emoji sie nur als Buchstaben darstellt. Die übrigen Flaggen (🏁 🚩 🎌 🏳️ 🏴 🏳️‍🌈 🏳️‍⚧️ 🏴‍☠️) bleiben enthalten |
| Suchdaten | Liste, Reihenfolge und Gruppen aus Unicode `emoji-test.txt`. Namen und Stichwörter deutsch/englisch aus Unicode CLDR (`annotations` + `annotationsDerived`). Die deutschen Begriffe werden um die Synonyme aus `Unicodefinder/symbols.csv` ergänzt (Zuordnung über den Basis-Codepunkt ohne `U+FE0F`). Die Suche findet beide Sprachen, unabhängig von der UI-Sprache |
| Bedienung | Neues Werkzeug **Emoji** (Taste `J`). Das Eigenschaftenpanel zeigt die Emoji-Auswahl. Klick auf die Zeichenfläche setzt das gewählte Emoji in Standardgröße, Aufziehen bestimmt die Größe. Ist ein Emoji-Objekt ausgewählt, tauscht ein Klick in der Auswahl sein Emoji aus (eine Undo-Aktion) |
| Skalieren | Eckgriffe skalieren proportional, mit `Umschalt` frei (Verzerren erlaubt). Schaltfläche „Seitenverhältnis zurücksetzen“ und Größeneingabe im Panel |
| Eigenschaften | Deckkraft, horizontal/vertikal spiegeln, Drehen (Drehgriff, `Umschalt` = 15°-Schritte, Winkeleingabe im Panel, „Drehung zurücksetzen“) |
| Hauttöne | Hautton-Leiste wie im Windows-Dialog (Standard-Gelb + 5 Töne) für alle Emojis mit Varianten. Der gewählte Ton wird in den Einstellungen gespeichert. Mit Auswahl ändert die Leiste den Hautton der ausgewählten Emojis |
| Nicht darstellbare Emojis | Die Auswahl zeigt nur Emojis, die die installierte Schrift als **ein** farbiges Zeichen darstellen kann. Objekte mit nicht darstellbaren Emojis (z. B. ein Projekt von einem neueren Windows) erscheinen als Platzhalter (gestrichelter Rahmen mit „?“), der Tooltip und das Panel nennen den Namen |
| Zuletzt verwendet / Favoriten | „Zuletzt verwendet“ (max. 30, neueste vorn, ohne Dubletten) und Favoriten (Rechtsklick → „Zu Favoriten hinzufügen/entfernen“) als eigene Kategorien ganz vorn, dauerhaft in `AppSettings` |
| Vorlagen | Keine Vorlagenliste (M12) für das Emoji-Werkzeug. Die letzte Konfiguration (Deckkraft, Standardgröße, Hautton) wird wie bei allen Werkzeugen gespeichert |

## Umfang

**Enthalten**
- Generator für die Emoji-Daten und ein eingebetteter Emoji-Katalog mit Suche
- Farbiges Rendering über DirectWrite mit Cache, gleich für Anzeige und Export
- Modellklasse `EmojiAnnotation` mit Drehung, Spiegelung und Deckkraft, Projektformat und Objekt-Zwischenablage
- Drehgriff, gedrehte Trefferprüfung und gedrehter Auswahlrahmen (zunächst nur für Emojis, aber allgemein über eine Schnittstelle angelegt)
- Werkzeug „Emoji“ mit Auswahlpanel (Suche, Kategorien, Zuletzt, Favoriten, Hautton)

**Nicht enthalten**
- Länderflaggen, eigene/benutzerdefinierte Emoji-Grafiken, animierte Emojis
- Drehung anderer Objekttypen (nur vorbereitet, siehe Technische Details)
- Emojis innerhalb von Textobjekten (Text bleibt WPF-Textrendering, also einfarbig)
- Vorlagen für das Emoji-Werkzeug

## Aufgaben

### 1. Emoji-Daten (Generator + Katalog in Core)
- [x] Generator-Skript `build/generate-emoji-data.ps1` (Aufruf mit `-SymbolsCsv "Unicodefinder/symbols.csv"`; Spalten `codepoint;name;category;char;synonyms`): lädt `emoji-test.txt` (aktuelle Unicode-Emoji-Version) sowie die CLDR-JSON-Dateien `annotations`/`annotationsDerived` für `de` und `en`, liest `symbols.csv` (Pfad als Parameter) und schreibt `emoji-data.json.gz`. Quelldateien und -versionen stehen im Kopf der Ausgabe. Das Ergebnis wird eingecheckt, der Build lädt nichts herunter
- [x] Inhalt je Emoji: Sequenz (ohne Hautton), Gruppe/Untergruppe, Reihenfolge, Name de/en (CLDR `tts`), Stichwörter de/en, CSV-Synonyme (de), Hautton-Varianten (nur einheitliche Töne, wie Windows) und Unicode-Emoji-Version. Nicht übernommen werden: `component`, `minimally-qualified`, `unqualified`, Regional-Indicator- und Tag-Flaggen
- [x] Core: `EmojiCatalog` (lädt die eingebettete Ressource einmalig, threadsicher, ohne WPF), `EmojiInfo`, `EmojiGroup`, `SkinTone` (Enum: `None`, `Light`, `MediumLight`, `Medium`, `MediumDark`, `Dark`) mit `EmojiCatalog.ApplySkinTone(sequence, tone)` und `GetBaseSequence(sequence)`
- [x] `EmojiSearch`: Normalisierung (Groß-/Kleinschreibung, Leerzeichen, Bindestriche, `ae/oe/ue/ss` ⇄ `ä/ö/ü/ß`), Wortpräfix-Treffer auf Name/Stichwörter/Synonyme beider Sprachen, mehrere Suchwörter mit UND verknüpft. Rangfolge: exakter Name > Namenspräfix > Stichwort > Synonym > Teiltreffer, bei Gleichstand Unicode-Reihenfolge. Auch die Eingabe des Emojis selbst findet es
- [x] `AppSettings`: `EmojiRecent` und `EmojiFavorites` (`StringList`), `EmojiDefaults` (Record: Deckkraft, Standardgröße, Hautton; Referenztyp wegen alter Einstellungsdateien, siehe M12)
- [x] `THIRD-PARTY-NOTICES.txt`: Unicode-Lizenz (Unicode License v3) für `emoji-test.txt`/CLDR und die Lizenz des Direct2D-Pakets ergänzen (`build/generate-notices.ps1` um einen festen Abschnitt für die Unicode-Daten erweitern)

### 2. Farbiges Rendering (Rendering)
- [x] Paket für DirectWrite/Direct2D aufnehmen (Vorschlag: `Vortice.Direct2D1` + `Vortice.DirectWrite`, MIT; Alternative: CsWin32-COM-Interop, siehe Offene Punkte), Version in `Directory.Packages.props`
- [x] `EmojiRasterizer`: rendert eine Sequenz in ein quadratisches `PixelBuffer` der gewünschten Pixelgröße (WIC-Bitmap-Renderziel, `D2D1_DRAW_TEXT_OPTIONS_ENABLE_COLOR_FONT`, unter Windows 11 COLRv1 über `ID2D1DeviceContext4`/`DrawColorBitmapGlyphRun` bzw. `TranslateColorGlyphRun`). Zentriert über die Ink-Bounds des Glyphen-Clusters, transparenter Hintergrund, nicht vormultipliziert (Konvention `PixelBuffer`)
- [x] `EmojiSupport.IsSupported(sequence)`: die Schrift enthält die Sequenz als **ein** Cluster ohne `.notdef`-Glyphe (Prüfung über das Text-Layout bzw. den Text-Analyzer). Ergebnis je Sitzung gecacht, Prüfung des ganzen Katalogs im Hintergrund beim ersten Öffnen der Auswahl
- [x] Cache: Schlüssel = Sequenz + Pixelgröße (auf Stufen gerundet, z. B. Zweierpotenzen bzw. 25-%-Schritte), LRU mit Speichergrenze (wie `EffectCache`). Anzeige rendert in der sichtbaren Größe (Zoom × DPI, max. 2048 px) und skaliert, Export rendert in der Zielgröße (max. 4096 px, darüber wird skaliert)
- [x] `AnnotationRenderer`: Zweig für `EmojiAnnotation` mit `PushTransform` (Mitte, Drehung, Spiegelung, nicht-proportionale Skalierung) und `PushOpacity`. Bei nicht darstellbarer Sequenz wird der Platzhalter gezeichnet. Anzeige und Export nutzen denselben Pfad

### 3. Modell & Drehung (Core)
- [x] `EmojiAnnotation` (Diskriminator `emoji`): `Sequence`, `Bounds` (`RectD`, ungedreht), `Rotation` (Grad, normalisiert auf [0, 360)), `FlipHorizontal`, `FlipVertical`, `Opacity`. `Bounds` als „Rahmen vor Drehung“, Drehung um die Mitte. Für Trefferprüfung und Ebenenliste berechnete Eigenschaft `RotatedBounds` (achsparallele Hülle, `[JsonIgnore]`)
- [x] Schnittstelle `IRotatableAnnotation` (`Rotation`, `Center`) als Vorbereitung für andere Typen (M5 „Ausblick Rotation“). Trefferprüfung über den invers transformierten Punkt (`HitTesting`)
- [x] `[JsonDerivedType]` in `Annotation`, Roundtrip-Test, Projekt-Golden-Datei um ein Emoji erweitern (neue Golden-Datei `golden-v1-emoji.gsts`, alte bleibt). `formatVersion` bleibt unverändert: ältere Programmversionen überspringen unbekannte Objekte mit Warnung (bestehende Konvention)
- [x] `ShapeDefaultsMap`/`ToolDefaults`-Muster für `EmojiDefaults` (Standardgröße in Bildpixeln, Standard 96)

### 4. Werkzeug & Auswahl (App)
- [x] `EmojiTool` (`Id` `emoji`, `NameKey` `Tool_Emoji`, Taste `J`, Symbol aus WPF-UI z. B. `Emoji24`) in `ToolRegistry.Create`
- [x] Platzieren: Klick setzt das Emoji mittig auf den Klickpunkt in Standardgröße. Aufziehen erzeugt ein Quadrat ab dem Startpunkt (`Umschalt` = frei, `Alt` = von der Mitte aus, wie bei den Formen). Mindestgröße 8 px. Ohne gewähltes Emoji wird das zuletzt verwendete genommen, sonst 😀. Danach Verhalten wie bei den Formwerkzeugen (Objekt ausgewählt, Werkzeug bleibt aktiv). Das Einfügen ist eine Undo-Aktion und trägt das Emoji in „Zuletzt verwendet“ ein
- [x] `SelectTool`: Eckgriffe proportional (Seitenverhältnis des aktuellen Rahmens), `Umschalt` frei. Kantengriffe skalieren frei. Griffe und Auswahlrahmen werden mitgedreht (`InteractionLayer`)
- [x] Drehgriff über der oberen Kante (Abstand zoom-unabhängig über `ToImageUnits`): Ziehen dreht um die Mitte, `Umschalt` rastet in 15°-Schritten ein. Mauszeiger „Drehen“. Ein Ziehvorgang = ein `ReplaceGeometryAction`
- [x] Mehrfachauswahl: Verschieben wie bisher. Skalieren der Gruppe skaliert Position und Größe gedrehter Emojis, die Drehung bleibt. Der Drehgriff erscheint nur bei genau einem drehbaren Objekt
- [x] Tastatur (`SelectTool.OnKeyDown`): Spiegeln und Drehen per Tastenkürzel (Belegung siehe Offene Punkte). Unabhängig davon sind beide über das Panel per Tastatur erreichbar

### 5. Eigenschaftenpanel & Emoji-Auswahl (App)
- [x] `EmojiPickerViewModel`: Suchtext (Entprellung ~150 ms), Kategorien (Favoriten, Zuletzt verwendet, dann Unicode-Gruppen ohne „Component“, Flaggen-Gruppe reduziert), gewähltes Emoji, Hautton, Befehle `Insert`, `ToggleFavorite`. Leere Kategorien (Favoriten/Zuletzt) werden ausgeblendet
- [x] `EmojiPicker` (`UserControl`): Suchfeld (`PlaceholderText`), Kategorie-Leiste mit Symbolen und Tooltips, virtualisiertes Raster (Zeilen à N Zellen über `VirtualizingStackPanel` oder `VirtualizingWrapPanel` von WPF-UI), Hautton-Leiste, Statuszeile mit Name des markierten Emojis. Vorschaubilder in 32 DIP × DPI aus dem Rasterizer, asynchron nachgeladen
- [x] Tastatur: Pfeiltasten im Raster, `Enter` fügt das markierte Emoji in die Mitte des sichtbaren Bildausschnitts ein (bzw. tauscht es bei Auswahl aus), `Strg+F`/Tippen springt ins Suchfeld, `Esc` leert die Suche
- [x] Mit Auswahl eines oder mehrerer Emoji-Objekte zusätzlich: Größe (B × H), „Seitenverhältnis zurücksetzen“, Drehung (Zahl + „Zurücksetzen“), Spiegeln horizontal/vertikal (Umschaltknöpfe), Deckkraft (vorhandener Regler). Gemischte Werte wie in `SelectionPropertiesViewModel`. Jede Änderung über `PropertyChangeAction` (merged), gesperrte Objekte unverändert
- [x] Ohne Auswahl bearbeitet das Panel die Werkzeug-Vorgabe (Deckkraft, Standardgröße, Hautton) und speichert sie (letzte Konfiguration, M12)
- [x] Platzhalter-Objekte: Panel-Hinweis „Dieses Emoji kann mit der installierten Windows-Schrift nicht dargestellt werden.“ und Name
- [x] Ebenenliste: Eintrag mit Emoji-Vorschau und lokalisiertem Namen
- [x] Alle Texte in `Strings.resx`/`Strings.de.resx`, `AutomationProperties.Name` für Rasterzellen (= Emoji-Name in UI-Sprache), Kategorien, Hauttöne und Panel-Elemente

### 6. Dokumentation
- [x] `docs/agent/architecture.md` aktualisieren (Skill `agent-architecture-doc`, Update-Modus): Emoji-Katalog, Rasterizer/DirectWrite, `IRotatableAnnotation`, Generator-Skript
- [x] `README.md`, `CHANGELOG.md`, `ShortcutCatalog` (Taste `J`)
- [x] Umsetzungsplan: M13 in Anforderungs- und Meilensteintabelle, Abhängigkeiten, Aufwand (eingetragen); Hinweis bei „Rotation einzelner Objekte“ anpassen

## Technische Details

- **Warum DirectWrite:** WPF zeichnet Farbschriften (COLR/CPAL) nur einfarbig. Direct2D mit `ENABLE_COLOR_FONT` liefert die Darstellung des Windows-Dialogs. Das Ergebnis wird als Bitmap in `PixelBuffer` übernommen, damit es in den bestehenden Render-/Exportpfad passt (`DrawImage` in `AnnotationRenderer`).
- **„Frei skalierbar“** bedeutet: Es wird immer in der benötigten Pixelgröße neu gerastert (Zoom, Export), nicht ein festes Bitmap hochskaliert. Die Rasterstufen begrenzen den Cache.
- **Threading:** Direct2D-Factory als Multithreaded-Factory. Das Rastern der Vorschaubilder läuft im Hintergrund, das Rendern für Anzeige/Export synchron mit Cache. WPF-`BitmapSource` erst auf dem UI-Thread erzeugen (`Freeze`).
- **Schriftprüfung:** Ein Emoji gilt als darstellbar, wenn das Layout genau ein Cluster mit Glyphen ≠ 0 liefert und der Cluster aus der Schrift „Segoe UI Emoji“ stammt (kein Font-Fallback). Länderflaggen werden schon im Generator ausgeschlossen, nicht erst durch die Prüfung.
- **Hautton-Logik:** Der Katalog speichert Varianten je Basis-Emoji. `ApplySkinTone` wählt die passende Variante oder liefert die Basis. Mehrpersonen-Emojis mit gemischten Tönen (🧑🏻‍🤝‍🧑🏿) werden nicht angeboten, einheitliche schon.
- **Datenmenge:** ca. 3.800 Sequenzen inkl. Varianten. Die komprimierte JSON-Ressource sollte unter 300 KB liegen, das Laden dauert < 100 ms (beim ersten Öffnen des Werkzeugs, nicht beim App-Start).
- **Drehung allgemein:** `IRotatableAnnotation` + Hilfsfunktionen (`RotationMath`: Punkt drehen, Hülle berechnen) liegen in Core. `SelectTool`, `InteractionLayer` und `HitTesting` fragen die Schnittstelle ab, sodass später weitere Typen drehbar werden können, ohne die Werkzeuge erneut umzubauen.
- **Zwischenablage:** Das Objektformat (JSON) übernimmt Emojis automatisch. Der PNG-Fallback läuft über den Renderer und ist damit farbig.
- **Kein WPF in Core:** Katalog, Suche, Hautton-Logik und Modell sind reine .NET-Typen. Rasterizer und Schriftprüfung liegen in `Rendering`.

## Tests

- `Core.Tests`: Katalog lädt, enthält 😀 ❤️ 1️⃣ 👨‍👩‍👧 🏳️‍🌈, enthält keine Länderflaggen und keine `component`-Einträge. Suche: „lachen“, „Lachen“, „smile“, „Herz“, „Hertz“ (kein Treffer), „Haende“ = „Hände“, mehrere Wörter (UND), Rangfolge, Eingabe des Emojis selbst. Hauttöne: Anwenden/Entfernen, Emoji ohne Varianten bleibt unverändert. Zuletzt-Liste (Maximum 30, Dubletten, Reihenfolge). Favoriten umschalten. `EmojiAnnotation`: JSON-Roundtrip, `Clone`, Trefferprüfung mit Drehung 0/45/90° und Spiegelung, `RotatedBounds`. Golden-Datei mit Emoji
- `App.Tests`: Platzieren per Klick/Aufziehen (Quadrat, mit `Umschalt` frei) als eine Undo-Aktion. Proportionale Eckgriffe. Drehgriff mit 15°-Raster. Gedrehtes Objekt per Klick innerhalb/außerhalb der gedrehten Fläche auswählbar. Austausch des Emojis bei Auswahl. Spiegeln/Drehen/Größe über das Panel inkl. gesperrter Objekte. Hautton-Leiste ändert die Auswahl. Letzte Konfiguration übersteht einen Neustart. Picker-ViewModel: Kategorien, Ausblenden leerer Kategorien, Entprellung, `Enter` fügt in die Ansichtsmitte ein
- Rendering (`[StaFact]`, nur wenn „Segoe UI Emoji“ installiert ist, sonst übersprungen): 😀 ergibt farbige Pixel (mindestens zwei deutlich verschiedene Farbtöne, nicht nur Graustufen). Gedrehtes/gespiegeltes Emoji im Export lageversetzt wie erwartet. Platzhalter für eine erfundene Sequenz. Export = Anzeige (gleicher Pfad). Cache-Grenze wird eingehalten
- Lokalisierungs- und Barrierefreiheitstests (`LocalizationCompletenessTests`, `AccessibilityTests`) bleiben grün

## Abnahmekriterien

- [x] Werkzeug „Emoji“ (Taste `J`) zeigt die Auswahl mit Suche, Kategorien, Zuletzt verwendet, Favoriten und Hautton-Leiste
- [x] Alle Emojis des Windows-Dialogs außer Länderflaggen sind auswählbar, in Farbe und so, wie Windows sie darstellt
- [x] Die Suche findet Emojis über deutsche und englische Begriffe sowie die Synonyme aus `symbols.csv`
- [x] Eingefügte Emojis lassen sich verschieben, proportional (mit `Umschalt` frei) skalieren, drehen, spiegeln und in der Deckkraft ändern, jeweils mit Undo/Redo
- [x] Emojis bleiben bei jeder Zoomstufe und im Export scharf (kein hochskaliertes Kleinbild)
- [x] Emojis überstehen Speichern/Laden als `.gsts` und Kopieren/Einfügen. Nicht darstellbare Emojis erscheinen als Platzhalter
- [x] Hautton, Favoriten, Zuletzt verwendet und die Werkzeug-Vorgabe überstehen einen Neustart
- [~] Bedienung per Tastatur möglich (Raster, `Enter`), Narrator liest die Emoji-Namen vor (Automationsnamen gesetzt; Vorlesen mit Narrator noch manuell zu prüfen)
- [x] Alle neuen Texte DE/EN, alle Tests grün, Build ohne Warnungen, `THIRD-PARTY-NOTICES.txt` ergänzt

## Entscheidungen bei der Umsetzung

- **Interop-Weg:** `Vortice.Direct2D1` 3.8.3 (MIT; enthält DirectWrite und WIC). Der Single-File-Publish (`win-x64`) funktioniert. Gezeichnet wird über das WIC-Renderziel (`ID2D1RenderTarget` mit `DrawTextOptions.EnableColorFont`); die Farbdarstellung (auch Windows 11, Fluent) wurde auf Windows 11 geprüft, ein `ID2D1DeviceContext4` war nicht nötig.
- **Tastenkürzel (im `SelectTool`):** `[` / `]` drehen um 15°, `Umschalt+H` / `Umschalt+V` spiegeln. `Strg+Umschalt+V` aus dem Vorschlag kollidiert mit „Einfügen“, `Strg+[` / `Strg+]` sind die Z-Reihenfolge.
- **Unicode-Version:** 18.0 (`emoji-test.txt` vom 30.04.2026); 1.661 Emojis (ohne Hautton-Varianten, davon 1.323 mit CSV-Synonymen) in 9 Gruppen, 99 KB komprimiert. Nicht darstellbare Emojis blendet die Schriftprüfung aus.
- **Abweichung vom Entwurf:** `EmojiAnnotation.Rect` ist der ungedrehte Rahmen, `Bounds` die achsparallele Hülle des gedrehten Rahmens (alle allgemeinen Verbraucher – Rubberband, Ebenenliste, Zuschnitt – bekommen so die richtigen Werte). Eine eigene Eigenschaft `RotatedBounds` entfällt. `IRotatableAnnotation` hat `Rotation`, `Center` und `Rect`.
- **Bedienung der Auswahl:** Klick wählt das Emoji (bzw. tauscht es bei ausgewähltem Emoji-Objekt aus), Doppelklick oder `Enter` fügt es in die Mitte des sichtbaren Bildausschnitts ein; Rechtsklick schaltet den Favoritenstatus um. Hat die Zeichenfläche den Fokus, gelten die normalen Tastenkürzel, im Emoji-Panel gehören Pfeiltasten, `Enter` und Tippen dem Raster bzw. der Suche.
- **Rasterung:** Die Ink-Bounds jedes Emojis werden einmal bei Schriftgröße 64 gemessen; Emojis werden an der Mitte ihrer Ink-Bounds ausgerichtet und so skaliert, dass ein Gesicht das Quadrat füllt (größere Glyphen werden verkleinert). Rastergrößen laufen in Stufen von etwa 25 % (4 … 4096 px; Anzeige höchstens 2048 px); der Cache ist ein LRU mit 48 MB.
- **Test-Anpassung:** `EffectCacheCleanupTests.SchliessenDesEditors_LeertDenEffektCache` prüft jetzt „weniger Einträge als vorher“ statt „0“, weil der Cache prozessweit von parallelen Tests mitbenutzt wird.
