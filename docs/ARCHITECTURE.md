# kofrenot: architecture

## Projects

```
kofrenot.App   (WPF, Windows only)
│  main window, settings window, log window, changelog window, tray icon
│  adapters: SerilogFileLog, SingleInstance, NativeWindows + WindowPlacer (window positions), GitHubReleaseFeed
│
│  implements ▼  IReleaseFeed · ILog
│
kofrenot.Core  (no Windows dependency, fully unit-tested on any OS)
   AppFiles · AppVersion · Changelog · ProjectLinks
   SettingsStore · SettingsCatalog
   PlacementRules · PlacementStore
   UpdateChecker · UpdateStore · Release
   ILog · RingLog · CompositeLog · FilteredLog
```

- All domain logic goes in `Core`. When it needs Windows, it takes an interface defined in `Core`, and `App` supplies the implementation.
- `App.xaml.cs` wires everything together at startup.
- Tests: `tests/kofrenot.Core.Tests` (xUnit), `Core` only.

### Folders
One namespace per folder (`Kofrenot.Core.Settings`, `Kofrenot.UI.Views`, ...). The tests use the same folders as `Core`, and import every `Core` namespace once in their `.csproj`.

| `kofrenot.Core` | Content |
| --- | --- |
| *(root)* | `AppVersion`; `AppFiles` (where every file of kofrenot is: the only place their names are written, listed in Settings > About; `AppFiles.For` picks the data folder, see [Portable copy](#portable-copy)); `Changelog` (reads the text shown by Settings > About); `ProjectLinks` (website, issues, releases) |
| `Settings/` | `SettingsStore` (`settings.json`: theme, debug logging, update check), `SettingsCatalog` (the entries of the Settings window, their tabs, and the search that finds them) |
| `Logging/` | `ILog` (with `NullLog`), `LogEntry`, `RingLog`, `CompositeLog`, `FilteredLog` |
| `Placement/` | Window positions: `WindowPlacement`, `PlacementRules`, `PlacementStore` (see [Window positions](#window-positions)) |
| `Updates/` | The daily update check, see [Updates](#updates): `UpdateChecker`, `Release`, `IReleaseFeed`, `UpdateStore` |

| `kofrenot.App` | Content |
| --- | --- |
| *(root)* | `App.xaml`: startup and wiring |
| `Views/` | Windows: `MainWindow`, `SettingsWindow` (tabs Appearance, Logging, About), `LogWindow`, `ChangelogWindow` (Settings > About > *Show changelog*); `MessageDialog` (messages in the app's theme, instead of the Windows `MessageBox`); `AppTheme` |
| `Tray/` | `TrayIcon` |
| `Platform/` | Implementations of the `Core` interfaces (`GitHubReleaseFeed`), `SingleInstance`, and window placement (`NativeWindows`, `WindowPlacer`) |
| `Logging/` | `LogHub`, `SerilogFileLog` |
| `Assets/` | `kofrenot.ico` (drawn by `build/make_icon.py`) |

## Stack
.NET 10, C#, WPF with its built-in Fluent theme for light and dark (`Application.ThemeMode`, `AppTheme`). WinForms only for the tray icon (`NotifyIcon`). xUnit for tests.

| Package | Project | Use |
| --- | --- | --- |
| `Serilog`, `Serilog.Sinks.File` | App | Log file |
| `coverlet.msbuild` | Tests | Coverage of `Core`, build fails below 95 % lines |
| `dotnet-reportgenerator-globaltool` | Local tool (`.config/dotnet-tools.json`) | HTML coverage report (`build.cmd`) |

The update check uses .NET's own `HttpClient` (`GitHubReleaseFeed` in App): kofrenot's only network access so far.

Shared build settings and the version are in `Directory.Build.props`; output goes to `artifacts/`. Builds are deterministic and `DeterministicSourcePaths` replaces the local repo path with `/_/` in DLLs, their embedded debug info (`DebugType=embedded`: no `.pdb` files) and Source Link data, so no local path (or user name) ships. The installer is built with [Inno Setup](https://jrsoftware.org/isinfo.php) (`build/installer.cmd`, `build/installer/kofrenot.iss`), a tool installed on the PC, not a package.

Publishing is self-contained, so the PC needs no .NET runtime (installing one needs administrator rights, and the portable zip must run on any PC). `PublishSingleFile` packs the runtime and every managed DLL into `kofrenot.exe`; the native DLLs stay next to it, so nothing is unpacked at start. No trimming: WPF and WinForms do not support it. `SatelliteResourceLanguages=en` leaves out the translated framework messages.

| File | Why |
| --- | --- |
| `kofrenot.exe` | The app and the .NET runtime |
| `wpfgfx_cor3.dll`, `PresentationNative_cor3.dll`, `D3DCompiler_47_cor3.dll`, `PenImc_cor3.dll`, `vcruntime140_cor3.dll` | WPF's native code |
| `CHANGELOG.md` | See [Changelog](#changelog) |

## Startup and windows

```
App.OnStartup
 ├─ SingleInstance.TryClaim ── another copy runs? ─► ask it to show its window, exit
 ├─ AppFiles.For ─► SettingsStore ─► AppTheme ─► LogHub
 ├─ MainWindow (+ WindowPlacer "Main") ─► UpdateChecker (background, once a day)
 ├─ TrayIcon: Show · Settings · Log window · Quit
 └─ MainWindow shown
```

- Closing the main window hides it; kofrenot keeps running in the notification area until *Quit* (`ShutdownMode=OnExplicitShutdown`).
- Settings is a modal dialog over the main window. The log window can be opened in front of it and stays open after it closes.

## Portable copy
- `AppFiles.For` picks the data folder: `<exe folder>\data` when `portable.txt` (`AppFiles.PortableMarker`) is next to kofrenot.exe, else `%LOCALAPPDATA%\kofrenot`. The start line of the log says `(portable)`.
- `build\installer.cmd` copies the published app to `artifacts\installer\portable\kofrenot`, adds the marker and zips that folder (`Compress-Archive`). The update check links to the release page, which has both files.

## Changelog
[CHANGELOG.md](../CHANGELOG.md), at the repo root, in the [Keep a Changelog](https://keepachangelog.com/en/1.1.0/) format (workflow in `CLAUDE.md`). `kofrenot.App.csproj` copies it next to kofrenot.exe (build and publish), so the setup and the zip ship it; *Show changelog* in Settings > About opens it as text in `ChangelogWindow` (`Changelog.Read`, at each opening), and the setup's last page offers to open it in Notepad.

## Updates
- `UpdateChecker` asks `IReleaseFeed` (GitHub's latest release, `ProjectLinks.LatestReleaseApi`) at most once a day, when *Check for updates* is on. Nothing is downloaded: the user is shown the release page.
- The Settings button is marked until the newer release is seen: opening Settings > About acknowledges it (`ShouldNotify`, `Acknowledge`); a later release or another installed version marks it again. The About tab is marked while a newer release exists.
- `UpdateStore` (`updates.json`) is state, not a setting.

## Window positions
- Saved in `windows.json` by window name (`Main`, `Settings`, `Log`) when a window is hidden or closed, restored when it is first shown. Kept out of `settings.json`: it is state, not a setting.
- Stored in screen pixels (WPF units change with each monitor's scaling), as the monitor's device name (`\\.\DISPLAY2`, steadier than a monitor number) and the position relative to its work area.
- `PlacementRules.Fit` (in `Core`, tested) makes a saved position visible again: back on its monitor, or on the primary one if it is gone, then shrunk to the work area and moved inside it.
- `PlacementRules.KeepVisible` does the same for a window on screen. `WindowPlacer.KeepOnScreen` applies it each time a window is shown and after a scaling change (`DpiChanged`). It checks the visible frame (without the invisible resize borders), so a window snapped to an edge is left alone.
- `App` reads the monitors and window rectangles through Win32 (`NativeWindows`); WinForms' `Screen` is not used, so WinForms stays limited to the tray.
- A maximized window keeps its normal size, to be restored to it.
- What is restored depends on the window (`RestoreMode`): the main and log windows their position and size, Settings its size only: as a dialog, it opens centered on the main window (`PlacementRules.CenterOn`).

## Logging
- `Core` writes through `ILog` (`log.Info(category, message)`); only `App` knows Serilog.
- Category: the area (`App`, `Action`, `Settings`, `Window`, `Update`, ...).
- **Info**: every action the user triggers, start/stop. **Warning/Error**: failures. **Debug**: details.
- Never logged: note or file content, typed search text, secrets.
- Local only: 3 files x 1 MB, plus the last 1000 entries in memory for the log window.

## Versioning
`<Version>` in `Directory.Build.props` is the only place the version is written. It is stamped into every assembly (AssemblyVersion, FileVersion, InformationalVersion), read back through `AppVersion` in `Core`, and passed to the installer by `build/installer.cmd`. Rules: [GOAL.md](GOAL.md#versioning).
