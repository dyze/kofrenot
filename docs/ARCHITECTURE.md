# kofrenot: architecture

## Projects

```
kofrenot.App   (WPF, Windows only)
│  main window (input box), creation dialog, settings window, pass phrase dialog, log window, changelog window, tray icon
│  VaultSession (opens, watches and adds to the vault through the creation dialog)
│  adapters: SerilogFileLog, SingleInstance, NativeWindows + WindowPlacer (window positions), GitHubReleaseFeed, Browser,
│            DpapiProtector, GlobalHotkey, VaultWatcher
│
│  implements ▼  IReleaseFeed · ILog · ISecretProtector
│
kofrenot.Core  (no Windows dependency, fully unit-tested on any OS)
   Graph: KnowledgeGraph · Node · Link · TypeCatalog · Change
   Storage: Vault · VaultFolder · VaultFormat · SecretBox · KeyWrap · SnapshotStore · KeyMemory
   Input: InputParser · GraphSearch · AddPlanner · InputPresenter · CreationForm
   AppFiles · AppVersion · Changelog · ProjectLinks
   SettingsStore · SettingsCatalog · Hotkey
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
| *(root)* | `AppVersion`; `AppFiles` (where every file of kofrenot is, including `keys.json` and the snapshots in `cache\`: the only place their names are written, listed in Settings > About; `AppFiles.For` picks the data folder, see [Portable copy](#portable-copy)); `Changelog` (reads the text shown by Settings > About); `ProjectLinks` (website, issues, releases) |
| `Settings/` | `SettingsStore` (`settings.json`: theme, debug logging, update check, vault folder, hotkey), `Hotkey`, `SettingsCatalog` (the entries of the Settings window, their tabs, and the search that finds them) |
| `Graph/` | `Node`, `Link`, `NodeType`, `LinkType`, `TypeCatalog` (built-in node and link types, link aliases), `NameKey` (the form names are compared in), `Change` (`NodeAdded`, `LinkAdded`), `KnowledgeGraph` (in-memory state, built by applying changes) |
| `Storage/` | `VaultFormat` (+ `NewerVaultFormatException`), `SecretBox` (AES-GCM seal / open), `KeyWrap` (KDF, wrap / unwrap the master key), `ChangeCodec` (change ⇄ sealed JSON), `VaultHeader`, `VaultFolder` (layout, inspect, create), `SnapshotStore`, `Vault` (open, append, refresh), `AtomicFile` (write as `.tmp`, then rename), `KeyMemory` (remembered master keys, `keys.json`), `ISecretProtector` |
| `Input/` | `InputParser` (text → `NodeRequest` / `LinkRequest`), `GraphSearch` (`SearchHit`), `AddPlanner` (`AddChoices` → `AddPlan`: `NodePlan`, `LinkPlan`, `PlannedSide`), `InputPresenter` (the input box: preview, result rows, homonym choices), `CreationForm` (the creation screen: types, status, what exists), `PlanText` (wording shared by both) |
| `Logging/` | `ILog` (with `NullLog`), `LogEntry`, `RingLog`, `CompositeLog`, `FilteredLog` |
| `Placement/` | Window positions: `WindowPlacement`, `PlacementRules`, `PlacementStore` (see [Window positions](#window-positions)) |
| `Updates/` | The daily update check, see [Updates](#updates): `UpdateChecker`, `Release`, `IReleaseFeed`, `UpdateStore` |

| `kofrenot.App` | Content |
| --- | --- |
| *(root)* | `App.xaml`: startup and wiring; `VaultSession` (the open vault: create, unlock, watch, `Create` (creation dialog, then add), forget the remembered key) |
| `Views/` | Windows: `MainWindow` (input box, shows `InputPresenter`), `CreationDialog` (shows a `CreationForm`), `SettingsWindow` (tabs General, Vault, Appearance, Logging, About), `PassphraseDialog`, `LogWindow`, `ChangelogWindow` (Settings > About > *Show changelog*); `MessageDialog` (messages and questions in the app's theme, instead of the Windows `MessageBox`); `AppTheme` |
| `Tray/` | `TrayIcon` |
| `Platform/` | Implementations of the `Core` interfaces (`GitHubReleaseFeed`, `DpapiProtector`), `Browser` (opens web pages), `SingleInstance`, `GlobalHotkey` (`RegisterHotKey`), `VaultWatcher` (`FileSystemWatcher`), and window placement (`NativeWindows`, `WindowPlacer`) |
| `Logging/` | `LogHub`, `SerilogFileLog` |
| `Assets/` | `kofrenot.ico` (drawn by `build/make_icon.py`) |

## Stack
.NET 10, C#, WPF with its built-in Fluent theme for light and dark (`Application.ThemeMode`, `AppTheme`). WinForms only for the tray icon (`NotifyIcon`). xUnit for tests.

| Package | Project | Use |
| --- | --- | --- |
| `Serilog`, `Serilog.Sinks.File` | App | Log file |
| `coverlet.msbuild` | Tests | Coverage of `Core`, build fails below 95 % lines |
| `dotnet-reportgenerator-globaltool` | Local tool (`.config/dotnet-tools.json`) | HTML coverage report (`build.cmd`) |

The update check uses .NET's own `HttpClient` (`GitHubReleaseFeed` in App): kofrenot's only network access so far. Encryption uses .NET's own `AesGcm` and `Rfc2898DeriveBytes` (Core), and `ProtectedData` (DPAPI, part of the Windows desktop framework, App): no package.

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
 ├─ VaultSession (KeyMemory + DpapiProtector)
 ├─ MainWindow (+ WindowPlacer "Main") ─► UpdateChecker (background, once a day)
 ├─ GlobalHotkey (Ctrl+Alt+K by default) ─► MainWindow shown
 ├─ TrayIcon: Show · Settings · Log window · Quit
 ├─ MainWindow shown
 └─ once idle: VaultSession.Open ─► remembered key, or PassphraseDialog (create / unlock) ─► Vault.Open + VaultWatcher
```

