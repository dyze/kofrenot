# kofrenot: roadmap

Scope of each planned version. Implemented features move to [CHANGELOG.md](../CHANGELOG.md). The goal, principles, non-goals and open questions are in [GOAL.md](GOAL.md). The features are sorted by priority.

| Version | Status      | Theme                                                                                   |
| ------- | ----------- | --------------------------------------------------------------------------------------- |
| 0.1     | In progress | Application shell; nodes and links typed in one box, stored in an encrypted folder      |
| 0.2     | To define   | Notes as nodes                                                                          |

## 0.1
The application shell (taken from coursee): see `[Unreleased]` in [CHANGELOG.md](../CHANGELOG.md). Then:

1. **Database folder.** Settings > Database: choose a folder. An empty folder becomes a new database (pass phrase asked twice); a database folder asks for its pass phrase. *Remember pass phrase* keeps the key in Windows (DPAPI), *Forget* removes it.
2. **Nodes and links.** Node type *generic* (name, creation date), link type *relates to* (aliases: *related to*), symmetric. Stored as encrypted change files ([ARCHITECTURE.md](ARCHITECTURE.md#database)).
3. **One input box** in the main window:
   - `Dune` adds a node; `Dune relates to Frank Herbert` adds a link, and the missing nodes.
   - Case- and accent-insensitive.
   - While typing: the matching nodes (any word starting with the typed text), and the links of a node matched exactly.
   - A preview says what Enter will add.
   - An exact match asks for confirmation before adding a duplicate, for nodes and links.
   - Duplicates (same name) carry a mark (`²`, `³`, ...). When a side of a link matches several nodes, Up/Down picks one (oldest by default), Tab switches side.
4. **Live refresh.** Files added by another app or the sync tool are read without a restart.
5. **Local snapshot.** Each device keeps the loaded graph in an encrypted local cache, so each change file is read only once ([ARCHITECTURE.md](ARCHITECTURE.md#local-snapshot)).
6. **Summon.** Tray icon or a global hotkey, Ctrl+Alt+K by default, changeable in Settings. Esc hides the main window.

Not in 0.1: renaming, deleting, editing properties, user-defined types.

## 0.2 (to define)
1. **Answer the open questions** needed for notes (note format): [GOAL.md](GOAL.md#open-questions).
2. **Notes** as a node type, with content.
