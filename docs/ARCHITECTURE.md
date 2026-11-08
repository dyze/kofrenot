# kofrenot: architecture

## Projects

```
kofrenot.App   (WPF, Windows only)
│  main window (input box, node details, note editor, dataset panel), creation dialog, settings window, pass phrase dialog, log window, changelog window, tray icon
│  WpfQuestions (the session's questions as dialogs)
│  adapters: SerilogFileLog, SingleInstance, NativeWindows + WindowPlacer (window positions), GitHubReleaseFeed, Browser,
│            DpapiProtector, GlobalHotkey, VaultWatcher, DispatcherUiThread, WindowsClipboard
│
│  implements ▼  IReleaseFeed · ILog · ISecretProtector · IUiThread · IVaultWatcher · IThumbnailMaker · ISessionQuestions · IClipboard
│
│        kofrenot.Android   (Avalonia, Android; reads server vaults: ANDROID-CONCEPT.md)
│        │  screens: sign in, pass phrase, vaults, home (search, pinned, recent, view tabs), node details, settings
│        │  adapters: KeystoreProtector, AvaloniaUiThread, PreparedAnswers (ISessionQuestions), NothingToWatch, AndroidThumbnailMaker, PickedFiles, CameraCapture
│        │  implements ▼  ILog · ISecretProtector · IUiThread · IVaultWatcher · IThumbnailMaker · ISessionQuestions
▼        ▼
kofrenot.Core  (no Windows dependency, fully unit-tested on any OS)
   Graph: KnowledgeGraph · Node · Link · TypeCatalog · CustomNodeTypes · NodeTypeSets · FieldValues · CustomLinkTypes · LinkAliases · Change
   Fields: FieldKind · FieldDefinition · FieldValue
   Storage: Vault · VaultFolder · VaultFormat · SecretBox · ChunkedBox · KeyWrap · SnapshotStore · NoteFiles · DatasetFiles · ThumbnailFiles · KeyMemory · VaultNameMemory
   Server: KofreClient · ExchangeStats · ServerSync · VaultMove · ServerSessionMemory   ── HTTPS ──►  Kofre server (own repository: kofreserver)
   Session: VaultSession · ServerConnection · SyncService   (the open vault and the registered ones; the server account; syncs in the background)
   Input: InputParser · GraphSearch · AddPlanner · InputPresenter · CreationForm · TypeSetPlanner · NodeTypePlanner · FieldPlanner
   Browse: NodeBrowser · NodeDetails · NodeFields · NodeVisual · NodeFacts · PinPlanner · RecentNodes · StartNodes · ViewTabs · ViewStates · ImportDestinations
   History: NodeHistory · RestorePlanner
   Notes: NotePlanner · MarkdownFormat
   Datasets: DatasetPlanner · FileImportPlanner · DatasetType · TextPreview · ByteSize · OpenedFiles · IThumbnailMaker · PasteOffer · IClipboard
   AppFiles · AppVersion · RunningCopy · QuitRefusal · Changelog · ProjectLinks
   SettingsStore · SettingsCatalog · Hotkey · VaultList
   PlacementRules · PlacementStore
   UpdateChecker · UpdateStore · Release
   ILog · RingLog · CompositeLog · FilteredLog
```

