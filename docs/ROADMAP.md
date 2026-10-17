# kofrenot: roadmap

Scope of each planned version. Implemented features move to [CHANGELOG.md](../CHANGELOG.md). The goal, principles, non-goals and open questions are in [GOAL.md](GOAL.md). The features are sorted by priority.

| Version | Status      | Theme                                                                                   |
| ------- | ----------- | --------------------------------------------------------------------------------------- |
| 0.1     | In progress | Application shell; nodes and links typed in one box, stored in an encrypted folder      |
| 0.2     | To define   | Notes as nodes                                                                          |

## 0.1
Implemented: see `[Unreleased]` in [CHANGELOG.md](../CHANGELOG.md). Left before the release: the owner's test of the whole version.

Not in 0.1: renaming, deleting, editing properties, user-defined types.

## 0.2 (to define)
1. **Notes** as a node type, with content.
2. **Collections** as a node type. Used to order nodes at top level. By default, a collection is created and all new nodes are attached to it. Current collection is shown at top of Main Window.
3. **Containers as node type.** Used to order nodes in a tree. Useful for datasets. By default, all new nodes are attached at root.
4. **Datasets** as node type
	- Load/Save content

## 0.3 (to define)
1. **Multiple databases.** User can register several databases on his app. Current database is selected using a combo in main window. Selection is memorized. Each database has his passphrase.
2. **Dedicated hot keys.** User can assign a hot key per database. e.g. CTRL ALT I for private database and CTRL ALT P for personal one.
3. **Web server.** Databases are stored on a dedicated web server. See dedicated concept  [SERVER-CONCEPT.md](SERVER-CONCEPT.md).