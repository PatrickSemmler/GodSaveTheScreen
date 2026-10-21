# GodSaveTheScreen

Screenshot- und Annotationstool für Windows (.NET 10, WPF). Es läuft im Infobereich der Taskleiste, nimmt Bildschirmbereiche per Hotkey auf und öffnet das Ergebnis in einem Editor, in dem nicht-destruktive Objekte (Formen, Text, Freihand, Unschärfe, Nummern, Lupe …) über das Bild gelegt werden.

> Screenshots werden mit dem ersten veröffentlichten Release ergänzt.

## Funktionen

**Aufnahme**
- Rechteck, Fenster (mit Fenstererkennung), Freiform (Lasso), Vollbild (aktueller Monitor oder alle Monitore)
- Frei einstellbarer Timer, mehrere Monitore mit unterschiedlicher Skalierung
- Globale Hotkeys, Tray-Symbol, Startfenster, Windows-Sprungliste
- Nach der Aufnahme optional: in die Zwischenablage kopieren, automatisch speichern, Editor öffnen

**Editor**
- Formen (Rechteck, Ellipse, Linie, Pfeil, Dreieck, Polygon), Text, nummerierte Marker, Stift, Textmarker, Radierer, virtuelles Lineal
- Eigene Vorlagen für jedes Zeichenwerkzeug (speichern, anwenden, umbenennen, löschen); jedes Werkzeug merkt sich seine zuletzt gewählte Konfiguration
- Unschärfe (Weichzeichnen, Verpixeln, Schwärzen) und Lupe
- Farbige Emojis (alle Windows-Emojis außer Länderflaggen) mit Suche auf Deutsch und Englisch, Kategorien, „Zuletzt verwendet“, Favoriten und Hauttönen; frei skalier-, dreh- und spiegelbar, mit Deckkraft
- Alle Objekte bleiben bearbeitbar: Auswahl, Griffe, Ebenenliste, Z-Reihenfolge, Sperren, Undo/Redo
- Zuschneiden wie im Snipping Tool (nicht-destruktiv, später wieder erweiterbar), Seitenverhältnisse, Pixel-Eingabe, Einrasten, „Auf Objekte zuschneiden“, „Leeren Rand entfernen“
- Zoom und Verschieben, Bild per Drag & Drop ein- und ausgeben
- Speichern als PNG, JPEG, BMP, GIF, TIFF und JPEG XR; Laden aller von Windows (WIC) unterstützten Formate

**Projekte (`.gsts`)**
- Originalbild, Zuschnitt und alle Objekte in einer ZIP-Datei – Bearbeitung nach dem Schließen fortsetzen
- Zuletzt verwendete Dateien in Startfenster, Tray, Editor und Sprungliste
- Automatische Sicherung ungespeicherter Arbeit und Wiederherstellung nach einem Absturz

**Bedienung**
- Deutsch und Englisch, Hell/Dunkel/Hochkontrast
- Alle Funktionen per Tastatur erreichbar; `Strg+F1` zeigt alle Tastenkürzel
- Ausführliche Benutzeranleitung auf Deutsch und Englisch (HTML neben der EXE): `F1` öffnet sie, `Umschalt+F1` springt zum Abschnitt des aktuellen Werkzeugs bzw. der Einstellungsseite

## Tastenkürzel (Auszug)

