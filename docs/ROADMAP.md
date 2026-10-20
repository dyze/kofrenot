# kofrenot: roadmap

Scope of each planned version. Implemented features move to [CHANGELOG.md](../CHANGELOG.md). The goal, principles, non-goals and open questions are in [GOAL.md](GOAL.md). The features are sorted by priority.

| Version | Status      | Theme                                                                              |
| ------- | ----------- | ---------------------------------------------------------------------------------- |
| 0.1     | Released    | Application shell; nodes and links typed in one box, stored in an encrypted folder |
| 0.2     | Released    | Notes: link types, notes with content, pinned and recent nodes                     |
| 0.3     | Released    | Datasets: files stored in the vault, read on demand                                |
| 0.4     | Released    | Views: flat and tree views over the graph, shown as tabs                           |
| 0.5     | Released    | Vaults: several vaults, a hot key per vault; change types, link nodes              |
| 0.6     | To define   | Server: vaults stored on a web server, shared with other users                     |
| 0.7     | To define   | Editing: restore versions, drop files                                              |

## 0.6 (to define)
Design: [SERVER-CONCEPT.md](SERVER-CONCEPT.md). Decisions: [GOAL.md](GOAL.md#decisions).

1. **Web server.** A generic, write-once store of files on Linux: accounts approved by the admin, stores with members (reader, writer, owner), REST API with tokens.
2. **Server vaults.** A vault is a folder (as today) or a store on a server, mirrored in a local folder: offline first, content downloaded on demand.
3. **Identity and recovery key.** Vault keys are wrapped for an identity (key pair) unlocked by the pass phrase or a recovery key shown once. Same for folder vaults (format 6, existing vaults upgraded without rewriting a file).
4. **Share complete vaults** with other users, after checking the key's fingerprint.
5. **Remove a member.** Server access removed at once, then key rotation: files added later are unreadable to them.
6. **Pinned for offline.** When a node is pinned for offline access, its content is downloaded ahead, so it is always readable without access to the vault storage.

## 0.7 (to define)
1. **Restore previous versions.** If a user made a mistake he can see the list of versions and restore one of them.
2. **File drop zone.** Main window shows a small zone in which user can drop one or several files. This is a way to secure quickly a set of files. The App, proposes a preview of actions that will be done. node names and types are determined automatically. User acks and app proceeds with actions.
3. Tree view accepts several dropped files too, with the same preview (one file: 0.5).
4. **Retry unreadable change files.** A change file skipped because it was locked or still being copied is read again a few seconds later, not only at the next sync event or when the window is shown ([GOAL.md](GOAL.md#robust-and-secure)).

## Later
1. **Pack vault.** Pack a vault to reduce its size and number of files (needs deletes on the server).
