# kofrenot: architecture

## Projects

```
kofrenot.App   (WPF, Windows only)
│  main window (input box, node details, note editor, dataset panel), creation dialog, settings window, pass phrase dialog, log window, changelog window, tray icon
│  VaultSession (opens, watches and adds to the vault through the creation dialog)
│  adapters: SerilogFileLog, SingleInstance, NativeWindows + WindowPlacer (window positions), GitHubReleaseFeed, Browser,
│            DpapiProtector, GlobalHotkey, VaultWatcher
│
│  implements ▼  IReleaseFeed · ILog · ISecretProtector
│
kofrenot.Core  (no Windows dependency, fully unit-tested on any OS)
   Graph: KnowledgeGraph · Node · Link · TypeCatalog · LinkAliases · Change
   Storage: Vault · VaultFolder · VaultFormat · SecretBox · ChunkedBox · KeyWrap · SnapshotStore · NoteFiles · DatasetFiles · KeyMemory
   Input: InputParser · GraphSearch · AddPlanner · InputPresenter · CreationForm
   Browse: NodeBrowser · NodeDetails · PinPlanner · RecentNodes
   Notes: NotePlanner · MarkdownFormat
   Datasets: DatasetPlanner · DatasetType · TextPreview · ByteSize
   AppFiles · AppVersion · RunningCopy · Changelog · ProjectLinks
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
| *(root)* | `AppVersion`; `RunningCopy` + `AppCopy` (another copy running: same or not, and the warning); `AppFiles` (where every file of kofrenot is, including `keys.json` and the snapshots in `cache\`: the only place their names are written, listed in Settings > About; `AppFiles.For` picks the data folder, see [Portable copy](#portable-copy)); `Changelog` (reads the text shown by Settings > About); `ProjectLinks` (website, issues, releases) |
| `Settings/` | `SettingsStore` (`settings.json`: theme, debug logging, update check, vault folder, hotkey), `Hotkey`, `SettingsCatalog` (the entries of the Settings window, their tabs, and the search that finds them) |
| `Graph/` | `Node`, `Link`, `NodeType`, `LinkType`, `LinkVerb` (a link type read one way), `TypeCatalog` (built-in node and link types, link aliases; `LinkName`: how a link reads from one of its nodes), `LinkAliases` (the user's aliases, see [Aliases and removals](#aliases-and-removals)), `RemovableChanges`, `NameKey` (the form names are compared in), `Change` (`NodeAdded`, `LinkAdded`, `NodeRenamed`, `AliasAdded`, `NodePinned`, `ChangesRemoved`, `ContentEdited`: `NoteEdited`, `DatasetEdited`), `DatasetFile` (a dataset version's file: name, size, SHA-256), `NodeNames` + `NameVersion` (a node's current names, see [Renames](#renames)), `ContentVersions` (the current versions of a note or dataset, see [Notes](#notes)), `KnowledgeGraph` (in-memory state, built by applying changes) |
| `Storage/` | `VaultFormat` (+ `NewerVaultFormatException`), `SecretBox` (AES-GCM seal / open), `ChunkedBox` (AES-GCM over 1 MB chunks, streamed), `KeyWrap` (KDF, wrap / unwrap the master key), `ChangeData` (a change as JSON, shared with the snapshot), `ChangeCodec` (change ⇄ sealed JSON), `VaultHeader`, `VaultFolder` (layout, inspect, create), `SnapshotStore`, `NoteFiles` (note content, `content/`), `DatasetFiles` (dataset content, `content/`: describe a file, write, export, open), `Vault` (open, append, refresh, save a note or a dataset version), `AtomicFile` (write as `.tmp`, then rename), `KeyMemory` (remembered master keys, `keys.json`), `ISecretProtector` |
| `Input/` | `InputParser` (text → `NodeRequest` / `LinkRequest`), `GraphSearch` (`SearchHit`), `AddPlanner` (`AddChoices` → `AddPlan`: `NodePlan`, `LinkPlan`, `PlannedSide`), `InputPresenter` (the input box: preview, result rows, homonym choices), `CreationForm` (the creation screen: steps, types, note content, dataset file, status, what exists), `PlanText` (wording shared by both), `RenamePlanner` (`RenamePlan`: a new name, its problem, homonyms), `AliasPlanner` (`AliasPlan`) |
| `Browse/` | `NodeBrowser` (the details screen: trail of open nodes, at most 4, the selected link of each), `NodeDetails`, `LinkRow`, `PinPlanner` (pin or unpin), `RecentNodes` (the nodes last opened on this device) |
| `Notes/` | `NotePlanner` (`NoteState`, `ConflictVersion`: load a note, plan its next version), `MarkdownFormat` (`TextEdit`: the toolbar's Markdown) |
| `Datasets/` | `DatasetPlanner` (`DatasetState`, `DatasetChoice`: load a dataset, plan an import or the version kept from a conflict), `ByteSize` (sizes as shown: 1.2 MB), `DatasetType` (`DatasetViewer`: the inner type, from the extension, and its viewer), `TextPreview` (the beginning of a text, for the viewer) |
| `Logging/` | `ILog` (with `NullLog`), `LogEntry`, `RingLog`, `CompositeLog`, `FilteredLog` |
| `Placement/` | Window positions: `WindowPlacement`, `PlacementRules`, `PlacementStore` (see [Window positions](#window-positions)) |
| `Updates/` | The daily update check, see [Updates](#updates): `UpdateChecker`, `Release`, `IReleaseFeed`, `UpdateStore` |

| `kofrenot.App` | Content |
| --- | --- |
| *(root)* | `App.xaml`: startup and wiring; `VaultSession` (the open vault: create, unlock, watch, `Create` (creation dialog, then add), forget the remembered key) |
| `Views/` | Windows: `MainWindow` (input box, shows `InputPresenter`), `NodeDetailsPanel` (in the main window, shows a `NodeBrowser`; `NoteEditor` for a note, `DatasetPanel` for a dataset: import, export, drag in and out, and `DatasetView`, its viewer), `CreationDialog` (shows a `CreationForm`, one step at a time; the focus starts on the step's type box), `MarkdownBox` (note text and toolbar), `SharedStyles` (the type scale: title, heading, body, secondary, caption, footer; shared buttons), `SettingsWindow` (tabs General, Vault, Link types (`LinkTypesSection`), Appearance, Logging, About), `PassphraseDialog`, `LogWindow`, `ChangelogWindow` (Settings > About > *Show changelog*); `MessageDialog` (messages and questions in the app's theme, instead of the Windows `MessageBox`); `AppTheme` |
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
 ├─ AppFiles.For ─► SettingsStore
 ├─ SingleInstance.TryClaim ── another copy runs? ─► the same exe and version: ask it to show its window
 │                                                 another copy: warn (RunningCopy), show it on request; exit
 ├─ AppTheme ─► LogHub
 ├─ VaultSession (KeyMemory + DpapiProtector)
 ├─ MainWindow (+ WindowPlacer "Main") ─► UpdateChecker (background, once a day)
 ├─ GlobalHotkey (Ctrl+Alt+K by default) ─► MainWindow shown
 ├─ TrayIcon: Show · Settings · Log window · Quit
 ├─ MainWindow shown
 └─ once idle: VaultSession.Open ─► remembered key, or PassphraseDialog (create / unlock) ─► Vault.Open + VaultWatcher
```

