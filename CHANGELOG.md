# Changelog

All notable changes to kofrenot are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/). Versions follow kofrenot's own `MAJOR.MINOR` scheme ([docs/GOAL.md](docs/GOAL.md#versioning)), not Semantic Versioning. Planned versions: [docs/ROADMAP.md](docs/ROADMAP.md).

## [Unreleased]

### Added
- **Directed link types** with a reverse: *contains* / *contained by*, *is a* (alias *is an*) / *has instance*. A link can be typed either way and reads from the node it is seen from.
- **Link type aliases** in Settings > Link types: your own phrases for a link type, either way, stored in the vault.
- **Pinned nodes**: pin a node from its details; pinned nodes are listed while the input box is empty. Stored in the vault.
- **Recent nodes**: the last 10 nodes opened, added or linked on this computer are listed after the pinned ones while the input box is empty.
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

[Unreleased]: https://github.com/dyze/kofrenot/compare/0.1...HEAD
[0.1]: https://github.com/dyze/kofrenot/releases/tag/0.1
