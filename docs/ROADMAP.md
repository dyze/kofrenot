# kofrenot: roadmap

Scope of each planned version. Implemented features move to [CHANGELOG.md](../CHANGELOG.md). The goal, principles, non-goals and open questions are in [GOAL.md](GOAL.md). The features are sorted by priority.

| Version | Status      | Theme                                                                              |
| ------- | ----------- | ---------------------------------------------------------------------------------- |
| 0.1     | Released    | Application shell; nodes and links typed in one box, stored in an encrypted folder |
| 0.2     | Released    | Notes: link types, notes with content, pinned and recent nodes                     |
| 0.3     | In progress | Datasets: files stored in the vault, read on demand                                |
| 0.4     | To define   | Structure: collections, containers, tree view                                      |
| 0.5     | To define   | Vaults: several vaults, a hot key per vault                                        |
| 0.6     | To define   | Server: vaults stored on a web server                                              |

## 0.3 (in progress)

Nothing left: what 0.3 adds is in [CHANGELOG.md](../CHANGELOG.md), *[Unreleased]*.

## 0.4 (to define)
Open: is a collection a root container, or a separate scope? How does it relate to the current vault (0.5)?

1. **Collections** as a node type. Used to order nodes at top level. By default, a collection is created and all new nodes are attached to it. Current collection is shown at top of Main Window.
	- Uses the link type "contains" (0.2).
2. **Containers as node type.** Used to order nodes in a tree. Useful for datasets. By default, all new nodes are attached at root.
	- Link type "contains" used to identify the content.
3. **Tree view.** User can enable a tree view to navigate thru containers and datasets more easily.
	- Standard file explore operations are handled: copy, move, delete...

## 0.5 (to define)
1. **Multiple vaults.** User can register several vaults on his app. 
	- Current vault is selected using a combo in main window. Selection is memorized. 
	- Each vault has his passphrase and is independent for others.
2. **Dedicated hot keys.** User can assign a hot key per vault. e.g. CTRL ALT I for private vault and CTRL ALT P for personal one.

## 0.6 (to define)
Needs the "Sync: no server for now" decision changed ([GOAL.md](GOAL.md#decisions)).

1. **Web server.** Vaults are stored on a dedicated web server. See dedicated concept  [SERVER-CONCEPT.md](SERVER-CONCEPT.md).
2. **Change types.** Change a node's type (e.g. a generic node into a note) and a link's type (or its direction) from the node details.
	- A new change in the vault, naming what it replaces, as renames do: types changed on two devices before they synced are flagged as a conflict.
	- Today the type is only chosen on the creation screen.
	- Only changing from generic to a more specific type might be sufficient for a first version.
3. **Restore previous versions.** If a user didn't a mistake he can see the list of versions and restore one of them.
4. **Node links.** User can link nodes using the node details screen.
5. File drop zone. Main window shows a small zone in which user can drop one or several files. This is a way to secure quickly a set of files. The App, proposes a preview of actions that will be done. node names and types are determined automatically. User acks and app proceeds with actions.