- Closing the main window (or Esc with an empty input box) hides it; kofrenot keeps running in the notification area until *Quit* (`ShutdownMode=OnExplicitShutdown`).
- A hotkey another program already uses is not registered: logged as a warning at start, refused in Settings > General (the previous one stays).
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
- The log window also keeps its column widths (`WindowPlacement.Columns`, given to `WindowPlacer.Attach`); saved widths are dropped if the number of columns changed.
- Opened from Settings, the log window comes in front and stays usable while Settings is open, without being always on top.
- What is restored depends on the window (`RestoreMode`): the main and log windows their position and size, Settings its size only: as a dialog, it opens centered on the main window (`PlacementRules.CenterOn`).

## Vault
Model and principles: [GOAL.md](GOAL.md#data-model).

### Folder

```
<vault folder>/
  kofrenot-vault.json      header, plain JSON: format, vault id, KDF salt + iterations, wrapped master key
  changes/
    3f/3f9c…e1.kfc         one encrypted change, named by a random id, written once
    a0/a07b…42.kfc
```

- **Only added, never modified.** A change file is written as `*.tmp` then renamed, so a reader or the sync tool never sees half a file. `.tmp` files are ignored.
- **Folders by the id's first two characters**, so no folder holds too many files.
- An unreadable file (corrupt, wrong key, still being copied) is skipped and logged once as a warning, then tried again at each refresh; the others still load. Other files (`desktop.ini`, ...) are ignored.
- A header or change file in a newer format: see [Format](#format).
- `VaultFolder.Inspect`: a new vault is only created in an empty (or missing) folder; a folder holding other files is refused, so nothing of the user's is mixed with it.

### Format
One number for the whole vault, `VaultFormat.Current` (1 so far), written in the header and in every change file (`format`). It is not the app version: it only changes when what is written in a vault breaks.

| Found | This kofrenot |
| --- | --- |
| format ≤ `VaultFormat.Current` | reads it |
| header with a newer format | does not open the vault |
| change file with a newer format (another device has a newer kofrenot) | closes the vault, at opening or at a refresh |
| format < 1 | the file is damaged: skipped like an unreadable file |

- `NewerVaultFormatException` (a `NotSupportedException`) carries the format found. `VaultSession.NewerFormat` keeps it: the main window and Settings > Vault say to upgrade, and *Get the latest kofrenot* opens the releases page.
- A breaking change (something an older kofrenot would misread or not read) raises `VaultFormat.Current`. The owner decides it first (`CLAUDE.md`). Changes that only add something older versions ignore safely (such as an unknown type, shown by its id) do not.
- Not done yet: rewriting a vault in a newer format (migration). It comes with the first breaking change.

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
- *Remember pass phrase* stores the master key (not the pass phrase: it opens at once and survives a pass phrase change), protected by DPAPI for the current Windows user, in `keys.json` in the local data folder, per vault id (`KeyMemory`). `Core` sees DPAPI through `ISecretProtector`; `App` implements it with `ProtectedData` (`DpapiProtector`).
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
- `Vault` is not thread-safe: `App` uses it from the UI thread.
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

- Stored in the local data folder, never in the vault folder: `cache\<vault id>.kfs` (`AppFiles`, `Vault.SnapshotOf`). It belongs to one device, so it never has to be synced or merged.
- Encrypted with the master key like a change file (`KFS1` · nonce · ciphertext · tag, AAD = vault id), so it reveals nothing more than the vault.
- Written as `*.tmp`, then renamed. It is only a cache: deleting it costs one full read.
- A change file that arrives late, older than changes already applied, is fine: in 0.1 changes only add, so their order does not change the result.
- Later, when changes modify nodes, ten edits of one node are still ten files, each read once per device; packing them into bigger files is an open question ([GOAL.md](GOAL.md#open-questions), *Packs*).

### Input
`Dune relates to Frank Herbert` → link; `Dune` → node.

```
text ──InputParser──► NodeRequest(name) | LinkRequest(left, type, right)
           │ first alias of a link type, as whole words, with text on both sides
           ▼
     GraphSearch (word prefix, case- and accent-insensitive) ──► Preview     (InputPresenter)
           ▼ Enter: request + homonyms chosen (AddChoices)
     CreationForm: node types, link type, what exists ──► AddPlan      (CreationDialog, modal)
           ▼ Enter
     VaultSession.Create ──► Vault.Append
```

- `VaultSession.Create(owner, request, choices)` opens the creation dialog over any window, so later features can reuse it. It returns false when cancelled or not written.
- The dialog only shows a `CreationForm`. Each type change makes a new plan, so a different link type can turn `AlreadyLinked` into a link that can be added. Only a new node gets a type box: an existing node keeps its type.
- The types offered come from `TypeCatalog` (only *generic* and *relates to* in 0.1).

- Names are compared by `NameKey`: lower case, accents removed, spaces collapsed. They are stored as typed, trimmed, with single spaces.
- A link phrase counts after at least one word; with nothing after it, the link is still being typed (`LinkProblem.SecondNodeMissing`).
- `GraphSearch`: exact names first, then names starting with the text, then the others, alphabetically; at most 20. Homonyms are listed oldest first, marked `¹`, `²`, ... (`SearchHit.Mark`).
- `AddPlanner` builds the `AddPlan` shown as preview: the nodes to create, the link, and `NeedsConfirmation` when a node of that name exists (warned about on the creation screen). An existing link is not added again (`LinkProblem.AlreadyLinked`). `AddChoices` gives, for each side, the chosen node among homonyms (the oldest by default) and the type of a new node (*generic* by default). A link from a node to itself is refused.
- A name containing a link alias cannot be entered (no quoting).

## Logging
- `Core` writes through `ILog` (`log.Info(category, message)`); only `App` knows Serilog.
- Category: the area (`App`, `Action`, `Settings`, `Window`, `Update`, ...).
- **Info**: every action the user triggers, start/stop. **Warning/Error**: failures. **Debug**: details.
- Never logged: node names, note or file content, typed text, keys, pass phrases. Ids and counts are fine.
- Local only: 3 files x 1 MB, plus the last 1000 entries in memory for the log window.
- The file is never restarted: each start appends to `kofrenot.log`, and a new file begins only at 1 MB. Every entry is flushed at once, so what happened before a crash stays in the file. The log window shows the current run only.

## Versioning
`<Version>` in `Directory.Build.props` is the only place the version is written. It is stamped into every assembly (AssemblyVersion, FileVersion, InformationalVersion), read back through `AppVersion` in `Core`, and passed to the installer by `build/installer.cmd`. Rules: [GOAL.md](GOAL.md#versioning).
