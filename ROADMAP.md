# kofrenot: roadmap

Scope of each planned version. Implemented features move to [CHANGELOG.md](CHANGELOG.md). The goal, principles, non-goals and open questions are in [GOAL.md](docs/GOAL.md). The features are sorted by priority.

| Version | Status    | Theme                                                                              |
| ------- | --------- | ---------------------------------------------------------------------------------- |
| 0.1     | Released  | Application shell; nodes and links typed in one box, stored in an encrypted folder |
| 0.2     | Released  | Notes: link types, notes with content, pinned and recent nodes                     |
| 0.3     | Released  | Datasets: files stored in the vault, read on demand                                |
| 0.4     | Released  | Views: flat and tree views over the graph, shown as tabs                           |
| 0.5     | Released  | Vaults: several vaults, a hot key per vault; change types, link nodes              |
| 0.6     | Released  | Server: own vaults stored on a web server, synced across devices                   |
| 0.7     | Released  | Sharing: identities, recovery key, vaults shared with other users                  |
| 0.8     | To define | Large vaults: content on demand, pinned for offline, faster transfers              |
| 0.9     | To define | Editing: restore versions, drop files                                              |

## 0.8 (to define)
1. **Content on demand.** A server vault downloads change files at once, content only when shown or exported.
2. **Pinned for offline.** When a node is pinned for offline access, its content is downloaded ahead, so it is always readable without access to the vault storage.
3. **Faster transfers.** Many small files downloaded in one stream; big files uploaded in parts, resumable.

## 0.9 (to define)
1. **Restore previous versions.** If a user made a mistake he can see the list of versions and restore one of them.
2. **File drop zone.** Main window shows a small zone in which user can drop one or several files. This is a way to secure quickly a set of files. The App, proposes a preview of actions that will be done. node names and types are determined automatically. User acks and app proceeds with actions.
3. Tree view accepts several dropped files too, with the same preview (one file: 0.5).
4. **Retry unreadable change files.** A change file skipped because it was locked or still being copied is read again a few seconds later, not only at the next sync event or when the window is shown ([GOAL.md](docs/GOAL.md#robust-and-secure)).

## Later
1. **Pack vault.** Pack a vault to reduce its size and number of files (needs deletes on the server).
2. **Re-encrypt on pack.** Packed files are encrypted again with the latest vault key, so old key epochs can be dropped ([Rotation](docs/SERVER-CONCEPT.md#rotation)).
3. **Move a server vault to a folder.** Its files are saved as they are into a folder vault; its id is kept.
4. **Import/Export a server repo.**
5. **Share folder vaults.** A member's identity added to the vault's `keys/identities/` by their kofrenot ([Sharing](docs/SERVER-CONCEPT.md#sharing)).
6. **Rotate key on demand**, not only when a member is removed ([Rotation](docs/SERVER-CONCEPT.md#rotation)).
7. **Edit a label in place.** A click on a label of the input box turns it back into text there, not only the last one with Backspace.
8. **Quote a name** containing a link phrase in the input box (`"This is a test"`), so such names can be typed.
9. **Cycle through proposals.** Several proposals in grey, the next one with a key, instead of typing more to narrow it.
10. **Propose node types** while typing, in the input box, instead of only on the creation screen.

