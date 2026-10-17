# kofrenot: roadmap

Scope of each planned version. Implemented features move to [CHANGELOG.md](../CHANGELOG.md). The goal, principles, non-goals and open questions are in [GOAL.md](GOAL.md). The features are sorted by priority.

| Version | Status      | Theme                                                                              |
| ------- | ----------- | ---------------------------------------------------------------------------------- |
| 0.1     | Released    | Application shell; nodes and links typed in one box, stored in an encrypted folder |
| 0.2     | To define   | Notes as nodes                                                                     |

## 0.2 (to define)
1. **Notes** as a node type, with content. content is saved in a separate file. only read on demand when editing a node.
2. **Collections** as a node type. Used to order nodes at top level. By default, a collection is created and all new nodes are attached to it. Current collection is shown at top of Main Window.
3. **Containers as node type.** Used to order nodes in a tree. Useful for datasets. By default, all new nodes are attached at root.
4. **Datasets** as node type.
	- content is saved in a separate file. only read on demand when editing a node.
	- Load content from client device using a file explorer or drag and drop.
	- Save content to client device using a file explorer or drag and drop.
5. Link types have **aliases**. e.g. "relates to" is equivalent to "relates" or simply "-".
	- A screen is available and allow user to display and edit those aliases.
6. Link types have **reverse** versions. "relates to" is the reverse of "related to"

## 0.3 (to define)
1. **Multiple vaults.** User can register several vaults on his app. Current vault is selected using a combo in main window. Selection is memorized. Each vault has his passphrase.
2. **Dedicated hot keys.** User can assign a hot key per vault. e.g. CTRL ALT I for private vault and CTRL ALT P for personal one.
3. **Web server.** Vaults are stored on a dedicated web server. See dedicated concept  [SERVER-CONCEPT.md](SERVER-CONCEPT.md).
4. **Pinned nodes.** user is able to pin nodes using node details. pinned nodes appears when search box is empty.
5. **Recent entries.** if nothing in search box. show recent entries (open last on top). 
6. **Tree view.** User can enable a tree view to navigate thru containers and datasets more easily.
	- Standard file explore operations are handled: copy, move, delete...