# kofrenot: roadmap

Scope of each planned version. Implemented features move to [CHANGELOG.md](../CHANGELOG.md). The goal, principles, non-goals and open questions are in [GOAL.md](GOAL.md). The features are sorted by priority.

| Version | Status      | Theme                                                                              |
| ------- | ----------- | ---------------------------------------------------------------------------------- |
| 0.1     | Released    | Application shell; nodes and links typed in one box, stored in an encrypted folder |
| 0.2     | Released    | Notes: link types, notes with content, pinned and recent nodes                     |
| 0.3     | Released    | Datasets: files stored in the vault, read on demand                                |
| 0.4     | Released    | Views: flat and tree views over the graph, shown as tabs                           |
| 0.5     | In progress | Vaults: several vaults, a hot key per vault; change types, link nodes              |
| 0.6     | To define   | Editing: restore versions, drop files                                              |
| 0.7     | To define   | Server: vaults stored on a web server                                              |

## 0.5 (in progress)
Several vaults, their names and hot keys: see [CHANGELOG.md](../CHANGELOG.md) `[Unreleased]`. Still to do:

1. **Change types.** Change a node's type (e.g. a generic node into a note) and a link's type (or its direction) from the node details.
	- A new change in the vault, naming what it replaces, as renames do: types changed on two devices before they synced are flagged as a conflict.
	- In 0.5 so that it shares the vault format 5 of the vault names: vaults are upgraded once.
	- Today the type is only chosen on the creation screen.
	- Only changing from generic to a more specific type might be sufficient for a first version.
2. **Drag datasets out of and into a tree view.** User can drag datasets from tree view to a PC folder, as from the node details. A file dropped on a node of a tree view opens the creation screen: a new dataset with that file, under that node. Several files at once come with the file drop zone (0.6), as they need its preview.
3. **Lock the vault.** Close the open vault without removing it from the list, e.g. a private vault while kofrenot keeps running.
4. **Vault name in the title.** The main window's title, so the taskbar, shows the open vault's name.
5. **Node links.** User can link nodes using the node details screen.
6. **Switch vaults with the keyboard.** Ctrl+1…9 in the main window switches to the vault at that place in the list.

## 0.6 (to define)
1. **Restore previous versions.** If a user made a mistake he can see the list of versions and restore one of them.
2. **File drop zone.** Main window shows a small zone in which user can drop one or several files. This is a way to secure quickly a set of files. The App, proposes a preview of actions that will be done. node names and types are determined automatically. User acks and app proceeds with actions.
3. Tree view accepts several dropped files too, with the same preview (one file: 0.5).

## 0.7 (to define)
Needs the "Sync: no server for now" decision changed ([GOAL.md](GOAL.md#decisions)).

1. **Web server.** Vaults are stored on a dedicated web server. See dedicated concept  [SERVER-CONCEPT.md](SERVER-CONCEPT.md).
2. **pinned for off line.** When a node is pinned for off line access, its content is already downloaded to be sure it is always accessible w/o access to vault storage. 
3. **Pack vault.** Pack a vault to reduce its size of number of file.