- All domain logic goes in `Core`. When it needs Windows, it takes an interface defined in `Core`, and `App` supplies the implementation.
- `App.xaml.cs` wires everything together at startup.
- Tests: `tests/kofrenot.Core.Tests` (xUnit), `Core` only.
- `samples/DemoVault` (not in `kofrenot.sln`): builds a demo vault of made-up data in `artifacts/demo` through `Core` (its planners, as the app writes: types, fields, link types, graph views, a history and a conflict), with `App`'s `WpfThumbnailMaker` and `DpapiProtector`, for screenshots (README, *Development*).
- The server, Kofre, is generic (accounts, stores, files: no kofrenot term) and lives in its own repository; kofrenot only talks to it over HTTP: [kofreserver](https://github.com/dyze/kofreserver), [SERVER-CONCEPT.md](SERVER-CONCEPT.md#generic-by-design).

### Folders
One namespace per folder (`Kofrenot.Core.Settings`, `Kofrenot.UI.Views`, ...). The tests use the same folders as `Core`, and import every `Core` namespace once in their `.csproj`.

| `kofrenot.Core` | Content |
| --- | --- |
| *(root)* | `AppVersion`; `RunningCopy` + `AppCopy` (another copy running: same or not, and what the user is told); `QuitRefusal` (why kofrenot must not quit, or switch vaults, now); `AppFiles` (where every file of kofrenot is, including `keys.json` and the snapshots in `cache\`: the only place their names are written, listed in Settings > About; `AppFiles.For` picks the data folder, see [Portable copy](#portable-copy)); `Changelog` (reads the text shown by Settings > About); `ProjectLinks` (website, issues, releases) |
| `Settings/` | `SettingsStore` (`settings.json`: theme, debug logging, update check, the registered vaults and the one selected, hotkey, server account, the Settings tab shown last, the height of a note's links; `IsHotkeyTaken`), `Hotkey`, `KeyBindings` (every shortcut, by area, for Settings > Key bindings: written by hand, **update it with the key handlers**), `VaultList` + `RegisteredVault` + `VaultLocation` + `StoreAddress` (see [Registered vaults](#registered-vaults)), `ServerAccount`, `SettingsCatalog` (the entries of the Settings window, their tabs, and the search that finds them) |
| `Fields/` | `FieldKind` + `FieldKinds` (what a field holds, how it is written), `FieldDefinition` (a field of a type), `FieldValue` + `FieldInput` (a value typed, written and shown), see [Types and fields](#types-and-fields) |
| `Graph/` | `Node`, `Link`, `NodeType` (a name, an icon, a colour and fields), `LinkType`, `LinkVerb` (a link type read one way), `TypeCatalog` (built-in node and link types, or a vault's with the user's link types and aliases; `LinkName`, `VerbOf`: how a link reads from one of its nodes), `CustomNodeTypes` + `CustomNodeType` (the user's node types, see [Types and fields](#types-and-fields)), `TypeIcons`, `CustomLinkTypes` + `CustomLinkType` (the user's link types, see [User link types](#user-link-types)), `LinkAliases` (the user's aliases, see [Aliases and removals](#aliases-and-removals)), `RemovableChanges`, `NameKey` (the form names are compared in), `Change` (`NodeAdded`, `LinkAdded`, `NodeRenamed`, `NodeRetyped`, `NodeTypeAdded`, `NodeTypeRemoved`, `NodeTypeDefined`, `FieldSet`, `NodeColored`, `AliasAdded`, `LinkTypeDefined`, `NodePinned`, `ChangesRemoved`, `ContentEdited`: `NoteEdited`, `DatasetEdited`, `ViewEdited`, `MappingEdited`; `VaultNamed`), `ViewDefinition`, `MappingDefinition` + `FieldRule` + `LinkRule` (a mapping's definition, see [Imports](#imports)), `NodeColor` + `ColorVersion` + `NodeColorNames` (the palette, see [Colours](#colours)), `DatasetFile` (a dataset version's file: name, size, SHA-256), `CurrentVersions` + `IReplacing` (the versions no other replaces: shared by names, types, contents and the vault's name), `NodeNames` + `NameVersion` (a node's current names, see [Renames](#renames)), `NodeTypeSets` + `TypeVersion` (a node's set of types, see [Types and fields](#types-and-fields)), `FieldValues` + `FieldVersions` (the current values of a node's fields), `NodeVersions` + `INodeVersion` (the current versions of a note, dataset or view, or of a colour, see [Notes](#notes)), `KnowledgeGraph` (in-memory state, built by applying changes) |
| `Storage/` | `VaultFormat` (+ `NewerVaultFormatException`, `Since`, `UpgradeQuestion`), `VaultUpgrades` (upgrades agreed, `vault-upgrades.json`, see [Upgrade](#upgrade)), `SecretBox` (AES-GCM seal / open), `ChunkedBox` (AES-GCM over 1 MB chunks, streamed), `KeyWrap` (KDF, wrap / unwrap the master key), `ChangeData` (a change as JSON, shared with the snapshot), `ChangeCodec` (change ⇄ sealed JSON), `VaultHeader`, `VaultFolder` (layout, inspect, create, `SizeOf` + `FolderSize`: its files and bytes, shown in Settings > Vault, measured off the UI thread), `SnapshotStore`, `NoteFiles` (note content, `content/`), `DatasetFiles` (dataset content, `content/`: describe a file, write, export, open), `ThumbnailFiles` (thumbnails, `content/`), `Vault` (open, append, refresh, save a note or a dataset version, `WriteDataset`: its files only, off the UI thread; `Vault.Upgrade`: its format, writes refused until an upgrade is agreed), `AtomicFile` (write as `.tmp`, then rename), `KeyMemory` (remembered master keys, `keys.json`) and `VaultNameMemory` (the last name read in each vault, `vault-names.json`), both through `ProtectedStore`, `ISecretProtector` |
| `Input/` | `InputParser` (text → `NodeRequest` / `LinkRequest`), `GraphSearch` (`SearchHit`), `AddPlanner` (`AddChoices` → `AddPlan`: `NodePlan`, `LinkPlan`, `PlannedSide`), `InputPresenter` (the input box: preview, result rows, homonym choices), `QueryParser` + `NodeQuery` + `TimeQuery` (a query typed after `?`: a type, a link phrase, a time; `QueryCompletion` proposes its words and lists the ways to start one) run by `NodeQuerySearch` (`QueryResult`), `HomeRoom` + `HomeColumns` (the empty box's rows fitted to the window: how many, in which columns), `ChipInput` (`InputChip`: its labels) and `InputCompletion` (`Proposal`: the rest shown in grey), `CreationForm` (the creation screen: steps, types, note content, dataset file, status, what exists), `PlanText` (wording shared by both), `RenamePlanner` (`RenamePlan`: a new name, its problem, homonyms), `VaultNamePlanner` (`VaultNamePlan`), `TypeSetPlanner` (`TypeSetPlan`: a type added to a node or taken out), `LinkChangePlanner` (`LinkChangePlan`: a link's type or direction, as a removal and a new link), `DetailsLink` (the link row of the details: + Link, Ctrl+K), `AliasPlanner` (`AliasPlan`), `LinkTypePlanner` (`LinkTypePlan`, `LinkTypeRemoval`), `NodeTypePlanner` (`NodeTypeDraft`, `NodeTypePlan`, `NodeTypeRemoval`: the user's node types), `FieldPlanner` (`FieldPlan`: a node's field values), `TypeCopyPlanner` (`TypeCopy`: node types copied from another vault), `LinkTypeCopyPlanner` (`LinkTypeCopy`: link types and aliases copied from another vault) |
| `Browse/` | `StartNodes` + `TimeAgo` (what the empty input box lists: pinned, then recent with their latest activity), `OfflineNodes` (kept offline on this device), `IconPaths` (the [icons](#datasets) of `Icons/`, by name), `NodeBrowser` (the details screen: trail of open nodes, at most 4, the selected link of each), `NodeDetails`, `LinkRow`, `NodeVisual` (`NodeIcon`: how a node is pictured in rows, see [Datasets](#datasets)), `NodeFacts` (type, size, last change: a view's Details), `PinPlanner` (pin or unpin), `DeletionPlanner` (`NodeDeletion`: one node deleted from its details, with its links and pins, or one link), `RecentNodes` (the nodes last opened on this device, with when), `ViewTabs` and `ViewStates` (the tabs open, the nodes expanded in each tree, on this device), `ImportDestinations` (where the last import went, per vault, on this device), through `LocalJsonFile` `NodeFields` + `NodeField` (a node's fields, from its types and its own) |
| `Imports/` | Records of a file imported as nodes, see [Imports](#imports): `MappingPlanner` (`MappingState`: the mappings, one loaded, added, saved; `Suggest`: field rules by name), `JsonRecordReader` (`RecordFile`, `ImportRecord`: a `.json` array of objects, keys as dotted paths), `ImportPlanner` (`RecordPlan`, `RecordItem`, `RecordOutcome`, `RecordChoice`: each record new, updated, to check, unchanged or not imported, then the changes as chosen), with `ImportRules` (the rules against the vault's types) and `NodeFinder` (a record's node: match value, then names); nodes of another vault: `VaultImportPlanner` (`VaultImportPlan`, `VaultImportItem`, `VaultImportWrite`), with `VaultNodeWrites` (a node added or what differs), `VaultLinkPlanner` (its links, `LinkWriter`) and `VaultContent` (a vault's content as read) |
| `History/` | `NodeHistory` (`HistoryEntry`, `VersionKind`: a node's versions, newest first), `RestorePlanner` (`RestorePlan`: a version written again), see [History](#history) |
| `Notes/` | `NotePlanner` (`NoteState`, `ConflictVersion`: load a note, plan its next version), `SharedNotePlanner` (`SharedNote`: text shared by another app as a new note, previewed first), `MarkdownFormat` (`TextEdit`: the toolbar's Markdown), `TaskList` (`TaskItem`: a note as a task list), `TaskListEditing` (a task list being edited: the order shown, checked items collapsed, an item added, removed or moved within its part; used by both apps) |
| `Datasets/` | `DatasetPlanner` (`DatasetState`, `DatasetChoice`: load a dataset, plan an import or the version kept from a conflict), `FileImportPlanner` (`ImportFile`, `ImportRow`, `ImportPreview`, `ImportPlan`, `FileImportWalk`: files and folders dropped or chosen turned into new datasets and generic nodes, or updates of those there, previewed first), `FolderExportPlanner` (`FolderExport`, `ExportedFile`: nodes dragged out turned into files and folders), `OpenedFiles` (files handed to another app: opened with it, or written by the camera app; deleted when done or at the next start), `CapturedFile` (`CaptureKind`: a photo or video taken, named after when), `ByteSize` (sizes as shown: 1.2 MB), `DatasetType` (`DatasetCategory`, `DatasetViewer`: the inner type, from the extension, its category and viewer), `IThumbnailMaker` (implemented by App), `PasteOffer` (`Pasted`: what the clipboard holds that a Paste button takes, read when pasted; a pasted image's name), `IClipboard` (`ClipboardContent`, implemented by App), `TextPreview` (the beginning of a text, for the viewer), `ImageZoom` (the image window's zoom: fit, steps, percent) |
| `Server/` | Server vaults, see [Server vaults](#server-vaults): `KofreClient` (the server's REST API, renews its token; `IStoreFiles`, `ServerException` + `ServerFailure`), `ExchangeStats` (`ExchangeKind`, `ExchangeDay`, `ExchangeRow`: the exchanges counted, see [Logging](#logging)), `SyncStatus` (where a server vault's sync stands, and how it is told), `ServerSync` (mirror ⇄ store), `StorePaths`, `VaultMove` (a folder vault into a mirror), `ServerSessionMemory` (`server-session.json`, through `ProtectedStore`) |
| `Session/` | What a client holds while it runs, shared by every client ([Android](ANDROID-CONCEPT.md#architecture)): `ServerConnection` (the server account: sign up, in, out; its session kept through `ISecretProtector`), `SyncService` (syncs every server vault in the background, see [Server vaults](#server-vaults); its timers through `TimeProvider`), `IUiThread` (the thread their events are raised on); `VaultSession` (the registered vaults and the open one: create, unlock, lock, switch, register, write, upgrade (`.Upgrade`), keys (`.Keys`, `.Trust`), the account's pass phrase (`.Account`), server vaults (`.Server`), sharing and members (`.Sharing`, `.Members`)), asking the user through `ISessionQuestions` (pass phrases, recovery keys, confirmations, the creation and view screens, and `Tell` for what failed) and watching a folder vault through `IVaultWatcher`. A client that edits content only (`ClientMode.Content`, `VaultSession.EditsContentOnly`: Android) saves notes and imports files (`CanWrite(ask, content: true)`), refuses every other write to a vault or the server, and does not open a vault that needs one first (an upgrade, a hand-over to the account: `CanUseHere`) `VaultSession.OtherVaults` (another registered vault read without switching to it: its node types) |
| `Logging/` | `ILog` (with `NullLog`), `LogEntry`, `RingLog`, `CompositeLog`, `FilteredLog` |
| `Placement/` | Window positions: `WindowPlacement`, `PlacementRules`, `PlacementStore` (see [Window positions](#window-positions)) |
| `Updates/` | The daily update check, see [Updates](#updates): `UpdateChecker`, `Release`, `IReleaseFeed`, `UpdateStore` |

| `kofrenot.App` | Content |
| --- | --- |
| *(root)* | `App.xaml`: startup and wiring; `App.Hotkeys` (the hotkey that shows kofrenot, and the vaults') |
| `Views/` | Windows: `WpfQuestions` (the session's `ISessionQuestions`, as the dialogs below); `MainWindow` (input box, shows `InputPresenter`, its rows in a `ColumnFlowPanel`; `MainWindow.Home`: measures them and the room for `HomeRoom`; `MainWindow.Vaults`: the vault box, `VaultChoice`, and a vault's hotkey), `NodeDetailsPanel` (in the main window, shows a `NodeBrowser`; `NoteEditor` for a note, its links under it as tall as their rows up to a dragged height (`NodeDetailsPanel.LinksArea`), `DatasetPanel` for a dataset: import, export, drag in and out, and `DatasetView`, its viewer), `NodeVisualView` + `ThumbnailCache` (a node's picture in rows), `IconView` (one of the [icons](#datasets)), `CreationDialog` (shows a `CreationForm`, one step at a time; the node types in a list with their icons (`TypeChoice`), the focus starting on the chosen one; the link type in a combo, as it has six entries), `MarkdownBox` (note text and toolbar), `SharedStyles` (the type scale: title, heading, body, secondary, caption, footer; shared buttons), `SettingsWindow` (tabs General, Key bindings (`KeyBindingsSection`), Vault (`VaultsSection`; `StoreChoiceDialog`, `DeleteServerVaultDialog`), Server (`ServerSection`), Node types (`NodeTypesSection` with `CopyLine`, `NodeTypeForm`), Link types (`LinkTypesSection`, `LinkTypesSection.Copy` with `LinkCopyLine`), Appearance, Logging, About; `HotkeyCapture`: the shortcut pressed in a hotkey box), `PassphraseDialog`, `HistoryDialog` (a node's versions, from `NodeDetailsPanel.History`), `ImportsWindow` (**Imports**, from `MainWindow.Records`: the mappings), `MappingDialog` (+ `MappingRows`: a mapping edited, a sample tried), `RecordImportDialog` (+ `RecordImportView`: an import's preview, the choices made), `ImageWindow` (a dataset's image), `LogWindow`, `ChangelogWindow` (Settings > About > *Show changelog*); `MessageDialog` (messages and questions in the app's theme, instead of the Windows `MessageBox`); `AppTheme` |
| `Tray/` | `TrayIcon` |
| `Platform/` | Implementations of the `Core` interfaces (`GitHubReleaseFeed`, `DpapiProtector`, `DispatcherUiThread`), `WpfThumbnailMaker` (`IThumbnailMaker`), `DragExport` (files exported, then dragged out), `Browser` (opens web pages), `FileLauncher` (a dataset's file opened with its app, decrypted by Core's `OpenedFiles` into `open/` of the data folder, emptied at each start), `SingleInstance`, `GlobalHotkey` (`RegisterHotKey`, several by id), `WindowsClipboard` (`IClipboard`: `AddClipboardFormatListener` on a message-only window; an image read as its own PNG, else its bitmap encoded so), `VaultWatcher` (`FileSystemWatcher`) + `VaultWatching` (`IVaultWatcher`), and window placement (`NativeWindows`, `WindowPlacer`) |
| `Logging/` | `LogHub`, `SerilogFileLog` |
| `Assets/` | `kofrenot.ico`: the K of the app, 16 to 256 px (also the tray's, the windows' and the installer's) |

| `kofrenot.Android` | Content |
| --- | --- |
| *(root)* | `MainApplication`, `MainActivity` (`FLAG_SECURE`; syncs while shown: `App.Resumed`, `Paused`), `App.axaml` (styles, the rows' templates), `AppServices` (wires Core once, in the app's private folder); `LogHub` and `SerilogFileLog` linked from App |
| `Views/` | `Shell` (the screens, back, messages, confirmations and texts asked (`AskText`: a remark under the box, OK once it is fine) over them; opens a vault, asking its pass phrase first; `Run`: a tap's task, an unexpected failure logged and told, not a crash. A screen is refreshed when the vault or the account changes; after a sync only its status is shown again, `IPage.SyncChanged`, so lists keep their place), `SignInPage`, `PassPhrasePage` (checks the pass phrase off the UI thread, `VaultSession.UnlockAheadAsync`; the opening then uses what it unlocked, once), `VaultsPage`, `HomePage` (search, pinned, recent, view tabs; `TreeRow`; the search box's text read as the input box's, what it adds offered under it), `CreationPage` (a `CreationForm` filled on the phone, added by `VaultSession.Add`: the link's type, each new node's type, a note's text), `NodePage` (a `NodeBrowser`; `NodePage.Actions`: **⋯** rename, colour, pin, delete through Core's `DeletionPlanner`, a link removed by a long press; `NodePage.Fields`: a field filled by a tap, one of its own added, a type added or taken out, a conflict's value kept, through Core's `FieldPlanner`, `TypeSetPlanner`, `RenamePlanner` and `ColorPlanner`; `NodePage.History`: its versions, restored), `TaskListEditor` (a note's items, on `TaskListEditing`), `TabStripPanel` (the tabs that fit, `TabFit`), `SettingsPage` (and `MembersPage`: the open vault's members, shared, a role changed, left; `VaultSession.CanManageMembers`, a removal left to Windows), `Rows` (`NodeRow`, `HeaderRow`, `NodeLook`: colours), `IconResources` (the icons as resources) |
| `Platform/` | `KeystoreProtector` (`ISecretProtector`, an AES key of the Android Keystore), `AvaloniaUiThread`, `PreparedAnswers` (`ISessionQuestions` answered before the session asks: Android has no dialog that waits; a pass phrase asked is noted in `Needed`, for `PassPhrasePage`; a question the phone asked on its own screen first answered by `Confirmed`, a share by `Share`), `NothingToWatch`, `AndroidThumbnailMaker` (`IThumbnailMaker`: `BitmapFactory`, decoded at a fraction of its size), `PickedFiles` (files picked with Android's picker as `ImportFile`s, read through the `ContentResolver`), `SharedContent` (what another app shares: `MainActivity` takes `SEND` and `SEND_MULTIPLE` of any type, as one `SingleTask` instance; files become `ImportFile`s, text a note through Core's `SharedNotePlanner`, added by `Shell.Adding` once a vault is open), `AppChooser` (**Open** and **Share…**: the file decrypted into `cache/open/` by Core's `OpenedFiles`, shared through `FileProvider`, `Resources/xml/opened_files.xml`, with the app picked in Android's chooser or share sheet; emptied at each start), `CameraCapture` (**＋** > *Take a photo*, *Record a video*: the phone's camera app started with `IMAGE_CAPTURE` or `VIDEO_CAPTURE`, writing one file of `cache/camera/` granted through the same `FileProvider`, so kofrenot needs no camera permission and nothing goes through the gallery; the result comes back through Avalonia's `ActivityResult` event (overriding `OnActivityResult` again, which Avalonia's activity overrides, leaves .NET Android's marshal methods out of the build, and the app crashes at start). The file, named by Core's `CapturedFile`, is imported as any file, then deleted (`OpenedFiles.Delete`; the folder emptied at each start)) |
| `Resources/` | Adaptive icon (`mipmap-anydpi-v26/icon.xml`: `mipmap-xxxhdpi/icon_foreground.png`, the K within the centre 66 dp, on `icon_background`; `mipmap-xxxhdpi/icon.png` for launchers without), theme, `network_security_config.xml` (HTTPS only, but to the phone itself) |

## Stack
.NET 10, C#, WPF with its built-in Fluent theme for light and dark (`Application.ThemeMode`, `AppTheme`). WinForms only for the tray icon (`NotifyIcon`). Avalonia on Android. xUnit for tests.

| Package | Project | Use |
| --- | --- | --- |
| `Serilog`, `Serilog.Sinks.File` | App, Android | Log file |
| `Avalonia.Android`, `Avalonia.Themes.Fluent` | Android | UI on Android, its Fluent theme for light and dark |
| `Xamarin.AndroidX.Core` (from `Avalonia.Android`) | Android | `FileProvider`: a dataset's file handed read only to another app (*Open with…*) |
| `coverlet.msbuild` | Tests | Coverage of `Core`, build fails below 95 % lines |
| `dotnet-reportgenerator-globaltool` | Local tool (`.config/dotnet-tools.json`) | HTML coverage report (`build\windows.cmd`) |

The update check (`GitHubReleaseFeed` in App) and the server (`KofreClient` in Core) use .NET's own `HttpClient`: kofrenot's only network access. Encryption uses .NET's own `AesGcm` and `Rfc2898DeriveBytes` (Core), and `ProtectedData` (DPAPI, part of the Windows desktop framework, App): no package.

Shared build settings and the version are in `Directory.Build.props` (`Version`, `-dev` between releases, and `VersionNumber` derived from it for the places that take numbers only: the APK's version code, the setup's `VersionInfoVersion`); output goes to `artifacts/`. Builds are deterministic and `DeterministicSourcePaths` replaces the local repo path with `/_/` in DLLs, their embedded debug info (`DebugType=embedded`: no `.pdb` files) and Source Link data, so no local path (or user name) ships. The installer is built with [Inno Setup](https://jrsoftware.org/isinfo.php) (`build/installer.cmd`, `build/installer/kofrenot.iss`), a tool installed on the PC, not a package.

Android (`build\android.cmd`): an APK for Android 8.0 (API 26) and later on arm64 (`build\android.cmd emulator` adds x64, for an emulator on a PC: `RuntimeIdentifiers` in the project, chosen by `KofrenotEmulator`), its version name from `<Version>` (`0.10-dev`), its version code from `VersionNumber`, the version without `-dev` (0.9 is 9, 1.0 is 1000), profiled AOT for a faster start, always built from clean: an incremental build after a failed one can leave out the marshal methods (no native `onCreate`, a crash at start, yet a build that succeeds). Not in `kofrenot.sln`: it needs the .NET Android workload. Android's implicit usings `Android.App` and `Android.Widget` are removed, as they clash with Avalonia's (`Application`, `Button`).

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
 ├─ ServerConnection, SyncService, VaultSession (KeyMemory + VaultNameMemory + DpapiProtector, DispatcherUiThread, VaultWatching, WpfThumbnailMaker)
 ├─ MainWindow (+ WindowPlacer "Main") ─► UpdateChecker (background, once a day)
 ├─ GlobalHotkey: Ctrl+Alt+K by default ─► MainWindow shown; each vault's ─► MainWindow.ToggleVault
 ├─ TrayIcon: Show · Settings · Log window · Quit
 ├─ --hidden? ─ no ─► MainWindow shown
 │            └ yes ─► stays in the tray until the window is first shown
 └─ once idle after the window is shown: VaultSession.Open(WpfQuestions) (the selected vault) ─► remembered key, or PassphraseDialog (create / unlock) ─► Vault.Open + VaultWatcher
```

- **Title bar** of the main window (`MainWindow.Caption`): drawn by kofrenot with WPF's `WindowChrome` (`CaptionHeight` 40, glass frame, `UseAeroCaptionButtons`: Windows still draws minimize, maximize with its Snap Layouts, and close, whose 138 px are kept free). It holds the app icon, the vault box (or the vault's name, or *kofrenot*), the lock, *Read only*, the sync status and Settings, marked `IsHitTestVisibleInChrome`; the rest drags the window. The tabs have the row below to themselves, so they do not move with the vault box. Maximized, the content is moved back in by the window's frame (`FitMaximized`).
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
- **Start with Windows**: the per-user Run key `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`, value `kofrenot` = `"<exe>" --hidden` (`AutoStartCommand`, `IAutoStart` in Core; `RegistryAutoStart` in App). Written by the setup task `autostart` (ticked by default) or by Settings > General, which shows it ticked only when the value starts this exe (`AutoStartStatus`: Off, ThisCopy, OtherCopy). Uninstall removes the value when it points at the installed exe.
- **Privacy screen** (`MainWindow.Privacy`, `SettingsStore.PrivacyScreen`, on by default): with a vault open, every hide (Esc, close, a vault hotkey) and minimize covers the window's content first (`PrivacyCover` over `MainArea`, hidden, so it takes no focus), then hides or minimizes it once the cover is drawn, so the taskbar and Alt+Tab pictures show the cover. A minimize is caught as `WM_SYSCOMMAND`/`SC_MINIMIZE`; one that comes another way still covers, after. The image window closes. Shown again, the window focuses **Show**; only Esc works besides it. The vault stays open.
- **Auto-lock** (`MainWindow.AutoLock`, decided by `Core`'s `PrivacyGuard`): a 5 s timer reads the idle time (`GetLastInputInfo`, input in any program) and asks the guard for the next step; `SystemEvents` (session locked, suspend) make a hide or a lock due at once (`OnWindowsLock`), the suspend waiting up to 2 s for it.

  | Step | When | What |
  | --- | --- | --- |
  | Cover | `CoverAfterMinutes` without input, the vault on screen | `CoverNow`, even with the privacy screen off |
  | Hide | Once, after Windows locked or slept, set to `Hide` (the default) | `CoverNow`, then hide the window; the dialogs stay |
  | Lock | `LockAfterMinutes` without input, or due since Windows locked or slept, set to `Lock` | cover, close the dialogs (innermost first; Settings without its question), then `VaultSession.Lock` |

  A lock comes before a hide, and stays due until the vault is closed: refused by `QuitRefusal` (a note not saved, an import), it waits, covered, and is retried each tick; the wait is logged once.
- Settings is a modal dialog over the main window. The log window can be opened in front of it and stays open after it closes.

## Portable copy
- `AppFiles.For` picks the data folder: the one after `--data` (`AppFiles.DataOption`, made full) among the start arguments; else `<exe folder>\data` when `portable.txt` (`AppFiles.PortableMarker`) is next to kofrenot.exe; else `%LOCALAPPDATA%\kofrenot`. The start line of the log says `(portable)` or `(data in <folder>)`.
- One copy runs at a time whatever its data folder: the single-instance lock and the running-copy check go by session and program path, not by data folder.
- `build\installer.cmd` copies the published app to `artifacts\installer\portable\kofrenot`, adds the marker and zips that folder (`Compress-Archive`). The update check links to the release page, which has both files.

## Changelog
[CHANGELOG.md](../CHANGELOG.md), at the repo root, in the [Keep a Changelog](https://keepachangelog.com/en/1.1.0/) format (workflow in `CLAUDE.md`). `kofrenot.App.csproj` copies it next to kofrenot.exe (build and publish), so the setup and the zip ship it; *Show changelog* in Settings > About opens it as text in `ChangelogWindow` (`Changelog.Read`, at each opening), and the setup's last page offers to open it in Notepad.

## Updates
- `UpdateChecker` asks `IReleaseFeed` (GitHub's latest release, `ProjectLinks.LatestReleaseApi`) at most once a day, when *Check for updates* is on. Nothing is downloaded: the user is shown the release page. A release is newer when its number is higher, or equal to that of a version under development (`Release.IsNewer`: `0.10` is newer than `0.10-dev`).
- The Settings button is marked until the newer release is seen: opening Settings > About acknowledges it (`ShouldNotify`, `Acknowledge`); a later release or another installed version marks it again. The About tab is marked while a newer release exists.
- `UpdateStore` (`updates.json`) is state, not a setting.

## Window positions
- Saved in `windows.json` by window name (`Main`, `Settings`, `Log`, `Image`) when a window is hidden or closed, restored when it is first shown. Kept out of `settings.json`: it is state, not a setting.
- Stored in screen pixels (WPF units change with each monitor's scaling), as the monitor's device name (`\\.\DISPLAY2`, steadier than a monitor number) and the position relative to its work area.
- `PlacementRules.Fit` (in `Core`, tested) makes a saved position visible again: back on its monitor, or on the primary one if it is gone, then shrunk to the work area and moved inside it.
- `PlacementRules.KeepVisible` does the same for a window on screen. `WindowPlacer.KeepOnScreen` applies it each time a window is shown and after a scaling change (`DpiChanged` with a new scaling: WPF also raises it with the same one, when the content changes). Every move `KeepOnScreen` makes is logged at Debug, with what it saw. It checks the visible frame (without the invisible resize borders), so a window snapped to an edge is left alone.
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
    epoch-0/9e1c….3f0a….kfw the vault key of epoch 0, wrapped for that identity, signed by who wrote it, written once
  changes/
    3f/3f9c…e1.kfc         one encrypted change, named by a random id, written once
    a0/a07b…42.kfc
  content/
    3f/3f9c…e1.kfn         the content of a note version, named by its change (editNote), written once
    b2/b2d4…07.kfd         the file of a dataset version, named by its change (editDataset), written once
    b2/b2d4…07.kft         the thumbnail of an image dataset version (JPEG, PNG when transparent; 128 px before 0.8), same name, written once, before the .kfd
```

- **Only added, never modified.** A change file is written as `*.tmp` then renamed, so a reader or the sync tool never sees half a file. `.tmp` files are ignored.
- **Folders by the id's first two characters**, so no folder holds too many files.
- An unreadable file (corrupt, wrong key, still being copied) is skipped and logged once as a warning, then tried again 2, 5, 10 and 30 s later (`VaultSession.RetryDelays`) and at each refresh; the others still load. Other files (`desktop.ini`, ...) are ignored.
- A header or change file in a newer format: see [Format](#format).
- `VaultFolder.Inspect`: a new vault is only created in an empty (or missing) folder; a folder holding other files is refused, so nothing of the user's is mixed with it. A missing folder of a vault opened before (its id known) is out of reach, never a new vault.

### Registered vaults
The vaults kofrenot knows are in `settings.json`, in the user's order, with the one selected: one vault is open at a time, and vaults never mix.

```
settings.json  "vaults": [{ "folder", "id"?, "hotkey"?, "locked"?, "server"?, "store"?, "role"? }], "selectedVault": "<folder>"
               "server": { "url", "email" }   the server account, one per kofrenot
               "vaultFolder": the selected one too, for a kofrenot 0.4 started again; read alone (0.4) ─► the only vault
```

- `VaultLocation` is where a vault is: a folder, or a store on a server (`StoreAddress`) with its mirror as folder ([Server vaults](#server-vaults)). Locations are compared by folder, ignoring case and a trailing separator. `RegisteredVault` adds its id, null until its header is read (`VaultSession.Identify`), and its optional hotkey. `VaultList` is immutable and lists each location and id once.
- Switching (`VaultSession.SwitchTo`) closes the open vault, then opens the selected one. When it does not open (pass phrase cancelled, not a vault, an error), the vault open before is used again, without asking its pass phrase. The main window refuses to switch while a file is being imported or a note cannot be saved (`QuitRefusal`).
- `Register` (Settings > Vault > *Add...*) refuses a folder holding other files, and a vault listed already, by folder or by the id in its header; a new vault whose creation is cancelled is not added. `Unregister` forgets the vault's key and name on this device; its folder is not touched.
- **Lock** (`VaultSession.Lock`, padlock or Ctrl+L): the vault is closed and marked locked in `settings.json` (`"locked": true`). `Open` leaves a locked vault closed (at start, on a switch, on its hotkey), and `SwitchTo` does not go back to the previous vault for it. `Unlock` asks the pass phrase, skipping the remembered key (kept for later), then clears the mark; cancelled, the vault stays locked.
- **Role** (`RegisteredVault.Role`, `"role": "Reader"` or `"Writer"`): this account's role in a server vault shared with it, none for its own. Set when it is added from the server (the store's role), then read in its member records each time it opens or changes (`VaultSession.MyRole`), so a list of vaults shows it without opening them (kofrenot for Android).
- **Name**: asked on creation (`PassphraseDialog.AskNew`) and written as the vault's first change, then by Rename: a `nameVault` change, encrypted like the others, replacing the current names (`CurrentVersions`), as renames do: two names made apart are a conflict, shown in Settings > Vault, resolved by naming again (`VaultNamePlanner`). `VaultNameMemory` keeps the last name read on this device (`vault-names.json`, protected by DPAPI like `keys.json`), so a locked vault shows it; otherwise its folder's name (a server vault: its server's). A server knows a vault by its store id only ([Server vaults](#server-vaults)).

### Server vaults
A vault kept on a Kofre server ([kofreserver](https://github.com/dyze/kofreserver)): one store per vault, mirrored in a local folder that `Vault` reads as any vault folder. Its files and their encryption are those of a folder vault, its keys wrapped for the server account's identity ([Account identity](#account-identity)): the server only stores them. Design and later steps: [SERVER-CONCEPT.md](SERVER-CONCEPT.md).

```
 Vault ── reads, writes ──► mirror  %LocalAppData%\kofrenot\stores\<store>\   (folder vault layout)
                              ▲  │                                    stores\<store>.sync.json: feed position, files both sides have
          downloads (feed) ───┘  └─── uploads: the mirror's files the store lacks (header, keys, contents, then changes)
                              ServerSync ◄──► KofreClient ── HTTPS, JWT ──► Kofre server
```

- **Sync** (`SyncService`, Core `Session/`, started on the UI thread through `IUiThread`): every 30 s, 2 s after a change or when the window is shown, one sync at a time, for every server vault of the list, in the background. A download is written as `.tmp` then renamed, so `VaultWatcher` sees it and the vault refreshes; after each sync the session also reads the changes downloaded (`VaultSession.AfterSync`), which is how Android, with no watcher, sees them. Android syncs both ways too (since 0.10), but only while shown (`SyncService.Pause`, `ResumeAsync`). Offline, writes stay in the mirror until the next sync: nothing is queued elsewhere. Files are only added on both sides, so a sync is two copies, never a merge.
- **Content on demand** (since 0.8): a sync leaves notes' and datasets' files (`content/*.kfn`, `*.kfd`: `StorePaths.IsOnDemand`) on the store, marked as known there so they are never uploaded back; thumbnails (`.kft`) come at once, being small and shown in lists; one missing in the mirror (deleted, or not arrived) is downloaded when shown (`Vault.ReadThumbnail`), and one the store does not have (`404`) is not asked for again. Downloads are tracked by the file's path, as a dataset version's file and thumbnail share its id. Content already in a mirror stays. **Keep offline** (since 0.10): the nodes a device keeps offline (`OfflineNodes`, `offline.json`, per vault, on this device only, like `recent.json`) have their current content versions downloaded ahead, one at a time (`Vault.KeepOfflineAsync`, through `VaultSession.KeepOfflineAsync`: when the vault opens, after each sync that went through, and when a node is kept offline); nothing is ever evicted. Nothing is written to the vault, so a reader, and the phone, keep nodes offline too.

  ```
  note shown, card in view ──Vault.ReadNote──► here? read : FetchAsync (no retry)  ──┐
  text or image viewed, export, copy, keep ──Vault.FetchAsync(retry: true) ─────────┤
                                     ContentFetcher (IContentFetcher, the account's client) ──► .tmp, then renamed
  Vault.ContentFetched ─► VaultSession: Changed once no download runs ─► what shows it reads it again
  ```

  One download per version at a time. A failure (`ContentState`: offline, sign in, refused) is kept, shown in place of the content, and not tried again automatically until a sync goes through (`Vault.RetryFailed`, in `VaultSession.AfterSync`); an action of the user tries again at once. A folder vault has no fetcher: missing content waits for the sync tool, as before. A server vault logs at Debug whether content was `already here` or `downloaded on demand`.
- **Status**: `SyncStatus` (Core): a `Level` (synced, waiting, offline, problem), `Summary` for the main window's header (a dot coloured by the level, a click syncs now or opens Settings > Server), `Details` for its tooltip, `Describe` within a sentence (Settings > Vault, messages). A server stopped behind its proxy (502, 503, 504) counts as offline. A failure is logged once, not at every try, and the sync that goes through again.
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
| From 0.6 (format 5), moved from a folder, or made by an early 0.7 | Opened with its own pass phrase, then **attached** (`VaultKeys.Attach`, when `VaultKeys.NeedsAttach`: never a vault another account owns, whose keys are its owner's to give): at its upgrade, or at its next opening when the server can be asked. Its keys are wrapped for the account, then a new epoch for the account only: the old pass phrase opens only the files written before |
| Remembered key | Used only while it opens the latest epoch (`VaultKeys.IsCurrent`); else the pass phrase is asked |

- **The account's pass phrase** is chosen at the first need (a new server vault, or the first attach), or earlier: offered right after signing in, and in Settings > Server (*Pass phrase* > *Choose...*), so others can share with the account before it has a vault. Its recovery key is then shown once. Changed in Settings > Server (*Pass phrase* > *Change...*: its current one asked, then the new one); after the recovery key unlocks a server vault, a new one is asked too.
- **Server version**: `GET /v1/version`, at sign-in and at each sync. Below 0.2 (`KofreVersion.Minimum`; a 0.1 server answers 404), Settings > Server says the server is too old, and what needs the account's data is refused (creating, attaching, changing the pass phrase); the files still sync.

#### Sharing
A server vault is shared by its owner with other accounts on the server ([design](SERVER-CONCEPT.md#sharing)), in Settings > Vault > *Members* > *Share...* (`VaultSession.Share`, `ShareDialog`):

```
email ─► GET /v1/accounts?email= ─► {id, public}: AccountIdentities.KeyOf ─► ShareCandidate
       ─► Fingerprint.Of(public key), compared out of band ─► "They read the same fingerprint"
       ─► VaultSharing.Share: VaultKeys.Share (every epoch wrapped for their identity)
                              + addMember changes (the owner's record the first time, then theirs)
       ─► PUT /v1/stores/{id}/members/{account} {role} ─► sync uploads the key files and changes
```

| | |
| --- | --- |
| Members | Recorded in the vault (`MemberAdded`, `MemberRemoved`: account, identity, public keys, email, role), each **signed by the owner** (`MemberRecords`): only those count, the latest per account (a role changed: a new record, `VaultSharing.ChangeRole`, and the role on the server; no key change); listed in Settings > Vault with their fingerprint, the owner's included |
| Fingerprint | First 160 bits of the SHA-256 of the public keys (encryption, signing), 8 groups of 4 Base32 characters; the account's own in Settings > Server (*Your key*) |
| Owner's key | `KeySigner`: this account's signing key for its own vaults (the vault's own identity before it is attached); for a vault shared with it, the owner kept in `vault-owners.json` (`VaultOwners`: read once from the store's members and the owner's public data). Epoch wraps (`KFW2`) not signed by it are ignored, by `Unlock` and the latest epoch alike |
| Member's side | The store appears in *Vault from the server…*; it opens with the member's own account pass phrase (their identity's wraps) |
| Reader | The vault opens read only: every change is refused (`VaultSession.CanWrite`); the server refuses its uploads anyway |
| Who shares | The owner only (the server enforces it), from a vault opened with its account's identity |

#### Members leaving
A member is removed by the owner (*Members* > *Remove...*), or leaves (*Leave...*: the vault goes out of their list, its copy stays). Then the key is rotated ([design](SERVER-CONCEPT.md#rotation)):

```
owner's kofrenot: DELETE /members/{account} (or, at its next opening, MembersAsync: a recorded member gone)
   ─► VaultSharing.Remove: VaultKeys.Rotate (epoch N+1, derived from the owner's private key, wrapped
      for each member left and the owner) + ChangesRemoved of their records ─► vault.UseKeys(N+1)
   ─► SyncAsync (the new keys uploaded) ─► PUT /fence {N+1}           (VaultSession.Members.cs)
other devices, after each sync (VaultSession.AfterSync):
   ─► keys/epoch-(N+1) wrapped for them: taken up (vault.UseKeys)
   ─► files refused below the fence (ServerSync.Fenced): Vault.Reseal, same id, latest key; uploaded again
```

- `ServerSync` sends `Store-Fence` with each upload: the epoch in the file's header (`SealKind.EpochOf`), else (header, keys) the mirror's latest. A `409 fence` leaves the file waiting (`SyncResult.Fenced`), never marked as on the server.
- A removed member's device: its sync is refused (`403`), shown as "no longer on the server, or no longer shared with you"; what it has stays readable to it.

### Format
One number for the whole vault, `VaultFormat.Current`, written in the header and in every change file (`format`). It is not the app version: it only changes when what is written in a vault breaks.

| Format | Since | Adds |
| --- | --- | --- |
| 1 | 0.1 | `addNode`, `addLink`, `renameNode` |
| 2 | 0.2 | directed links (`contains`, `is-a`): a 0.1 app would read them as symmetric; `addAlias`, `pinNode`, `remove`, `editNote` with the `content/` files |
| 3 | 0.3 | `editDataset` with its chunked `.kfd` content files: a 0.2 app does not know them |
| 4 | 0.4 | `remove` of nodes and links: a 0.3 app would still show them; `editView` |
| 5 | 0.5 | `nameVault`, `retypeNode`: a 0.4 app cannot read them |
| 6 | 0.7 | `keys/` and the key epoch in every file (`KFC2`, `KFN2`, `KFT2`, `KFD2`); a header without key: a 0.6 app cannot open the keys; `defineLinkType` (added before 0.7 was released, so the number did not change: owner's decision) |
| 7 | 0.8 | `colorNode`: a 0.7 app cannot read it |
| 8 | 0.11 | `defineNodeType`, `addNodeType`, `removeNodeType`, `setField` ([Types and fields](#types-and-fields)): a 0.10 app cannot read them |
| 9 | 0.13 | the field kinds `email` and `address` ([Types and fields](#types-and-fields)): a 0.12 app would drop a node type with such a field, and its values |
| 10 | 0.14 | `editView` with `"display": "graph"` and `placeNodes` ([Views](#views)): a 0.13 app cannot read them |
| 11 | 0.16 | **read-only compatible**, `oldestReader` 10, `oldestWriter` 11: the field kind `phone` (a 0.15 app keeps it as `FieldKind.Unknown`, its value shown as text); qualifiers, `setField` on a `link` and `defineLinkType` with `fields`, of any link type, built-in ones included ([Types and fields](#types-and-fields), [User link types](#user-link-types)); `addLink` with `"separate": true`; a tree's `order` ([Views](#views)); a date as vague as known, its month `1965-08` or its year `1965` (`FieldValue`). A 0.15 app skips the qualifiers and a vague date's value, ignores `fields`, `separate` and `order` (a separate link then shows as a copy, the oldest), and would drop the qualifiers of a link type it renames: so it only reads. Every file 0.16 writes carries both levels, the header too |
| 12 | 0.17 | **compatible**, `oldestReader` 10, `oldestWriter` 11: `editMapping` and the built-in node type `mapping` ([Imports](#imports)); `editView` with `"display": "query"` ([Views](#views)). A 0.16 app skips `editMapping` and a query view's `editView` (the view shows with no definition), shows a mapping as a node of an unknown type, and still writes to the vault: it cannot drop a definition it does not see, kept in its own changes. A 0.15 app only reads, as for format 11 |

#### Compatibility levels
Since 0.15, a file of a newer format can say what an older kofrenot may still do with it. Both are optional, the file's format when missing (every file written up to format 10):

| Number | Where | Meaning |
| --- | --- | --- |
| `format` | header, each change file | the format it was written in |
| `oldestReader` | header, each change file | the oldest format that can read it, skipping what it does not know |
| `oldestWriter` | header, each change file | the oldest format that can write to the vault |

`oldestWriter` is in each change file too, not only in the header: the header is written once, and the first change of a newer format raises the vault (`Vault.OldestWriter`, the highest of all its files, as `Vault.Format`).

| Found (F = `VaultFormat.Current`) | This kofrenot |
| --- | --- |
| format ≤ F | reads it |
| header or change file with `oldestReader` > F (another device has a newer kofrenot) | does not open the vault, or closes it at a refresh |
| `oldestReader` ≤ F < format | reads it with the skip rules below |
| `oldestWriter` > F, in the header or any change file | **read only** (`Vault.IsReadOnly`): the vault refuses every write (`Vault.EnsureCanWrite`), pins and graph positions too; sync keeps downloading |
| format < 1, or `oldestReader` / `oldestWriter` not between 1 and format | the file is damaged: skipped like an unreadable file |

Skip rules, for a change of a newer format:

| Found | Does |
| --- | --- |
| an unknown `op`, or a known one this version cannot apply (`setField` on a `link`, not a `node`) | skips the change, logged at Debug (a count, no id); not read again until kofrenot is updated, never kept in the snapshot |
| an unknown property | ignores it |
| a field of an unknown kind | keeps it as `FieldKind.Unknown`, its value shown as text, never dropped |

- `NewerVaultFormatException` (a `NotSupportedException`) carries the format found. `VaultSession.NewerFormat` keeps it: the main window and Settings > Vault say to upgrade, and *Get the latest kofrenot* opens the releases page.
- `VaultSession.IsReadOnly` covers both read-only cases (a reader's role, a newer kofrenot), `ReadOnlyReason` says which; the screens already hide what changes the vault. On Windows *Read only* in the title bar opens the releases page when a newer kofrenot is the reason; on Android the banner says it.
- Each new format states its `oldestReader` and `oldestWriter`, with the reason, in the format table above: compatible (older, older: an older writer cannot overwrite the addition), read-only compatible (older, new: an older writer could drop it), incompatible (new, new: keys, encryption, removals). A test writes the new format and reads it as the old one would ([QUALIFIERS-CONCEPT.md](QUALIFIERS-CONCEPT.md#deciding-a-future-formats-level)).
- A breaking change (something an older kofrenot would misread or not read) raises `VaultFormat.Current`. The owner decides it first (`CLAUDE.md`). Changes that only add something older versions ignore safely (such as an unknown type, shown by its id) do not.
- No migration: files are never rewritten, so a vault holds files of several formats, each read as written.

#### Upgrade
Every file is written in `VaultFormat.Current`, with `VaultFormat.OldestReader` and `OldestWriter`, so the first write to a vault of an older format raises it: the older kofrenot then closes it, or, from the reader's format on, opens it read only. The user agrees first; the question names both (`VaultFormat.UpgradeQuestion`: *kofrenot older than 0.16 can no longer change it, and older than 0.15 no longer opens it*).

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
keys/epoch-N/<id>.<random>.kfw   vault key of epoch N = "KFW2" · one-time public key · nonce · key · tag · signature (ECDSA P-256 of its writer; "KFW1": unsigned, read where no signature is asked)
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

- Built into .NET (`Rfc2898DeriveBytes`, `AesGcm`, `ECDiffieHellman`, `ECDsa`, `HKDF`): no package.
- **Signing key** (`Signing`): derived from the identity's private key (HKDF to a P-256 scalar), so an identity needs no second secret; its public part is in the identity (`SigningKey`) and the account's public data, added at its next unlock for an identity made before.
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
{ "format": 6, "at": "…", "op": "defineLinkType",
  "linkType": { "id": "<first definition id>", "name": "wrote", "reverse": "written by", "replaces": ["<definition id>", "…"] } }
{ "format": 4, "at": "…", "op": "pinNode", "pin": { "node": "<node id>" } }
{ "format": 7, "at": "…", "op": "colorNode", "color": { "node": "<node id>", "color": "yellow", "replaces": ["<colour change id>", "…"] } }
{ "format": 8, "at": "…", "op": "defineNodeType",
  "nodeType": { "id": "<first definition id>", "name": "Person", "icon": "Contact", "color": "blue",
                "fields": [{ "id": "…", "name": "Born", "kind": "date" }, { "id": "…", "name": "Genre", "kind": "choice", "choices": ["Novel", "Poem"] }],
                "replaces": ["<definition id>", "…"] } }
{ "format": 8, "at": "…", "op": "setField",
  "field": { "node": "<node id>", "owner": "<node type id>", "id": "<field id>", "name": "Portrait", "kind": "image",
             "file": { "name": "bruce.jpg", "size": 52430, "sha256": "9f86…08" }, "content": "<the change whose file it keeps, if not this one>", "replaces": ["…"] } }
{ "format": 8, "at": "…", "op": "addNodeType", "addType": { "node": "<node id>", "type": "<node type id>" } }
{ "format": 8, "at": "…", "op": "removeNodeType", "removeType": { "node": "<node id>", "removes": ["<node id, retype or addNodeType id>", "…"] } }
{ "format": 8, "at": "…", "op": "setField",
  "field": { "node": "<node id>", "owner": "<node type id> or own", "id": "<field id>", "name": "Born", "kind": "date", "value": "1920-10-08", "replaces": ["<value id>", "…"] } }
{ "format": 4, "at": "…", "op": "editNote", "note": { "node": "<node id>", "replaces": ["<version id>", "…"] } }
{ "format": 4, "at": "…", "op": "editDataset",
  "dataset": { "node": "<node id>", "file": { "name": "survey.csv", "size": 52430, "sha256": "9f86…08" }, "replaces": ["<version id>", "…"] } }
{ "format": 4, "at": "…", "op": "editView",
  "view": { "node": "<node id>", "display": "tree", "query": { "root": "<node id>", "linkType": "contains" }, "replaces": ["<version id>", "…"] } }
{ "format": 10, "at": "…", "op": "editView",
  "view": { "node": "<node id>", "display": "graph",
            "query": { "root": "<node id>", "depth": 3, "layout": "layered", "links": [{ "type": "<link type id>", "role": "down" }],
                       "nodeTypes": ["<node type id>"], "fields": ["Born"] }, "replaces": ["…"] } }
{ "format": 10, "at": "…", "op": "placeNodes", "place": { "view": "<view node id>", "positions": [{ "node": "<node id>", "x": 120, "y": -40 }] } }
{ "format": 11, "oldestReader": 10, "oldestWriter": 11, "at": "…", "op": "defineLinkType",
  "linkType": { "id": "<first definition id, or a built-in type's id>", "name": "wrote", "reverse": "written by",
                "fields": [{ "id": "…", "name": "Date", "kind": "date" }], "replaces": ["<definition id>", "…"] } }
{ "format": 11, "oldestReader": 10, "oldestWriter": 11, "at": "…", "op": "setField",
  "field": { "link": "<link id>", "owner": "<link type id> or own", "id": "<field id>", "name": "Date", "kind": "date", "value": "1965-08-01", "replaces": ["…"] } }
{ "format": 11, "oldestReader": 10, "oldestWriter": 11, "at": "…", "op": "addLink",
  "link": { "id": "…", "type": "<link type id>", "from": "<node id>", "to": "<node id>", "created": "…", "separate": true } }
{ "format": 11, "oldestReader": 10, "oldestWriter": 11, "at": "…", "op": "editView",
  "view": { "node": "<node id>", "display": "tree", "query": { "root": "<node id>", "linkType": "wrote" }, "order": { "field": "<qualifier id>" }, "replaces": ["…"] } }
{ "format": 12, "oldestReader": 10, "oldestWriter": 11, "at": "…", "op": "editMapping",
  "mapping": { "node": "<node id>", "source": "json", "nodeType": "<node type id>", "name": ["title_fr", "title"], "match": "<field id>", "otherNames": ["<field id>"],
               "fields": [{ "key": "publisher.name", "field": "<field id>" }], "links": [{ "key": "authors", "nodeType": "<node type id>", "linkType": "wrote", "reverse": true }],
               "replaces": ["<version id>", "…"] } }
{ "format": 12, "oldestReader": 10, "oldestWriter": 11, "at": "…", "op": "editView",
  "view": { "node": "<node id>", "display": "query",
            "query": { "linkType": "read", "nodeType": "<node type id>", "reverse": true, "node": "<node id>",
                       "qualifiers": [{ "field": "<qualifier id>", "value": "2026" }], "columns": [{ "type": "wrote", "reverse": true }] }, "replaces": ["…"] } }
{ "format": 4, "at": "…", "op": "remove", "removes": ["<node, link, alias, link or node type definition, pin or placement id>", "…"] }
```

`renameNode` was added to format 1 before 0.1 was released, so the format number did not change (owner's decision).

- Changes are applied in `at` order (then id) to an in-memory graph. Search and preview work on it only.
- A type unknown to this version is kept and shown by its id.
- Duplicate nodes (same type and normalized name) are all kept and marked; duplicate links (same type and nodes, either direction when symmetric: copies) count as one, the oldest shown (`LinkIndex`). A link added `separate` (`Link.IsSeparate`, since format 11) is no copy: keyed by its own id too, it shows beside the others (`KnowledgeGraph.LinksBetween`).
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
- It holds the changes themselves, as in their files (`ChangeData`, `SnapshotStore.Format` 5), so a new kind of change needs nothing more. As they are written again in the current format, it also keeps the highest vault format of their files and their highest `oldestWriter` (`Vault.Format`, `Vault.OldestWriter`, see [Format](#format)). A snapshot of an older format is not used: every change is read again once.
- It keeps the changes as this kofrenot read them, so a change of a newer format lost what it did not know. It also keeps the format of the kofrenot that saved it (`savedBy`): a kofrenot of another format does not use it when it holds changes newer than that reader, and reads every change again, whole.
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

### Types and fields
Since format 8 ([decisions](GOAL.md#composition-types-made-of-fields)). A node has a **set** of types; a type (`NodeType`) is a name, an icon, a colour and a list of fields (`FieldDefinition`: an id, a name, a `FieldKind`).

```
types of a node (NodeTypeSets)                    fields of a node (NodeFields)
addNode.type ─┐ retypeNode (≤ format 7)           each type, in order: its small fields
              ├─ current ones (CurrentVersions)    (empty until set)
addNodeType ──┤                                 + its own fields with a value, in the order set:
removeNodeType ── takes out by the changes          owner "own", or a field that left its type
              ▼   that gave a type                  or a type the node no longer has
   set of types (oldest first), minus             same name twice: "Born (Person)", "Born (own)"
   the user's types removed from the vault
```

| Part | How |
| --- | --- |
| Field kinds (`FieldKind`, `FieldKinds`) | Small: text, number, date, url, email, address, phone, checkbox, choice (metadata, in the change); a url, an email, an address and a phone open their link (`FieldValue.LinkOf`: itself, `mailto:`, a Google Maps search, `tel:` with its digits and leading +). Image: described in the change, its file and thumbnail stored as a dataset version's (`OfTypes`: what a type's field can be). Content: markdown, file, view (a note's, a dataset's, a view's own changes) |
| Values (`FieldValue`) | Text, written the same everywhere: number `1234.5`, date `2026-10-27`, or only its month `2026-10` or its year `2026` (since format 11; ISO text, so ranges and sorting compare them as written; `FirstDayOf` opens a calendar on it), url with its scheme, checkbox `true`/`false`, choice as its option. Typed numbers and dates are also read in the user's culture; a value read is checked against its kind, else the change is refused |
| Built-in types (`TypeCatalog`) | *generic* (none), *note* (Body: markdown), *dataset* (File: file), *view* (Definition: view) |
| User types (`CustomNodeTypes`) | `defineNodeType` (`NodeTypeDefined`), its id the first definition's; later ones replace it, two made apart are a conflict (latest shown); removed with all its definitions by `remove`. A definition of a built-in id is ignored |
| A node's types (`NodeTypeSets`, `KnowledgeGraph.TypesOf`, `TypeIdsOf`, `HasType`) | The type it was added with and the retypes of format ≤ 7 (two current ones are both kept), then `addNodeType` (`NodeTypeAdded`) and `removeNodeType` (`NodeTypeRemoved`, listing the changes it takes out, the node's id for the type it was added with). None left: *generic* |
| The type shown (`Node.Type`) | Its type with content if it has one, else its latest: what the icon, the content and the tabs follow |
| Values (`FieldValues`, `KnowledgeGraph.FieldsOf`) | `setField` (`FieldSet`): its holder (`FieldHolder`: a node, or a link since format 11), owner (type id or `own`), field id, name and kind, value (none clears it), replaces. Current values per (owner, field), two made apart a conflict |
| Qualifiers (`KnowledgeGraph.QualifiersOf`, `NodeFields.OfLink`) | A link's values, keyed by its id as a node's are (ids never collide): its link type's fields (`CustomLinkTypes.FieldsOf`), then its own, as a node's. The values of a link's copies are merged (`CopiesOf`), so two set apart are a conflict; a separate link has its own. `NodeFields.ShownOf`: the first with a value, its type's first, shown in link rows and graph lines. `FieldPlanner` takes any `FieldHolder` (a `Node` or a `Link` converts), so filling, conflicts, images and history are the fields' |
| Images (`FieldSet.Image`) | The node's picture (`NodeVisual.Thumbnail`) in the rows when it is not an image dataset: its first image field set, in the order of its fields. An image's `setField` has a `file` (name, size, SHA-256) instead of a value: its content (`.kfd`) and thumbnail are named by the change's id, or by `content`, the change whose file it keeps (one of a conflict kept). `FieldSet.Image` gives it as the `DatasetEdited` it is stored as, never written as one, so `WriteDataset`, `FetchAsync`, `ReadThumbnail`, the export and the image window are the datasets'. `FieldPlanner.SetImage` takes images the viewer decodes, up to `DatasetType.MaxImageSize`; `VaultSession.SaveImage` writes the files off the UI thread, then the change. `FieldPlanner.SetImageFrom` takes an image dataset's latest version instead (`ImagePicker.Images`): only the change is written, its `content` the dataset version's id, so nothing is copied and a newer version of the dataset leaves the field as it was (content is never deleted); no new vault format: `content` is read since kofrenot 0.11. A file shared to the phone (`ImagePicker.CanBeImage`) is offered to the nodes with an image field (`ImagePicker.Targets`), the node shown first |

- `TypeSetPlanner` adds a type (taking *generic* out, and the other type with content: a node is a note, a dataset or a view at most) or takes one out. A type with content taken out hides its content (kept by `NodeVersions` whatever the type), never deletes it; the plan says what is hidden (`TypeSetPlan.Hidden`), for the confirmation. A view asks its definition first (`VaultSession.ChangeTypes`).
- `NodeTypePlanner` adds, changes (replacing every current definition: a conflict is resolved) and removes the user's types. A field keeps its id when renamed, so its values follow it; a field taken out, or a type removed, leaves its values on the nodes as their own fields (nothing is written to them).
- `FieldPlanner` sets, clears, keeps one value of a conflict, adds a field of a node's own (a name, a kind but choice, a first value; cleared, it goes) and merges two fields of one name, keeping one value.
- `TypeCopyPlanner` copies the user's types of another vault, keeping their type and field ids: one not here is added (or added back, removed here), one here is updated (replacing its definitions) or left as it is (`TypeCopy.Outcome`); one whose name another type here has is refused. Values are never copied. `LinkTypeCopyPlanner` does the same for link types (`LinkTypeCopy`, a line per type of the other vault with a definition or aliases in use): a type of the user's keeps its id, refused when one of its names is a phrase of another link type here; a built-in type's qualifiers are copied as its definition; each alias in use is added under a new id (an id removed here would stay removed), unless the same way of the same type reads its phrase here already, or left out when another reads it. Links are never copied. `VaultSession.GraphOf` reads the other vault: unlocked as when opened (remembered key, the account's identity, else its pass phrase: `UnlockKeys` takes the vault it unlocks), opened from its folder and snapshot, never written to (a server vault as last synced).
- `GraphSearch` finds nodes by name, then by a value (`NodeFields`, word start, as names), with the field that matched (`SearchHit.Field`); a field's name and a range (`Born 1900..1950`) the nodes whose value is in it (`ValueQuery`: dates as ISO text, an end of a year or month included whole; numbers as numbers).
- `LinkSearch` finds links by a qualifier (`written by, Date 1965`, `Frank Herbert wrote, Date 1960..1970`, `, Source isfdb`): the text before the `,` a link phrase, a node and a link phrase, or nothing; after it, a qualifier's name of the links found and a value (`ValueQuery`, a word starting after any sign). `InputPresenter` lists the hits (`LinkHit.Found`: the other end of the node typed, else the node read from) instead of a plan.
- A **link** keeps no history of its own, its qualifiers' values excepted (`NodeHistory.OfLink`, restored by `RestorePlanner.Plan(link, …)`): `LinkChangePlanner` changes its type or direction with a `remove` of the link and its copies, and a new `addLink` (separate if it was), with its qualifiers' values copied (an image keeping its file), those of its old type as its own. Changed on two devices before they synced, both new links show, as any two links.

### Colours
Any node can have a colour of a fixed palette (`NodeColor`: none, yellow, green, blue, pink, purple, teal, grey), given by a `colorNode` change (`NodeColored`) that names it in lower case (null for none; `NodeColorNames`) and lists the colours it **replaces**, as a type change. A node has none until the first one, which replaces nothing.

```
c1 (yellow, replaces [])
c1 ◄─ c2 (green, device A)
c1 ◄─ c3 (blue, device B, before syncing) ⇒ conflict: green, blue; blue (the latest) shown
ColorPlanner: the colour chosen replaces every current one ⇒ conflict resolved
```

- Kept per node by `NodeVersions<NodeColored>` (shared with content versions); `KnowledgeGraph.ColorsOf` gives the current ones, `ColorOf` the one shown, `ColoringsOf` all of them for `NodeFacts.Modified`. A name this version does not know (a later palette's) is kept and shown as none.
- Shown in `NodeVisual.Color`, so every row with a node's picture (search, links, input box labels, views) draws a dot on it (`NodeVisualView`), and on the palette button next to the details' pin (`NodeDetailsPanel.Color`: a dot of it; a click opens a popup with a swatch per colour, the current one ringed and checked, each one in a conflict; a click on a swatch writes the change). A note's text gets it as its background (`NoteEditor`).
- `NoteColors` (App) draws each colour in one mid shade, seen on both themes, for dots and swatches, and see-through (25 %) as a background, so it stays pale on the light theme and dark on the dark one.

### Aliases and removals
What the user can take back (nodes, links, aliases, link types, pins, placements) is added by one change and removed by another, `remove`, listing the ids it removes. Nothing is deleted from the vault.

```
a1 addAlias "is in" → contained by      device A
a2 addAlias "is in" → contained by      device B, before syncing (a copy)
r1 remove [a1, a2]                      removes every copy
current = added − removed               (RemovableChanges; a removal may arrive first)
```

- **Nodes and links** (since format 4): a removed node is no longer shown, nor searched, pinned or linked; a rename or version arriving after changes nothing. A removal wins, whatever the order the files arrive in.
- A removed link shows its next copy, if any: `KnowledgeGraph.CopiesOf` gives every copy, so a removal names them all.

- `LinkAliases` builds `KnowledgeGraph.Types`: the link types (built in and the user's, `CustomLinkTypes`) with the aliases in use added to their phrases, oldest first. Everything that reads the input (`InputPresenter`, `CreationForm`, `RenamePlanner`, `NodeBrowser`) uses it.
- A phrase taken already (built in, or by an older alias: the same phrase added for two types on two devices) leaves the alias unused: it is listed struck through, and can be removed.
- An alias of a type this version does not know is kept, not shown.
- `AliasPlanner` refuses an empty phrase and one in use, and warns about the node names containing it: the input box would read them as links.
- Settings > Link types (`LinkTypesSection`, `LinkTypesSection.Copy` with `LinkCopyLine`) lists each type both ways, with its own phrases and the user's aliases (× removes). Phrases are not logged, only the link type.

### User link types
A link type of the user's is named by `defineLinkType` changes (`LinkTypeDefined`), as a node by renames: the first one adds it, its id (a GUID) being the type's id in links, aliases and views; later ones rename it, listing the names they **replace**. Two renames made apart are a conflict: the latest is shown, and Settings asks to rename it.

```
d1 defineLinkType id=d1 "wrote" / "written by"           adds the type
d2 defineLinkType id=d1 "authored" / "authored by" [d1]  renames it
r1 remove [d1, d2, its aliases]                          removes it, once no link has it
```

- `CustomLinkTypes` builds the vault's types: the built-in ones, then the user's, oldest first. The input box reads the first type with a phrase (`InputParser`), so a phrase added on two devices is read as the older type's; the newer has `TakenBy`, flagged in Settings. A definition with a built-in id gives it fields only (`BuiltInDefined`), its names kept.
- **Qualifiers** (since format 11): `defineLinkType` carries the type's `fields` (`CustomLinkType.Fields`, a node type's kinds, image included). `LinkTypePlanner.Qualifiers` writes a new definition with the type's current names (a built-in type's own), replacing every current one; a rename keeps the fields. Settings > Link types > **Qualifiers** edits the list (`QualifiersEditor`, the node types' `FieldRow`s); checked by `CheckedFields`, shared with `NodeTypePlanner`.
- Everything reading `KnowledgeGraph.Types` gets them: the input box, labels and proposals (`ChipInput`, `InputCompletion`), the creation screen, *Change link type*, trees, `RenamePlanner`'s link phrase rule.
- A link of a type not arrived yet reads one way; when its type arrives or is removed and its symmetry changes, `LinkIndex.Refile` files its links again, so copies either way count once.
- `LinkTypePlanner` refuses an empty name, the same name both ways, a phrase in use (its own excepted, for a rename), a rename that changes which way it reads (its links would read wrong), and a rename to the same names outside a conflict; it warns about node names containing a name. It removes a type with its names and aliases, refused while links between shown nodes have it. Names are not logged, only the type's id.
- Link types have no icon: their labels use the accent color.
- **Start list** (`StartNodes`, both apps): the pinned nodes, then up to 10 recent ones, each once, none pinned (left out before the 10 are taken). A recent node was opened on this device, or changed on any device within 7 days; it is sorted, and labelled (`TimeAgo`: *changed 5 min ago*, *opened yesterday*), by the later of both. Its last change (`NodeFacts.ModifiedOf`: created, renamed, retyped, coloured, a content version, a link added) is read from the graph, so it shows the changes of every device; nothing is stored. A link removed is not counted (its removal names no node).
- Recent nodes (`RecentNodes`, `recent.json`) are kept on each device, never in the vault: opening a node writes nothing there. Each is kept with when it was opened (`OpenedNode`); one kept before 0.10, its id alone, is read without a time and listed after the others. `MainWindow` records each node the details show, and the nodes a creation involves, new or existing (`AddPlan.Nodes`), and passes the list to `InputPresenter.Recent`.
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
- A version whose file is not here yet (the sync tool copied the change first, or a server vault downloads it: [Content on demand](#server-vaults)) shows as such; the note cannot be edited until it arrives, so no version is replaced unseen.
- A save replaces the versions *loaded*: a version saved meanwhile on another device stays current, as a new conflict.
- `NoteEditor` (in `NodeDetailsPanel`) saves on Ctrl+S, the save button, and when the note is left (another node, back, window hidden). While it has the focus, the details leave the keys to it, except Esc (leaves the note, saving it). On Android, `NodePage.Note` does the same: saved by the save icon and when the screen is left (`IPage.Leave`: another screen shown, Back, the app paused).
- `MarkdownBox` (text and toolbar) is shared by `NoteEditor` and the creation steps. The toolbar inserts Markdown (`MarkdownFormat`): bold, italic, heading, list, link. The text is not rendered, except as a task list.
- **Task lists** (`TaskList`): items are Markdown task lines, so nothing else is stored and the vault format is unchanged. The text stays the one source: the list view only rewrites it.

  ```
  text ──TaskList.IsList (every filled line an item)──► TaskListBox: rows of checkbox + text
       ◄─TaskList.Format(items)── row checked, typed in, added (Enter), removed (Backspace), moved (TaskListEditing)
  Checkboxes on:  TaskList.Show  (each line → "- [ ] line", items kept, blank lines dropped)
  Checkboxes off: TaskList.Hide  (each item → its text, checked or not)
  ```

  The text's line breaks are kept (`\r\n` typed in a text box, or `\n`). `TaskListEditing` (Core) holds the list being edited, for `TaskListBox` (Windows) and `TaskListEditor` (Android): the items not checked, then those checked under their count; `NoteEditor`'s status starts with `TaskList.ProgressOf` (*2/4 done*). An item moves within its part only, by its grip (`TaskListBox.Drag`: a line over the row's top or bottom edge shows where it goes) or Alt+Up/Down.
- Note content is never logged. The search does not look into it (an open question: [GOAL.md](GOAL.md#open-questions), *Search*).

### Datasets
A dataset is a node of type `dataset` holding one file, in versions like a note (`NodeVersions`, shared with notes): each import is a content file (`content/<id>.kfd`, chunked: see [Encryption](#encryption)) and an `editDataset` change of the same id, holding the file's name, size and SHA-256, and naming the versions it **replaces**.

```
import ──DatasetFiles.Describe──► DatasetFile(name, size, sha256) ──DatasetPlanner.Import──► DatasetEdited(replaces = versions loaded)
       ──Vault.Save──► an image's thumbnail, the content file (checked against size and sha256), then change file
export ──DatasetFiles.Export──► the latest current version, decrypted as a stream into the chosen file (.tmp, then renamed)
keep   ──DatasetPlanner.Keep──► a conflict's chosen version again, as a new version: its content opened and sealed again under the new id
files  ──FileImportPlanner.Preview──► ImportPreview(a row per file or folder: node name, type, size, homonym, its folder's row; counts, total size; skipped) ──accepted: Plan──► NodeAdded (+ LinkAdded under its folder's node, the top ones under a tree node) ──VaultSession.ImportFiles──► each file described, then sealed, in turn
```

- Folders dropped (`FileImportPlanner.Preview(paths)`): walked whole, a folder's folders first, then its files, by name. A folder becomes a *generic* node, its items linked under it by the tree's link type, or *contains* from the drop zone. Folder links (junctions, symbolic links) are skipped, so a walk never loops; hidden and system items inside a folder (`desktop.ini`, `Thumbs.db`) are left out. The walk is `FileImportWalk`.
- Already there (`ImportRow.Existing`): each item is matched by name (`NameKey`) with its parent's children as the tree shows them (`TreeLinks`): a file with the oldest dataset, a folder with the oldest other node, whose children are matched in turn. A file matched is a new version (`ImportPlan.Updates`, `DatasetPlanner.Import`: the same file writes nothing, counted in the log); a folder matched adds nothing. Windows asks once more before updating (`MainWindow.ConfirmUpdates`). A new node is a homonym when its name is taken under its parent, or in the vault for a top item without one; items of one folder whose names would be the same keep their extension.
- Files given (`FileImportPlanner.Preview(files)`: picked, taken or shared on Android) go through the same walk (`FileImportWalk.Add(files)`) as the top items of a drop, so they are matched and named alike.
- Pasted (Windows): `WindowsClipboard` keeps what the clipboard holds (`ClipboardContent`: the paths copied in Explorer, whether it has an image), told by `WM_CLIPBOARDUPDATE`; each Paste button or menu item is shown only while `PasteOffer` says it fits (`PasteButtons`, followed while on screen). Pasting reads it (`PasteOffer.Read`: the paths first, else the image as a PNG `ImportFile`, *Pasted image yyyy-MM-dd HH.mm.ss.png*, in memory), then shows it first: the import preview for new datasets (`Pasted.Preview`, as a drop), `PastePreviewDialog` for one file (a dataset's new version, an image field's image). Nothing is written before it is accepted; there is no Ctrl+V for it, which stays a tree's own.
- Destination (`ImportDestinations`, `Browse`): where an import goes when it was not dropped on a node, as `ImportDestination(view, node)`: the node, linked by the tree view's link type (`Resolve` gives the `ParentLink`, or null when the view, its tree definition or the node is gone). The last one is kept per vault on this device (`import-destinations.json`, ids only, through `NodeListsFile`); null for no parent. `TreeViews` lists the tree views whose root is there. Windows: `ImportPreviewDialog` shows it (*Into*), **Change…** opens `ImportDestinationDialog` (a view, then its tree) and the preview is made again for it; Android: `Shell.Adding.OfferDestinations`, a sheet (the last one, *Add elsewhere…* with `PickDestinationPage`, or no parent), for files and for shared text (`SharedNotePlanner.Preview(text, under)`: a homonym only under that node; `Plan` adds the link).

- The metadata is in the graph and the snapshot, so a dataset's file name and size are shown offline, before its content is synced. The content is never kept in memory.
- An import reads the file twice: once for its hash, once to seal it. A file changed in between is refused, and nothing is written.
- Importing the same file again (name, size and hash), with no conflict, writes nothing.
- In a folder vault, a version whose content file is not synced yet is shown as such and cannot be exported. A server vault downloads it first ([Content on demand](#server-vaults)): viewed, exported, kept from a conflict, or copied; a drag out downloads it, then asks to drag again (Windows needs the file before the drag starts; a tree offers it only once here). Importing over it is allowed: its name and size are seen.
- Hashing, sealing and exporting run off the UI thread (`Task.Run`, in `VaultSession.SaveDataset` and `ExportDataset`); the change is then appended on it. Meanwhile `DatasetPanel` says what it does and takes no other import (`VaultSession.IsImporting`, also for a file chosen on the creation screen, imported once its node is added). An import shows its progress in percent (`IProgress<long>`, told the bytes read by `HashingStream` once per MB and at the end, through `DatasetFiles.Describe`, then `Vault.WriteDataset`; `ByteSize.Percent`): reading, then importing (the thumbnail, made first, is not counted). An import started from the creation screen only says "Importing…".
- Drag out (`DragExport`): Windows only takes a file on disk, so the file is first exported, holding the UI thread (the cursor waits, and "Preparing the file to drag…" is drawn before) into a folder of its own in the temporary folder (`%TEMP%\kofrenot\<random>\<name>`), given to Windows as a file drop, and deleted when the drag ends; folders left by a drag that did not end are deleted at the next one. Its name is made safe for Windows first (`DatasetFile.SafeName`). From a tree view, a drag is in two steps (`ViewPanel.DragNode`): first inside kofrenot, carrying the row's key as text (format `kofrenot.TreeItem`, the `TreeItem` kept by `ViewPanel`), to move or copy; when the pointer leaves the window, button held (`Tree_QueryContinueDrag`, `NativeWindows.CursorPosition` against `VisibleBounds`), that drag is cancelled and a second `DoDragDrop` goes on with the node's files (`FolderExportPlanner`: a dataset's file, a node with children a folder), exported then. A move inside kofrenot decrypts nothing; back over kofrenot, the files drag moves nothing. Never lazily, when another program asks: a hand-made `IDataObject`, which WPF wraps, crashed Explorer on the drop; only a standard `DataObject` holding text and file paths is handed to other programs. One file dropped on a tree node opens the creation screen with it (`CreationDialog.Ask(…, file)`), under that node; several files or any folder, and files or folders dropped on the main window's drop zone, open `ImportPreviewDialog` (`FileImportPlanner`), then `VaultSession.ImportFiles`.
- File names, paths and content are never logged (an exception's message names the file: it is shown, and only its type is logged); ids and sizes are.

**Viewer.** A dataset's inner type is its file's extension (`DatasetType.Of`, read from the name: nothing more is stored), which gives its category (for its icon) and its viewer:

| Category | Extensions | Viewer |
| --- | --- | --- |
| Image | png, jpg, jpeg, gif, bmp, tif, tiff, ico, webp | Image: in its own window (`ImageWindow`), decoded by WPF (`BitmapImage`), up to `DatasetType.MaxImageSize` (64 MB); webp through Windows' codec (Webp Image Extensions, in Windows 11), else said not shown |
| Image | svg, heic, raw | None |
| Text | txt, md, csv, tsv, log, rtf | Text: its first `TextPreview.MaxChars` (256 K) characters, UTF-8 unless a byte order mark says otherwise |
| Code | html, htm, cs, py, js, ts, css, sql, sh, cmd, ps1, java, c, cpp, h, go, rs | Text (HTML as text, not rendered) |
| Data | json, xml, yaml, yml, ini, toml | Text |
| Audio, Video, Archive, Pdf | wav, mp3, flac, ogg, m4a, aac, wma; mp4, mkv, avi, mov, webm, wmv; zip, 7z, rar, tar, gz, bz2, xz; pdf | None: "export it to open it" |
| Other | the others | None |

- A text is shown in the details by `DatasetView` (in `DatasetPanel`); an image in `ImageWindow`, opened by *View image* or a double-click on the file: one window, reused by each image, resizable, its place remembered (`Image`), closed when the vault changes. It opens fitted (`ImageZoom.Fit`, never enlarged) and follows the window's size until zoomed: the wheel zooms around the pointer (the image point under it stays), +/- around the middle, 0 fits again; a drag with the left button moves an image larger than the window (the hand cursor says so; a press on a scroll bar stays the scroll bar's). Zoom 1 is one image pixel per screen pixel, whatever the scaling. Both read the latest version through `DatasetFiles.OpenRead` and decode it off the UI thread, **in memory only**: nothing decrypted reaches the disk (an export, a drag out, or **Open** in its app, is the only way out). An image is frozen to cross to the UI thread; a text shown already is not read again. A text can wrap its lines (*Wrap lines*, shared by every text while kofrenot runs).
- The details row of a dataset takes room only while its viewer shows a text (`NodeDetailsPanel.FitDatasetRow`), as a note's editor does.

**Thumbnails and icons.** Every node is pictured in the search rows, the details' link rows and a dataset's header by a `NodeVisual`: an icon, and for an image the version whose thumbnail replaces it.

```
import ──Vault.WriteDataset──► image the viewer shows (≤ 64 MB)? ──IThumbnailMaker.Make (WpfThumbnailMaker)──► content/<id>.kft
row    ──NodeVisual.Of(graph, node)──► Icon (type, the file's category, a flat view's list) + Thumbnail (latest version id, for an image)
       ──NodeVisualView──► icon at once ──ThumbnailCache.Get (off the UI thread)──► the thumbnail, once read
```

- `WpfThumbnailMaker` decodes the image straight to its thumbnail size (`ThumbnailSize.Of`: the shorter side 256 px, the longer at most 512, so a card that fills its frame stays sharp; `DecodePixelWidth`, the size read from the header first), never enlarging a small one, and saves a JPEG (quality 85), or a PNG when it has transparency (an alpha channel, or a palette with a see-through colour). Thumbnails made before 0.8 are 128 px on their longest side: written once, they stay so. An image it cannot decode gets no thumbnail; the dataset keeps its icon.
- The thumbnail is written before the content file and the change (`Vault.WriteDataset`, off the UI thread), so a device seeing the change normally finds it. A missing one shows the icon: not synced yet, or a version without thumbnail; a server vault downloads it when shown ([Content on demand](#server-vaults)). Keeping a version from a conflict makes its thumbnail again from its content.
- `.kft` files are sealed with the master key ("KFT1", bound to the version id), like note content: a thumbnail shows the data, so it is content. Never logged, never in the snapshot.
- `ThumbnailCache.Shared` keeps the 100 latest decoded thumbnails in memory, for the open vault only (`Use`, at each vault change), read through `Vault.ReadThumbnail`, so typing in the input box does not read them again.
- **Icons**, the same on both apps: the SVGs of `src/kofrenot.Core/Icons` (24×24, one filled path each, embedded in Core), by name (`delete`, `note`, `contact`…). `IconPaths` reads their paths once and names a node's (`NameOf`: its type's icon, else that of what it holds, a `NodeIcon` in lower case with hyphens between its words, `DataObject` → `data-object`; `NameOfType`: a `TypeIcons` name in lower case, `Birthday-cake` → `birthday-cake`, shown as *Birthday cake*, `TypeIcons.LabelOf`). A type icon added is a new name in `TypeIcons.All`: an older kofrenot shows no icon for it, so the vault format does not change. The vaults are pictured by `vault` (a folder) or `cloud` (a server), a restore by `refresh`, a value set on two devices by `alert` (Windows' `CautionText`). Windows draws one with `IconView` (`Kind`, `Size`, in the inherited foreground; geometries parsed once, frozen); Android adds each as a resource `icon.{name}` at start (`IconResources`), shown by `PathIcon`. A pinned node, or one kept offline, shows the `-filled` icon in the accent. Replacing an icon is replacing its SVG.

### Views
A view is a node of type `view`. Its definition is in `editView` versions, as a dataset's file description: the change holds it, so it is in the graph and the snapshot, and a view works offline. Versions replace one another as for notes (`NodeVersions`, `KnowledgeGraph.ViewVersionsOf`).

| Definition | Display | Read by (`Core/Views`) |
| --- | --- | --- |
| `TreeDefinition(root, linkType, order)` | `tree` | `TreeView`: `Root`, then `ChildrenOf(item)`, read when asked; sorted by a qualifier of the links when `order` names one (`QualifierOrder`, since format 11) |
| `FlatDefinition(nodeType)` | `flat` | `FlatView.Nodes` |
| `GraphDefinition(root, depth, layout, links, nodeTypes, fields)` | `graph` | `GraphView.Read`: the whole graph at once, laid out by `GraphLayout` ([Graphs](#graphs)) |
| `QueryDefinition(nodeType, linkType, reverse, node, qualifiers, columns)` | `query` | `QueryView.Rows`: the nodes reading the link type to `node` (to any when none) through a link whose qualifiers match (`ValueQuery`), of `nodeType` (any when none), by name; each row's cells the names of the nodes its links of each column lead to (since format 12) |

```
Projects ─contains─► Alpha ─contains─► Shared      TreeItem(node, path, canExpand)
         ─contains─► Beta  ─contains─► Shared      Shared: one item under each parent
                           ─contains─► Projects    on its path: listed, not expandable (cycle cut)
```

- Children: the nodes the view's links lead to, from the node (either way for a symmetric type), each once; ordered by name as the search compares them (`NameKey`), homonyms oldest first (`NodeOrder`), or by the value of the qualifier `order` (a number as a number, else as text), those without one after, by name.
- Both read the graph each time, so changes from elsewhere show. An unknown or removed root has no tree.

**Tabs.** `TabList` gives the main window's tabs: `Tab.Search`, then the views open on this device (`ViewTabs`, `tabs.json`, ids only, kept per vault like `recent.json` through `NodeListsFile`) that the graph knows as views; `Closed` the others, for **+**; `TabFit` the tabs a strip shows (as many as fit whole, the selected one always in sight), the others listed by **▾**, on both apps (`TabStripPanel`); `Next` the tab Ctrl+Tab (or Ctrl+Shift+Tab) selects, wrapping around, handled first by `MainWindow`'s key preview so the details and the input box's Tab are not reached. A tab shows the view's latest version (`ViewPlanner.Load`). How each view was left is kept by `ViewStates` (`view-states.json`, per vault and view a `ViewState`: the nodes expanded, each by `TreeItem.Key`, its path of ids, whether Details is on, Unlinked beside a tree, and a graph's Link names), saved at each change; a key no longer in the tree is ignored. `ViewStates` and `NodeListsFile` read and write through `LocalJsonFile`.

```
MainWindow ── TabStrip (TabRow) ── Search: input box, results, details
                                └─ a view: ViewPanel (WPF TreeView, or ListBox) ──Enter──► details, Back ► the view
```

- `ViewPanel` builds a `TreeViewItem` per `TreeItem`, with a placeholder child until expanded (children read then). A refresh (`Show`, at each change of the vault) keeps the expanded paths (`ViewStates`) and the selection.
- The details opened from a view go back to it (`NodeDetailsPanel.HomeLabel` names it); selecting another tab leaves them (the note saved).
- The creation screen's view step (`CreationForm.IsView`, `ViewDraft`): a tree needs its root, found by `ViewDraft.FindRoots` (the search, 8 nodes); `CanLeaveStep` and `CanAdd` wait for it. `ViewsToSave` gives each new view's first `editView`, appended after the plan's changes; the main window then opens it in a tab.
- Editing a view later: `VaultSession.EditView` (from **Edit…** in the details, `NodeDetailsPanel.View`, or **⚙** on the selected tab, `MainWindow.Tabs`) loads it (`ViewPlanner.Load`: versions, latest definition, conflict), shows `ViewDialog` (its name, checked by `RenamePlanner`, and its definition), then saves `ViewPlanner.Save`: a version replacing every version loaded, or nothing when unchanged; and the rename, when the name changed. `ViewDraft.Of` turns a saved definition back into a draft (a root gone is to choose again; an unknown type kept by its id); `ViewDraft.Describe` says it in a sentence.
- `ViewDefinitionEditor` (display, root search, link type, node type, query and columns) is the one editor of a draft, on the creation screen and in `ViewDialog`.
- A query view (`QueryDraft`): the query typed is read by `QueryParser` with its qualifiers, its node kept by id (one node of that name, or none for any; several or none of that name: a problem), so a rename changes nothing it finds; `QueryDraft.TextOf` writes it back with the names of the day. **Save as a view…** in the input box (`InputPresenter.CanSaveAsView`, `VaultSession.CreateQueryView`) opens the creation screen with it (`CreationForm.QueryView`).

**Operations.** Each planner gives a plan to show first, then its changes; the tree's link type drives them, with the same children as the tree (`TreeLinks`).

| Planner | Plan | Changes |
| --- | --- | --- |
| `DeletePlanner.Plan(item)` | `DeletePlan`: the nodes (the node and its whole subtree, even a node with another parent), their links, `OtherParents` | one `remove`: the nodes, and every copy of their links |
| `MovePlanner.Plan(item, target)` | `MovePlan`: `Problem` (`Root`, `SameParent`, `IntoItsSubtree`) or none | `remove` of the link from the parent shown (every copy), `addLink` from the target; other parents stay |
| `CopyPlanner.Plan(node, target, isAvailable)` | `CopyPlan`: the nodes, `Missing` (content not synced, an image's included: no copy) | new nodes (same name, all their types, their colour), their links and one to the target; each note, dataset and view gets its original's latest version, each field the value shown; a dataset's and an image's file is written again under the copy's version (`ContentCopy`) |

- A copied note's or dataset's content is written again under the new version: `Vault.WriteCopies(plan.Contents)` first (it touches no memory, so a large dataset can be sealed off the UI thread), then each change is appended (`VaultSession.Copy`). Stopped halfway, it leaves versions of nodes not added: never shown.
- In the app, `ViewPanel.Operations` keeps the node taken (Ctrl+C or Ctrl+X, in memory, not the Windows clipboard) and pastes it under the selected one (Ctrl+V); Del deletes. A node cut is drawn dimmed (its `TreeItem.Key`: that place only, not its other parents) until pasted, or `CancelCut` (Esc, from `MainWindow`). `OperationPreview` gives the text shown first (`MessageDialog.Confirm` with its lines): a deletion and a copy are confirmed, a refused move or a copy of content not synced (`Vault.HasContent`) is only told. After a change the tree keeps the target expanded and selected.
- Outside a tree, `DeletionPlanner` (`Browse`) deletes **one node** (`Node`: one `remove` of the node, every copy of its links, its pins; `NodeDeletion.Question`) or **one link** (`Link(link, seenFrom)`: every copy; `LinkRemoval.Question` names it as read from the node shown, and each node left with no link). Both apps use it: on Windows `NodeDeleter` from the details (`NodeDetailsPanel.Delete`, Ctrl+Del), a list (`ViewPanel.DeleteListed`, Del) and the search (`MainWindow.Delete`); on Android `NodePage.Actions`. None is offered in a vault read only (`VaultSession.IsReadOnly`).
- The search of a view (`ViewPanel.Search`): `TreeView.Find` walks the whole tree in the order shown (a node, then its children; each place of a node with several parents; cycles cut as in the tree), `FlatView.Find` the list, `GraphContent.Find` a graph, all with `GraphSearch.MatcherOf`, the input box's matching. The place gone to is kept by its key: typing stays on it while it matches, Enter moves from it. Each redraw (`Show`, at each change of the vault) counts the nodes found again without moving (`RefreshFound`): *6 found* when the place gone to no longer matches. Going to one expands `TreeItem.AncestorKeys` (kept in `ViewStates`, as by hand), selects it, scrolls to it. The text typed is never logged; Esc goes through `ViewPanel.HandleEscape` (the search, then a cut).
- Unlinked (`UnlinkedNodes`, Core): the nodes no directed link leads to (`LinksOf`, a link whose type is symmetric aside), views and the roots of tree views (each view's current `TreeDefinition`) left out; `Find(text)` gives the newest first, at most `MaxShown` (100), and how many there are (`UnlinkedList`); an empty text keeps them all, else `GraphSearch.MatcherOf`. One pass over the nodes and their indexed links: a few ms for 10k nodes. `PutUnder(nodes, parentLink)` gives an `addLink` per node, leaving out a node already under the target, the target itself, and one above it (`TreeLinks.Subtree`: a cycle). On Windows, `UnlinkedPane` beside the tree (a toggle on its bar, `ViewState.ShowsUnlinked`, per view on this device), read again at each `Show`; its rows selected drag as their ids (`UnlinkedPane.DragFormat`), and `ViewPanel.Unlinked` writes `PutUnder` on the drop. On Android, `UnlinkedPage` (**⋮** of a tree tab), one node at a time through `PickDestinationPage` from the tree's root.
- Details (`ViewPanel.Details`): `NodeFacts.Of` gives a node's type (a dataset's: `DatasetType` of its latest file), size (that file's) and last change (the latest of its creation, `NamesOf` and its content versions), all metadata. A row with details is a `Grid`: the name (`*`, cut short), then fixed columns, under `DetailsHeader`. A tree's row is made as wide as the tree's viewport less its own start (`FitToTree`, at `Loaded` and when the tree is resized), since a `TreeViewItem` does not stretch its header; a list's `ListBoxItem` stretches it.
- Cards (`ViewPanel.Cards`, a toggle on a list's bar, `ViewState.ShowsCards`, per view on this device): the same `ListBox`, its panel swapped for a `MasonryPanel` (columns of one width, each card under the shortest), each item a card as tall as its content (`CardOf`: picture and name, as a row's, so rename, find, keys and menus are unchanged; a pin when pinned). Their order is `CardOrder.Of` (Core): pinned first, then in each part the cards ranked by hand, then the others by name. A card dropped on another (`ViewPanel.CardDrag`, format `kofrenot.Card`; out of the window it goes on as files, as a tree's node) takes its place within its part: `CardOrder.Move` writes one `placeNodes` for the list's view, each card's rank as its `y` (`x` = 0), only the ranks that changed. So the order syncs as a graph's placements do, with no new change or format (see Placements). A card's body is `NodeCard.Of` (Core): a note's `NotePreview` (a task list's unchecked items and the number checked, or its first 6 lines, cut short), *not synced* for a note whose content has not arrived, a dataset's file kind and size (an image's thumbnail above, filling its frame, cropped around its centre: `NodeVisualView`), a view's `ViewDraft.Describe`.

  ```
  Show ─► cards built empty ─► laid out ─► ScrollChanged (scroll, resize, first layout)
                                              └─► each card in the viewport, not read yet: NodeCard.Of(graph, node, readNote)
  next Show (any change of the vault) ─► cards built again: previews dropped, those in view read again
  ```

  Only the cards in view read their note: content is never read for the whole list, nor kept beyond the cards shown.
- Renaming in place (`ViewPanel.Rename`, F2 or **Rename**): the name's `TextBlock` in the row's header gives way to a `TextBox`; Enter plans with `RenamePlanner` (an `Empty` or `LinkPhrase` problem: refused, its `Status` in a tooltip under the box), leaving it saves when it can, Esc (first in `HandleEscape`) cancels. While renaming, the view's keys, drag and double-click leave the box alone; a redraw (`Show`) drops it. A list's rows are built in code (`RowOf`), as the tree's, so both share the header and the rename. Empty names are refused wherever a name is set: the input box (`InputParser`), the creation screen (`CanAdd`), renames (`RenameProblem.Empty`).
- The bar over a view (`ViewPanel.Actions`): **Edit** opens the node selected (`OpenSelected`, as Enter); New…, Edit and Rename are also first in the tree's and the list's right-click menus. Cut, Copy, Paste and Delete run the same operations, enabled with a node selected (Paste: with one taken too). **New** opens `CreationForm.NewNode` through `VaultSession.CreateNode`: in a tree with `Under` (`ParentLink`: the node selected, or the root, and the tree's link type), whose `LinksToSave` adds the link from it with the new node; in a list with the list's type fixed. The new node is then selected.
- Drag and drop (`ViewPanel.DragDrop`): the drag starts past the system's minimum distance, carrying the `TreeItem` (format `kofrenot.TreeItem`, inside kofrenot only). Over a node, `MovePlanner.CanMove` gives the cursor (move, or none when refused); Ctrl held is a copy. The drop runs the same move or copy as Ctrl+V.
- Several nodes (`ViewPanel.Selection`): a tree's are kept by key (`_marked`, painted on their header), as WPF's `TreeView` selects one; a list is a `ListBox` in `Extended` mode. A plain press on one of several waits for the release before selecting it alone, so they can be dragged together. With several, the one-node actions and their keys are off.
- Drag out (`ViewPanel.DragOut`): several nodes in a tree, or any in a list, through `FolderExportPlanner` (Core): a dataset's latest file, a node with children in the tree a folder of them (`TreeLinks`), names made safe (`DatasetFile.SafeNameOf`) and numbered when taken, a node met twice (selected with its parent, a cycle) written once; files not on the device and nodes with no file nor children counted, told after the drop. They are written first (`FolderExport.WriteTo`, through `DragExport`, the cursor waiting), then dragged; the drag carries `*` in format `kofrenot.TreeItem`, never a row's key, so a drop on a node inside kofrenot does nothing.

#### Graphs
A graph is read whole at each `Show` (at most `GraphView.MaxNodes`, 500), then laid out in Core, so another client can draw the same.

```
GraphDefinition ─► GraphView.Read ─► GraphContent(nodes, edges, isCut) ─► GraphLayout.Of(content, layout, PositionsIn(view))
  root, depth       breadth first from the root, either way,                  LayeredLayout or NetworkLayout, then
  links (type,role) through its link types, of its node types;                the moved nodes put back, the others
  nodeTypes, fields then the nodes placed in the view not reached;            pushed right off them
                    every link of those types between them
```

| Role | Layered | Drawn (`GraphLines`) |
| --- | --- | --- |
| `down` / `up` | the lower node a rank below the upper one; a symmetric type is free | from the upper card's bottom to the lower one's top, bent halfway |
| `beside` | one group, side by side on one rank (union-find) | thick line, an arrow when directed |
| `free` | no rank: a node with no other link takes its neighbour's | dashed line, an arrow when directed |

- **Edges** (`GraphEdge`): a link, its role, its colour (`Color`, the place of its type among the view's link types, `GraphView.Legend`), `IsDirected` (its type is not symmetric) and its `Name` read from its `from` node. The app draws them in `GraphPalette` (8 mid shades, round again past the last), a straight line from border to border (`GraphLayout.BorderPoint`, none when the cards cover each other), the arrow's tip on the border too, the name in a tooltip and, with **Link names** (`ViewState.ShowsLinkNames`, on this device), on the line. A legend lists the types when there are several.
- **Search**: the view's search (`ViewPanel.Search`) uses `GraphContent.Find` (`GraphSearch.MatcherOf`, in the order shown); going to a node selects it and centres the view on it.
- **Minimap** (`GraphMinimap`): the cards scaled into 180×120 (`GraphLayout.BoundsOf`, also what **Fit** frames), the part shown as a frame, from the inverse of the board's transform; a click or a drag centres the view there. Hidden while every card is shown.
- **Links by hand** (`GraphLinkPlanner`): `Verbs` (each way of the view's known link types), `Choices(from, to)` (one per verb, read from `from`, `Exists` when the nodes are linked so, a symmetric link either way), `Add` (a `LinkAdded`, nothing when it exists). The selected card's handle, on its edge nearest to the mouse (`GraphLayout.NearestBorderPoint`, the right edge until the mouse moves), dragged onto another card (`GraphLayout.CardAt`, the mouse being captured), adds the only choice, else the one picked in a menu. While dragged over another card, that card is ringed and the band goes from border to border with a second handle (straight: a rank's bend is known once the type is). A menu closed gives the keyboard back to the graph only when no other menu was opened meanwhile. **New node** on a card goes through the creation screen with a `ParentLink`, reversed (`IsReverse`, the new node as `from`) for a reverse verb, its type fixed when the view keeps one node type; a node the graph does not show (its type, or past the depth) is said so. **Remove link…** on a line uses `DeletionPlanner.Link`, confirmed. No change of format: these write `addLink`, `addNode` and `remove`.

- **Layered** (`LayeredLayout`): groups ranked by longest path from the top, a cycle cut where it closes; a group with no parent goes just above its highest child (a spouse's parents sit over the spouse); rows ordered by barycentres (8 sweeps), then each group centred over its parents, or its children going up (4 sweeps), each row shifted as a whole.
- **Network** (`NetworkLayout`): Fruchterman–Reingold, 150 rounds, from a golden-angle spiral in the order reached: the same graph gives the same places. Moved nodes stay, and pull or push the others.
- **Placements**: a drop writes one `placeNodes` (`GraphPlacer.Move`, the cards selected moved together in one, rounded to whole units, nothing when unmoved). `NodePlacements` keeps, per view and node, the newest one not removed (`at`, then id): no conflict, a place matters less than a name. **Reset layout** writes one `remove` of every placement of the view (`GraphPlacer.Reset`), confirmed first. A placement is kept per view: one node can sit apart in two graphs. A list view's placements are its cards' order (`CardOrder`). A node placed in the view that its links do not reach (added unlinked with **New node…** on the background, or unlinked since) is shown too, after those reached, by name, kept by the view's node types and counted in `MaxNodes`: the placements keep it there (`GraphView(graph, definition, view)`), so a reset takes it out.
- In the app, `GraphPanel` (in `ViewPanel`, whose bar holds its buttons and the search: `ViewPanel.Graph`) draws a `GraphCard` per node on a `Canvas`, zoomed and panned by one `MatrixTransform`, kept while the same view is shown again and fitted for another one. A press on a card selects it, past 4 pixels drags it with the others selected (not in a vault only read); a drag on the background draws a box selecting the cards it touches (`GraphLayout.CardsIn`, `GraphPanel.Selection`), a right-drag past 4 pixels pans (a right-click without moving opens the menu). Android shows a message: graphs are drawn on Windows only.

### Imports
Behavior for the user: [README.md](../README.md#imports); design and decisions: [IMPORT-CONCEPT.md](IMPORT-CONCEPT.md). Windows only; Android shows a mapping as a node.

```
books.json ─► JsonRecordReader ─► RecordFile ─┐
mapping node ─► MappingPlanner.Load ──────────┼─► ImportPlanner ─► RecordPlan ─► RecordImportDialog ─► ChangesFor(choices) ─► VaultSession.Write
graph ─► ImportRules (rules still valid) ─────┘     (NodeFinder)
```

- A mapping is a node of the built-in type `mapping` (`TypeCatalog.Mapping`, not offered for a node: `TypeCatalog.Chosen`), its definition in `editMapping` versions (`MappingEdited`, a `ContentEdited`), as a view's: in the graph and the snapshot, replaced as notes are (`NodeVersions`, `KnowledgeGraph.MappingVersionsOf`), listed and restored by its history.
- `ImportRules.Of` keeps the rules whose field (small kinds only) and types still exist, naming the others (`RecordPlan.Warnings`); a mapping whose type is gone is refused.
- `NodeFinder` indexes the type's nodes by match value and by names (each node's name and other-name fields, `NameKey`): one node found by the match value is it; else the record's names (its name, then its other names) against those; one node with another match value is *to check*; several are ambiguous.
- `ImportPlanner` parses each value as its field's kind (`FieldValue.Parse`, invariant culture), compares it with the current one (no change when equal without conflict, else a `setField` replacing every current value), adds missing links only, renames when the name differs. Two records for one node, or one new node (same name or match value), are both left out. Linked nodes are found by name among their type (several: listed, not linked), else added once, by the first record chosen that needs them (`RecordPlan.ChangesFor`).
- One `VaultSession.Write` writes the changes, logged at Info with counts only (`Records imported (12 new, 3 updated)`); nothing of the file is logged.

#### From a vault
Design: [IMPORT-CONCEPT.md](IMPORT-CONCEPT.md#from-a-vault). No new change and no format change.

```
VaultImportDialog ─► VaultSession.VaultOf (other vault, read only) ─► NodeQuerySearch.Find (no limit) ─► hits
hits ─► VaultImportPlanner ─► VaultImportPlan ─► RecordImportDialog ─► WriteFor(choices) ─► VaultSession.ImportFromVault
          (TypeCopyPlanner, LinkTypeCopyPlanner,                                            Vault.WriteCopies(…, source) then Append
           VaultNodeWrites, VaultLinkPlanner)
```

- `VaultImportPlanner` takes the hits (and, asked, their subtrees: `TreeLinks`); views, and a node whose type (or, for a mapping, a type or link type its definition names: `TypeNeeds`) has the name of one here (`TakenTypes`), are not imported; a mapping's definition is copied as a new `editMapping` version when it differs (`MappingDefinition.Equals`), its types copied with it. A node finds the node here of its id, else one of its name (`NodesNamed`) and first type: unchanged when nothing differs (its links then lead to it), else *to check*; several: ambiguous; two nodes for one: neither.
- `VaultNodeWrites` plans a new node with its id there (`addNode`, `addNodeType`, `colorNode`, `setField`, `editNote`/`editDataset`), or what differs on the node found: a rename over its names, types added (never removed), a colour, values replacing the current ones (by value and file SHA-256), a note or file as a new version replacing the current ones (a note compared by text, one not downloaded here replaced). Content not there is downloaded on import from a server vault (`VaultContent.CanDownload`, told in its details); in a folder vault it makes the node not imported.
- `VaultLinkPlanner` reads every link of the nodes imported: added when both nodes are here once imported (as imported, else the same id here) and the link is not; its qualifiers come with it (an image neither there nor to download is left out, told); its link type is copied when missing (`LinkTypeCopyPlanner`, the user's types, `Adds`), else the link is left out, counted. A node unchanged but for a link is updated.
- `WriteFor` resolves the links again with the choices (a node skipped takes its links out, one added as new keeps them), and writes only the node and link types the nodes and links written need.
- `ImportFromVault` reads the content from the other `Vault` (decrypted with its keys) downloading there first what is not (`VaultSession.Fetch` on that vault, opened with its `ContentFetcher`: a failure writes nothing, told), and seals it here (`WriteCopies` with a source), then appends; logged at Info with the vault's id and counts only.

### History
Behavior for the user: [README.md](../README.md#node-details). Nothing in a vault is overwritten (each version names those it [replaces](#renames)), so a node's history is read from the graph, offline; no format change.

```
versions of "Dune"                      Restore n1
  n3 text     current                   n4 text  replaces n3, content of n1 written again under n4
  n2 text                               ⇒ current: n4; n1…n3 stay in the history
  n1 text     ◄── chosen
```

- `NodeHistory.Of` lists every content version (`KnowledgeGraph.ContentHistoryOf`: a note's texts, a dataset's files, a view's or a mapping's definitions), every name (`NameHistoryOf`, the name it was added with first), colour and field value, newest first. A `HistoryEntry` says what it held from the metadata only (a name, a colour, a value, a file's name and size, what a view shows; nothing for a note's text) and whether it is current. It can be restored unless it is the only current one of its kind (or of its field).
- `RestorePlanner` writes it again as a new version replacing every current one of its kind, as a rename does, so restoring one of a conflict resolves it. A note's text and a dataset's file are written again under the new version's id (`ContentCopy`, as a copy in a tree does: `VaultSession.Restore`, downloaded first in a server vault); an image keeps the file it was written with (`FieldSet.Content`), a view's definition is in its change.
- Types (added, taken out) and links have no versions, a link's qualifiers excepted (`NodeHistory.OfLink`, over its copies; restored on the link shown); a node removed is not restored. Android restores too (`ClientMode.Phone` allows it).

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

- `VaultSession.Create(ask, request, choices)` opens the creation dialog (`ISessionQuestions.AskCreation`) over any window, so later features can reuse it. It returns false when cancelled or not written.
- The dialog only shows a `CreationForm`. Each type change makes a new plan, so a different link type can turn `AlreadyLinked` into a link that can be added. Only a new node gets a step (`CreationForm.Steps`): an existing node keeps its type.
- A creation without typed text (`CreationForm.NewNode`, from **New view…**) is one node request: `IsNameEditable`, the name typed on the screen (`Name`; `CanAdd` waits for one), and with a type given, `IsTypeFixed`: the type list is hidden, and changing it throws.
- Nothing is written before the last step. `NotesToSave` gives the first version of each new note with content, for its `NodeAdded`, and `DatasetsToSave` each new dataset's with a file chosen (by a file dialog or a drop on its step); the dialog asks before discarding typed content or a chosen file.
- The types offered come from `KnowledgeGraph.Types`: node type *generic*; link types *relates to* (symmetric), *contains* and *is a*, then the user's. The creation screen offers each link type both ways (`TypeCatalog.Verbs`): a `LinkVerb` is a type read forward or reverse, and a reverse verb links from the right node to the left one.

- Names are compared by `NameKey`: lower case, accents removed, spaces collapsed. They are stored as typed, trimmed, with single spaces.
- A link phrase counts after at least one word; with nothing after it, the link is still being typed (`LinkProblem.SecondNodeMissing`).
- `GraphSearch`: exact names first, then names starting with the text, then the others, alphabetically; at most 20. Homonyms are listed oldest first, marked `¹`, `²`, ... (`SearchHit.Mark`).
- `AddPlanner` builds the `AddPlan` shown as preview: the nodes to create, the link, and `NeedsConfirmation` when a node of that name exists (warned about on the creation screen). An existing link is not added again (`LinkProblem.AlreadyLinked`).
- **Qualifiers** (`QualifierText.Split`, through `InputParser.Parse(text, graph)`): after the second node, `,` and a qualifier's name of the link type, as whole words (the longest first), start one; its value runs to the next one, a `,` inside kept (`LinkRequest.Qualifiers`). `QualifierPlanner` writes them: on a new link its values (one that does not fit: `LinkProblem.QualifierInvalid`); on a link already there (`LinkPlan.IsUpdate`), through `FieldPlanner.Set`, a value added, changed or cleared (`QualifierChange`, said by `PlanText.Action`), or `QualifiersUnchanged`. With qualifiers typed and links already between both nodes (`LinkPlan.Between`), `AddChoices.Link` picks the one updated, past the last a separate new link; `InputPresenter` lists them (`ResultRow.LinkOption`). `CreationForm.Qualifiers` / `SetQualifier` show and change them on the link step. `AddChoices` gives, for each side, the chosen node among homonyms (the oldest by default) and the type of a new node (*generic* by default). A link from a node to itself is refused.
- A name containing a link alias cannot be entered (no quoting).
- Labels and proposals (`MainWindow.Chips`): `ChipInput` keeps the labels (first node, link type, second node) and the text after them (`Tail`), the only text of the `TextBox`; the labels sit over its start, its padding following their width. `ChipInput.Text` joins them and is what `InputPresenter` reads, so labels change how the text shows, never what it means. Labels form, and a proposal shows, only for text added at the end (`Type`): a label turned back into text (`Backspace`) forms again with the next space. `InputCompletion` proposes ranked `Proposal`s: the node names starting with the side typed (case ignored, the shortest first), then the link phrases starting with the last words, never a complete one. `ChipInput.Proposals` keeps the first `MaxProposals` (5), the first selected until `MoveProposal` (Up/Down) selects another, the first again once `Tail` changes; accepted (`Accept`), the one selected replaces that side as a label. After a link type, `, ` and a qualifier's name followed by a space make the second node and the name labels (`ChipInput` keeps the qualifiers' names with their values, the last one's being `Tail`); the names starting after the last `,` are proposed (`QualifierText.Proposed`, `Proposal.IsQualifier`). The grey rest is a `TextBlock` placed at the text's end (`GetRectFromCharacterIndex`); with several proposals and the keyboard in the box, a `Popup` lists their rests from there instead (`ChipBox.ProposalList`), moved up so the one selected lies on the line of the text, and Enter accepts it as Tab does. Enter, when the plan can be added, first labels everything as read (`LabelAll`) and lets the labels render before the modal creation screen; cancelled, they stay. Showing the window again turns the labels back into text (`Unlabel`), selected.

### Node details
Behavior for the user: [README.md](../README.md#node-details).

```
search                                    details, level 2 of 4
┌─────────────────────────────────── ⚙ ┐  ┌─────────────────────────────────── ⚙ ┐
│ [ dune                             ] │  │ ⌂ Home › Dune › Frank Herbert        │
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
- **Qualifiers** (`NodeDetailsPanel.Qualifiers`): the chevron at the right of a link row, before its remove button (always shown when `LinkRow.HasQualifiers`, else on hover or selection, never then in a vault read only), unfolds them under it (`_unfolded`, by link id, kept from node to node), built with the fields' blocks (`FieldBlockOf`, `EditorOf`, `BoxOf`, image editors), which take the `FieldHolder` they write; a box's key includes its holder's id. Keys typed in them go to the fields' handling (`IsInQualifiers`, `HandleFieldKey`): Enter writes the box, it does not open the link. `LinkRow.Qualifier` is the one shown after the other node. A click on a graph line (`GraphPanel.OpenLink`) opens its first node with the link unfolded (`UnfoldLink`). On Android a tap on a link with qualifiers opens its sheet (`NodePage.Qualifiers`), its history on `HistoryPage(link)`.
- `NodeDetailsPanel` takes the place of the search in `MainWindow` and handles Up/Down/Enter/Esc while shown. It raises `BackToSearch` when the trail is empty. The trail is closed when its vault closes.
- Layout: breadcrumb; properties (types as chips, `NodeDetailsPanel.Type`; fields, `NodeDetailsPanel.Fields`, with a tab per type and *Its own* when there are several (`NodeDetailsPanel.Tabs`: a content type's tab shows its note, dataset or view, which show only then; the tab chosen kept by its name, the content's first); an image's thumbnail, `NodeDetailsPanel.FieldImage`; a box typed in, `NodeDetailsPanel.FieldEdit`: marked once changed, kept while the details are shown again, written by `LeaveFields` before they are left, as `LeaveNote`), and for a dataset its file on their right (`DatasetPanel`, half the width: file, then its buttons, wrapped when narrow; the column has no width for other types); a note's editor under them; links across the width. The properties row grows only while a dataset's text is shown (`FitDatasetRow`).

## Logging
- `Core` writes through `ILog` (`log.Info(category, message)`); only `App` knows Serilog.
- Category: the area (`App`, `Action`, `Settings`, `Window`, `Update`, ...).
- **Info**: every action the user triggers, start/stop. **Warning/Error**: failures. **Debug**: details.
- Never logged: node and vault names, note or file content, typed text, keys, pass phrases. Ids and counts are fine.
- Local only: 3 files x 1 MB, plus the last 1000 entries in memory for the log window.
- The file is never restarted: each start appends to `kofrenot.log`, and a new file begins only at 1 MB. Every entry is flushed at once, so what happened before a crash stays in the file. The log window shows the current run only.
- **Exchanges with the server** (`ExchangeStats`, Settings > Logging): `KofreClient` records each request it sends (a token refresh included) with its kind, read from its address (`ExchangeKind`: a store's file by its folder and extension), its bytes (the request's and the response's `Content-Length`; a JSON body of unknown length counts 0) and whether it failed. Per local day and kind, the last 30 days, in `exchanges.json` (`AppFiles.ExchangeStats`, through `LocalJsonFile`), saved after each sync and at exit: numbers only, never an address or an id.

## Versioning
`<Version>` in `Directory.Build.props` is the only place the version is written. It is stamped into every assembly (AssemblyVersion, FileVersion, InformationalVersion), read back through `AppVersion` in `Core`, and passed to the installer by `build/installer.cmd`. Rules: [GOAL.md](GOAL.md#versioning).

The build identity is the git commit, not a build number. The SDK appends it to InformationalVersion (`0.4+<commit>`, also the exe's *Product version*); `Directory.Build.targets` adds the commit's date in seconds since 1970, UTC (`.1793110700`: deterministic, the same for every build of the commit), then `.dirty` when the working tree has uncommitted changes. `AppVersion.Of` drops it (main window, update check, tray); `AppVersion.WithCommitOf` shows the short commit and its date in local time (`0.11 (4e9c313, 2026-10-27 14:05)`, `0.11 (4e9c313-dirty, 2026-10-27 14:05)`; a build from before 0.11 has no date) in Settings > About and the startup log line, on Windows and Android; `AppVersion.BuildOf` keeps it whole, to tell copies apart. Built outside git, there is no commit and only the version shows.
