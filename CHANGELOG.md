# Changelog

All notable changes to kofrenot are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/). Versions follow kofrenot's own `MAJOR.MINOR` scheme ([docs/GOAL.md](docs/GOAL.md#versioning)), not Semantic Versioning. Planned versions: [docs/ROADMAP.md](docs/ROADMAP.md).

## [Unreleased]

### Added
- **Views**: a node type showing the vault another way: a tree from a node, through a link type (*contains* by default), or a list of the nodes of a type. Chosen on its creation step; kept in the vault, so every device has it, and it works offline. In a tree, a node with several parents shows under each, and a cycle is cut. Enter or a double-click opens a node's details; Back returns to the view.
- **Find in a view**: a search box over a view goes to the nodes whose name matches, in the whole tree, without hiding the others; Enter (or ↓) goes to the next one, Shift+Enter to the previous, Ctrl+F gets to it.
- **New node from a view**: **New**, in the bar over a view, adds a node, its name typed on the creation screen: in a tree, under the node selected, linked the tree's way; in a list, of the list's type. It goes first among the recent nodes, as does a view made with **New view…**.
- **Edit a view**: change what a view shows from its details (**Edit…**) or **⚙** on its tab, when selected. Each change is a new version; edits made on two computers before they synced are resolved by saving.
- **Tree operations**: in a tree view, copy (Ctrl+C), cut (Ctrl+X) and paste (Ctrl+V) a node under another one, or delete it (Del), also from a bar of buttons over the view or a right-click menu; or drag a node onto another to move it (Ctrl held: copy it). A node cut is dimmed until pasted (Esc cancels). A copy takes the whole subtree as new nodes; a move keeps the node's other parents; a deletion takes the whole subtree. A copy and a deletion show first what they will do.
- **Build commit** in Settings > About and the startup log line: `0.4 (4e9c313)`, with `-dirty` for a build with uncommitted changes. The main window still shows the version alone.
- **Replace a running copy**: starting kofrenot while another build of it runs (another version, commit or uncommitted changes, or another exe) names both, and offers to close the running one and start this one. A copy of 0.3 or older cannot be asked to close: the message says to quit it from the tray.
- **Tabs** at the top of the main window: *Search* first, then the views open on this computer, kept between starts. **+** opens a view, or creates one (**New view…**: its name typed on the creation screen, its type fixed), **×** closes its tab; a new view opens in a tab. **Ctrl+Tab** and **Ctrl+Shift+Tab** select the next and the previous tab, as in Settings. A tree keeps the nodes expanded on this computer, between starts too.

### Changed
- **Quit is refused** while a note has unsaved changes or a file is being imported, from the tray or when another copy asks: the window shows why.
- **Vault format 4**, to remove nodes and links, and for views. kofrenot 0.3 does not open a vault written by this version, and offers the update. Vaults of formats 1 to 3 are read as they are.

## [0.3] - 2026-10-18

### Added
- **Datasets**: a node type holding one file of any kind and size, encrypted in the vault. Chosen on its creation step (or dropped there), then imported again from its details (**Import…**, Ctrl+I, or a file dropped on it) and exported (**Export…**, Ctrl+E, or dragged out). Each import is a new version; files imported on two computers before they synced show a conflict, to keep one version. The file's name, size and date are shown offline; the file itself is read only when viewed or exported. An import shows its progress in percent.
- **Dataset type and viewer**: a dataset's type is its file's extension (*PNG file*, *JSON file*, ...). Texts (txt, md, csv, json, xml, html, yaml, code, ...) are shown in its details, images (png, jpg, gif, bmp, tif, ico) in their own resizable window (**View image**, or a double-click on the file), fitted then zoomed with the mouse wheel or +/- (0 fits again) and moved by dragging, decrypted in memory only: nothing decrypted is written to disk. A long text shows its beginning, and can wrap its lines; other types are exported to be opened.
- **Vault size** in Settings > Vault: its size on disk and its number of files, next to its nodes and links.
- **Thumbnails and icons**: every node shows an icon in the search results, the links and a dataset's details, by its type and for a dataset by its file's kind (image, text, code, audio, video, archive, PDF). An image dataset shows a thumbnail instead, made once at import and stored encrypted in the vault.

