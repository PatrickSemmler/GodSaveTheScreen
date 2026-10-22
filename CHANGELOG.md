# Änderungen

Das Format orientiert sich an [Keep a Changelog](https://keepachangelog.com/de/1.1.0/). Die Versionen sind vierstellig (`Major.Minor.Patch.Fix`): neue Funktion → 2. Stelle, erweiterte Funktion → 3. Stelle, Fehlerbehebung → 4. Stelle.

## [Unveröffentlicht]

## [1.10.2.0] – 2026-10-22

Erste öffentliche Version auf GitHub ([Release](https://github.com/PatrickSemmler/GodSaveTheScreen/releases/tag/v1.10.2.0)). Enthält alle Änderungen seit 1.0.0; die Versionsnummer in Klammern nennt den Stand, mit dem eine Änderung hinzukam.

### Neu
- **Info-Seite (1.10.2.0):** Einstellungen → Info zeigt den Entwickler und einen Link zur Projektseite auf GitHub (https://github.com/PatrickSemmler/GodSaveTheScreen) mit Quellcode, Releases und Fehlermeldungen.
- **Setup: Zielordner und Startmenü (1.10.1.0):** Setup-EXE (unter „Optionen“) und MSI lassen den Zielordner wählen (je Installationsart, mit „Durchsuchen“) und die Startmenü-Verknüpfung abwählen. Still: `InstallFolder=…`/`StartMenuShortcut=0` (Setup-EXE) bzw. `INSTALLFOLDER=…`/`STARTMENUSHORTCUT=0` (MSI). Updates übernehmen ohne Angabe Ordner und Startmenü-Wahl der vorhandenen Installation.
- **Setup und MSI (1.10.0.0):** Neben dem ZIP gibt es je Plattform (x64, ARM64) eine Setup-EXE und ein MSI-Paket, beide auf Deutsch und Englisch. Installation „Nur für mich“ (ohne Administratorrechte) oder „Für alle Benutzer“, Startmenü-Verknüpfung, Dateizuordnung `.gsts` und Eintrag unter „Apps“. Für die Softwareverteilung still installierbar (`msiexec /i … /qn ALLUSERS=1` bzw. `…-Setup.exe /quiet InstallScope=PerMachine`). Neuere Versionen ersetzen die vorhandene Installation. „Mit Windows starten“ bleibt eine Einstellung der App. Release-Dateien liegen jetzt in `dist\`.
- **Lizenz (1.9.0.0):** GodSaveTheScreen steht unter der Apache License 2.0 (Dateien `LICENSE` und `NOTICE`). Die Lizenzseite (`GodSaveTheScreen-Lizenz.html`/`-License.html` neben der EXE, Einstellungen → Info → „Lizenz und Drittanbieter-Lizenzen anzeigen“) enthält eine Kurzfassung, Hinweise für Forks, den Lizenztext und alle Drittanbieter-Komponenten mit ihren Lizenzen.
- **Benutzeranleitung (1.8.0.0):** Ausführliche Hilfe auf Deutsch und Englisch als eigenständige HTML-Datei neben der EXE (Inhaltsverzeichnis, Suche, helles/dunkles Design, Bildschirmfotos der Anwendung). `F1` oder die Hilfe-Schaltflächen in Startfenster, Editor, Einstellungen und Tray öffnen sie in der Programmsprache; `Umschalt+F1` springt zum Abschnitt des aktiven Werkzeugs, der ausgewählten Objekte, der Ebenenliste bzw. der angezeigten Einstellungsseite.
- **Bilder als Objekte (1.7.0.0):** Neues Werkzeug „Bild“ (`I`) für alle WIC-Formate und SVG; außerdem „Bild einfügen…“, Drop auf die Zeichenfläche und `Strg+Umschalt+V`. Bilder sind drehbar, spiegelbar und haben Deckkraft, Schatten und Kontur, die der Transparenz des Bildes folgen. Bei animierten GIFs und mehrseitigen TIFFs wird das Einzelbild per Vorschau gewählt. Bilder werden im Projekt eingebettet.
- **Kontur für Pfeil und Linie (1.6.2.0):** Farbe, Stärke und Unschärfe einer Kontur um Linie und Spitzen, auch bei gestrichelten Linien.
- **Objekte drehen (1.6.1.0):** Rechteck, Ellipse, Dreieck, Polygon, Stift/Textmarker und Text lassen sich über einen Drehgriff (`Umschalt` = 15°-Schritte), `[`/`]` oder den Panel-Abschnitt „Drehung“ drehen; der Winkel bleibt jederzeit änderbar.
- **Sprechblase (1.6.0.0):** Neues Werkzeug (`K`) mit abgerundetem Rechteck, Ellipse oder Gedankenblase, Text direkt in der Blase, frei ziehbarer (auf Wunsch gebogener) Spitze, Leuchtrand, Schatten, Drehung und Vorlagen.
- **Standard wiederherstellen (1.5.0.0):** Schaltfläche im Eigenschaftenpanel setzt die Einstellungen des aktiven Werkzeugs auf die eingebauten Standardwerte zurück.
- **Stempel (1.4.0.0, 1.4.1.0):** Neues Werkzeug (`S`) mit Rahmen (Rechteck, abgerundet, doppelt, oval), mehrzeiligem Text und realistisch abgenutztem Stempel-Look (ausgefranste Kanten, Fehlstellen, Kratzer), Neigung, Tinten-Effekt, Schnellauswahl häufiger Texte sowie Fett, Kursiv, Unter- und Durchstreichen. Standardschrift Bahnschrift Bold.
- **Bildbereiche entfernen (1.3.0.0):** Werkzeug „Bereich auswählen und entfernen“ (`W`: Rechteck, Ellipse, Lasso, Zauberstab, weiche Kante, Auswahl umkehren) und „Hintergrundradierer“ (`D`) machen Teile des Screenshots transparent – nicht-destruktiv, mit „Ganzen Screenshot wiederherstellen“ und „Auf sichtbaren Bereich zuschneiden“.
- **Schatten und weiche Kontur:** Schattenfarbe, -größe und -unschärfe für Formen, Linien/Pfeile, Text, Marker und Emojis; weiche Textkontur.
- **Zeichenfläche wächst mit:** Objekte, die über den Bildrand hinausragen, werden nicht mehr abgeschnitten; der zusätzliche Bereich erscheint als Schachbrett und wird transparent exportiert (JPG/BMP: weiß).
- **Farbige Emojis (M13):** Neues Werkzeug „Emoji“ (`J`). Alle Emojis des Windows-Emoji-Dialogs außer Länderflaggen, gezeichnet in Farbe mit der Windows-Schrift „Segoe UI Emoji“ (Aussehen je nach Windows-Version) und bei jeder Zoomstufe sowie im Export scharf. Die Auswahl im Eigenschaftenpanel bietet Suche (deutsch und englisch, mit Synonymen), Kategorien, „Zuletzt verwendet“, Favoriten (Rechtsklick) und Hauttöne. Klick setzt ein Emoji, Aufziehen bestimmt die Größe; Emojis lassen sich proportional oder frei skalieren, **drehen** (Drehgriff, `Umschalt` = 15°-Raster, Eingabe in Grad), spiegeln und mit Deckkraft versehen. Ein Klick in der Auswahl tauscht das Emoji eines ausgewählten Objekts aus. Emojis überstehen Speichern/Laden (`.gsts`) und Kopieren/Einfügen; Emojis, die die installierte Schrift nicht darstellen kann, erscheinen als Platzhalter.
- **Werkzeugvorlagen (M12):** Alle Zeichenwerkzeuge (Formen, Linie/Pfeil, Stift, Textmarker, Text, Marker, Unschärfe, Lupe) haben eine Vorlagenliste im Eigenschaftenpanel statt Schaltflächen. Aktuelle Einstellungen lassen sich unter eigenem Namen speichern (Dialog mit Autovervollständigung, Rückfrage beim Überschreiben ohne Beachtung der Groß-/Kleinschreibung); Vorlagen können umbenannt und gelöscht werden, die mitgelieferten Textvorlagen ebenfalls („Standardvorlagen wiederherstellen“).
- **Letzte Konfiguration:** Unschärfe, Lupe sowie Glättung und Druck des Stifts werden wie die übrigen Werkzeuge dauerhaft gemerkt.

### Geändert
- Die Info-Seite der Einstellungen zeigt Copyright und Lizenz statt der bisherigen (unvollständigen) Paketliste.
- Die Tastenkürzel-Übersicht liegt jetzt auf `Strg+F1` (bisher `F1`, `Strg+?` funktioniert weiterhin).
- Das Dateiformat `.gsts` kennt den neuen Objekttyp `emoji` (`formatVersion` bleibt 1; ältere Programmversionen überspringen unbekannte Objekte mit Warnung).
- Textvorlagen übernehmen jetzt den ganzen Stil inklusive Schriftart und Ausrichtung.
- Eigenschaftenpanel überarbeitet (1.7.1.0): aufklappbare Karten mit Kurzfassung im Kopf, Ein/Aus-Schalter für Kontur, Schatten, Hintergrund und Leuchten, einheitliche Zahlenfelder mit Pfeilen und Mausrad. Der Auf-/Zuklapp-Zustand wird gemerkt.
- Das Dateiformat `.gsts` kennt die Objekttypen `stamp`, `callout` und `image` sowie optional eine Transparenzmaske (`mask.bin`); `formatVersion` bleibt 1.
- Die Versionsnummer ist vierstellig (1.2.0.0); Release-Dateien heißen z. B. `GodSaveTheScreen-1.10.2.0-win-x64.zip`.

### Behoben
- Die Standard-Hotkeys `Strg+Umschalt+1`–`4` registrierten eine falsche Taste; die Aufnahme „Rechteck“ löste schon bei `Strg+Umschalt` aus (1.7.1.1).
- Die Werkzeugsymbole in der Hilfe waren abgeschnitten (1.8.0.1); die Hilfedateien landeten bei einem normalen Build in einem Unterordner statt neben der EXE (1.8.0.2).
- Fehler beim Speichern der Einstellungen (z. B. gesperrte Datei) werden protokolliert, statt unbemerkt zu bleiben.
- Temporäre Bilddateien aus „Bild ziehen“ werden nach einem Tag aufgeräumt.

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
