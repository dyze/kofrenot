# kofrenot: roadmap

Scope of each planned version. Implemented features move to [CHANGELOG.md](../CHANGELOG.md). The goal, principles, non-goals and open questions are in [GOAL.md](GOAL.md). The features are sorted by priority.

| Version | Status      | Theme                                                                              |
| ------- | ----------- | ---------------------------------------------------------------------------------- |
| 0.1     | Released    | Application shell; nodes and links typed in one box, stored in an encrypted folder |
| 0.2     | Released    | Notes: link types, notes with content, pinned and recent nodes                     |
| 0.3     | Released    | Datasets: files stored in the vault, read on demand                                |
| 0.4     | Released    | Views: flat and tree views over the graph, shown as tabs                           |
| 0.5     | Implemented | Vaults: several vaults, a hot key per vault; change types, link nodes              |
| 0.6     | To define   | Editing: restore versions, drop files                                              |
| 0.7     | To define   | Server: vaults stored on a web server                                              |

## 0.6 (to define)
1. **Restore previous versions.** If a user made a mistake he can see the list of versions and restore one of them.
2. **File drop zone.** Main window shows a small zone in which user can drop one or several files. This is a way to secure quickly a set of files. The App, proposes a preview of actions that will be done. node names and types are determined automatically. User acks and app proceeds with actions.
3. Tree view accepts several dropped files too, with the same preview (one file: 0.5).

## 0.7 (to define)
Needs the "Sync: no server for now" decision changed ([GOAL.md](GOAL.md#decisions)).

1. **Web server.** Vaults are stored on a dedicated web server. See dedicated concept  [SERVER-CONCEPT.md](SERVER-CONCEPT.md).
2. **pinned for off line.** When a node is pinned for off line access, its content is already downloaded to be sure it is always accessible w/o access to vault storage. 
3. **Pack vault.** Pack a vault to reduce its size of number of file.
