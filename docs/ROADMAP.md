# kofrenot: roadmap

Scope of each planned version. Implemented features move to [CHANGELOG.md](../CHANGELOG.md). The goal, principles, non-goals and open questions are in [GOAL.md](GOAL.md). The features are sorted by priority.

| Version | Status      | Theme                                                                              |
| ------- | ----------- | ---------------------------------------------------------------------------------- |
| 0.1     | Released    | Application shell; nodes and links typed in one box, stored in an encrypted folder |
| 0.2     | Released    | Notes: link types, notes with content, pinned and recent nodes                     |
| 0.3     | Released    | Datasets: files stored in the vault, read on demand                                |
| 0.4     | To define   | Views: flat and tree views over the graph, shown as tabs                           |
| 0.5     | To define   | Vaults: several vaults, a hot key per vault                                        |
| 0.6     | To define   | Server: vaults stored on a web server                                              |

## 0.4
Views: different ways to look at the same graph. No node has to belong to anything; a view only reads the graph.

```
 [ Search ] [ Files ] [ Memos ] [+]          tabs on top of the main window
              │          └─ flat: every node of type note
              └─ tree: from "Projects", following "contains"
```

1. **Views.** A view is a node of type *view*: searchable, pinnable, synced and versioned like any node. It holds a display and a query, in versions as a dataset's file description: kept in the vault's metadata, so a view works offline.
	- Displays: *flat* (a list) and *tree*.
	- Queries: *root node + link type to follow* (tree), or *node type =* (flat). Richer queries come later.
2. **Tabs.** Views open as tabs on top of the main window. *Search* (today's screen) is always the first tab and cannot be closed; view tabs can be added and closed.
3. **Tree view.** Navigates from a root node through "contains" links (0.2). No container type: any node with "contains" links holds others.
	- A node can have several parents: it is shown under each. Cycles are cut.
	- **Move:** only the link to the parent shown in the tree changes; the node's other parents stay.
	- **Copy:** copies the node with its whole subtree as new nodes; a copied dataset starts with its current file. What will be copied is shown first.
	- **Delete:** deletes the node with its whole subtree, after a preview of the consequences (nodes deleted, links removed). A node of the subtree that also has another parent is deleted too: the preview lists those other parents.

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
5. **File drop zone.** Main window shows a small zone in which user can drop one or several files. This is a way to secure quickly a set of files. The App, proposes a preview of actions that will be done. node names and types are determined automatically. User acks and app proceeds with actions.
6. **pinned for off line.** When a node is pinned for off line access, its content is already downloaded to be sure it is always accessible w/o access to vault storage. 
7. **Pack vault.** Pack a vault to reduce its size of number of file.

