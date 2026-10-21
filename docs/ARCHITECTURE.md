# kofrenot: architecture

## Projects

```
kofrenot.App   (WPF, Windows only)
│  main window (input box, node details, note editor, dataset panel), creation dialog, settings window, pass phrase dialog, log window, changelog window, tray icon
│  VaultSession (the registered vaults; opens one, watches it and adds to it through the creation dialog)
│  ServerConnection + SyncService (the server account; syncs the server vaults in the background)
│  adapters: SerilogFileLog, SingleInstance, NativeWindows + WindowPlacer (window positions), GitHubReleaseFeed, Browser,
│            DpapiProtector, GlobalHotkey, VaultWatcher
│
│  implements ▼  IReleaseFeed · ILog · ISecretProtector
│
kofrenot.Core  (no Windows dependency, fully unit-tested on any OS)
   Graph: KnowledgeGraph · Node · Link · TypeCatalog · LinkAliases · Change
   Storage: Vault · VaultFolder · VaultFormat · SecretBox · ChunkedBox · KeyWrap · SnapshotStore · NoteFiles · DatasetFiles · ThumbnailFiles · KeyMemory · VaultNameMemory
   Server: KofreClient · ServerSync · VaultMove · ServerSessionMemory   ── HTTPS ──►  Kofre server (own repository: kofreserver)
   Input: InputParser · GraphSearch · AddPlanner · InputPresenter · CreationForm
   Browse: NodeBrowser · NodeDetails · NodeVisual · NodeFacts · PinPlanner · RecentNodes · ViewTabs · ViewStates
   Notes: NotePlanner · MarkdownFormat
   Datasets: DatasetPlanner · DatasetType · TextPreview · ByteSize · IThumbnailMaker
   AppFiles · AppVersion · RunningCopy · QuitRefusal · Changelog · ProjectLinks
   SettingsStore · SettingsCatalog · Hotkey · VaultList
   PlacementRules · PlacementStore
   UpdateChecker · UpdateStore · Release
   ILog · RingLog · CompositeLog · FilteredLog
```

