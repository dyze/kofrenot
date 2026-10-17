# Changelog

All notable changes to kofrenot are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/). Versions follow kofrenot's own `MAJOR.MINOR` scheme ([docs/GOAL.md](docs/GOAL.md#versioning)), not Semantic Versioning. Planned versions: [docs/ROADMAP.md](docs/ROADMAP.md).

## [Unreleased]

### Added
- **Application shell**, taken from coursee: a main window (placeholder content) that hides on close, a tray icon (Show, Settings, Log window, Quit), and a single running copy.
- **Settings**: Appearance (light, dark or follow Windows), Logging (debug logging, log window) and About (version, links, changelog, files), with a search box.
- **Logging**: local rolling file (3 x 1 MB) and a live log window.
- **Window positions** remembered per monitor, and the log window's column widths.
- **Database**: a folder of encrypted files, chosen in Settings > Database (*Explore to* opens it). An empty folder becomes a new database, protected by a pass phrase asked twice; *Remember pass phrase* keeps its key in Windows, *Forget* removes it.
- **Nodes and links**: nodes of type *generic* (name, creation date), joined by *relates to* links (alias: *related to*). Several apps can use the same database at once; changes made elsewhere are read without a restart.
- **Input box** in the main window: `Dune` adds a node, `Dune relates to Frank Herbert` a link and the nodes it needs. While typing, the matching nodes and the links of an exact match are listed, and a preview says what Enter adds. Case and accents are ignored. Homonyms are marked `¹`, `²`, and Up/Down picks one (Tab: the other side of a link).
- **Creation screen**: Enter in the input box shows what will be added before adding it: the type of each new node (*Generic* by default) and the link type, both chosen from the built-in types, and what exists already (a name already used, with its nodes and their types; for a link, whether each side is new). Enter adds, Esc cancels.
- **Hotkey** Ctrl+Alt+K shows kofrenot from anywhere, changeable in Settings > General. Esc clears the input box, then hides the window.
- **Local snapshot**: each computer keeps the loaded database in an encrypted cache, so it opens fast.
- **Update check** once a day against GitHub releases, switchable in About.
- **Installer and portable zip** (`build\installer.cmd`), self-contained.

[Unreleased]: https://github.com/dyze/kofrenot/commits/main
