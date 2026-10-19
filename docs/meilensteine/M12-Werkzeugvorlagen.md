# M12 – Werkzeugvorlagen & letzte Konfiguration

← [M11](M11-Feinschliff-Release.md) · [Umsetzungsplan](../Umsetzungsplan.md)

## Ziel

Die Schnellvorlagen des Textwerkzeugs (M6) werden zu einer allgemeinen, vom Benutzer erweiterbaren Vorlagenfunktion für alle Zeichenwerkzeuge ausgebaut. Zusätzlich merkt sich jedes Werkzeug seine zuletzt gewählte Konfiguration dauerhaft.

## Voraussetzungen

- M0–M11 abgeschlossen

## Entscheidungen (mit dem Auftraggeber geklärt)

| Thema | Entscheidung |
|---|---|
| Werkzeuge | Alle Werkzeuge mit Stileinstellungen: Formen/Linien/Pfeile (alle `UsesShapeStyle`-Werkzeuge), Stift, Textmarker, Text, Nummernmarker, Unschärfe, Lupe. **Nicht:** Auswahl, Radierer, Lineal, Zuschneiden |
| Geltung | Pro Werkzeug: jedes Werkzeug hat eine eigene Vorlagenliste und eigene Namen (Rechteck und Ellipse getrennt) |
| Mitgelieferte Textvorlagen | Beschriftung, Warnung, Notiz, Hinweis erscheinen als normale Vorlagen: umbenennen, löschen und überschreiben erlaubt; Schaltfläche „Standardvorlagen wiederherstellen“ legt gelöschte/umbenannte Standardvorlagen wieder an, ohne eigene zu verändern |
| Speicherort | Einstellungsdatei (`AppSettings`, wandert im Portable-Modus mit) |
| Umfang einer Vorlage | Ganzer Stil des Werkzeugs, beim Text inkl. Schriftart, Schnitt und Ausrichtung (ändert das bisherige Verhalten, bei dem Schriftart/Ausrichtung beim Anwenden erhalten blieben) |
| Quelle beim Speichern | Panel-Stand: bei Auswahl die gemeinsamen Werte der Auswahl (gemischte Werte werden aus der aktuellen Werkzeug-Vorgabe ergänzt), sonst die Werkzeug-Vorgabe |
| Anwenden | Wie bisher: mit Auswahl auf die ausgewählten (nicht gesperrten) Objekte als eine Undo-Aktion, ohne Auswahl auf die Werkzeug-Vorgabe |
| Anzeige | Liste (`ListBox`) mit Name und kleiner Stilvorschau statt Schaltflächen; Klick wendet an; darunter Schaltflächen Speichern, Umbenennen, Löschen für den markierten Eintrag |

## Umfang

**Enthalten**
- Datenmodell und Persistenz der Vorlagen je Werkzeug
- Vorlagenliste im Eigenschaftenpanel aller Zeichenwerkzeuge
- Dialog „Als Vorlage speichern“ mit Autovervollständigung, Überschreiben-Rückfrage (Groß-/Kleinschreibung ignorierend)
- Umbenennen und Löschen von Vorlagen
- Dauerhaft gespeicherte letzte Konfiguration jedes Werkzeugs

**Nicht enthalten**
- Werkzeugübergreifende Vorlagen, Import/Export von Vorlagen, Sortieren per Drag & Drop (Liste alphabetisch)

## Aufgaben

### 1. Datenmodell & Persistenz (Core)
- [x] `ToolPreset` (`Name`, Stilwert) und `ToolPresetStore`: Werkzeug-ID → Liste der Vorlagen; Wertgleichheit wie `ShapeDefaultsMap`, damit `AppSettings` vergleichbar bleibt
- [x] Stilwerte je Werkzeugart serialisierbar: `ShapeStyle`, `TextStyle`, `MarkerStyle` sowie neue Records für Freihand (`FreehandOptions` + Form-Stil des Stifts), Unschärfe (`BlurStyle`: Modus, Stärke, Form, Weichkante, Schwärzungsfarbe) und Lupe (`MagnifierStyle`: Zoom, Form, Rand, Linie/Quellrahmen/Basisbild/Glättung)
- [x] `AppSettings`: neue Eigenschaften für Vorlagenspeicher sowie letzte Konfiguration für Freihand, Unschärfe und Lupe (Form, Text und Marker werden bereits über `ToolDefaults` gespeichert); Schema-Version/Migration: fehlende Felder = Standardwerte
- [x] Regeln des Vorlagennamens zentral: Trim, nicht leer, maximale Länge (z. B. 60), Vergleich mit `StringComparison.CurrentCultureIgnoreCase`
- [x] Mitgelieferte Standardvorlagen als Quelle für „Erststart“ und „Standardvorlagen wiederherstellen“ (Text: bisherige vier Vorlagen; andere Werkzeuge: leer oder wenige sinnvolle, siehe Offene Punkte)