- All domain logic goes in `Core`. When it needs Windows, it takes an interface defined in `Core`, and `App` supplies the implementation.
- `App.xaml.cs` wires everything together at startup.
- Tests: `tests/kofrenot.Core.Tests` (xUnit), `Core` only.
- The server, Kofre, is generic (accounts, stores, files: no kofrenot term) and lives in its own repository; kofrenot only talks to it over HTTP: [kofreserver](https://github.com/dyze/kofreserver), [SERVER-CONCEPT.md](SERVER-CONCEPT.md#generic-by-design).

### Folders
One namespace per folder (`Kofrenot.Core.Settings`, `Kofrenot.UI.Views`, ...). The tests use the same folders as `Core`, and import every `Core` namespace once in their `.csproj`.

| `kofrenot.Core` | Content |
| --- | --- |
| *(root)* | `AppVersion`; `RunningCopy` + `AppCopy` (another copy running: same or not, and what the user is told); `QuitRefusal` (why kofrenot must not quit, or switch vaults, now); `AppFiles` (where every file of kofrenot is, including `keys.json` and the snapshots in `cache\`: the only place their names are written, listed in Settings > About; `AppFiles.For` picks the data folder, see [Portable copy](#portable-copy)); `Changelog` (reads the text shown by Settings > About); `ProjectLinks` (website, issues, releases) |
| `Settings/` | `SettingsStore` (`settings.json`: theme, debug logging, update check, the registered vaults and the one selected, hotkey, server account; `IsHotkeyTaken`), `Hotkey`, `KeyBindings` (every shortcut, by area, for Settings > Key bindings: written by hand, **update it with the key handlers**), `VaultList` + `RegisteredVault` + `VaultLocation` + `StoreAddress` (see [Registered vaults](#registered-vaults)), `ServerAccount`, `SettingsCatalog` (the entries of the Settings window, their tabs, and the search that finds them) |
| `Graph/` | `Node`, `Link`, `NodeType`, `LinkType`, `LinkVerb` (a link type read one way), `TypeCatalog` (built-in node and link types, link aliases; `LinkName`, `VerbOf`: how a link reads from one of its nodes), `LinkAliases` (the user's aliases, see [Aliases and removals](#aliases-and-removals)), `RemovableChanges`, `NameKey` (the form names are compared in), `Change` (`NodeAdded`, `LinkAdded`, `NodeRenamed`, `NodeRetyped`, `AliasAdded`, `NodePinned`, `ChangesRemoved`, `ContentEdited`: `NoteEdited`, `DatasetEdited`, `ViewEdited`; `VaultNamed`), `DatasetFile` (a dataset version's file: name, size, SHA-256), `CurrentVersions` + `IReplacing` (the versions no other replaces: shared by names, types, contents and the vault's name), `NodeNames` + `NameVersion` (a node's current names, see [Renames](#renames)), `NodeTypes` + `TypeVersion` (a node's current types, see [Type changes](#type-changes)), `ContentVersions` (the current versions of a note or dataset, see [Notes](#notes)), `KnowledgeGraph` (in-memory state, built by applying changes) |
| `Storage/` | `VaultFormat` (+ `NewerVaultFormatException`, `Since`, `UpgradeQuestion`), `VaultUpgrades` (upgrades agreed, `vault-upgrades.json`, see [Upgrade](#upgrade)), `SecretBox` (AES-GCM seal / open), `ChunkedBox` (AES-GCM over 1 MB chunks, streamed), `KeyWrap` (KDF, wrap / unwrap the master key), `ChangeData` (a change as JSON, shared with the snapshot), `ChangeCodec` (change ⇄ sealed JSON), `VaultHeader`, `VaultFolder` (layout, inspect, create, `SizeOf` + `FolderSize`: its files and bytes, shown in Settings > Vault, measured off the UI thread), `SnapshotStore`, `NoteFiles` (note content, `content/`), `DatasetFiles` (dataset content, `content/`: describe a file, write, export, open), `ThumbnailFiles` (thumbnails, `content/`), `Vault` (open, append, refresh, save a note or a dataset version, `WriteDataset`: its files only, off the UI thread; `Vault.Upgrade`: its format, writes refused until an upgrade is agreed), `AtomicFile` (write as `.tmp`, then rename), `KeyMemory` (remembered master keys, `keys.json`) and `VaultNameMemory` (the last name read in each vault, `vault-names.json`), both through `ProtectedStore`, `ISecretProtector` |
| `Input/` | `InputParser` (text → `NodeRequest` / `LinkRequest`), `GraphSearch` (`SearchHit`), `AddPlanner` (`AddChoices` → `AddPlan`: `NodePlan`, `LinkPlan`, `PlannedSide`), `InputPresenter` (the input box: preview, result rows, homonym choices), `CreationForm` (the creation screen: steps, types, note content, dataset file, status, what exists), `PlanText` (wording shared by both), `RenamePlanner` (`RenamePlan`: a new name, its problem, homonyms), `VaultNamePlanner` (`VaultNamePlan`), `RetypePlanner` (`RetypePlan`), `LinkChangePlanner` (`LinkChangePlan`: a link's type or direction, as a removal and a new link), `DetailsLink` (the link row of the details: + Link, Ctrl+K), `AliasPlanner` (`AliasPlan`) |
| `Browse/` | `NodeBrowser` (the details screen: trail of open nodes, at most 4, the selected link of each), `NodeDetails`, `LinkRow`, `NodeVisual` (`NodeIcon`: how a node is pictured in rows, see [Datasets](#datasets)), `NodeFacts` (type, size, last change: a view's Details), `PinPlanner` (pin or unpin), `RecentNodes` (the nodes last opened on this device), `ViewTabs` and `ViewStates` (the tabs open, the nodes expanded in each tree, on this device), through `LocalJsonFile` |
| `Notes/` | `NotePlanner` (`NoteState`, `ConflictVersion`: load a note, plan its next version), `MarkdownFormat` (`TextEdit`: the toolbar's Markdown) |
| `Datasets/` | `DatasetPlanner` (`DatasetState`, `DatasetChoice`: load a dataset, plan an import or the version kept from a conflict), `ByteSize` (sizes as shown: 1.2 MB), `DatasetType` (`DatasetCategory`, `DatasetViewer`: the inner type, from the extension, its category and viewer), `IThumbnailMaker` (implemented by App), `TextPreview` (the beginning of a text, for the viewer), `ImageZoom` (the image window's zoom: fit, steps, percent) |
| `Server/` | Server vaults, see [Server vaults](#server-vaults): `KofreClient` (the server's REST API, renews its token; `IStoreFiles`, `ServerException` + `ServerFailure`), `ServerSync` (mirror ⇄ store), `StorePaths`, `VaultMove` (a folder vault into a mirror), `ServerSessionMemory` (`server-session.json`, through `ProtectedStore`) |
| `Logging/` | `ILog` (with `NullLog`), `LogEntry`, `RingLog`, `CompositeLog`, `FilteredLog` |
| `Placement/` | Window positions: `WindowPlacement`, `PlacementRules`, `PlacementStore` (see [Window positions](#window-positions)) |
| `Updates/` | The daily update check, see [Updates](#updates): `UpdateChecker`, `Release`, `IReleaseFeed`, `UpdateStore` |

| `kofrenot.App` | Content |
| --- | --- |
| *(root)* | `App.xaml`: startup and wiring; `App.Hotkeys` (the hotkey that shows kofrenot, and the vaults'); `VaultSession` (the open vault: create, unlock, watch, `Create` (creation dialog, then add); `VaultSession.Vaults`: switch, register, unregister, move, name, forget a remembered key; `VaultSession.Server`: create on, add from, move to and delete from the server) |
| `Sync/` | `ServerConnection` (the server account: sign up, in, out; its session kept with DPAPI), `SyncService` + `SyncStatus` (syncs every server vault in the background, see [Server vaults](#server-vaults)) |
| `Views/` | Windows: `MainWindow` (input box, shows `InputPresenter`; `MainWindow.Vaults`: the vault box, `VaultChoice`, and a vault's hotkey), `NodeDetailsPanel` (in the main window, shows a `NodeBrowser`; `NoteEditor` for a note, `DatasetPanel` for a dataset: import, export, drag in and out, and `DatasetView`, its viewer), `NodeVisualView` + `ThumbnailCache` (a node's picture in rows), `CreationDialog` (shows a `CreationForm`, one step at a time; the node types in a list with their icons (`TypeChoice`), the focus starting on the chosen one; the link type in a combo, as it has six entries), `MarkdownBox` (note text and toolbar), `SharedStyles` (the type scale: title, heading, body, secondary, caption, footer; shared buttons), `SettingsWindow` (tabs General, Key bindings (`KeyBindingsSection`), Vault (`VaultsSection`; `StoreChoiceDialog`, `DeleteServerVaultDialog`), Server (`ServerSection`), Link types (`LinkTypesSection`), Appearance, Logging, About; `HotkeyCapture`: the shortcut pressed in a hotkey box), `PassphraseDialog`, `ImageWindow` (a dataset's image), `LogWindow`, `ChangelogWindow` (Settings > About > *Show changelog*); `MessageDialog` (messages and questions in the app's theme, instead of the Windows `MessageBox`); `AppTheme` |
| `Tray/` | `TrayIcon` |
| `Platform/` | Implementations of the `Core` interfaces (`GitHubReleaseFeed`, `DpapiProtector`), `WpfThumbnailMaker` (`IThumbnailMaker`), `DragExport` + `LazyFileDrop` (a file given only when asked for), `Browser` (opens web pages), `SingleInstance`, `GlobalHotkey` (`RegisterHotKey`, several by id), `VaultWatcher` (`FileSystemWatcher`), and window placement (`NativeWindows`, `WindowPlacer`) |
| `Logging/` | `LogHub`, `SerilogFileLog` |
| `Assets/` | `kofrenot.ico` (drawn by `build/make_icon.py`) |

## Stack
.NET 10, C#, WPF with its built-in Fluent theme for light and dark (`Application.ThemeMode`, `AppTheme`). WinForms only for the tray icon (`NotifyIcon`). xUnit for tests.

| Package | Project | Use |
| --- | --- | --- |
| `Serilog`, `Serilog.Sinks.File` | App | Log file |
| `coverlet.msbuild` | Tests | Coverage of `Core`, build fails below 95 % lines |
| `dotnet-reportgenerator-globaltool` | Local tool (`.config/dotnet-tools.json`) | HTML coverage report (`build.cmd`) |

The update check (`GitHubReleaseFeed` in App) and the server (`KofreClient` in Core) use .NET's own `HttpClient`: kofrenot's only network access. Encryption uses .NET's own `AesGcm` and `Rfc2898DeriveBytes` (Core), and `ProtectedData` (DPAPI, part of the Windows desktop framework, App): no package.

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
 ├─ AppTheme
 ├─ SingleInstance.TryClaim ── another copy runs? ─► see the list below
 ├─ LogHub
 ├─ VaultSession (KeyMemory + VaultNameMemory + DpapiProtector)
 ├─ MainWindow (+ WindowPlacer "Main") ─► UpdateChecker (background, once a day)
 ├─ GlobalHotkey: Ctrl+Alt+K by default ─► MainWindow shown; each vault's ─► MainWindow.ToggleVault
 ├─ TrayIcon: Show · Settings · Log window · Quit
 ├─ MainWindow shown
 └─ once idle: VaultSession.Open (the selected vault) ─► remembered key, or PassphraseDialog (create / unlock) ─► Vault.Open + VaultWatcher
```

- Closing the main window (or Esc with an empty input box) hides it; kofrenot keeps running in the notification area until *Quit* (`ShutdownMode=OnExplicitShutdown`).
- A hotkey another program already uses is not registered: logged as a warning at start, refused in Settings > General or > Vault (the previous one stays). A hotkey kofrenot has already (the main one, another vault's) is refused too (`SettingsStore.IsHotkeyTaken`). The vaults' hotkeys are registered again when the list changes (`App.RegisterVaultHotkeys`, ids after the main one's).
- One copy runs at a time (they would write the same files): the first owns the mutex `Local\kofrenot-single-instance` and waits on the events `Local\kofrenot-show-window` and `Local\kofrenot-quit`. A copy started while another runs finds it (`SingleInstance.FindRunning`: its process, exe and full build `0.4+<commit>[.dirty]`), and decides:

  ```
  same exe and build ─► set show event, exit
  other copy ─ quit event exists? ─ no (≤ 0.3) ─► RunningCopy.Warning: Show the running one / Close; exit
                    │ yes
     RunningCopy.ReplaceQuestion ─ Show the running one ─► set show event, exit
                    │ Close it, start this one
     set quit event ─► running copy: QuitRefusal.Reason?
                          │ none: quits, frees the mutex ─► this copy runs (10 s at most, else NotClosed)
                          │ a reason: shows it in its window, sets Local\kofrenot-quit-refused ─► Refused; exit
  ```

  The path counts, not only the build: a portable copy keeps its data in its own folder. The settings are read first, for the theme of these dialogs.
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
- Saved in `windows.json` by window name (`Main`, `Settings`, `Log`, `Image`) when a window is hidden or closed, restored when it is first shown. Kept out of `settings.json`: it is state, not a setting.
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
  kofrenot-vault.json      header, plain JSON: format, vault id (up to format 5: KDF salt + iterations, wrapped master key)
  keys/                    since format 6 (Encryption)
    identities/9e1c….json  an identity: public key, private key sealed by the pass phrase and by the recovery key
    epoch-0/9e1c….kfw      the vault key of epoch 0, wrapped for that identity, written once
  changes/
    3f/3f9c…e1.kfc         one encrypted change, named by a random id, written once
    a0/a07b…42.kfc
  content/
    3f/3f9c…e1.kfn         the content of a note version, named by its change (editNote), written once
    b2/b2d4…07.kfd         the file of a dataset version, named by its change (editDataset), written once
    b2/b2d4…07.kft         the thumbnail of an image dataset version (128 px PNG), same name, written once, before the .kfd
```

- **Only added, never modified.** A change file is written as `*.tmp` then renamed, so a reader or the sync tool never sees half a file. `.tmp` files are ignored.
- **Folders by the id's first two characters**, so no folder holds too many files.
- An unreadable file (corrupt, wrong key, still being copied) is skipped and logged once as a warning, then tried again at each refresh; the others still load. Other files (`desktop.ini`, ...) are ignored.
- A header or change file in a newer format: see [Format](#format).
- `VaultFolder.Inspect`: a new vault is only created in an empty (or missing) folder; a folder holding other files is refused, so nothing of the user's is mixed with it. A missing folder of a vault opened before (its id known) is out of reach, never a new vault.

### Registered vaults
The vaults kofrenot knows are in `settings.json`, in the user's order, with the one selected: one vault is open at a time, and vaults never mix.

```
settings.json  "vaults": [{ "folder", "id"?, "hotkey"?, "locked"?, "server"?, "store"? }], "selectedVault": "<folder>"
               "server": { "url", "email" }   the server account, one per kofrenot
               "vaultFolder": the selected one too, for a kofrenot 0.4 started again; read alone (0.4) ─► the only vault
```

- `VaultLocation` is where a vault is: a folder, or a store on a server (`StoreAddress`) with its mirror as folder ([Server vaults](#server-vaults)). Locations are compared by folder, ignoring case and a trailing separator. `RegisteredVault` adds its id, null until its header is read (`VaultSession.Identify`), and its optional hotkey. `VaultList` is immutable and lists each location and id once.
- Switching (`VaultSession.SwitchTo`) closes the open vault, then opens the selected one. When it does not open (pass phrase cancelled, not a vault, an error), the vault open before is used again, without asking its pass phrase. The main window refuses to switch while a file is being imported or a note cannot be saved (`QuitRefusal`).
- `Register` (Settings > Vault > *Add...*) refuses a folder holding other files, and a vault listed already, by folder or by the id in its header; a new vault whose creation is cancelled is not added. `Unregister` forgets the vault's key and name on this device; its folder is not touched.
- **Lock** (`VaultSession.Lock`, padlock or Ctrl+L): the vault is closed and marked locked in `settings.json` (`"locked": true`). `Open` leaves a locked vault closed (at start, on a switch, on its hotkey), and `SwitchTo` does not go back to the previous vault for it. `Unlock` asks the pass phrase, skipping the remembered key (kept for later), then clears the mark; cancelled, the vault stays locked.
- **Name**: asked on creation (`PassphraseDialog.AskNew`) and written as the vault's first change, then by Rename: a `nameVault` change, encrypted like the others, replacing the current names (`CurrentVersions`), as renames do: two names made apart are a conflict, shown in Settings > Vault, resolved by naming again (`VaultNamePlanner`). `VaultNameMemory` keeps the last name read on this device (`vault-names.json`, protected by DPAPI like `keys.json`), so a locked vault shows it; otherwise its folder's name (a server vault: its server's). A server knows a vault by its store id only ([Server vaults](#server-vaults)).

### Server vaults
A vault kept on a Kofre server ([kofreserver](https://github.com/dyze/kofreserver)): one store per vault, mirrored in a local folder that `Vault` reads as any vault folder. Its files and their encryption are those of a folder vault, its keys wrapped for the server account's identity ([Account identity](#account-identity)): the server only stores them. Design and later steps: [SERVER-CONCEPT.md](SERVER-CONCEPT.md).

```
 Vault ── reads, writes ──► mirror  %LocalAppData%\kofrenot\stores\<store>\   (folder vault layout)
                              ▲  │                                    stores\<store>.sync.json: feed position, files both sides have
          downloads (feed) ───┘  └─── uploads: the mirror's files the store lacks (header, keys, contents, then changes)
                              ServerSync ◄──► KofreClient ── HTTPS, JWT ──► Kofre server
```

- **Sync** (`SyncService`, App): every 30 s, 2 s after a change or when the window is shown, one sync at a time, for every server vault of the list, in the background. A download is written as `.tmp` then renamed, so `VaultWatcher` sees it and the vault refreshes. Offline, writes stay in the mirror until the next sync: nothing is queued elsewhere. Files are only added on both sides, so a sync is two copies, never a merge.
- **Status**: `SyncStatus` (synced, offline, sign in to sync, files to upload), in the main window's footer and in Settings > Vault. A failure is logged once, not at every try.
- **Account** (`ServerConnection`): one per kofrenot, in `settings.json`. Only its session is kept, in `server-session.json` (DPAPI, through `ProtectedStore`); the password never is. `KofreClient` renews the access token before it expires and when refused; a refused refresh signs out.
- **Create on the server**: the pass phrase asked first; once given, the store is made (in the dialog, which shows a server failure and stays open), the vault created in its mirror, listed and opened with the key just made. Cancelled, nothing is made, on the server or here. An empty store chosen in *Vault from the server* goes the same way.
- **Add from the server**: the stores not listed (shown by id and size: the name is encrypted in the vault), downloaded, then opened. An empty mirror is never made a new vault: it waits for its download.
- **Move to the server** (`VaultMove`): the vault's files copied as they are into a new store's mirror (other files left), the registration replaced (same id, hotkey, remembered key and name), then uploaded. The folder is left as it was.
- **Delete from the server**: the vault's name typed again and the account's password; the store goes to the server's trash, the vault out of the list, and its mirror is deleted. **Remove** keeps the mirror: added again, it syncs from where it was.
- Paths from the server are checked (`StorePaths`): only a vault's file names, so a store cannot write outside its mirror.

#### Account identity
One identity for all the server vaults of the account ([design](SERVER-CONCEPT.md#keys)): its pass phrase and recovery key open them all.

```
server: GET/PUT /v1/accounts/me/data   public: { id, public key }    private: Identity (sealed)
            │ version-checked: a write names the version it replaces (409 when another device wrote)
            ▼
AccountIdentities ─► account-identity.json (this computer: server vaults unlock offline)
            │ refreshed at sign-in and at each sync (ServerConnection.RefreshAsync)
            ▼
VaultKeys.Unlock(…, account): the account's identity first, then the vault's own, then its header
```

| Server vault | Its keys |
| --- | --- |
| New | Epoch 0 wrapped for the account's identity (`VaultFolder.Create(folder, identity, unlocked)`); no identity of its own, no recovery key of its own |
| From 0.6 (format 5), moved from a folder, or made by an early 0.7 | Opened with its own pass phrase, then **attached** (`VaultKeys.Attach`): at its upgrade, or at its next opening when the server can be asked. Its keys are wrapped for the account, then a new epoch for the account only: the old pass phrase opens only the files written before |
| Remembered key | Used only while it opens the latest epoch (`VaultKeys.IsCurrent`); else the pass phrase is asked |

- **The account's pass phrase** is chosen at the first need (a new server vault, or the first attach), then its recovery key shown once. Changed in Settings > Server (*Pass phrase*: its current one asked, then the new one); after the recovery key unlocks a server vault, a new one is asked too.
- **Server version**: `GET /v1/version`, at sign-in and at each sync. Below 0.2 (`KofreVersion.Minimum`; a 0.1 server answers 404), Settings > Server says the server is too old, and what needs the account's data is refused (creating, attaching, changing the pass phrase); the files still sync.

### Format
One number for the whole vault, `VaultFormat.Current`, written in the header and in every change file (`format`). It is not the app version: it only changes when what is written in a vault breaks.

| Format | Since | Adds |
| --- | --- | --- |
| 1 | 0.1 | `addNode`, `addLink`, `renameNode` |
| 2 | 0.2 | directed links (`contains`, `is-a`): a 0.1 app would read them as symmetric; `addAlias`, `pinNode`, `remove`, `editNote` with the `content/` files |
| 3 | 0.3 | `editDataset` with its chunked `.kfd` content files: a 0.2 app does not know them |
| 4 | 0.4 | `remove` of nodes and links: a 0.3 app would still show them; `editView` |
| 5 | 0.5 | `nameVault`, `retypeNode`: a 0.4 app cannot read them |
| 6 | 0.7 | `keys/` and the key epoch in every file (`KFC2`, `KFN2`, `KFT2`, `KFD2`); a header without key: a 0.6 app cannot open the keys |

| Found | This kofrenot |
| --- | --- |
| format ≤ `VaultFormat.Current` | reads it |
| header with a newer format | does not open the vault |
| change file with a newer format (another device has a newer kofrenot) | closes the vault, at opening or at a refresh |
| format < 1 | the file is damaged: skipped like an unreadable file |

- `NewerVaultFormatException` (a `NotSupportedException`) carries the format found. `VaultSession.NewerFormat` keeps it: the main window and Settings > Vault say to upgrade, and *Get the latest kofrenot* opens the releases page.
- A breaking change (something an older kofrenot would misread or not read) raises `VaultFormat.Current`. The owner decides it first (`CLAUDE.md`). Changes that only add something older versions ignore safely (such as an unknown type, shown by its id) do not.
- No migration: files are never rewritten, so a vault holds files of several formats, each read as written.

#### Upgrade
Every file is written in `VaultFormat.Current`, so the first write to a vault of an older format raises it: the older kofrenot then closes it. The user agrees first.

```
open: unlock, read ─► Vault.Format = highest of header, change files, snapshot
                               │
          Format < Current (Vault.NeedsUpgrade), not agreed in vault-upgrades.json?
             │ yes                                              │ no
             ▼                                                  ▼
   VaultFormat.UpgradeQuestion                        AgreeToUpgrade, used
   [Upgrade and open] ─► VaultUpgrades.Agree, AgreeToUpgrade, used
   [Not now] ─► unlocked, not used (VaultSession.OlderFormat):
                the main window offers "Upgrade and open…" (VaultSession.Upgrade:
                refresh, then ask again unless another device upgraded it)
```

- **5 → 6**: once agreed, the pass phrase is asked again (`PassphraseDialog.AskToUpgrade`): `VaultKeys.Upgrade` adds `keys/` (an identity, the master key as epoch 0), then the recovery key is shown. No file is rewritten: the header keeps its key until the pass phrase changes. If another device upgraded it, its identity opens with the same pass phrase and nothing is added. Cancelled, the vault waits, as with *Not now*.
- `Vault` refuses every write (`Append`, `Save`, `WriteDataset`, `WriteCopies`) with `InvalidOperationException` until `AgreeToUpgrade`: no content file is written ahead of a refusal.
- `VaultUpgrades` (`vault-upgrades.json`, ids only) keeps the highest format agreed per vault, on this device: asked once, and again only for a newer format.
- `VaultFormat.Since` names the first kofrenot writing each format, for the question.

### Encryption

Since format 6 ([design](SERVER-CONCEPT.md#keys)):

```
pass phrase ─PBKDF2-SHA256 (600 000)─┐     recovery key (32 random bytes, shown once) ─HKDF─┐
                                     ▼ AES-256-GCM                          AES-256-GCM ▼
keys/identities/<id>.json   Identity: P-256 public key, private key sealed twice ("KFK1", "KFR1")
                                     │ ECDH with a one-time key pair, HKDF-SHA256, AES-256-GCM
                                     ▼
keys/epoch-N/<id>.kfw       vault key of epoch N = "KFW1" · one-time public key · nonce · key · tag
                            (EpochKeys; AAD = vault id · epoch · identity id)
                                     ▼
KeyRing { epoch → key } ─► KeyedBox: AES-256-GCM, new 12-byte nonce per file, AAD = header · file id
                                     ▼
change file = "KFC2" · epoch (4 bytes, big-endian) · nonce · ciphertext · tag
```

| Kind (`SealKind`) | Before format 6, read with epoch 0 | Since |
| --- | --- | --- |
| Change, note, thumbnail, dataset, snapshot | `KFC1`, `KFN1`, `KFT1`, `KFD1`, `KFS1` | `KFC2`, `KFN2`, `KFT2`, `KFD2`, `KFS2` · epoch |

- **Format 5** and before: the header holds the master key, wrapped by the pass phrase (PBKDF2-SHA256, then AES-256-GCM, "KFK1"); it is epoch 0.
- **Unlock** (`VaultKeys.Unlock`): each identity is tried with the pass phrase (or the recovery key), then the header's key; the identity unwraps its keys of every epoch into a `KeyRing`. New files use the latest epoch. Only epoch 0 exists until members can be removed ([Rotation](SERVER-CONCEPT.md#rotation)).
- **Change the pass phrase** (`VaultKeys.ChangePassPhrase`, folder vaults): the identity file is rewritten (`.tmp`, then renamed); a header still holding the master key is rewritten without it (format 6), so the old pass phrase opens nothing. A server vault opens with its account's pass phrase, changed in Settings > Server ([Account identity](#account-identity)).
- **Recovery key**: 52 Base32 characters in groups of 4 (`RecoveryKey`); case, spaces and dashes ignored. After unlocking with it, a folder vault asks a new pass phrase.

A dataset's file can be of any size, so it is sealed in chunks (`ChunkedBox`), read and written as a stream:

```
.kfd file = "KFD2" · epoch · nonce prefix (8 random bytes) · chunk 0 · chunk 1 · … · last chunk
chunk i   = AES-256-GCM(1 MB of the file) · tag
            nonce = prefix · i (4 bytes, big-endian)    AAD = "KFD2" · epoch · version id · last (1 byte)
```

- A chunk changed, moved, left out or added after the last one fails to open; so does a file cut after a chunk (no last one). An empty file is one empty last chunk.
- Memory use stays at about two chunks, whatever the size.

- Built into .NET (`Rfc2898DeriveBytes`, `AesGcm`, `ECDiffieHellman`, `HKDF`): no package.
- A wrong pass phrase fails to unseal the identity (GCM tag), so no extra check value is needed.
- *Remember pass phrase* stores the identity's private key (a format 5 vault: its master key, replaced at upgrade), not the pass phrase: it opens at once and survives a pass phrase change. Protected by DPAPI for the current Windows user, in `keys.json` in the local data folder, per vault id (`KeyMemory`). A master key remembered for a vault that has identities is not used: the pass phrase is asked, then the identity remembered. `Core` sees DPAPI through `ISecretProtector`; `App` implements it with `ProtectedData` (`DpapiProtector`).
- The key, the pass phrase and names are never logged.

### Change content
UTF-8 JSON before encryption:

```json
{ "format": 4, "at": "2026-10-16T09:12:00Z", "op": "addNode",
  "node": { "id": "…", "type": "generic", "name": "Dune", "created": "2026-10-16T09:12:00Z" } }
{ "format": 4, "at": "…", "op": "addLink",
  "link": { "id": "…", "type": "relates-to", "from": "<node id>", "to": "<node id>", "created": "…" } }
{ "format": 4, "at": "…", "op": "renameNode",
  "rename": { "node": "<node id>", "name": "Dune (novel)", "replaces": ["<node id or rename id>", "…"] } }
{ "format": 4, "at": "…", "op": "addAlias",
  "alias": { "linkType": "contains", "reverse": true, "phrase": "is in" } }
{ "format": 4, "at": "…", "op": "pinNode", "pin": { "node": "<node id>" } }
{ "format": 4, "at": "…", "op": "editNote", "note": { "node": "<node id>", "replaces": ["<version id>", "…"] } }
{ "format": 4, "at": "…", "op": "editDataset",
  "dataset": { "node": "<node id>", "file": { "name": "survey.csv", "size": 52430, "sha256": "9f86…08" }, "replaces": ["<version id>", "…"] } }
{ "format": 4, "at": "…", "op": "editView",
  "view": { "node": "<node id>", "display": "tree", "query": { "root": "<node id>", "linkType": "contains" }, "replaces": ["<version id>", "…"] } }
{ "format": 4, "at": "…", "op": "remove", "removes": ["<node, link, alias or pin id>", "…"] }
```

`renameNode` was added to format 1 before 0.1 was released, so the format number did not change (owner's decision).

- Changes are applied in `at` order (then id) to an in-memory graph. Search and preview work on it only.
- A type unknown to this version is kept and shown by its id.
- Duplicate nodes (same type and normalized name) are all kept and marked; duplicate links (same type and nodes, either direction when symmetric: copies) count as one, the oldest shown (`LinkIndex`).
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
- It holds the changes themselves, as in their files (`ChangeData`, `SnapshotStore.Format` 4), so a new kind of change needs nothing more. As they are written again in the current format, it also keeps the highest vault format of their files (`Vault.Format`, see [Format](#format)). A snapshot of an older format is not used: every change is read again once.
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

### Type changes
A type change works as a rename: a `retypeNode` change (`NodeRetyped`) listing the types it **replaces**, the node's id standing for the type it was added with. The node is shown with the latest current type (`NodeTypes`, `KnowledgeGraph.TypesOf`); two current types are a conflict, shown in the details, resolved by keeping one.

- `RetypePlanner` lets a *generic* node become a note, a dataset or a view (`TargetsOf`), so no content is hidden; a node in conflict may keep any of its types. A view asks its definition first (`VaultSession.Retype`): its first `editView` is written with the type.
- A **link** keeps no history of its own: `LinkChangePlanner` changes its type or direction with a `remove` of the link and its copies, and a new `addLink`. Changed on two devices before they synced, both new links show, as any two links.

### Aliases and removals
What the user can take back (nodes, links, aliases, pins) is added by one change and removed by another, `remove`, listing the ids it removes. Nothing is deleted from the vault.

```
a1 addAlias "is in" → contained by      device A
a2 addAlias "is in" → contained by      device B, before syncing (a copy)
r1 remove [a1, a2]                      removes every copy
current = added − removed               (RemovableChanges; a removal may arrive first)
```

- **Nodes and links** (since format 4): a removed node is no longer shown, nor searched, pinned or linked; a rename or version arriving after changes nothing. A removal wins, whatever the order the files arrive in.
- A removed link shows its next copy, if any: `KnowledgeGraph.CopiesOf` gives every copy, so a removal names them all.

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
       ──Vault.Save──► an image's thumbnail, the content file (checked against size and sha256), then change file
export ──DatasetFiles.Export──► the latest current version, decrypted as a stream into the chosen file (.tmp, then renamed)
keep   ──DatasetPlanner.Keep──► a conflict's chosen version again, as a new version: its content opened and sealed again under the new id
```

- The metadata is in the graph and the snapshot, so a dataset's file name and size are shown offline, before its content is synced. The content is never kept in memory.
- An import reads the file twice: once for its hash, once to seal it. A file changed in between is refused, and nothing is written.
- Importing the same file again (name, size and hash), with no conflict, writes nothing.
- A version whose content file is not synced yet is shown as such and cannot be exported. Importing over it is allowed: its name and size are seen.
- Hashing, sealing and exporting run off the UI thread (`Task.Run`, in `VaultSession.SaveDataset` and `ExportDataset`); the change is then appended on it. Meanwhile `DatasetPanel` says what it does and takes no other import (`VaultSession.IsImporting`, also for a file chosen on the creation screen, imported once its node is added). An import shows its progress in percent (`IProgress<long>`, told the bytes read by `HashingStream` once per MB and at the end, through `DatasetFiles.Describe`, then `Vault.WriteDataset`; `ByteSize.Percent`): reading, then importing (the thumbnail, made first, is not counted). An import started from the creation screen only says "Importing…".
- Drag out (`DragExport`): Windows only takes a file on disk, so the file is first exported, holding the UI thread (the cursor waits, and "Preparing the file to drag…" is drawn before) into a folder of its own in the temporary folder (`%TEMP%\kofrenot\<random>\<name>`), given to Windows as a file drop, and deleted when the drag ends; folders left by a drag that did not end are deleted at the next one. Its name is made safe for Windows first (`DatasetFile.SafeName`). From a tree view, the same drag moves nodes, so the file is offered lazily (`LazyFileDrop`): exported only when another program asks for it, never for a move inside kofrenot. One file dropped on a tree node opens the creation screen with it (`CreationDialog.Ask(…, file)`), under that node.
- File names, paths and content are never logged (an exception's message names the file: it is shown, and only its type is logged); ids and sizes are.

**Viewer.** A dataset's inner type is its file's extension (`DatasetType.Of`, read from the name: nothing more is stored), which gives its category (for its icon) and its viewer:

| Category | Extensions | Viewer |
| --- | --- | --- |
| Image | png, jpg, jpeg, gif, bmp, tif, tiff, ico | Image: in its own window (`ImageWindow`), decoded by WPF (`BitmapImage`), up to `DatasetType.MaxImageSize` (64 MB) |
| Image | webp, svg, heic, raw | None |
| Text | txt, md, csv, tsv, log, rtf | Text: its first `TextPreview.MaxChars` (256 K) characters, UTF-8 unless a byte order mark says otherwise |
| Code | json, xml, html, htm, yaml, yml, ini, toml, cs, py, js, ts, css, sql, sh, cmd, ps1, java, c, cpp, h, go, rs | Text (HTML as text, not rendered) |
| Audio, Video, Archive, Pdf | wav, mp3, flac, ogg, m4a, aac, wma; mp4, mkv, avi, mov, webm, wmv; zip, 7z, rar, tar, gz, bz2, xz; pdf | None: "export it to open it" |
| Other | the others | None |

- A text is shown in the details by `DatasetView` (in `DatasetPanel`); an image in `ImageWindow`, opened by *View image* or a double-click on the file: one window, reused by each image, resizable, its place remembered (`Image`), closed when the vault changes. It opens fitted (`ImageZoom.Fit`, never enlarged) and follows the window's size until zoomed: the wheel zooms around the pointer (the image point under it stays), +/- around the middle, 0 fits again; a drag with the left button moves an image larger than the window (the hand cursor says so; a press on a scroll bar stays the scroll bar's). Zoom 1 is one image pixel per screen pixel, whatever the scaling. Both read the latest version through `DatasetFiles.OpenRead` and decode it off the UI thread, **in memory only**: nothing decrypted reaches the disk (an export, or a drag out, is the only way out). An image is frozen to cross to the UI thread; a text shown already is not read again. A text can wrap its lines (*Wrap lines*, shared by every text while kofrenot runs).
- The details row of a dataset takes room only while its viewer shows a text (`NodeDetailsPanel.FitDatasetRow`), as a note's editor does.

**Thumbnails and icons.** Every node is pictured in the search rows, the details' link rows and a dataset's header by a `NodeVisual`: an icon, and for an image the version whose thumbnail replaces it.

```
import ──Vault.WriteDataset──► image the viewer shows (≤ 64 MB)? ──IThumbnailMaker.Make (WpfThumbnailMaker)──► content/<id>.kft
row    ──NodeVisual.Of(graph, node)──► Icon (type, or the file's category) + Thumbnail (latest version id, for an image)
       ──NodeVisualView──► glyph at once ──ThumbnailCache.Get (off the UI thread)──► the thumbnail, once read
```

- `WpfThumbnailMaker` decodes the image straight to 128 px on its longest side (`DecodePixelWidth` or `DecodePixelHeight`, the size read from the header first), never enlarging a small one, and saves a PNG. An image it cannot decode gets no thumbnail; the dataset keeps its icon.
- The thumbnail is written before the content file and the change (`Vault.WriteDataset`, off the UI thread), so a device seeing the change normally finds it. A missing one (not synced yet, or a version imported before thumbnails) shows the icon. Keeping a version from a conflict makes its thumbnail again from its content.
- `.kft` files are sealed with the master key ("KFT1", bound to the version id), like note content: a thumbnail shows the data, so it is content. Never logged, never in the snapshot.
- `ThumbnailCache.Shared` keeps the 200 latest decoded thumbnails in memory, for the open vault only (`Use`, at each vault change), so typing in the input box does not read them again.
- Icons are Segoe Fluent glyphs (`NodeVisualView.GlyphOf`): generic, note, file, image, text, code, audio, video, archive, PDF, view.

### Views
A view is a node of type `view`. Its definition is in `editView` versions, as a dataset's file description: the change holds it, so it is in the graph and the snapshot, and a view works offline. Versions replace one another as for notes (`ContentVersions`, `KnowledgeGraph.ViewVersionsOf`).

| Definition | Display | Read by (`Core/Views`) |
| --- | --- | --- |
| `TreeDefinition(root, linkType)` | `tree` | `TreeView`: `Root`, then `ChildrenOf(item)`, read when asked |
| `FlatDefinition(nodeType)` | `flat` | `FlatView.Nodes` |

```
Projects ─contains─► Alpha ─contains─► Shared      TreeItem(node, path, canExpand)
         ─contains─► Beta  ─contains─► Shared      Shared: one item under each parent
                           ─contains─► Projects    on its path: listed, not expandable (cycle cut)
```

- Children: the nodes the view's links lead to, from the node (either way for a symmetric type); nodes are ordered by name as the search compares them (`NameKey`), homonyms oldest first (`NodeOrder`).
- Both read the graph each time, so changes from elsewhere show. An unknown or removed root has no tree.

**Tabs.** `TabList` gives the main window's tabs: `Tab.Search`, then the views open on this device (`ViewTabs`, `tabs.json`, ids only, kept per vault like `recent.json` through `NodeListsFile`) that the graph knows as views; `Closed` the others, for **+**; `Next` the tab Ctrl+Tab (or Ctrl+Shift+Tab) selects, wrapping around, handled first by `MainWindow`'s key preview so the details and the input box's Tab are not reached. A tab shows the view's latest version (`ViewPlanner.Load`). How each view was left is kept by `ViewStates` (`view-states.json`, per vault and view a `ViewState`: the nodes expanded, each by `TreeItem.Key`, its path of ids, and whether Details is on), saved at each change; a key no longer in the tree is ignored. `ViewStates` and `NodeListsFile` read and write through `LocalJsonFile`.

```
MainWindow ── TabStrip (TabRow) ── Search: input box, results, details
                                └─ a view: ViewPanel (WPF TreeView, or ListBox) ──Enter──► details, Back ► the view
```

- `ViewPanel` builds a `TreeViewItem` per `TreeItem`, with a placeholder child until expanded (children read then). A refresh (`Show`, at each change of the vault) keeps the expanded paths (`ViewStates`) and the selection.
- The details opened from a view go back to it (`NodeDetailsPanel.HomeLabel` names it); selecting another tab leaves them (the note saved).
- The creation screen's view step (`CreationForm.IsView`, `ViewDraft`): a tree needs its root, found by `ViewDraft.FindRoots` (the search, 8 nodes); `CanLeaveStep` and `CanAdd` wait for it. `ViewsToSave` gives each new view's first `editView`, appended after the plan's changes; the main window then opens it in a tab.
- Editing a view later: `VaultSession.EditView` (from **Edit…** in the details, `NodeDetailsPanel.View`, or **⚙** on the selected tab, `MainWindow.Tabs`) loads it (`ViewPlanner.Load`: versions, latest definition, conflict), shows `ViewDialog`, then saves `ViewPlanner.Save`: a version replacing every version loaded, or nothing when unchanged. `ViewDraft.Of` turns a saved definition back into a draft (a root gone is to choose again; an unknown type kept by its id); `ViewDraft.Describe` says it in a sentence.
- `ViewDefinitionEditor` (display, root search, link type, node type) is the one editor of a draft, on the creation screen and in `ViewDialog`.

**Operations.** Each planner gives a plan to show first, then its changes; the tree's link type drives them, with the same children as the tree (`TreeLinks`).

| Planner | Plan | Changes |
| --- | --- | --- |
| `DeletePlanner.Plan(item)` | `DeletePlan`: the nodes (the node and its whole subtree, even a node with another parent), their links, `OtherParents` | one `remove`: the nodes, and every copy of their links |
| `MovePlanner.Plan(item, target)` | `MovePlan`: `Problem` (`Root`, `SameParent`, `IntoItsSubtree`) or none | `remove` of the link from the parent shown (every copy), `addLink` from the target; other parents stay |
| `CopyPlanner.Plan(node, target, isAvailable)` | `CopyPlan`: the nodes, `Missing` (content not synced: no copy) | new nodes (same type and name), their links and one to the target; each note, dataset and view gets its original's latest version |

- A copied note's or dataset's content is written again under the new version: `Vault.WriteCopies(plan.Contents)` first (it touches no memory, so a large dataset can be sealed off the UI thread), then each change is appended (`VaultSession.Copy`). Stopped halfway, it leaves versions of nodes not added: never shown.
- In the app, `ViewPanel.Operations` keeps the node taken (Ctrl+C or Ctrl+X, in memory, not the Windows clipboard) and pastes it under the selected one (Ctrl+V); Del deletes. A node cut is drawn dimmed (its `TreeItem.Key`: that place only, not its other parents) until pasted, or `CancelCut` (Esc, from `MainWindow`). `OperationPreview` gives the text shown first (`MessageDialog.Confirm` with its lines): a deletion and a copy are confirmed, a refused move or a copy of content not synced (`Vault.HasContent`) is only told. After a change the tree keeps the target expanded and selected.
- The search of a view (`ViewPanel.Search`): `TreeView.Find` walks the whole tree in the order shown (a node, then its children; each place of a node with several parents; cycles cut as in the tree), `FlatView.Find` the list, both with `GraphSearch.MatcherOf`, the input box's matching. The place gone to is kept by its key: typing stays on it while it matches, Enter moves from it. Each redraw (`Show`, at each change of the vault) counts the nodes found again without moving (`RefreshFound`): *6 found* when the place gone to no longer matches. Going to one expands `TreeItem.AncestorKeys` (kept in `ViewStates`, as by hand), selects it, scrolls to it. The text typed is never logged; Esc goes through `ViewPanel.HandleEscape` (the search, then a cut).
- Details (`ViewPanel.Details`): `NodeFacts.Of` gives a node's type (a dataset's: `DatasetType` of its latest file), size (that file's) and last change (the latest of its creation, `NamesOf` and its content versions), all metadata. A row with details is a `Grid`: the name (`*`, cut short), then fixed columns, under `DetailsHeader`. A tree's row is made as wide as the tree's viewport less its own start (`FitToTree`, at `Loaded` and when the tree is resized), since a `TreeViewItem` does not stretch its header; a list's `ListBoxItem` stretches it.
- Renaming in place (`ViewPanel.Rename`, F2 or **Rename**): the name's `TextBlock` in the row's header gives way to a `TextBox`; Enter plans with `RenamePlanner` (an `Empty` or `LinkPhrase` problem: refused, its `Status` in a tooltip under the box), leaving it saves when it can, Esc (first in `HandleEscape`) cancels. While renaming, the view's keys, drag and double-click leave the box alone; a redraw (`Show`) drops it. A list's rows are built in code (`RowOf`), as the tree's, so both share the header and the rename. Empty names are refused wherever a name is set: the input box (`InputParser`), the creation screen (`CanAdd`), renames (`RenameProblem.Empty`).
- The bar over a view (`ViewPanel.Actions`): **Edit** opens the node selected (`OpenSelected`, as Enter); New…, Edit and Rename are also first in the tree's and the list's right-click menus. Cut, Copy, Paste and Delete run the same operations, enabled with a node selected (Paste: with one taken too). **New** opens `CreationForm.NewNode` through `VaultSession.CreateNode`: in a tree with `Under` (`ParentLink`: the node selected, or the root, and the tree's link type), whose `LinksToSave` adds the link from it with the new node; in a list with the list's type fixed. The new node is then selected.
- Drag and drop (`ViewPanel.DragDrop`): the drag starts past the system's minimum distance, carrying the `TreeItem` (format `kofrenot.TreeItem`, inside kofrenot only). Over a node, `MovePlanner.CanMove` gives the cursor (move, or none when refused); Ctrl held is a copy. The drop runs the same move or copy as Ctrl+V.

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
- A creation without typed text (`CreationForm.NewNode`, from **New view…**) is one node request: `IsNameEditable`, the name typed on the screen (`Name`; `CanAdd` waits for one), and with a type given, `IsTypeFixed`: the type list is hidden, and changing it throws.
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
- Search rows and detail links are two lists (`MainWindow`, `NodeDetailsPanel`) sharing their size, **›** button and row style (`Views/SharedStyles.xaml`: `RowFrame`, highlighted when hovered; `RowSelectionBar`, the accent bar of the selected row). A click selects a row through the same logic as the keys (`InputPresenter.Select`, `NodeBrowser.Select`; while a link is typed, it chooses a homonym), a double-click opens it as Enter does. Nothing takes the focus, so typing stays in the input box.
- `NodeDetailsPanel` takes the place of the search in `MainWindow` and handles Up/Down/Enter/Esc while shown. It raises `BackToSearch` when the trail is empty. The trail is closed when its vault closes.
- Layout: breadcrumb; properties, and for a dataset its file on their right (`DatasetPanel`, half the width: file, then its buttons, wrapped when narrow; the column has no width for other types); a note's editor under them; links across the width. The properties row grows only while a dataset's text is shown (`FitDatasetRow`).

## Logging
- `Core` writes through `ILog` (`log.Info(category, message)`); only `App` knows Serilog.
- Category: the area (`App`, `Action`, `Settings`, `Window`, `Update`, ...).
- **Info**: every action the user triggers, start/stop. **Warning/Error**: failures. **Debug**: details.
- Never logged: node and vault names, note or file content, typed text, keys, pass phrases. Ids and counts are fine.
- Local only: 3 files x 1 MB, plus the last 1000 entries in memory for the log window.
- The file is never restarted: each start appends to `kofrenot.log`, and a new file begins only at 1 MB. Every entry is flushed at once, so what happened before a crash stays in the file. The log window shows the current run only.

## Versioning
`<Version>` in `Directory.Build.props` is the only place the version is written. It is stamped into every assembly (AssemblyVersion, FileVersion, InformationalVersion), read back through `AppVersion` in `Core`, and passed to the installer by `build/installer.cmd`. Rules: [GOAL.md](GOAL.md#versioning).

The build identity is the git commit, not a build number. The SDK appends it to InformationalVersion (`0.4+<commit>`, also the exe's *Product version*), and `Directory.Build.targets` adds `.dirty` when the working tree has uncommitted changes. `AppVersion.Of` drops it (main window, update check, tray); `AppVersion.WithCommitOf` shows the short commit (`0.4 (4e9c313)`, `0.4 (4e9c313-dirty)`) in Settings > About and the startup log line; `AppVersion.BuildOf` keeps it whole, to tell copies apart. Built outside git, there is no commit and only the version shows.