- Closing the main window (or Esc with an empty input box) hides it; kofrenot keeps running in the notification area until *Quit* (`ShutdownMode=OnExplicitShutdown`).
- A hotkey another program already uses is not registered: logged as a warning at start, refused in Settings > General (the previous one stays).
- One copy runs at a time (they would write the same files). A copy started while another runs finds it (`SingleInstance.FindRunning`: its process, exe and version). Another exe or version (installed, portable, a build) gets a warning naming both (`RunningCopy.Warning`), with *Show the running one* or *Close*; nothing is stopped. The settings are read first, for the theme of that warning.
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
- The main window can instead appear by the mouse each time it is shown (Settings > General, `MainWindowPosition`): `WindowPlacer.PlaceMainWindow`, just before `Show`, centers it on the monitor under the pointer (`PlacementRules.CenterOnMonitorAt`) or on the pointer (`CenterOnPoint`), keeping its size, inside the work area. Twice, as a monitor with another scaling resizes it. *Where I left it* and a maximized window keep the restored position. Taken from coursee.

## Vault
Model and principles: [GOAL.md](GOAL.md#data-model).

### Folder

```
<vault folder>/
  kofrenot-vault.json      header, plain JSON: format, vault id, KDF salt + iterations, wrapped master key
  changes/
    3f/3f9c…e1.kfc         one encrypted change, named by a random id, written once
    a0/a07b…42.kfc
  content/
    3f/3f9c…e1.kfn         the content of a note version, named by its change (editNote), written once
    b2/b2d4…07.kfd         the file of a dataset version, named by its change (editDataset), written once
```

- **Only added, never modified.** A change file is written as `*.tmp` then renamed, so a reader or the sync tool never sees half a file. `.tmp` files are ignored.
- **Folders by the id's first two characters**, so no folder holds too many files.
- An unreadable file (corrupt, wrong key, still being copied) is skipped and logged once as a warning, then tried again at each refresh; the others still load. Other files (`desktop.ini`, ...) are ignored.
- A header or change file in a newer format: see [Format](#format).
- `VaultFolder.Inspect`: a new vault is only created in an empty (or missing) folder; a folder holding other files is refused, so nothing of the user's is mixed with it.

### Format
One number for the whole vault, `VaultFormat.Current`, written in the header and in every change file (`format`). It is not the app version: it only changes when what is written in a vault breaks.

| Format | Since | Adds |
| --- | --- | --- |
| 1 | 0.1 | `addNode`, `addLink`, `renameNode` |
| 2 | 0.2 | directed links (`contains`, `is-a`): a 0.1 app would read them as symmetric; `addAlias`, `pinNode`, `remove`, `editNote` with the `content/` files |
| 3 | 0.3 | `editDataset` with its chunked `.kfd` content files: a 0.2 app does not know them |

| Found | This kofrenot |
| --- | --- |
| format ≤ `VaultFormat.Current` | reads it |
| header with a newer format | does not open the vault |
| change file with a newer format (another device has a newer kofrenot) | closes the vault, at opening or at a refresh |
| format < 1 | the file is damaged: skipped like an unreadable file |

- `NewerVaultFormatException` (a `NotSupportedException`) carries the format found. `VaultSession.NewerFormat` keeps it: the main window and Settings > Vault say to upgrade, and *Get the latest kofrenot* opens the releases page.
- A breaking change (something an older kofrenot would misread or not read) raises `VaultFormat.Current`. The owner decides it first (`CLAUDE.md`). Changes that only add something older versions ignore safely (such as an unknown type, shown by its id) do not.
- No migration: files are never rewritten, so a vault holds files of several formats, each read as written.

### Encryption

```
pass phrase ──PBKDF2-SHA256 (salt, 600 000 iterations)──► pass-phrase key
                                                             │ AES-256-GCM
master key (32 random bytes) ◄──────── unwraps ──────────────┘   (header)
   │ AES-256-GCM, new 12-byte nonce per file, AAD = file id
   ▼
change file = "KFC1" · nonce · ciphertext · tag
```

A dataset's file can be of any size, so it is sealed in chunks (`ChunkedBox`), read and written as a stream:

```
.kfd file = "KFD1" · nonce prefix (8 random bytes) · chunk 0 · chunk 1 · … · last chunk
chunk i   = AES-256-GCM(1 MB of the file) · tag
            nonce = prefix · i (4 bytes, big-endian)    AAD = "KFD1" · version id · last (1 byte)
```

- A chunk changed, moved, left out or added after the last one fails to open; so does a file cut after a chunk (no last one). An empty file is one empty last chunk.
- Memory use stays at about two chunks, whatever the size.

- Built into .NET (`Rfc2898DeriveBytes`, `AesGcm`): no package.
- A wrong pass phrase fails to unwrap the master key (GCM tag), so no extra check value is needed.
- Changing the pass phrase (later) rewrites the header only.
- *Remember pass phrase* stores the master key (not the pass phrase: it opens at once and survives a pass phrase change), protected by DPAPI for the current Windows user, in `keys.json` in the local data folder, per vault id (`KeyMemory`). `Core` sees DPAPI through `ISecretProtector`; `App` implements it with `ProtectedData` (`DpapiProtector`).
- The key, the pass phrase and names are never logged.

### Change content
UTF-8 JSON before encryption:

```json
{ "format": 3, "at": "2026-10-16T09:12:00Z", "op": "addNode",
  "node": { "id": "…", "type": "generic", "name": "Dune", "created": "2026-10-16T09:12:00Z" } }
{ "format": 3, "at": "…", "op": "addLink",
  "link": { "id": "…", "type": "relates-to", "from": "<node id>", "to": "<node id>", "created": "…" } }
{ "format": 3, "at": "…", "op": "renameNode",
  "rename": { "node": "<node id>", "name": "Dune (novel)", "replaces": ["<node id or rename id>", "…"] } }
{ "format": 3, "at": "…", "op": "addAlias",
  "alias": { "linkType": "contains", "reverse": true, "phrase": "is in" } }
{ "format": 3, "at": "…", "op": "pinNode", "pin": { "node": "<node id>" } }
{ "format": 3, "at": "…", "op": "editNote", "note": { "node": "<node id>", "replaces": ["<version id>", "…"] } }
{ "format": 3, "at": "…", "op": "editDataset",
  "dataset": { "node": "<node id>", "file": { "name": "survey.csv", "size": 52430, "sha256": "9f86…08" }, "replaces": ["<version id>", "…"] } }
{ "format": 3, "at": "…", "op": "remove", "removes": ["<alias or pin change id>", "…"] }
```

`renameNode` was added to format 1 before 0.1 was released, so the format number did not change (owner's decision).

- Changes are applied in `at` order (then id) to an in-memory graph. Search and preview work on it only.
- A type unknown to this version is kept and shown by its id.
- Duplicate nodes (same type and normalized name) are all kept and marked; duplicate links (same type and nodes, either direction when symmetric) count as one.
- `Vault` is not thread-safe: `App` uses it from the UI thread.
- **Live refresh**: `App` watches the folder (`FileSystemWatcher`) and calls `Refresh`, which reads only the new files. It also refreshes when the main window is shown, as watchers can miss events on file shares.

### Local snapshot
Reading thousands of small files at each start is slow, above all on a file share. So each device reads a change file **once**, then keeps the result in a snapshot:

```
start ─► snapshot (local) ─► the changes it holds, applied to the graph
           │ missing, unreadable or other format: start from an empty graph
           ▼
      list changes/ (names only) ─► read the files not in the snapshot ─► apply
           ▼
      save snapshot (after loading, and after each refresh or addition that changed it)
```

- Stored in the local data folder, never in the vault folder: `cache\<vault id>.kfs` (`AppFiles`, `Vault.SnapshotOf`). It belongs to one device, so it never has to be synced or merged.
- Encrypted with the master key like a change file (`KFS1` · nonce · ciphertext · tag, AAD = vault id), so it reveals nothing more than the vault.
- Written as `*.tmp`, then renamed. It is only a cache: deleting it costs one full read.
- A change file that arrives late, older than changes already applied, is fine: adds do not depend on order, and renames name what they replace (see [Renames](#renames)), so the result is the same in any order.
- It holds the changes themselves, as in their files (`ChangeData`, `SnapshotStore.Format` 3), so a new kind of change needs nothing more. A snapshot of an older format is not used: every change is read again once.
- Ten renames of one node are still ten files, each read once per device; packing them into bigger files is an open question ([GOAL.md](GOAL.md#open-questions), *Packs*).

### Renames
A rename never overwrites: it is a new change listing the names it **replaces**. A node's current names are those no rename replaces (`NodeNames`):

```
Dune (added) ◄─replaces── r1 "Dune (novel)"     device A
             ◄─replaces── r2 "Dune, 1965"       device B, before syncing
current names: r1, r2 ⇒ a conflict: shown name = the latest (r2), both offered in the details
r3 "Dune (novel)" replaces r1 + r2 ⇒ one current name again
```

- The result does not depend on the order the files arrive in. A rename that arrives before its node is kept and applied when the node arrives.
- `RenamePlanner` writes a rename that replaces every current name, so renaming a node in conflict, or keeping one of its names, resolves the conflict. It refuses an empty name, a name with a link phrase (the input box could not find it), and the same name when there is no conflict. Homonyms are allowed, with a warning.
- The search indexes the shown name only.

### Aliases and removals
What the user can take back (aliases, pins) is added by one change and removed by another, `remove`, listing the ids it removes. Nothing is deleted from the vault.

```
a1 addAlias "is in" → contained by      device A
a2 addAlias "is in" → contained by      device B, before syncing (a copy)
r1 remove [a1, a2]                      removes every copy
current = added − removed               (RemovableChanges; a removal may arrive first)
```

- `LinkAliases` builds `KnowledgeGraph.Types`: the built-in types with the aliases in use added to their phrases, oldest first. Everything that reads the input (`InputPresenter`, `CreationForm`, `RenamePlanner`, `NodeBrowser`) uses it.
- A phrase taken already (built in, or by an older alias: the same phrase added for two types on two devices) leaves the alias unused: it is listed struck through, and can be removed.
- An alias of a type this version does not know is kept, not shown.
- `AliasPlanner` refuses an empty phrase and one in use, and warns about the node names containing it: the input box would read them as links.
- Settings > Link types (`LinkTypesSection`) lists each type both ways, with its built-in phrases and the user's aliases (× removes). Phrases are not logged, only the link type.
- Recent nodes (`RecentNodes`, `recent.json`) are kept on each device, never in the vault: opening a node writes nothing there. `MainWindow` records each node the details show, and the nodes a creation involves, new or existing (`AddPlan.Nodes`), and passes the list to `InputPresenter.Recent`, which lists the known ones not pinned.
- Pins: `KnowledgeGraph.PinnedNodes` lists each pinned node once, the first pinned first; a node pinned on two devices has two pins, and `PinPlanner` unpins by removing both. `InputPresenter` lists them under a header (`ResultRow.IsHeader`) while the input box is empty.

### Notes
A note is a node of type `note`. Its content is Markdown, in versions: each save is a content file (`content/<id>.kfn`, sealed with the master key, "KFN1", bound to its id) and an `editNote` change of the same id naming the versions it **replaces**, as renames do.

```
save   ──NotePlanner.Save──► NoteEdited(replaces = versions loaded) ──Vault.Save──► content file, then change file
open   ──NotePlanner.Load──► latest current version's content (read now, never kept in the snapshot)
v1 ◄─ v2 (device A)
v1 ◄─ v3 (device B, before syncing) ⇒ conflict: v2, v3 offered; the next save replaces both
```

- Only metadata (the versions) is in the graph and the snapshot; content is read when the note is shown, so a vault opens as fast with long notes.
- A version whose file is not synced yet (the sync tool copied the change first) shows as such; the note cannot be edited until it arrives, so no version is replaced unseen.
- A save replaces the versions *loaded*: a version saved meanwhile on another device stays current, as a new conflict.
- `NoteEditor` (in `NodeDetailsPanel`) saves on Ctrl+S, the save button, and when the note is left (another node, back, window hidden). While it has the focus, the details leave the keys to it, except Esc (leaves the note, saving it).
- `MarkdownBox` (text and toolbar) is shared by `NoteEditor` and the creation steps. The toolbar inserts Markdown (`MarkdownFormat`): bold, italic, heading, list, link. The text is not rendered.
- Note content is never logged. The search does not look into it (an open question: [GOAL.md](GOAL.md#open-questions), *Search*).

### Datasets
A dataset is a node of type `dataset` holding one file, in versions like a note (`ContentVersions`, shared with notes): each import is a content file (`content/<id>.kfd`, chunked: see [Encryption](#encryption)) and an `editDataset` change of the same id, holding the file's name, size and SHA-256, and naming the versions it **replaces**.

```
import ──DatasetFiles.Describe──► DatasetFile(name, size, sha256) ──DatasetPlanner.Import──► DatasetEdited(replaces = versions loaded)
       ──Vault.Save──► content file (checked against size and sha256), then change file
export ──DatasetFiles.Export──► the latest current version, decrypted as a stream into the chosen file (.tmp, then renamed)
keep   ──DatasetPlanner.Keep──► a conflict's chosen version again, as a new version: its content opened and sealed again under the new id
```

- The metadata is in the graph and the snapshot, so a dataset's file name and size are shown offline, before its content is synced. The content is never kept in memory.
- An import reads the file twice: once for its hash, once to seal it. A file changed in between is refused, and nothing is written.
- Importing the same file again (name, size and hash), with no conflict, writes nothing.
- A version whose content file is not synced yet is shown as such and cannot be exported. Importing over it is allowed: its name and size are seen.
- Hashing, sealing and exporting run off the UI thread (`Task.Run`, in `VaultSession.SaveDataset` and `ExportDataset`); the change is then appended on it. Meanwhile `DatasetPanel` says what it does and takes no other import (`VaultSession.IsImporting`, also for a file chosen on the creation screen, imported once its node is added).
- Drag out (`DragExport`): Windows only takes a file on disk, so the file is first exported (the cursor waits) into a folder of its own in the temporary folder (`%TEMP%\kofrenot\<random>\<name>`), given to Windows as a file drop, and deleted when the drag ends; folders left by a drag that did not end are deleted at the next one. Its name is made safe for Windows first (`DatasetFile.SafeName`).
- File names, paths and content are never logged (an exception's message names the file: it is shown, and only its type is logged); ids and sizes are.

**Viewer.** A dataset's inner type is its file's extension (`DatasetType.Of`, read from the name: nothing more is stored), which picks its viewer:

| Viewer | Extensions | Shown |
| --- | --- | --- |
| Image | png, jpg, jpeg, gif, bmp, tif, tiff, ico | decoded by WPF (`BitmapImage`), up to `DatasetType.MaxImageSize` (64 MB) |
| Text | txt, md, csv, tsv, log, json, xml, html, htm, yaml, yml, ini, toml, cs, py, js, ts, css, sql, sh, cmd, ps1 | its first `TextPreview.MaxChars` (256 K) characters, UTF-8 unless a byte order mark says otherwise; HTML as text, not rendered |
| None | the others | "export it to open it" |

- `DatasetView` (in `DatasetPanel`) reads the latest version through `DatasetFiles.OpenRead` and decodes it off the UI thread, **in memory only**: nothing decrypted reaches the disk (an export, or a drag out, is the only way out). An image is frozen to cross to the UI thread; a version shown already is not read again.
- The details row of a dataset takes room only while its viewer shows something (`NodeDetailsPanel.FitDatasetRow`), as a note's editor does.

### Input
`Dune relates to Frank Herbert` → link; `Dune` → node.

```
text ──InputParser──► NodeRequest(name) | LinkRequest(left, verb, right)
           │ first phrase of a link type either way, as whole words, with text on both sides
           ▼
     GraphSearch (word prefix, case- and accent-insensitive) ──► Preview     (InputPresenter)
           ▼ Enter: request + homonyms chosen (AddChoices)
     CreationForm, in steps: each new node (type, note content, dataset file), then the link ──► AddPlan + notes + datasets   (CreationDialog, modal)
           ▼ Enter
     VaultSession.Create ──► Vault.Append (changes), then Vault.Save (each note's and dataset's first version)
```

- `VaultSession.Create(owner, request, choices)` opens the creation dialog over any window, so later features can reuse it. It returns false when cancelled or not written.
- The dialog only shows a `CreationForm`. Each type change makes a new plan, so a different link type can turn `AlreadyLinked` into a link that can be added. Only a new node gets a step (`CreationForm.Steps`): an existing node keeps its type.
- Nothing is written before the last step. `NotesToSave` gives the first version of each new note with content, for its `NodeAdded`, and `DatasetsToSave` each new dataset's with a file chosen (by a file dialog or a drop on its step); the dialog asks before discarding typed content or a chosen file.
- The types offered come from `TypeCatalog`: node type *generic*; link types *relates to* (symmetric), *contains* and *is a*. The creation screen offers each link type both ways (`TypeCatalog.Verbs`): a `LinkVerb` is a type read forward or reverse, and a reverse verb links from the right node to the left one.

- Names are compared by `NameKey`: lower case, accents removed, spaces collapsed. They are stored as typed, trimmed, with single spaces.
- A link phrase counts after at least one word; with nothing after it, the link is still being typed (`LinkProblem.SecondNodeMissing`).
- `GraphSearch`: exact names first, then names starting with the text, then the others, alphabetically; at most 20. Homonyms are listed oldest first, marked `¹`, `²`, ... (`SearchHit.Mark`).
- `AddPlanner` builds the `AddPlan` shown as preview: the nodes to create, the link, and `NeedsConfirmation` when a node of that name exists (warned about on the creation screen). An existing link is not added again (`LinkProblem.AlreadyLinked`). `AddChoices` gives, for each side, the chosen node among homonyms (the oldest by default) and the type of a new node (*generic* by default). A link from a node to itself is refused.
- A name containing a link alias cannot be entered (no quoting).

### Node details
Behavior for the user: [README.md](../README.md#node-details).

```
search                                    details, level 2 of 4
┌─────────────────────────────────── ⚙ ┐  ┌─────────────────────────────────── ⚙ ┐
│ [ dune                             ] │  │ ⌂ Search › Dune › Frank Herbert      │
│ Enter to see the details of “Dune”.  │  │ Frank Herbert                        │
│ › Dune                           [›] │  │ Type      Generic                    │
│     relates to Frank Herbert         │  │ Created   2026-10-16 09:12           │
│   Dune Messiah                   [›] │  │ Links (2)                            │
└──────────────────────────────────────┘  │   relates to   Dune         (greyed) │
                                          │ › relates to   Children of Dune  [›] │
                                          └──────────────────────────────────────┘
```

- `InputPresenter` keeps the selected row (`MoveSelection`, `SelectedRow`) while the text stays the same: a node row, or a link row (`ResultRow.From` is the node it belongs to). While a link is typed, Up/Down choose homonyms (`MoveChoice`) instead. `ResultRow.Node` is the node the row opens, and gives it its **›** button. A link row opens `From` as level 1 and `Node` as level 2 (`NodeBrowser.Open`).
- `NodeBrowser` holds the trail: one level per node opened, each keeping its selected link, so going back shows it again. `LinkRow.CanOpen` is false at `MaxDepth` (4) and for a node already in the trail. `Details` is read from the graph each time, so a refresh shows changes from elsewhere.
- Search rows and detail links are two lists (`MainWindow`, `NodeDetailsPanel`) sharing their size and **›** button (`Views/SharedStyles.xaml`).
- `NodeDetailsPanel` takes the place of the search in `MainWindow` and handles Up/Down/Enter/Esc while shown. It raises `BackToSearch` when the trail is empty. The trail is closed when its vault closes.

## Logging
- `Core` writes through `ILog` (`log.Info(category, message)`); only `App` knows Serilog.
- Category: the area (`App`, `Action`, `Settings`, `Window`, `Update`, ...).
- **Info**: every action the user triggers, start/stop. **Warning/Error**: failures. **Debug**: details.
- Never logged: node names, note or file content, typed text, keys, pass phrases. Ids and counts are fine.
- Local only: 3 files x 1 MB, plus the last 1000 entries in memory for the log window.
- The file is never restarted: each start appends to `kofrenot.log`, and a new file begins only at 1 MB. Every entry is flushed at once, so what happened before a crash stays in the file. The log window shows the current run only.

## Versioning
`<Version>` in `Directory.Build.props` is the only place the version is written. It is stamped into every assembly (AssemblyVersion, FileVersion, InformationalVersion), read back through `AppVersion` in `Core`, and passed to the installer by `build/installer.cmd`. Rules: [GOAL.md](GOAL.md#versioning).
