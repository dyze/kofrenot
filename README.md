# kofrenot

A private, self-hosted knowledge system: store, organize, search, version and share notes and files securely across devices, with full offline use of metadata and on-demand access to content.

| Doc | Content |
| --- | --- |
| [docs/GOAL.md](docs/GOAL.md) | Goal, principles, non-goals, open questions |
| [ROADMAP.md](ROADMAP.md) | Scope of each planned version |
| [CHANGELOG.md](CHANGELOG.md) | What each version changed, most recent first ([Keep a Changelog](https://keepachangelog.com/en/1.1.0/)) |
| [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) | Projects, stack, startup, window positions, updates, logging |

## Status

Version 0.6: nodes and links typed in one box (directed link types, your own aliases), notes with Markdown content, datasets (files of any size, with thumbnails and a viewer for images and texts), views (trees and lists over the graph, in tabs, where nodes are added, renamed, copied, moved and deleted, and files dropped), pinned and recent nodes, all stored encrypted in folders or on your own Kofre server, synced across your devices and usable offline: several vaults, each with its name, lock and hotkey. What comes next: [ROADMAP.md](ROADMAP.md).

## Install

```
build\installer.cmd
```

Builds `artifacts\installer\kofrenot-setup-<version>.exe` and `kofrenot-portable-<version>.zip` (needs the .NET 10 SDK and [Inno Setup](https://jrsoftware.org/isinfo.php) 6.3+). Run the setup to install kofrenot for your user only, no administrator rights: `%LOCALAPPDATA%\Programs\kofrenot`, with a Start Menu shortcut and, if chosen, a desktop shortcut. Its last page offers to open the changelog. The app is self-contained, so the PC needs no .NET runtime: `kofrenot.exe` holds the app and the runtime, next to five native DLLs of WPF and `CHANGELOG.md`.

**Portable**: unzip `kofrenot-portable-<version>.zip` anywhere (a USB stick, say) and run `kofrenot\kofrenot.exe`. While `portable.txt` is next to it, kofrenot keeps its data in the `data` folder beside it instead of `%LOCALAPPDATA%\kofrenot`. Delete `portable.txt` to use `%LOCALAPPDATA%\kofrenot` again.

Upgrade: run a newer setup over the old one. Uninstall: *Settings > Apps*. Your data (see below) is kept.

## Usage

- **Main window.** Opens at start. Closing it hides it: kofrenot keeps running in the notification area. Starting kofrenot again shows the running copy. Starting *another* copy (installed, portable, or another build: version, commit or uncommitted changes) while one runs names both, and offers to close the running one and start this one; *Show the running one* keeps it. A copy of 0.3 or older cannot be asked to close: quit it from its tray icon.
- **Hotkey.** Ctrl+Alt+K shows the main window from any program (Settings > General to change it). Each vault can have its own hotkey too (see [Vaults](#vault)).
- **Tray icon.** Click: show the main window. Menu: Show, Settings, Log window (ticked while it is open: a click shows or closes it), Quit. kofrenot does not quit, from the tray or for another copy, while a note has unsaved changes or a file is being imported: its window shows why.

### Vault
A vault is a folder: on this computer, on a file share, or in a folder a sync tool copies between computers. Add it in Settings > Vault, which also shows the open vault's nodes, links and size on disk (measured each time the tab is shown).

- An **empty folder** becomes a new vault: kofrenot asks its name (the folder's, to begin with) and a pass phrase, twice, then shows its **recovery key** once: write it down or keep it in a password manager. Everything in the vault is encrypted: the pass phrase or the recovery key opens it; **if both are lost, the vault cannot be read**.
- A **vault folder** asks for its pass phrase when opened, unless *Remember pass phrase on this computer* was ticked (kept by Windows for your account; Settings > Vault > *Forget* removes it). Pass phrase lost: *Lost it? Use the recovery key*, then choose a new pass phrase.
- **Change the pass phrase**: Settings > Vault > *Pass phrase* > *Change...*, the vault open (folder vaults). The recovery key stays the same.
- A vault from kofrenot 0.6 or older is **upgraded** when opened, if you agree: its pass phrase is asked again and its recovery key shown. kofrenot 0.6 no longer opens it then.
- A folder holding other files is refused, and so is a vault in the list already.
- **Several vaults**: add as many as you like; one is open at a time, and they never mix (no search or link across vaults). With two or more, a box at the top left of the main window switches between them: the open vault is closed, then the chosen one opened (its pass phrase asked unless remembered; cancelled, the previous one comes back). The note shown is saved first; not while a file is being imported. kofrenot opens the last vault chosen at start. A vault whose folder cannot be reached is marked *unavailable*.
- **Name**: asked when the vault is created; changed in Settings > Vault > *Rename*, for the open vault. The name is stored, encrypted, in the vault, so every computer shows it; locked, a vault shows the last name seen on this computer (or its folder's name). Named differently on two computers before they synced, both names are shown there: rename it to keep one.
- **Hotkey per vault** (optional, Settings > Vault): shows the main window on that vault, switching to it; pressed again while it is in front, it hides it. In the main window, **Ctrl+1** to **Ctrl+9** switch to the vault at that place in the list. The window's title (and the taskbar) is the vault's name.
- **Lock** (the padlock next to the vault box, or **Ctrl+L**) closes the open vault and keeps it locked, also when you come back to it and after a restart (🔒 in the vault box), until **Unlock**, which always asks its pass phrase, even when it is remembered.
- **Remove** takes a vault out of the list and forgets its pass phrase on this computer: its folder is not touched, so it can be added again.
- Several computers or copies can add to the same vault at once; what the others add appears without a restart.
- A vault written by a newer kofrenot (on another computer, say) is not opened, or is closed as soon as it is seen: install the latest kofrenot (*Get the latest kofrenot* in the main window).
- A vault written by an older kofrenot asks first to be upgraded: once this kofrenot changes it, the older one no longer opens it, so update kofrenot on your other computers first. *Not now* leaves the vault unused, and untouched, until *Upgrade and open…* in the main window. The answer is asked once per vault on each computer.

### Server vaults
A vault can also be kept on a Kofre server, yours or one you are given an account on ([run one](https://github.com/dyze/kofreserver)): kofrenot syncs it across your devices itself, without a sync tool. The server keeps the files as they are, encrypted; your **account's pass phrase** opens them, and never leaves your computers: the server cannot read your vaults. It needs a Kofre server from 0.2: Settings > Server says when the server is older.

- **Account** (Settings > Server): type the server's address (`https://…`), your email and a password (10 characters at least), then *Sign up*. Once the server's admin approves the account, *Sign in*. kofrenot stays signed in across restarts (the session is kept by Windows for your account; the password is not kept). *Sign out* forgets the session: your server vaults stay, synced again once you sign in. *Password* > *Change...* asks the current password and a new one: your other devices are signed out.
- **Add** in Settings > Vault: *New vault on the server* (its name and your account's pass phrase are asked; the first time, you choose that pass phrase, twice, and its recovery key is shown once: it opens all your server vaults), or *Vault from the server…*: your vaults on the server not listed yet, shown by their size until opened (their name is encrypted in them). An empty one becomes a new vault.
- **Move to server…** (Settings > Vault, for a folder vault): copies it to the server as it is, then lists it as a server vault, with its name, hotkey and remembered pass phrase. Its folder is left as it was: delete it once you no longer need it. At its next opening it moves to your account's pass phrase, as a vault from 0.6 does.
- **Offline first**: a server vault is kept whole on each computer, so it opens, searches and adds offline. What you add is uploaded at the next sync: every 30 seconds, soon after a change, and when the window is shown. The footer of the main window, and Settings > Vault, say where the sync stands: *synced*, *offline*, *sign in to sync*, and the files waiting to be uploaded.
- **Delete from server…** (Settings > Vault): type the vault's name and your password. The vault goes to the server's trash, for every device (the server deletes it for good after 30 days, unless its admin restores it), and leaves the list. *Remove* only takes it out of this computer's list: it stays on the server.
- **One pass phrase for the account**: a server vault from kofrenot 0.6 (or moved from a folder) opens once more with its own pass phrase, then asks your account's (chosen then, if you have none yet): from then on that one opens it, and its own pass phrase only its older files. *Lost it? Use the recovery key* takes the account's recovery key, then asks a new pass phrase. Settings > Server > *Pass phrase* > *Change...* changes it for all your server vaults; the recovery key stays the same.
- **Share** (Settings > Vault > *Members* > *Share...*, your server vault open): type the other person's email on the same server, then read the **fingerprint** of their key to them, by phone or in person: they see theirs in Settings > Server (*Your key*). If it is the same, tick it, choose *Reader* or *Writer*, and *Share*. The vault appears in their *Vault from the server…* and opens with their own pass phrase. A reader cannot change it. The members are listed with their role and fingerprint. They need kofrenot 0.7 and an account pass phrase first.
- **Remove a member, or leave**: the owner chooses a member in the list, then *Remove...*; a member *Leave...*s the vault (it goes out of their list; its copy on their computer stays). The vault's key then changes, by the owner's kofrenot: what is written from then on is unreadable to them, what they had stays theirs. Your other devices take up the new key at their next sync.

### Input box
Type in the box at the top of the main window:

| You type | Enter offers to add |
| --- | --- |
| `Dune` | the node *Dune* |
| `Dune relates to Frank Herbert` (or `related to`) | a link between *Dune* and *Frank Herbert*, and either node that does not exist yet |
| `Kitchen contains Knives` or `Knives contained by Kitchen` | the same link, from *Kitchen* to *Knives* |
| `Dune is a novel` (or `is an`) or `novel has instance Dune` | the same link, from *Dune* to *novel* |

*relates to* reads the same both ways. Your own link types (Settings > Link types) are typed the same way. The other link types read from each side: the details of *Knives* show *contained by Kitchen*. A node name cannot contain a link phrase, so a name like *This is a test* cannot be typed.

- While you type, the nodes whose name has a word starting with the text are listed; a node matched exactly shows its links. Case, accents and extra spaces are ignored.
- The line under the box says what Enter will add.
- **Labels**: once a link type is recognized and you type a space after it, the first node and the link type become labels in the box, so you see at once how it is read. A node's label shows its icon, with the number of nodes of that name when there are several; a new node's label is green and dashed, with a `+` icon (Enter will add it); the link type's is in the accent color. **Enter** first turns the rest into labels, the second node too, then opens the creation screen; cancelled, the labels stay. **Backspace** at the start of the text turns the last label back into text.
- **Proposals**: while you type at the end of the text, the rest of an existing node's name (the shortest first), or else of a link type, is shown in grey. **Tab** or **→** accepts it, as a label; typing more narrows it, **Esc** hides it.
- **Enter** opens the creation screen, in steps: one per new node (its type, *Generic* by default, for a *Note* its content, for a *Dataset* its file), then for a link its two nodes and the link type. Each step starts on its list of types, on the right, each with its icon: **Up/Down** or a click choose one; **Tab** goes to a note's content. **Enter** (or **Ctrl+Enter** in a note's content) goes to the next step, and on the last one adds everything at once; **Back** returns to a step. **Esc** cancels the whole creation, asking first if you typed note content or chose a file.
- A node name that exists already is warned about on the creation screen: adding makes another node with that name. A link that exists already cannot be added again.
- Nodes with the same name are marked `¹`, `²`, ... When one side of a link matches several, **Up/Down** picks one (the oldest first), **Tab** switches to the other side (when nothing is proposed in grey).
- While the box is empty, the **pinned** nodes are listed, then the **recent** ones (the last 10 opened, added or linked on this computer, the latest first): **Down** selects one, **Enter** opens it.
- **Esc** clears the box, then hides the window.

### Node details
- In the search, **Down** selects a row and **Up/Down** move the selection, over the nodes found and their links; a **click** selects a row too, and typing clears the selection. The row under the mouse is highlighted across its width, and the selected one is marked by a bar at its start. **Enter** then opens the details instead of adding, and so do a **double-click** and the **›** button at the end of a row. A link row opens its other node, one level under the node it belongs to. While a link is typed, a click on one of the homonyms chooses it.
- The details show the node's name, type, creation date and last modification (the latest of its creation, renames and content changes, as a view's Details), then its links. **Up/Down** or a **click** select a link, and **Enter**, a **double-click** or **›** opens the node at its other end, up to 4 levels deep. A node already open in the breadcrumb is greyed.
- The breadcrumb at the top shows where you are. Click an item to go back to it; **⌂ Search** returns to the search. **Esc** goes back one level, and from the first level returns to the search as you left it.
- A **click on the name**, or **F2**, renames the node in place: **Enter** or a click elsewhere saves, **Esc** cancels. A name that cannot be saved (empty, or with a link phrase such as *relates to*) is refused by Enter, and given up when you click elsewhere. A name another node already has is allowed, with a warning. The old name stays in the vault's history.
- A node renamed on two computers before they synced shows a conflict under its name, with a button for each name: click the one to keep, or rename it.
- The **pin** button next to the name pins the node (filled pin) or unpins it. Pins are stored in the vault, so every device lists them.
- The **type** of a *generic* node is a combo: choose note, dataset or view to make it one, after a warning, as it cannot be undone (a view then asks what it shows). Changed differently on two computers before they synced, both types are shown: click the one to keep.
- **+ Link** next to *Links* (or **Ctrl+K**) shows a box over the links: type as in the input box, after the node's name (`relates to Frank Herbert`, `contains Chapter 1`); **Enter** shows the creation screen (the other node is added if needed), **Esc** clears, then hides it.
- **Right-click a link**, then **Change link type**: every link type, each way it reads, the current one checked. The link is replaced by the new one.

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

- The **tabs** are at the top of the main window: *Search* first, then the views open on this computer, kept from one start to the next. A **click** selects a tab, and so do **Ctrl+Tab** and **Ctrl+Shift+Tab** (the next and the previous one, from a node's details too); **×** closes a view's tab, **+** opens another view, or **New view…** creates one: the creation screen asks its name and what it shows. A view you create opens in a tab at once. A tree opens again with the nodes you left expanded, also after a restart.
- In a tree, a node is expanded with its arrow (or **Right**); a node with several parents shows under each; a node that comes back on its own path is not expanded again.
- **Enter** or a **double-click** opens a node's details; Back returns to the view.
- **Find in a view**: type in the box at the right of the bar (**Ctrl+F** gets there) to go to the first node whose name matches, as in the input box (a word starting with the text, case and accents ignored). The whole tree is searched, also what is not expanded: the nodes above the one found are expanded, nothing is hidden. *2 of 5* says how many are found; **Enter** or **↓** goes to the next, **Shift+Enter** to the previous; **Down** goes into the view, on it; **Esc** clears the search.
- **Details**, a toggle on the bar, shows each node's **type** (a dataset's: its file's kind, *PNG file*), **size** (a dataset's file) and when it was last **modified** (created, renamed, or its content changed), in columns under their names, as in a file explorer. It is kept per view on this computer; off at first.
- **New**, **Edit** and **Rename** are in the bar over a view, and first in its **right-click** menu (a tree's and a list's). **Edit** opens the node's details, as **Enter** does. **Rename** (or **F2**) edits the name of the node selected in place: **Enter** or a click elsewhere saves, **Esc** cancels; an empty name, or one with a link phrase, is refused by Enter (the reason shows under it) and given up by a click elsewhere. **New** adds a node on the creation screen, its name typed there: in a tree, under the node selected (the root when none), linked to it the tree's way (*contains* by default); in a list, of the list's type.
- In a tree, the bar's buttons, the keys or a **right-click** menu change it:

  | Keys | Does |
  | --- | --- |
  | **Ctrl+C**, then **Ctrl+V** on another node | copies the node with everything under it, as new nodes under that one (notes, datasets and views with their latest version), after showing what will be copied |
  | **Ctrl+X**, then **Ctrl+V** on another node | moves the node there: it leaves the parent it was shown under; its other parents keep it. Until then it is shown dimmed; **Esc** cancels the cut |
  | **Del** | deletes the node with everything under it, even a node that is also elsewhere, after showing what will be deleted and where else those nodes are |

  Or **drag** a node onto another to move it there, holding **Ctrl** to copy it instead; the node under the pointer is highlighted, and the cursor says when it cannot go there. A node cannot move under itself. A note or dataset whose content has not arrived on this computer yet cannot be copied.
- **Drag a dataset out** of a tree to a folder (or any program that takes files): its file is exported only when it leaves kofrenot, so moving it in the tree stays instant. **Drop a file** from elsewhere on a node of a tree: the creation screen proposes a new dataset with that file, named after it (full file name), under that node. One file at a time.
- A view is a node like the others: found by the search, pinned, linked, synced. Its definition is kept in the vault.
- **Edit what a view shows**: **Edit…** next to *Shows* in its details, or **⚙** on its tab, when selected. Each change is a new version; a view edited on two computers before they synced shows the latest definition, and saving keeps yours.

## Settings

Gear button in the main window, or tray menu > Settings. A dialog over the main window: changes are saved at once, Esc (or closing it) closes it. *Find a setting* (Ctrl+F) shows the matching settings of every tab; Ctrl+Tab changes tab.

| Tab | Options |
| --- | --- |
| Key bindings | Every shortcut, by area (main window, input box, node details, notes, datasets, views, creation screen, image window, Settings), with the global hotkeys: read only |
| General | Hotkey that shows kofrenot (click the box, then press the new shortcut; *Default*: Ctrl+Alt+K); main window position: centered on the mouse's monitor (default), at the mouse, or where you left it. Its size is kept, and it always stays fully on screen |
| Vault | The vaults, in the order of the main window's box: *Add...*, *Remove*, *Up*, *Down*. For the one chosen in the list: its name (*Rename*, when open), its hotkey (click the box, then press the shortcut; *None*), its folder (*Explore to*), whether it is open; its pass phrase (*Change...*, when open; folder vaults); the remembered pass phrase (*Forget*) |
| Link types | Each link type, both ways, with its phrases. Add your own aliases (for example `-` for *relates to*): pick the link type, type the phrase, *Add*; × removes one. Add your own link types under *Your link types*: its name (*wrote*) and how it reads the other way (*written by*), or nothing there for one that reads the same both ways (*knows*), then *Add*. *Rename* changes its names, not the way it reads; × removes it once no link has it. Both are stored in the vault, so every device has them. A phrase in use is refused, and node names containing it are warned about (the input box would read them as links) |
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
| `view-states.json` | The nodes expanded in each tree view, per vault (ids only, no names) |
| `vault-upgrades.json` | The vaults whose upgrade you agreed to (ids only, no names) |
| `keys.json` | Remembered pass phrases (the keys that open the vaults, protected by Windows for your account) |
| `vault-names.json` | The last name seen in each vault, to show it while locked (protected by Windows for your account) |
| `cache\` | One encrypted snapshot per vault opened here, so it opens fast. Safe to delete |
| `server-session.json` | The session on your server, so kofrenot stays signed in (protected by Windows for your account) |
| `account-identity.json` | Your server account's identity, as on the server (sealed by its pass phrase), so server vaults open offline |
| `stores\` | Each server vault, kept whole on this computer (encrypted), with where its sync stands. Files not uploaded yet are only here |
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
