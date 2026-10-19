# kofrenot

A private, self-hosted knowledge system: store, organize, search, version and share notes and files securely across devices, with full offline use of metadata and on-demand access to content.

| Doc | Content |
| --- | --- |
| [docs/GOAL.md](docs/GOAL.md) | Goal, principles, non-goals, open questions |
| [docs/ROADMAP.md](docs/ROADMAP.md) | Scope of each planned version |
| [CHANGELOG.md](CHANGELOG.md) | What each version changed, most recent first ([Keep a Changelog](https://keepachangelog.com/en/1.1.0/)) |
| [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) | Projects, stack, startup, window positions, updates, logging |

## Status

Version 0.3: nodes and links typed in one box (directed link types, your own aliases), notes with Markdown content, datasets (files of any size, with thumbnails and a viewer for images and texts), pinned and recent nodes, all stored encrypted in a folder. What comes next: [docs/ROADMAP.md](docs/ROADMAP.md).

## Install

```
build\installer.cmd
```

Builds `artifacts\installer\kofrenot-setup-<version>.exe` and `kofrenot-portable-<version>.zip` (needs the .NET 10 SDK and [Inno Setup](https://jrsoftware.org/isinfo.php) 6.3+). Run the setup to install kofrenot for your user only, no administrator rights: `%LOCALAPPDATA%\Programs\kofrenot`, with a Start Menu shortcut and, if chosen, a desktop shortcut. Its last page offers to open the changelog. The app is self-contained, so the PC needs no .NET runtime: `kofrenot.exe` holds the app and the runtime, next to five native DLLs of WPF and `CHANGELOG.md`.

**Portable**: unzip `kofrenot-portable-<version>.zip` anywhere (a USB stick, say) and run `kofrenot\kofrenot.exe`. While `portable.txt` is next to it, kofrenot keeps its data in the `data` folder beside it instead of `%LOCALAPPDATA%\kofrenot`. Delete `portable.txt` to use `%LOCALAPPDATA%\kofrenot` again.

Upgrade: run a newer setup over the old one. Uninstall: *Settings > Apps*. Your data (see below) is kept.

## Usage

- **Main window.** Opens at start. Closing it hides it: kofrenot keeps running in the notification area. Starting kofrenot again shows the running copy. Starting *another* copy (installed, portable, or another build: version, commit or uncommitted changes) while one runs names both, and offers to close the running one and start this one; *Show the running one* keeps it. A copy of 0.3 or older cannot be asked to close: quit it from its tray icon.
- **Hotkey.** Ctrl+Alt+K shows the main window from any program (Settings > General to change it).
- **Tray icon.** Click: show the main window. Menu: Show, Settings, Log window (ticked while it is open: a click shows or closes it), Quit. kofrenot does not quit, from the tray or for another copy, while a note has unsaved changes or a file is being imported: its window shows why.

### Vault
A vault is a folder: on this computer, on a file share, or in a folder a sync tool copies between computers. Choose it in Settings > Vault, which also shows its nodes, links and size on disk (measured each time the tab is shown).

- An **empty folder** becomes a new vault: kofrenot asks a pass phrase twice. Everything in it is encrypted with that pass phrase: **if it is lost, the vault cannot be read**.
- A **vault folder** asks for its pass phrase at start, unless *Remember pass phrase on this computer* was ticked (kept by Windows for your account; Settings > Vault > *Forget* removes it).
- A folder holding other files is refused.
- Several computers or copies can add to the same vault at once; what the others add appears without a restart.
- A vault written by a newer kofrenot (on another computer, say) is not opened, or is closed as soon as it is seen: install the latest kofrenot (*Get the latest kofrenot* in the main window).

### Input box
Type in the box at the top of the main window:

| You type | Enter offers to add |
| --- | --- |
| `Dune` | the node *Dune* |
| `Dune relates to Frank Herbert` (or `related to`) | a link between *Dune* and *Frank Herbert*, and either node that does not exist yet |
| `Kitchen contains Knives` or `Knives contained by Kitchen` | the same link, from *Kitchen* to *Knives* |
| `Dune is a novel` (or `is an`) or `novel has instance Dune` | the same link, from *Dune* to *novel* |

*relates to* reads the same both ways. The other link types read from each side: the details of *Knives* show *contained by Kitchen*. A node name cannot contain a link phrase, so a name like *This is a test* cannot be typed.

- While you type, the nodes whose name has a word starting with the text are listed; a node matched exactly shows its links. Case, accents and extra spaces are ignored.
- The line under the box says what Enter will add.
- **Enter** opens the creation screen, in steps: one per new node (its type, *Generic* by default, for a *Note* its content, for a *Dataset* its file), then for a link its two nodes and the link type. Each step starts on its list of types, on the right, each with its icon: **Up/Down** or a click choose one; **Tab** goes to a note's content. **Enter** (or **Ctrl+Enter** in a note's content) goes to the next step, and on the last one adds everything at once; **Back** returns to a step. **Esc** cancels the whole creation, asking first if you typed note content or chose a file.
- A node name that exists already is warned about on the creation screen: adding makes another node with that name. A link that exists already cannot be added again.
- Nodes with the same name are marked `¹`, `²`, ... When one side of a link matches several, **Up/Down** picks one (the oldest first), **Tab** switches to the other side.
- While the box is empty, the **pinned** nodes are listed, then the **recent** ones (the last 10 opened, added or linked on this computer, the latest first): **Down** selects one, **Enter** opens it.
- **Esc** clears the box, then hides the window.

### Node details
- In the search, **Down** selects a row and **Up/Down** move the selection, over the nodes found and their links; a **click** selects a row too, and typing clears the selection. The row under the mouse is highlighted across its width, and the selected one is marked by a bar at its start. **Enter** then opens the details instead of adding, and so do a **double-click** and the **›** button at the end of a row. A link row opens its other node, one level under the node it belongs to. While a link is typed, a click on one of the homonyms chooses it.
- The details show the node's name, type and creation date, then its links. **Up/Down** or a **click** select a link, and **Enter**, a **double-click** or **›** opens the node at its other end, up to 4 levels deep. A node already open in the breadcrumb is greyed.
- The breadcrumb at the top shows where you are. Click an item to go back to it; **⌂ Search** returns to the search. **Esc** goes back one level, and from the first level returns to the search as you left it.
- **F2** or **✎** renames the node in place: **Enter** saves, **Esc** cancels. A name another node already has is allowed, with a warning. The old name stays in the vault's history.
- A node renamed on two computers before they synced shows a conflict under its name, with a button for each name: click the one to keep, or rename it.
- The **pin** button next to the name pins the node (filled pin) or unpins it. Pins are stored in the vault, so every device lists them.

### Notes
A node of type *Note* (chosen on the creation screen) has content: Markdown text, typed on its creation step, then edited under its properties in the details.

- Type in the box (**Tab** or a click gets there). The toolbar adds **bold**, *italic*, a heading, a list or a link; the text stays as typed, it is not rendered.
- **Ctrl+S** or the save button saves, and so does leaving the note (another node, back, hiding the window). **Esc** leaves the text box.
- Each save is a new version in the vault: older versions are kept. The content is read only when the note is opened.
- A note edited on two computers before they synced shows both versions: keep one, or merge them by hand and save.
- A note saved elsewhere whose content has not arrived yet (the sync tool copies files in any order) cannot be edited until it does.

### Datasets
A node of type *Dataset* holds one file of any kind and size, stored encrypted in the vault. Choose it on the creation step (**Choose…**, or drop it there), or later in the details.

- The details show the file next to the node's properties, the links under both: its name, its type (its extension: *PNG file*, *JSON file*, ...), size and import date, even before the file itself has synced to this computer.
- **Texts** (txt, md, csv, json, xml, html, yaml, code, ...) are shown under them, and **images** (png, jpg, gif, bmp, tif, ico) in their own resizable window: **View image**, or double-click the file. It opens fitted to the window; the **mouse wheel** zooms around the pointer, **+**/**−** zoom in and out, **0** fits again; drag a zoomed image with the left mouse button to move it. Both are decrypted in memory only: nothing decrypted is written to disk. A long text shows its first 256 K characters; click in it to scroll or select, **Esc** leaves it; *Wrap lines* wraps long lines instead of scrolling sideways (kept for every text until kofrenot stops) (and closes the image window). Other types are exported to be opened.
- Every node shows an icon in the search results and the links: by its type, and for a dataset by its file (image, text, code, audio, video, archive, PDF). An image dataset shows a **thumbnail** instead, made once at import and stored encrypted in the vault.
- **Import…** (**Ctrl+I**), or a file dropped on the dataset, imports a new version. Its progress shows in percent, first while the file is read, then while it is encrypted. Importing the same file again does nothing.
- **Export…** (**Ctrl+E**) saves the file where you choose; or drag the file's name out to a folder or another app (a large file is prepared first: kofrenot says so and waits a moment).
- Each import is a new version in the vault: older versions are kept. The file is read only when shown or exported.
- Files imported on two computers before they synced show both versions: keep one.
- A dataset imported elsewhere whose file has not arrived yet cannot be shown or exported until it does.

### Views
A node of type *View* shows the vault another way, in a tab of its own. Choose on its creation step what it shows:

| Shows | Choose | For example |
| --- | --- | --- |
| A tree | the node it starts from (type part of its name, **Up/Down** or a click choose among the nodes found) and the link type it follows (*contains* by default) | your projects and what they contain |
| A list | a node type | every note |

- The **tabs** are at the top of the main window: *Search* first, then the views open on this computer, kept from one start to the next. A **click** selects a tab, **×** closes a view's tab, **+** opens another view. A view you create opens in a tab at once.
- In a tree, a node is expanded with its arrow (or **Right**); a node with several parents shows under each; a node that comes back on its own path is not expanded again.
- **Enter** or a **double-click** opens a node's details; Back returns to the view.
- In a tree, the keys or a **right-click** menu change it:

  | Keys | Does |
  | --- | --- |
  | **Ctrl+C**, then **Ctrl+V** on another node | copies the node with everything under it, as new nodes under that one (notes, datasets and views with their latest version), after showing what will be copied |
  | **Ctrl+X**, then **Ctrl+V** on another node | moves the node there: it leaves the parent it was shown under; its other parents keep it |
  | **Del** | deletes the node with everything under it, even a node that is also elsewhere, after showing what will be deleted and where else those nodes are |

  Or **drag** a node onto another to move it there, holding **Ctrl** to copy it instead; the node under the pointer is highlighted, and the cursor says when it cannot go there. A node cannot move under itself. A note or dataset whose content has not arrived on this computer yet cannot be copied.
- A view is a node like the others: found by the search, pinned, linked, synced. Its definition is kept in the vault.
- **Edit what a view shows**: **Edit…** next to *Shows* in its details, or **⚙** on its tab, when selected. Each change is a new version; a view edited on two computers before they synced shows the latest definition, and saving keeps yours.

## Settings

Gear button in the main window, or tray menu > Settings. A dialog over the main window: changes are saved at once, Esc (or closing it) closes it. *Find a setting* (Ctrl+F) shows the matching settings of every tab; Ctrl+Tab changes tab.

| Tab | Options |
| --- | --- |
| General | Hotkey that shows kofrenot (click the box, then press the new shortcut; *Default*: Ctrl+Alt+K); main window position: centered on the mouse's monitor (default), at the mouse, or where you left it. Its size is kept, and it always stays fully on screen |
| Vault | Vault folder (*Choose...*, *Explore to*), whether it is open; the remembered pass phrase (*Forget*) |
| Link types | Each link type, both ways, with its phrases. Add your own aliases (for example `-` for *relates to*): pick the link type, type the phrase, *Add*; × removes one. They are stored in the vault, so every device has them. A phrase in use is refused, and node names containing it are warned about (the input box would read them as links) |
| Appearance | Theme: follow Windows (default), light or dark |
| Logging | Detailed (debug) logging; open the log window |
| About | Version, with the commit it was built from (`-dirty`: uncommitted changes), website, report an issue, changelog; update check (once a day, on by default; a dot on the gear button and on the tab when a newer release exists); kofrenot's files, each with *Show in folder* |

## Data and logs

Everything stays on your computer, in `%LOCALAPPDATA%\kofrenot` (a portable copy: its `data` folder):

| File | Content |
| --- | --- |
| `settings.json` | Settings |
| `windows.json` | Where each window was left |
| `updates.json` | Last update check |
| `recent.json` | The nodes you last opened, per vault (ids only, no names) |
| `tabs.json` | The views open as tabs, per vault (ids only, no names) |
| `keys.json` | Remembered pass phrases (the vault keys, protected by Windows for your account) |
| `cache\` | One encrypted snapshot per vault opened here, so it opens fast. Safe to delete |
| `logs\kofrenot.log` | Log, at most 3 files of 1 MB, oldest deleted |

Your vaults are in the folders you chose, not here.

kofrenot logs start/stop, every action you trigger (settings change, links opened, nodes added) and errors, never the names you type, the names of your files, or your pass phrase.

A dataset dragged out of kofrenot is first exported into `%TEMP%\kofrenot`, and deleted there once dropped.

**Log window**: tray menu > *Log window*, or Settings > Logging (it opens in front of Settings and stays open after Settings is closed). Live view of the last 1000 entries, level and text filters, Copy, Clear (window only), Open log folder.

**Network**: only the daily update check (GitHub's latest release), which can be switched off. Nothing is downloaded or installed by kofrenot itself.

## Build

Requires the .NET 10 SDK.

```
build.cmd     # Windows
./build.sh    # Linux / macOS (builds and tests; the app itself only runs on Windows)
```

Both restore, build in Release and run the tests. Output goes to `artifacts/`. The version is set in `Directory.Build.props` only.

The build fails if `Core` line coverage is below **95 %**. `build.cmd` also writes an HTML coverage report to `artifacts/coverage/report/`; `build.cmd open` opens it in the browser. Before building, `build.cmd` stops any kofrenot started from `artifacts\`, because it locks the files being rebuilt. Other copies keep running.
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
