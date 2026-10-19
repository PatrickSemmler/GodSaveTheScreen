# M0 – Grundgerüst & Infrastruktur

← [Umsetzungsplan](../Umsetzungsplan.md) · Weiter: [M1](M01-Aufnahme-Kern.md)

## Ziel

Eine leere, aber vollständig verdrahtete Anwendung: Solution mit allen Projekten, Generic Host mit DI, WPF-UI-Theme, Logging, Lokalisierung, Einstellungsablage, Single-Instance und lauffähige Testprojekte. Alle späteren Meilensteine bauen ausschließlich auf diesen Mustern auf.

## Voraussetzungen

- .NET SDK 10.0.x, Visual Studio 2026 / Rider
- Git-Repository (angelegt)

## Umfang

**Enthalten**
- Solution `GodSaveTheScreen.slnx` mit `src/` und `tests/` gemäß Umsetzungsplan 3.1
- `Directory.Build.props`, `Directory.Packages.props`, `.editorconfig`
- Generic Host, DI, Logging, globale Fehlerbehandlung
- WPF-UI-Theme (System/Hell/Dunkel) mit leerem Hauptfenster
- Lokalisierungsinfrastruktur DE/EN
- `ISettingsService` mit JSON-Ablage (normal + portable)
- Single-Instance mit Weiterleitung der Kommandozeile

**Nicht enthalten**
- Jegliche Aufnahme- oder Editorfunktion

## Aufgaben

### 1. Solution & Build
- [x] `dotnet new sln --format slnx`, Projekte anlegen:
  - `src/GodSaveTheScreen.Core` (classlib, `net10.0`)
  - `src/GodSaveTheScreen.Capture` (classlib, `net10.0-windows`)
  - `src/GodSaveTheScreen.Rendering` (classlib, `net10.0-windows`, `UseWPF`)
  - `src/GodSaveTheScreen.App` (`wpf`, `net10.0-windows`, `OutputType=WinExe`)
  - `tests/GodSaveTheScreen.Core.Tests`, `tests/GodSaveTheScreen.App.Tests` (xUnit v3)
- [x] Projektreferenzen nur in erlaubter Richtung (App → Rendering/Capture → Core; Tests → jeweiliges Projekt)
- [x] `Directory.Build.props`: `Nullable=enable`, `ImplicitUsings=enable`, `LangVersion=latest`, `TreatWarningsAsErrors=true`, `AnalysisLevel=latest-recommended`, gemeinsame `Version`/`Company`/`Product`
- [x] `Directory.Packages.props` mit `ManagePackageVersionsCentrally=true` und allen Paketen aus dem Umsetzungsplan 2.1
- [x] `.editorconfig` (File-scoped Namespaces, `var` bei offensichtlichem Typ, `_camelCase` für private Felder)
- [x] `app.manifest` in App: `PerMonitorV2`, `longPathAware`, supportedOS Windows 10/11

### 2. Host & DI
- [x] `App.xaml`: `StartupUri` entfernen, `ShutdownMode="OnExplicitShutdown"`
- [x] `App.xaml.cs`: `Host.CreateApplicationBuilder()` in `OnStartup`, Registrierungen in Erweiterungsmethoden je Bereich (`services.AddCore()`, `services.AddCapture()`, `services.AddRendering()`, `services.AddAppServices()`)
- [x] `OnExit`: Host stoppen und entsorgen
- [x] `IWindowFactory`: Fenster werden per DI erzeugt, ViewModel als `DataContext` (kein separater `IViewLocator` nötig)

### 3. Logging & Fehlerbehandlung
- [x] Serilog: rollierende Datei `%LOCALAPPDATA%\GodSaveTheScreen\logs\log-.txt` (7 Tage), Mindestlevel per Einstellung
- [x] Handler für `DispatcherUnhandledException`, `AppDomain.UnhandledException`, `TaskScheduler.UnobservedTaskException` → loggen + Fehlerdialog (`IDialogService.ShowError`)