### 2. Dienst (App)
- [x] `IToolPresetService` (`GetPresets(toolId)`, `Save(toolId, name, style)`, `Rename`, `Delete`, `RestoreDefaults`, `Exists(toolId, name)`, Änderungsereignis); speichert über `ISettingsService` asynchron wie `ToolDefaultsService`
- [x] `ToolDefaultsService` um Freihand-, Unschärfe- und Lupenvorgaben erweitern; `EditorContext.Freehand`, `BlurTemplate`, `MagnifierTemplate` werden beim Öffnen eines Editorfensters daraus initialisiert und bei Änderung zurückgeschrieben (bisher nur je Fenster)
- [x] Letzte Konfiguration: jede Änderung im Panel ohne Auswahl wird gespeichert; auch beim Anwenden einer Vorlage auf die Vorgabe

### 3. ViewModel & Panel
- [x] Gemeinsames `PresetListViewModel` (je aktives Werkzeug) mit `Items`, `SelectedItem`, Befehlen `Apply`, `Save`, `Rename`, `Delete`, `RestoreDefaults`; angebunden an `TextPropertiesViewModel`, `MarkerPropertiesViewModel`, `SelectionPropertiesViewModel`, `ToolOptionsViewModel`
- [x] Bisherige `TextPreset`-Schaltflächen im `PropertyPanel` durch die Vorlagenliste ersetzen (gemeinsames `UserControl` `PresetList`)
- [x] Listeneintrag mit kleiner Vorschau (Stil als Miniatur, z. B. Linie/Form/„Abc“), Tooltip mit Name
- [x] Anwenden je Stilart: Mapping Stil → Auswahl (nutzt `StyleApplier`), Rückgabe der Werkzeug-Vorgabe; Auswahl aus gemischten Objekttypen wirkt nur auf Objekte des passenden Typs
- [x] Löschen mit Rückfrage; Umbenennen mit demselben Namensdialog (Überschreiben-Rückfrage bei Kollision mit einer anderen Vorlage, Ziel-Vorlage wird dann ersetzt)

### 4. Dialog „Als Vorlage speichern“
- [x] `PresetNameDialog` (modal, an Editorfenster): Eingabefeld, editierbare `ComboBox` oder `TextBox` mit Vorschlagsliste für Autovervollständigung aus den vorhandenen Vorlagennamen **dieses Werkzeugs** (Präfix-/Teiltreffer, Groß-/Kleinschreibung ignorierend)
- [x] `Enter` = Speichern, `Esc` = Abbrechen; Speichern deaktiviert bei leerem/zu langem Namen
- [x] Existiert der Name (case-insensitiv, auch bei nur abweichender Groß-/Kleinschreibung, egal ob getippt oder aus der Liste gewählt): Rückfrage „Eine Vorlage mit dem Namen ‚X‘ existiert bereits. Soll sie überschrieben werden?“; bei Ja wird die bestehende Vorlage ersetzt und übernimmt die **neu eingegebene** Schreibweise; bei Nein erscheint der Dialog erneut
- [x] Alle Texte in `Strings.resx`/`Strings.de.resx`, `AutomationProperties.Name` für alle Bedienelemente

### 5. Dokumentation
- [x] `docs/agent/architecture.md` aktualisieren (Skill `agent-architecture-doc`, Update-Modus): Vorlagen, Persistenz der letzten Konfiguration
- [x] M6-Dokument: Verweis, dass die Vorlagen in M12 erweitert wurden; `README.md` und `CHANGELOG.md` ergänzen
- [x] Umsetzungsplan: M12 in der Meilensteintabelle (bereits eingetragen)

## Technische Details

- Vorlagenvergleich ausschließlich über eine zentrale Methode, damit Dialog, Dienst und Umbenennen identisch prüfen.
- Persistenz-Beschränkung: nur Werte, die in `Core` ohne WPF-Typen darstellbar sind (Farben als `ArgbColor`).
- Reihenfolge der Liste: alphabetisch (kulturabhängig, ignoriert Groß-/Kleinschreibung).
- Die Vorlagenliste ist sichtbar, sobald ein unterstütztes Werkzeug aktiv ist oder eine passende Auswahl besteht (wie `IsApplicable` der bestehenden ViewModels).
- Migration: bestehende Einstellungen ohne Vorlagenspeicher legen beim ersten Start die Standardvorlagen an.

