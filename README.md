# kofrenot

A private, self-hosted knowledge system: store, organize, search, version and share notes and files securely across devices, with full offline use of metadata and on-demand access to content.

| Doc | Content |
| --- | --- |
| [docs/GOAL.md](docs/GOAL.md) | Goal, principles, non-goals, open questions |
| [docs/ROADMAP.md](docs/ROADMAP.md) | Scope of each planned version |
| [CHANGELOG.md](CHANGELOG.md) | What each version changed, most recent first ([Keep a Changelog](https://keepachangelog.com/en/1.1.0/)) |
| [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) | Projects, stack, startup, window positions, updates, logging |

## Status

Version 0.1, in progress: the Windows application (main window, tray icon, hotkey, settings, log window, update check, installer, portable zip) and a first knowledge base: nodes and links, typed in one box, stored encrypted in a folder. No notes yet: see [docs/ROADMAP.md](docs/ROADMAP.md).

## Install

```
build\installer.cmd
```

Builds `artifacts\installer\kofrenot-setup-<version>.exe` and `kofrenot-portable-<version>.zip` (needs the .NET 10 SDK and [Inno Setup](https://jrsoftware.org/isinfo.php) 6.3+). Run the setup to install kofrenot for your user only, no administrator rights: `%LOCALAPPDATA%\Programs\kofrenot`, with a Start Menu shortcut and, if chosen, a desktop shortcut. Its last page offers to open the changelog. The app is self-contained, so the PC needs no .NET runtime: `kofrenot.exe` holds the app and the runtime, next to five native DLLs of WPF and `CHANGELOG.md`.

**Portable**: unzip `kofrenot-portable-<version>.zip` anywhere (a USB stick, say) and run `kofrenot\kofrenot.exe`. While `portable.txt` is next to it, kofrenot keeps its data in the `data` folder beside it instead of `%LOCALAPPDATA%\kofrenot`. Delete `portable.txt` to use `%LOCALAPPDATA%\kofrenot` again.

Upgrade: run a newer setup over the old one. Uninstall: *Settings > Apps*. Your data (see below) is kept.

## Usage

- **Main window.** Opens at start. Closing it hides it: kofrenot keeps running in the notification area. Starting kofrenot again shows the running copy.
- **Hotkey.** Ctrl+Alt+K shows the main window from any program (Settings > General to change it).
- **Tray icon.** Click: show the main window. Menu: Show, Settings, Log window (ticked while it is open: a click shows or closes it), Quit.

### Database
A database is a folder: on this computer, on a file share, or in a folder a sync tool copies between computers. Choose it in Settings > Database.

- An **empty folder** becomes a new database: kofrenot asks a pass phrase twice. Everything in it is encrypted with that pass phrase: **if it is lost, the database cannot be read**.
- A **database folder** asks for its pass phrase at start, unless *Remember pass phrase on this computer* was ticked (kept by Windows for your account; Settings > Database > *Forget* removes it).
- A folder holding other files is refused.
- Several computers or copies can add to the same database at once; what the others add appears without a restart.

### Input box
Type in the box at the top of the main window:

| You type | Enter offers to add |
| --- | --- |
| `Dune` | the node *Dune* |
| `Dune relates to Frank Herbert` (or `related to`) | a link between *Dune* and *Frank Herbert*, and either node that does not exist yet |

- While you type, the nodes whose name has a word starting with the text are listed; a node matched exactly shows its links. Case, accents and extra spaces are ignored.
- The line under the box says what Enter will add.
- **Enter** opens the creation screen: the type of each new node (*Generic* by default), the link type, and what exists already (the nodes with that name, which side of a link is a new node). **Enter** there adds it, **Esc** goes back to the box.
- A node name that exists already is warned about on the creation screen: adding makes another node with that name. A link that exists already cannot be added again.
- Nodes with the same name are marked `¹`, `²`, ... When one side of a link matches several, **Up/Down** picks one (the oldest first), **Tab** switches to the other side.
- **Esc** clears the box, then hides the window.

## Settings

Gear button in the main window, or tray menu > Settings. A dialog over the main window: changes are saved at once, Esc (or closing it) closes it. *Find a setting* (Ctrl+F) shows the matching settings of every tab; Ctrl+Tab changes tab.

| Tab | Options |
| --- | --- |
| General | Hotkey that shows kofrenot (click the box, then press the new shortcut; *Default*: Ctrl+Alt+K) |
| Database | Database folder (*Choose...*, *Explore to*), whether it is open; the remembered pass phrase (*Forget*) |
| Appearance | Theme: follow Windows (default), light or dark |
| Logging | Detailed (debug) logging; open the log window |
| About | Version, website, report an issue, changelog; update check (once a day, on by default; a dot on the gear button and on the tab when a newer release exists); kofrenot's files, each with *Show in folder* |

## Data and logs

Everything stays on your computer, in `%LOCALAPPDATA%\kofrenot` (a portable copy: its `data` folder):

| File | Content |
| --- | --- |
| `settings.json` | Settings |
| `windows.json` | Where each window was left |
| `updates.json` | Last update check |
| `keys.json` | Remembered pass phrases (the database keys, protected by Windows for your account) |
| `cache\` | One encrypted snapshot per database opened here, so it opens fast. Safe to delete |
| `logs\kofrenot.log` | Log, at most 3 files of 1 MB, oldest deleted |

Your databases are in the folders you chose, not here.

kofrenot logs start/stop, every action you trigger (settings change, links opened, nodes added) and errors, never the names you type or your pass phrase.

**Log window**: tray menu > *Log window*, or Settings > Logging (it opens in front of Settings and stays open after Settings is closed). Live view of the last 1000 entries, level and text filters, Copy, Clear (window only), Open log folder.

**Network**: only the daily update check (GitHub's latest release), which can be switched off. Nothing is downloaded or installed by kofrenot itself.

## Build

Requires the .NET 10 SDK.

```
build.cmd     # Windows
./build.sh    # Linux / macOS (builds and tests; the app itself only runs on Windows)
```

Both restore, build in Release and run the tests. Output goes to `artifacts/`. The version is set in `Directory.Build.props` only.

The build fails if `Core` line coverage is below **95 %**. `build.cmd` also writes an HTML coverage report to `artifacts/coverage/report/`; `build.cmd open` opens it in the browser.

## Layout

| Folder | Purpose |
| --- | --- |
| `src/` | `kofrenot.App` (WPF) and `kofrenot.Core` |
| `tests/` | Unit tests for `Core` |
| `docs/` | Documentation |
| `build/` | Installer and portable zip script (`installer.cmd`, `installer/kofrenot.iss`), icon generator (`make_icon.py`) |
| `lib/` | Third-party binaries, if any |
| `packages/` | Local NuGet packages, if any |
| `samples/` | Samples |
| `artifacts/` | Build output (git-ignored) |
| `.config/` | Local dotnet tools (ReportGenerator) |

## License

[MIT](LICENSE)
