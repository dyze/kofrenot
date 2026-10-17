# kofrenot: roadmap

Scope of each planned version. Implemented features move to [CHANGELOG.md](../CHANGELOG.md). The goal, principles, non-goals and open questions are in [GOAL.md](GOAL.md). The features are sorted by priority.

| Version | Status      | Theme                                                                              |
| ------- | ----------- | ---------------------------------------------------------------------------------- |
| 0.1     | Released    | Application shell; nodes and links typed in one box, stored in an encrypted folder |
| 0.2     | In progress | Notes: link types, notes with content, pinned and recent nodes                     |
| 0.3     | To define   | Structure: collections, containers, tree view, datasets                            |
| 0.4     | To define   | Vaults: several vaults, a hot key per vault                                        |
| 0.5     | To define   | Server: vaults stored on a web server                                              |

## 0.2
Done so far: see [CHANGELOG.md](../CHANGELOG.md) `[Unreleased]`. User-defined link types come later.

1. Link types have **aliases**. e.g. "relates to" is equivalent to "relates" or simply "-".
	- A screen is available and allow user to display and edit those aliases.
	- Stored in the vault, so every device shares them.
2. **Notes** as a node type, with content in a separate file, only read on demand when the note is opened.
	- Content is Markdown (UTF-8), edited in node details, with a toolbar for basic formatting (bold, italic, headings, lists, links).
	- Each save is a new content version naming the one it replaces (as renames do). Two versions saved in parallel on two devices are flagged as a conflict; the user keeps one or merges by hand.
3. **Pinned nodes.** user is able to pin nodes using node details. pinned nodes appears when search box is empty.
	- Stored in the vault, so pins follow the user across devices.
4. **Recent entries.** if nothing in search box. show recent entries (open last on top).
	- Kept on each device: opening a node writes nothing to the vault.

## 0.3 (to define)
Open: is a collection a root container, or a separate scope? How does it relate to the current vault (0.4)?

1. **Collections** as a node type. Used to order nodes at top level. By default, a collection is created and all new nodes are attached to it. Current collection is shown at top of Main Window.
	- Uses the link type "contains" (0.2).
2. **Containers as node type.** Used to order nodes in a tree. Useful for datasets. By default, all new nodes are attached at root.
	- Link type "contains" used to identify the content.
3. **Tree view.** User can enable a tree view to navigate thru containers and datasets more easily.
	- Standard file explore operations are handled: copy, move, delete...
4. **Datasets** as node type. Needs the "Content on demand" open question decided ([GOAL.md](GOAL.md#open-questions)).
	- Content is saved in a separate file. only read on demand when editing a node.
	- Load content from client device using a file explorer or drag and drop.
	- Save content to client device using a file explorer or drag and drop.

## 0.4 (to define)
1. **Multiple vaults.** User can register several vaults on his app. 
	- Current vault is selected using a combo in main window. Selection is memorized. 
	- Each vault has his passphrase and is independent for others.
2. **Dedicated hot keys.** User can assign a hot key per vault. e.g. CTRL ALT I for private vault and CTRL ALT P for personal one.

## 0.5 (to define)
Needs the "Sync: no server for now" decision changed ([GOAL.md](GOAL.md#decisions)).

1. **Web server.** Vaults are stored on a dedicated web server. See dedicated concept  [SERVER-CONCEPT.md](SERVER-CONCEPT.md).