## Tests

- `Core.Tests`: Namensvergleich (`Rot`/`rot`/`ROT`, Umlaute, Leerzeichen am Rand), Speichern/Überschreiben/Umbenennen/Löschen, Wertgleichheit des Speichers, JSON-Roundtrip aller Stilarten, Migration alter Einstellungen
- `App.Tests`: Vorlage speichern aus Panel-Stand (mit und ohne Auswahl, gemischte Werte), Anwenden auf Auswahl als eine Undo-Aktion, gesperrte Objekte unverändert, Anwenden auf Werkzeug-Vorgabe, Listen je Werkzeug getrennt, „Standardvorlagen wiederherstellen“ lässt eigene Vorlagen unberührt, letzte Konfiguration wird nach Neustart (neue Settings-Instanz) wiederhergestellt – für Form, Stift, Textmarker, Text, Marker, Unschärfe, Lupe
- Dialog-ViewModel: Autovervollständigung nur mit Namen des aktuellen Werkzeugs, Überschreiben-Rückfrage bei jeder Schreibweise, Abbrechen ändert nichts
- Lokalisierungs- und Barrierefreiheitstests (`LocalizationCompletenessTests`, `AccessibilityTests`) bleiben grün

## Abnahmekriterien

- [x] Jedes unterstützte Werkzeug zeigt eine Vorlagenliste statt Schaltflächen; Klick wendet die Vorlage an
- [x] „Als Vorlage speichern“ öffnet den Namensdialog mit Autovervollständigung der Namen des aktuellen Werkzeugs
- [x] Vorhandener Name (auch mit abweichender Groß-/Kleinschreibung) löst die Überschreiben-Rückfrage aus; „Nein“ ändert nichts
- [x] Vorlagen lassen sich umbenennen und löschen, auch die mitgelieferten; „Standardvorlagen wiederherstellen“ funktioniert
- [x] Vorlagen und die letzte Konfiguration jedes Werkzeugs überstehen einen Neustart der Anwendung
- [x] Vorlagen eines Werkzeugs erscheinen nicht bei anderen Werkzeugen
- [x] Alle neuen Texte DE/EN, alle Tests grün, Build ohne Warnungen

## Geklärte Punkte

- Schreibweise nach dem Überschreiben: die neu eingegebene wird übernommen.
- Mitgelieferte Vorlagen gibt es nur für Text (Beschriftung, Warnung, Notiz, Hinweis).
- Keine Begrenzung der Anzahl der Vorlagen je Werkzeug.

## Umsetzungsstand und Abweichungen

- Namensvergleich über `StringComparison.OrdinalIgnoreCase` (Analyzer CA1309 verbietet kulturabhängige Vergleiche); Umlaute werden ohne Beachtung der Groß-/Kleinschreibung gleichgesetzt (`ä`/`Ä`), `ß` und `SS` gelten als verschieden.
- Autovervollständigung: editierbare `ComboBox` (`IsTextSearchEnabled`, Präfix-Ergänzung) mit den Namen des Werkzeugs; der Dialog selbst (`PresetNameDialog`) ist nicht automatisiert getestet, die Namenslogik über `IDialogService.AskPresetName`.
- Es gibt eine Vorlagenliste am Ende des Eigenschaftenpanels (`PresetList`), nicht je Abschnitt. Mit Auswahl richtet sich die Liste nach dem Objekttyp der (einheitlichen) Auswahl, bei gemischten Objektarten ist sie ausgeblendet. Linie und Pfeil sind getrennte Werkzeuge mit getrennten Listen; bei einer ausgewählten Linie zählt das aktive Werkzeug, sonst der Pfeil, wenn das Ende eine Spitze hat.
- Die Glättung/der Druck des Stifts gehören zur Stift-/Textmarker-Vorlage; die Option „Textmarker hinter Objekten“ ist dokumentweit und nicht Teil der Vorlage.
- Neue Felder `AppSettings.BlurDefaults`, `MagnifierDefaults`, `FreehandDefaults` sind Referenztypen: alte Einstellungsdateien ohne diese Felder liefern sonst `0`/`false` statt der Standardwerte (`Normalized` ersetzt `null`).
- Offen zur **manuellen Prüfung:** Aussehen und Bedienung der Liste/des Dialogs im laufenden Editor (Tab-Reihenfolge, Hochkontrast, Narrator).
- Der Test `EffectCacheCleanupTests.SchliessenDesEditors_LeertDenEffektCache` (M11) schlägt im Gesamtlauf auch ohne M12 fehl (gemeinsamer statischer Cache, reihenfolgeabhängig); einzeln läuft er durch.