### Changed
- **Rows** in the search and the links: highlighted across their width when hovered, the selected one marked by a bar. A click selects a row, a double-click opens its details.
- **Dataset details**: the file next to the node's properties, its buttons under it, the links across the width below.
- **Creation screen**: a new node's type is chosen in a list on the right, each type with its icon, instead of a combo.
- **Vault format 3.** kofrenot 0.2 does not open a vault written by this version, and offers the update. Vaults of formats 1 and 2 are read as they are.

## [0.2] - 2026-10-18

### Added
- **Directed link types** with a reverse: *contains* / *contained by*, *is a* (alias *is an*) / *has instance*. A link can be typed either way and reads from the node it is seen from.
- **Link type aliases** in Settings > Link types: your own phrases for a link type, either way, stored in the vault.
- **Pinned nodes**: pin a node from its details; pinned nodes are listed while the input box is empty. Stored in the vault.
- **Recent nodes**: the last 10 nodes opened, added or linked on this computer are listed after the pinned ones while the input box is empty.
- **Another copy running**: starting a copy of kofrenot while another one runs (another exe or version) now says so, naming both, instead of silently showing the other.
- **Main window position** (Settings > General): centered on the mouse's monitor (default), at the mouse, or where you left it, as in coursee.
- **Notes**: a node type with Markdown content, edited in its details with a toolbar (bold, italic, heading, list, link) and saved as a new version each time. Edits made on two computers before they synced are shown as a conflict, to keep one version or merge them by hand.

### Changed
- **Creation screen in steps**: one step per new node (its type, and for a note its content), then the link. Nothing is added before the last step; Esc cancels everything.
- The same text has the same size in every window (one type scale: node name, window title, content, secondary text, status, footer).
- **Vault format 2.** kofrenot 0.1 does not open a vault written by this version, and offers the update. Vaults of format 1 are read as they are.
- The local snapshot keeps the changes themselves (snapshot format 3): every change is read again once after the update.

## [0.1] - 2026-10-17

### Added
- **Application shell**, taken from coursee: a main window that hides on close, a tray icon (Show, Settings, Log window, Quit), and a single running copy.
- **Settings**: Appearance (light, dark or follow Windows), Logging (debug logging, log window) and About (version, links, changelog, files), with a search box.
- **Logging**: local rolling file (3 x 1 MB) and a live log window.
- **Window positions** remembered per monitor, and the log window's column widths.
- **Vault**: a folder of encrypted files, chosen in Settings > Vault (*Explore to* opens it). An empty folder becomes a new vault, protected by a pass phrase asked twice; *Remember pass phrase* keeps its key in Windows, *Forget* removes it.
- **Vault format**: a vault and each of its change files carry a format number. A vault written by a newer kofrenot is not opened (or is closed when such a change arrives), and the main window offers to get the latest kofrenot.
- **Nodes and links**: nodes of type *generic* (name, creation date), joined by *relates to* links (alias: *related to*). Several apps can use the same vault at once; changes made elsewhere are read without a restart.
- **Input box** in the main window: `Dune` adds a node, `Dune relates to Frank Herbert` a link and the nodes it needs. While typing, the matching nodes and the links of an exact match are listed, and a preview says what Enter adds. Case and accents are ignored. Homonyms are marked `¹`, `²`, and Up/Down picks one (Tab: the other side of a link).
- **Creation screen**: Enter in the input box shows what will be added before adding it: the type of each new node (*Generic* by default) and the link type, both chosen from the built-in types, and what exists already (a name already used, with its nodes and their types; for a link, whether each side is new). Enter adds, Esc cancels.
- **Node details**: select a node found (or one of its links) with Up/Down, then Enter (or its › button) shows its properties and links instead of the search. From link to link, up to 4 levels, with a breadcrumb to go back; Esc goes back one level.
- **Renaming** a node in its details (F2 or ✎). A rename never overwrites: it lists the names it replaces, so a node renamed on two devices before they synced shows a conflict, with each name offered to keep.
- **Hotkey** Ctrl+Alt+K shows kofrenot from anywhere, changeable in Settings > General. Esc clears the input box, then hides the window.
- **Local snapshot**: each computer keeps the loaded vault in an encrypted cache, so it opens fast.
- **Update check** once a day against GitHub releases, switchable in About.
- **Installer and portable zip** (`build\installer.cmd`), self-contained.

[Unreleased]: https://github.com/dyze/kofrenot/compare/0.3...HEAD
[0.3]: https://github.com/dyze/kofrenot/compare/0.2...0.3
[0.2]: https://github.com/dyze/kofrenot/compare/0.1...0.2
[0.1]: https://github.com/dyze/kofrenot/releases/tag/0.1
