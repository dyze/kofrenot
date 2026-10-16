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

## Database
*Planned for 0.1, not implemented yet* ([ROADMAP.md](ROADMAP.md#01)). Model and principles: [GOAL.md](GOAL.md#data-model).

### Folder

```
<database folder>/
  kofrenot-db.json         header, plain JSON: format, database id, KDF salt + iterations, wrapped master key
  changes/
    3f/3f9c…e1.kfc         one encrypted change, named by a random id, written once
    a0/a07b…42.kfc
```

- **Only added, never modified.** A change file is written as `*.tmp` then renamed, so a reader or the sync tool never sees half a file. `.tmp` files are ignored.
- **Folders by the id's first two characters**, so no folder holds too many files.
- An unreadable file (corrupt, wrong key) is skipped and logged as a warning; the others still load.
- A header `format` newer than the app's: the database is not opened.

### Encryption

```
pass phrase ──PBKDF2-SHA256 (salt, 600 000 iterations)──► pass-phrase key
                                                             │ AES-256-GCM
master key (32 random bytes) ◄──────── unwraps ──────────────┘   (header)
   │ AES-256-GCM, new 12-byte nonce per file, AAD = file id
   ▼
change file = "KFC1" · nonce · ciphertext · tag
```

- Built into .NET (`Rfc2898DeriveBytes`, `AesGcm`): no package.
- A wrong pass phrase fails to unwrap the master key (GCM tag), so no extra check value is needed.
- Changing the pass phrase (later) rewrites the header only.
- *Remember pass phrase* stores the master key, protected by DPAPI for the current Windows user, in the local data folder, per database id. `Core` sees it through `ISecretProtector`; `App` implements it with `CryptProtectData` (P/Invoke, no package).
- The key, the pass phrase and names are never logged.

### Change content
UTF-8 JSON before encryption:

```json
{ "format": 1, "at": "2026-10-16T09:12:00Z", "op": "addNode",
  "node": { "id": "…", "type": "generic", "name": "Dune", "created": "2026-10-16T09:12:00Z" } }
{ "format": 1, "at": "…", "op": "addLink",
  "link": { "id": "…", "type": "relates-to", "from": "<node id>", "to": "<node id>", "created": "…" } }
```

- Changes are applied in `at` order (then id) to an in-memory graph. Search and preview work on it only.
- A type unknown to this version is kept and shown by its id.
- Duplicate nodes (same type and normalized name) are all kept and marked; duplicate links (same type and nodes, either direction when symmetric) count as one.
- **Live refresh**: `App` watches the folder (`FileSystemWatcher`) and calls `Refresh`, which reads only the new files. It also refreshes when the main window is shown, as watchers can miss events on file shares.

### Local snapshot
Reading thousands of small files at each start is slow, above all on a file share. So each device reads a change file **once**, then keeps the result in a snapshot:

```
start ─► snapshot (local) ─► graph + ids of the changes it holds
           │ missing, unreadable or other format: start from an empty graph
           ▼
      list changes/ (names only) ─► read the files not in the snapshot ─► apply
           ▼
      save snapshot (after loading, and after each refresh or addition that changed it)
```

- Stored in the local data folder, never in the database folder: `cache\<database id>.kfs` (`AppFiles`). It belongs to one device, so it never has to be synced or merged.
- Encrypted with the master key like a change file (`KFS1` · nonce · ciphertext · tag, AAD = database id), so it reveals nothing more than the database.
- Written as `*.tmp`, then renamed. It is only a cache: deleting it costs one full read.
- A change file that arrives late, older than changes already applied, is fine: in 0.1 changes only add, so their order does not change the result.
- Later, when changes modify nodes, ten edits of one node are still ten files, each read once per device; packing them into bigger files is an open question ([GOAL.md](GOAL.md#open-questions), *Packs*).

### Input
`Dune relates to Frank Herbert` → link; `Dune` → node.

```
text ──InputParser──► NodeRequest(name) | LinkRequest(left, type, right)
           │ first alias of a link type, as whole words, with text on both sides
           ▼
     GraphSearch (word prefix, case- and accent-insensitive) ──► Preview
           ▼ Enter
     AddPlan: nodes to create, link to create, duplicates to confirm ──► Database.Append
```

- Names are normalized for comparison only: trimmed, spaces collapsed, lower case, accents removed. The name is stored as typed (trimmed).
- A name containing a link alias cannot be entered (no quoting).

### Folders (planned)

| `kofrenot.Core` | Content |
| --- | --- |
| `Graph/` | `Node`, `Link`, `NodeType`, `LinkType`, `TypeCatalog` (built-in types and aliases), `Graph` (in-memory state) |
| `Storage/` | `DatabaseHeader`, `KeyVault` (KDF, wrap / unwrap), `ChangeCodec` (encrypt / decrypt, JSON), `SnapshotStore`, `Database` (open, create, append, refresh), `ISecretProtector` |
| `Input/` | `NameKey` (normalization), `InputParser`, `GraphSearch`, `AddPlan` |

## Logging
- `Core` writes through `ILog` (`log.Info(category, message)`); only `App` knows Serilog.
- Category: the area (`App`, `Action`, `Settings`, `Window`, `Update`, ...).
- **Info**: every action the user triggers, start/stop. **Warning/Error**: failures. **Debug**: details.
- Never logged: node names, note or file content, typed text, keys, pass phrases. Ids and counts are fine.
- Local only: 3 files x 1 MB, plus the last 1000 entries in memory for the log window.
- The file is never restarted: each start appends to `kofrenot.log`, and a new file begins only at 1 MB. Every entry is flushed at once, so what happened before a crash stays in the file. The log window shows the current run only.

## Versioning
`<Version>` in `Directory.Build.props` is the only place the version is written. It is stamped into every assembly (AssemblyVersion, FileVersion, InformationalVersion), read back through `AppVersion` in `Core`, and passed to the installer by `build/installer.cmd`. Rules: [GOAL.md](GOAL.md#versioning).