### 4. UI-Basis
- [x] WPF-UI-Ressourcen in `App.xaml` (`ui:ThemesDictionary`, `ui:ControlsDictionary`)
- [x] `ThemeService`: Theme gemäß Einstellung anwenden, auf Systemwechsel reagieren (`SystemThemeWatcher`)
- [x] Hauptfenster als `ui:FluentWindow` mit Titelleiste (statt Platzhalter-`MainWindow` direkt das `StartWindow` aus M1)
- [x] App-Icon (`Assets/app.ico`, mehrere Größen) und Tray-Variante (hell/dunkel) als Platzhalter

### 5. Lokalisierung
- [x] `Resources/Strings.resx` (Englisch, neutral) + `Resources/Strings.de.resx`; `PublicResXFileCodeGenerator`
- [x] Markup-Extension `LocExtension` (`{l:Loc Key}`) liefert `Strings.ResourceManager.GetString(key)`; im Designer Fallback auf Schlüssel
- [x] Kultur beim Start aus Einstellung (`System` | `de` | `en`) auf `CultureInfo.DefaultThreadCurrentUICulture` setzen
- [x] Test, der prüft, dass jeder Schlüssel in beiden `.resx` vorhanden ist

### 6. Einstellungen
- [x] `Core/Settings/AppSettings.cs` (record/Klasse mit Defaults): Sprache, Theme, LogLevel – weitere Felder folgen in späteren Meilensteinen
- [x] `ISettingsService` (`Current`, `SaveAsync()`, Ereignis `Changed`)
- [x] `JsonSettingsService`: `System.Text.Json` mit `JsonSerializerContext` (Source Generator), atomisches Schreiben (temp-Datei + `File.Replace`), unbekannte/fehlende Felder tolerant
- [x] `IAppPaths`: ermittelt Daten-/Log-Verzeichnis; `portable.txt` neben der EXE → `.\data\`

### 7. Single Instance
- [x] `SingleInstanceService`: `Mutex("Local\\GodSaveTheScreen-{SID}")`
- [x] Zweiter Start sendet Argumente per `NamedPipeClientStream` an die erste Instanz und beendet sich
- [x] Erste Instanz: `NamedPipeServerStream`-Schleife, Weitergabe über `WeakReferenceMessenger` (`CommandLineReceivedMessage`)
- [x] Kommandozeilen-Parser (einfach, eigen): `--capture <rect|window|full|freeform>`, `--delay <s>`, `<Dateipfade>` – Auswertung folgt in M1/M3/M4

## Technische Details

- **Warum Generic Host statt manueller DI:** Konfiguration, Logging und Lebenszyklus (`IHostedService` für Tray/Hotkeys/Pipe-Server) kommen einheitlich; Services können `IHostApplicationLifetime` nutzen.
- **Warum `Core` ohne WPF:** Datenmodell, Undo und Serialisierung sind so schnell und ohne STA testbar; verhindert, dass WPF-Typen (z. B. `Color`, `Point`) im Projektformat landen.
- `CommunityToolkit.Mvvm` ist auch im Core erlaubt (keine WPF-Abhängigkeit).

## Tests

- `Core.Tests`: `JsonSettingsService` Roundtrip, fehlende Datei → Defaults, beschädigte Datei → Defaults + Log, Portable-Pfadermittlung
- `App.Tests`: Ressourcen-Vollständigkeit DE/EN, Kommandozeilen-Parser

## Abnahmekriterien

- [x] `dotnet build` ohne Warnungen, `dotnet test` grün
- [x] App startet, zeigt Fluent-Hauptfenster im System-Theme, schreibt Logdatei
- [ ] Sprache per Einstellungsdatei auf `en`/`de` umschaltbar (sichtbar am Fenstertitel)
- [x] Zweiter Start beendet sich sofort; die erste Instanz loggt die übergebenen Argumente
- [ ] Mit `portable.txt` landen Einstellungen in `.\data\`

## Hinweise

- `docs/agent/architecture.md` nach M0 gegen den realen Code verifizieren und Kopfzeile auf den Commit setzen.