| Taste | Funktion |
|---|---|
| `Strg+Umschalt+1` … `4` | Aufnahme: Rechteck / Fenster / Freiform / Vollbild (Standard, änderbar) |
| `V` `R` `E` `L` `A` `G` `P` | Auswahl, Rechteck, Ellipse, Linie, Pfeil, Dreieck, Polygon |
| `T` `M` `B` `H` `X` `U` `Z` `C` `J` | Text, Marker, Stift, Textmarker, Radierer, Unschärfe, Lupe, Zuschneiden, Emoji |
| `Strg+Z` / `Strg+Y` | Rückgängig / Wiederholen |
| `Strg+C` / `Strg+Umschalt+C` | Auswahl bzw. Bild kopieren / Bild immer kopieren |
| `Strg+S` / `Strg+Umschalt+S` | Speichern / Speichern unter (Projekt oder Bild) |
| `Tab` / `Umschalt+Tab` | nächstes / vorheriges Objekt auswählen |
| `[` / `]` | ausgewähltes Emoji um 15° drehen (`Umschalt` am Drehgriff rastet ein); `Umschalt+H` / `Umschalt+V` spiegeln |
| Pfeiltasten (+`Umschalt`) | Auswahl um 1 px (10 px) verschieben, mit `Alt` Größe ändern |
| `F1` / `Umschalt+F1` | Hilfe / Hilfe zum aktuellen Werkzeug |
| `Strg+F1` | Alle Tastenkürzel |

## Installation und Portable-Modus

Es gibt keinen Installer. Das Release ist ein ZIP mit einer einzelnen EXE (enthält die .NET-Laufzeit, keine Installation nötig):

1. ZIP entpacken und `GodSaveTheScreen.exe` starten. Die Benutzeranleitung (`GodSaveTheScreen-Hilfe.html` / `GodSaveTheScreen-Help.html`) muss im selben Ordner bleiben.
2. Standardmäßig liegen Einstellungen in `%APPDATA%\GodSaveTheScreen`, Logs und Sicherungen in `%LOCALAPPDATA%\GodSaveTheScreen`.
3. **Portabler Modus:** eine leere Datei `portable.txt` neben die EXE legen – dann liegen alle Daten im Ordner `data` daneben (z. B. für USB-Sticks).

Die EXE ist nicht signiert. Beim ersten Start zeigt Windows SmartScreen deshalb unter Umständen „Der Computer wurde durch Windows geschützt“: *Weitere Informationen → Trotzdem ausführen*. Code-Signing ist ein möglicher Folgepunkt.

Die Taste `Druck` kann unter Windows 11 vom System belegt sein (*Einstellungen → Barrierefreiheit → Tastatur → „Schaltfläche ‚Druck‘ zum Öffnen der Bildschirmaufnahme verwenden“*). Erst nach dem Ausschalten lässt sie sich in den Einstellungen von GodSaveTheScreen einem Aufnahmemodus zuweisen.

## Aus dem Quelltext bauen

Voraussetzung: [.NET 10 SDK](https://dotnet.microsoft.com/download), Windows 10 (2004) oder neuer.

```powershell
dotnet build GodSaveTheScreen.slnx          # Bauen (Warnungen sind Fehler)
dotnet test                                 # Tests (xUnit v3)
dotnet run --project src/GodSaveTheScreen.App
.\build\release.ps1                         # Tests + Publish + ZIP in artifacts\release
```

Hinweise:
- Läuft die App, sperrt sie die DLLs in `bin\`: für parallele Builds `-p:ArtifactsPath=C:/Temp/gsts` verwenden.
- WPF ist nicht trimmbar, das Release wird deshalb ohne `PublishTrimmed` veröffentlicht.
- Die Versionsnummer steht in `Directory.Build.props`; der Git-Hash wird automatisch an die `InformationalVersion` gehängt.
- Die Lizenzhinweise der NuGet-Pakete erzeugt `build\generate-notices.ps1` (`THIRD-PARTY-NOTICES.txt`).

## Dokumentation

- Benutzeranleitung: Quellen in [docs/help](docs/help/) (`de/index.html`, `en/index.html`, gemeinsames `help.css`/`help.js`); der Build bettet CSS, Skript und Bilder ein und legt die fertigen HTML-Dateien neben die EXE. Bildschirmfotos neu erzeugen: `pwsh build/generate-help-screenshots.ps1`
- [Umsetzungsplan](docs/Umsetzungsplan.md) und [Meilensteine](docs/meilensteine/)
- [Architektur für Agenten](docs/agent/architecture.md)
- [Änderungen](CHANGELOG.md)
