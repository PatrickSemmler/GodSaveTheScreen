# Änderungen

Das Format orientiert sich an [Keep a Changelog](https://keepachangelog.com/de/1.1.0/), die Versionen folgen [SemVer](https://semver.org/lang/de/).

## [Unveröffentlicht]

### Neu
- **Info-Seite (1.10.2.0):** Einstellungen → Info zeigt den Entwickler und einen Link zur Projektseite auf GitHub (https://github.com/PatrickSemmler/GodSaveTheScreen) mit Quellcode, Releases und Fehlermeldungen.
- **Setup: Zielordner und Startmenü (1.10.1.0):** Setup-EXE (unter „Optionen“) und MSI lassen den Zielordner wählen (je Installationsart, mit „Durchsuchen“) und die Startmenü-Verknüpfung abwählen. Still: `InstallFolder=…`/`StartMenuShortcut=0` (Setup-EXE) bzw. `INSTALLFOLDER=…`/`STARTMENUSHORTCUT=0` (MSI). Updates übernehmen ohne Angabe Ordner und Startmenü-Wahl der vorhandenen Installation.
- **Setup und MSI (1.10.0.0):** Neben dem ZIP gibt es je Plattform (x64, ARM64) eine Setup-EXE und ein MSI-Paket, beide auf Deutsch und Englisch. Installation „Nur für mich“ (ohne Administratorrechte) oder „Für alle Benutzer“, Startmenü-Verknüpfung, Dateizuordnung `.gsts` und Eintrag unter „Apps“. Für die Softwareverteilung still installierbar (`msiexec /i … /qn ALLUSERS=1` bzw. `…-Setup.exe /quiet InstallScope=PerMachine`). Neuere Versionen ersetzen die vorhandene Installation. „Mit Windows starten“ bleibt eine Einstellung der App. Release-Dateien liegen jetzt in `dist\`.
- **Lizenz (1.9.0.0):** GodSaveTheScreen steht unter der Apache License 2.0 (Dateien `LICENSE` und `NOTICE`). Die Lizenzseite (`GodSaveTheScreen-Lizenz.html`/`-License.html` neben der EXE, Einstellungen → Info → „Lizenz und Drittanbieter-Lizenzen anzeigen“) enthält eine Kurzfassung, Hinweise für Forks, den Lizenztext und alle Drittanbieter-Komponenten mit ihren Lizenzen.
- **Benutzeranleitung (1.8.0.0):** Ausführliche Hilfe auf Deutsch und Englisch als eigenständige HTML-Datei neben der EXE (Inhaltsverzeichnis, Suche, helles/dunkles Design, Bildschirmfotos der Anwendung). `F1` oder die Hilfe-Schaltflächen in Startfenster, Editor, Einstellungen und Tray öffnen sie in der Programmsprache; `Umschalt+F1` springt zum Abschnitt des aktiven Werkzeugs, der ausgewählten Objekte, der Ebenenliste bzw. der angezeigten Einstellungsseite.
- **Farbige Emojis (M13):** Neues Werkzeug „Emoji“ (`J`). Alle Emojis des Windows-Emoji-Dialogs außer Länderflaggen, gezeichnet in Farbe mit der Windows-Schrift „Segoe UI Emoji“ (Aussehen je nach Windows-Version) und bei jeder Zoomstufe sowie im Export scharf. Die Auswahl im Eigenschaftenpanel bietet Suche (deutsch und englisch, mit Synonymen), Kategorien, „Zuletzt verwendet“, Favoriten (Rechtsklick) und Hauttöne. Klick setzt ein Emoji, Aufziehen bestimmt die Größe; Emojis lassen sich proportional oder frei skalieren, **drehen** (Drehgriff, `Umschalt` = 15°-Raster, Eingabe in Grad), spiegeln und mit Deckkraft versehen. Ein Klick in der Auswahl tauscht das Emoji eines ausgewählten Objekts aus. Emojis überstehen Speichern/Laden (`.gsts`) und Kopieren/Einfügen; Emojis, die die installierte Schrift nicht darstellen kann, erscheinen als Platzhalter.
- **Werkzeugvorlagen (M12):** Alle Zeichenwerkzeuge (Formen, Linie/Pfeil, Stift, Textmarker, Text, Marker, Unschärfe, Lupe) haben eine Vorlagenliste im Eigenschaftenpanel statt Schaltflächen. Aktuelle Einstellungen lassen sich unter eigenem Namen speichern (Dialog mit Autovervollständigung, Rückfrage beim Überschreiben ohne Beachtung der Groß-/Kleinschreibung); Vorlagen können umbenannt und gelöscht werden, die mitgelieferten Textvorlagen ebenfalls („Standardvorlagen wiederherstellen“).
- **Letzte Konfiguration:** Unschärfe, Lupe sowie Glättung und Druck des Stifts werden wie die übrigen Werkzeuge dauerhaft gemerkt.

### Geändert
- Die Info-Seite der Einstellungen zeigt Copyright und Lizenz statt der bisherigen (unvollständigen) Paketliste.
- Die Tastenkürzel-Übersicht liegt jetzt auf `Strg+F1` (bisher `F1`, `Strg+?` funktioniert weiterhin).
- Das Dateiformat `.gsts` kennt den neuen Objekttyp `emoji` (`formatVersion` bleibt 1; ältere Programmversionen überspringen unbekannte Objekte mit Warnung).
- Textvorlagen übernehmen jetzt den ganzen Stil inklusive Schriftart und Ausrichtung.

## [1.0.0] – 2026-10-19

Erste Version mit vollem Funktionsumfang (Meilensteine M0–M11).

### Neu
- **Info-Seite (1.10.2.0):** Einstellungen → Info zeigt den Entwickler und einen Link zur Projektseite auf GitHub (https://github.com/PatrickSemmler/GodSaveTheScreen) mit Quellcode, Releases und Fehlermeldungen.
- **Aufnahme:** Rechteck, Fenster, Freiform, Vollbild (aktueller Monitor / alle Monitore), Timer, globale Hotkeys, Tray-Symbol, Startfenster (M1–M3).
- **Editor:** Bilder laden und speichern (PNG, JPEG, BMP, GIF, TIFF, JPEG XR), Zwischenablage, Zoom/Pan, Drag & Drop (M4).
- **Objekte:** Formen, Text, nummerierte Marker, Stift, Textmarker, Radierer, Lineal, Unschärfe/Verpixeln/Schwärzen, Lupe – alle bearbeitbar, mit Undo/Redo, Ebenenliste und Z-Reihenfolge (M5–M8).
- **Zuschneiden** wie im Snipping Tool: nicht-destruktiv, Griffe, Seitenverhältnisse, Pixel-Eingabe, Einrasten, „Auf Objekte zuschneiden“, „Leeren Rand entfernen“ (M9).
- **Projektformat `.gsts`:** Originalbild, Zuschnitt und alle Objekte in einer ZIP-Datei; „Exportieren…“ für Bildformate; Liste „Zuletzt verwendet“ in Startfenster, Tray, Editor und Sprungliste; automatische Sicherung mit Wiederherstellung nach einem Absturz (M10).
- **Feinschliff (M11):**
  - Übersicht aller Tastenkürzel (`F1`), einmaliger Erststart-Hinweis, lokalisierte Tastenkürzel-Anzeige.
  - Barrierefreiheit: Bedienung der Objekte per Tastatur (`Tab`, Pfeiltasten, `Alt+Pfeil`), Automationsnamen für Symbolschaltflächen, Hochkontrast-Farben auf der Zeichenfläche.
  - Härtung: Warnung vor sehr großen Bildern, Hotkeys bei gesperrter Sitzung ignoriert, Absturzmeldung mit „Log-Ordner öffnen“, Effekt-Cache wird beim Schließen des Editors geleert, virtualisierte Ebenenliste.
  - Release: portable Single-File-EXE (ReadyToRun) per `build\release.ps1`, Versionierung mit Git-Hash, `THIRD-PARTY-NOTICES.txt`.

### Bekannte Einschränkungen
- Die EXE ist nicht signiert (SmartScreen-Hinweis beim ersten Start).
- Keine Dateizuordnung für `.gsts` im System; kein Installer.
