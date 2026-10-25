# kofrenot

A private knowledge system for Windows: notes, files and the links between them, encrypted, usable offline, and synced and shared through your own server.

![Typing a link in the input box: the known parts become labels, the rest of a name is proposed in grey](docs/images/main.png)

> **kofrenot + Kofre server.** kofrenot is the app; [**kofreserver**](https://github.com/dyze/kofreserver) is its companion server, to run yourself or to get an account on. It syncs your vaults across your devices and shares them with others. It only stores encrypted files: it cannot read them.
>
> ```
> kofrenot (laptop) ─┐                              ┌─ kofrenot (desktop)
>                    ├─► Kofre server (yours) ◄──────┤
> kofrenot (work PC) ┘   encrypted files only        └─ kofrenot (a friend: shared vault)
> ```

## Features

- **One box to find and add**: type `Dune`, or `Dune relates to Frank Herbert`, and the nodes and the link are added. Your own link types too.
- **Notes** in Markdown, with **task lists** and **colours**, as in Google Keep.
- **Datasets**: files of any kind and size, with thumbnails and a viewer for images and texts.
- **Views**: trees and lists over your nodes, in tabs, shown as rows or as **cards**.
- **Encrypted**: everything in a vault, names included, opened by your pass phrase or your recovery key.
- **Offline first**: browse, search and add without a network.
- **Sync and share** through a Kofre server, as a reader or a writer.
- **Versioned**: nothing is overwritten; edits made on two devices are shown, never lost.

| A note with its task list | Cards | A tree |
| --- | --- | --- |
| ![A note](docs/images/note.png) | ![Cards](docs/images/cards.png) | ![A tree](docs/images/tree.png) |

## Install

Download `kofrenot-setup-<version>.exe` from the [releases](https://github.com/dyze/kofrenot/releases) and run it: it installs for your user only, no administrator rights, and needs no .NET runtime. Run a newer setup to upgrade; uninstall from *Settings > Apps* (your data stays).

**Portable**: unzip `kofrenot-portable-<version>.zip` anywhere (a USB stick) and run `kofrenot\kofrenot.exe`: its data stays in the `data` folder beside it.

kofrenot runs in the notification area. **Ctrl+Alt+K** shows it from any program; closing the window hides it.

## Vaults

A vault holds your nodes, notes and files, encrypted. There are two kinds:

| | **Server vault** (recommended) | Folder vault |
| --- | --- | --- |
| Where | On a [Kofre server](https://github.com/dyze/kofreserver), and on each of your devices | A folder you choose |
| Sync | Built in: every 30 seconds, and soon after a change | Your own sync tool (OneDrive, Syncthing...), or none |
| Sharing | With other accounts on the server, as readers or writers | No |
| Content | Downloaded when first needed: a device keeps only what it uses | All of it, on every device |
| Backup | On the server, by its admin; your devices keep a copy | Yours to make |
| Pass phrase | One for all your server vaults, the account's | One per vault |

- **Add one** in Settings > Vault: *New vault on the server* (sign in first, in Settings > Server), *Vault from the server…*, or a folder (an empty one becomes a new vault). A folder vault can be moved to the server later (*Move to server…*).
- **Recovery key**: shown once when the vault (or the account's pass phrase) is created. Keep it with the pass phrase: **if both are lost, the vault cannot be read**.
- **Several vaults**: one is open at a time, switched in the title bar (or **Ctrl+1** to **Ctrl+9**); each can have its own hotkey. The **padlock** (**Ctrl+L**) locks the open one until its pass phrase is typed again.
- **Share** a server vault: Settings > Vault > *Members* > *Share...*, with the other person's email, once you both read the same key **fingerprint** (by phone or in person). Removing a member changes the vault's key.
- **Sync status** is in the title bar: a green dot when synced, amber offline, red when you must sign in. A click syncs now.

## Using kofrenot

### Input box
Type in the box at the top of the main window; the line under it says what Enter adds.

| You type | Enter adds |
| --- | --- |
| `Dune` | the node *Dune* |
| `Dune relates to Frank Herbert` | a link between them, and the nodes not there yet |
| `Kitchen contains Knives` | a link from *Kitchen* to *Knives*, read *Knives contained by Kitchen* from the other side |
| `Dune is a novel` | a link from *Dune* to *novel* |

- Matching nodes are listed as you type; **Tab** accepts the rest of a name proposed in grey.
- **Enter** shows the creation screen first: the type of each new node (generic, note, dataset or view), a note's text, a dataset's file.
- With the box empty, your **pinned** and **recent** nodes are listed.

### Node details
**Enter** or a double-click on a node opens its details: its name, type, colour, pin and links. **F2** renames it; **+ Link** (**Ctrl+K**) links it to another; a link opens the node at its other end. **Esc** goes back.

### Notes
A note is Markdown text, saved as a new version each time (**Ctrl+S**, or by leaving it). **Checkboxes** on its toolbar turns its lines into a task list.

### Datasets
A dataset holds one file: drop it on the creation screen or on the dataset. Texts and images are shown in kofrenot (decrypted in memory only), others are exported (**Export…**, or drag the file out). Each import is a new version.

### Views
A view shows your vault another way, in a tab: **a tree** from a node, following a link type (*contains* by default), or **a list** of every node of a type. **+** in the tab bar opens or creates one.

- In a tree: **Ctrl+C**, **Ctrl+X**, **Ctrl+V** and **Del** copy, move and delete, or drag a node onto another. Drop a file on a node to add it under it.
- **Cards** on a list's bar shows its nodes as cards, with a preview. **Details** shows type, size and last change in columns.
- **Ctrl+F** finds a node in the view.

Every shortcut is listed in Settings > **Key bindings**.

## Settings

Gear button in the title bar, or tray menu > Settings. Changes are saved at once; *Find a setting* (**Ctrl+F**) searches every tab.

| Tab | |
| --- | --- |
| General | The hotkey that shows kofrenot; where the main window appears |
| Key bindings | Every shortcut |
| Vault | Your vaults: add, remove, order, rename, hotkey, pass phrase, members |
| Server | Your account on a Kofre server: sign up, sign in, password, the account's pass phrase |
| Link types | The link types and their aliases; your own link types |
| Appearance | Light, dark, or as Windows |
| Logging | Debug logging, the log window |
| About | Version, update check, changelog, kofrenot's files |

## Your data

- **Where**: your vaults are in the folders you chose or on your server. kofrenot's own files (settings, remembered pass phrases protected by Windows, a cache, logs) are in `%LOCALAPPDATA%\kofrenot`, listed in Settings > About.
- **Private**: kofrenot logs what you do (*Node added*) and errors, never names, file names, content or pass phrases. Its only network use, besides your Kofre server, is a daily check for a newer release, which can be switched off.
- **Backup**:

  | What | Back up | Restore |
  | --- | --- | --- |
  | Server vault | On the server ([backup](https://github.com/dyze/kofreserver#backup-and-restore)) | Sign in, then *Vault from the server…* |
  | Folder vault | Its folder | Settings > Vault > *Add...* the folder |
  | Pass phrases, recovery keys | In a password manager, or on paper | Needed to open any restored vault |

## Development

| Doc | Content |
| --- | --- |
| [docs/GOAL.md](docs/GOAL.md) | Goal, principles, non-goals, open questions |
| [ROADMAP.md](ROADMAP.md) | Planned versions |
| [CHANGELOG.md](CHANGELOG.md) | What each version changed |
| [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) | Projects, stack, storage, sync, encryption |

**Build** (the .NET 10 SDK): `build.cmd` on Windows, `./build.sh` on Linux or macOS (build and tests only: the app runs on Windows). Both build in Release and run the tests; output goes to `artifacts/`. The build fails below **95 %** line coverage of `Core`; `build.cmd open` opens the coverage report. The version is set in `Directory.Build.props` only.

**Installer**: `build\installer.cmd` makes `artifacts\installer\kofrenot-setup-<version>.exe` and the portable zip (needs [Inno Setup](https://jrsoftware.org/isinfo.php) 6.3+).

**Another set of data**: `kofrenot.exe --data D:\kofre-test` keeps settings, vaults list, remembered pass phrases and server account in that folder: to test, or to try sharing with a second account. One kofrenot runs at a time: quit the other first (tray icon > *Quit*).

**Demo data** (for screenshots): `dotnet run --project samples\DemoVault` writes a vault of made-up books, notes, task lists, pictures and views to `artifacts\demo`; then, from the repository folder, `kofrenot.exe --data artifacts\demo\data` opens it (pass phrase `demo`, remembered on this computer; quit a running kofrenot first).

| Folder | Purpose |
| --- | --- |
| `src/` | `kofrenot.App` (WPF) and `kofrenot.Core` |
| `tests/` | Unit tests for `Core` |
| `docs/` | Documentation and its images |
| `build/` | Installer script, icon generator |
| `samples/` | `DemoVault`: the demo data generator |
| `lib/`, `packages/` | Third-party binaries and local NuGet packages, if any |
| `.config/` | Local dotnet tools (ReportGenerator) |
| `artifacts/` | Build output (git-ignored) |

## License

[MIT](LICENSE)
