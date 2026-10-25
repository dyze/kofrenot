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
| 0.8     | To define | Notes as in Keep; content on demand; session in Core for Android                   |
| 0.9     | To define | Large vaults: offline pins, faster transfers; editing                              |
| 0.10    | To define | Android: server vaults read and searched offline on the phone                      |

## 0.8 (to define)
Everything planned for it is in [CHANGELOG.md](CHANGELOG.md#unreleased).

## 0.9 (to define)
1. **Pinned for offline.** When a node is pinned for offline access, its content is downloaded ahead, so it is always readable without access to the vault storage.
2. **Faster transfers.** Many small files downloaded in one stream; big files uploaded in parts, resumable.
3. **Restore previous versions.** A node's versions are listed, so a mistake can be undone by restoring one of them.
4. **File drop zone.** A small zone in the main window takes one or several dropped files, to secure them quickly. Node names and types are chosen automatically; a preview shows what will be added, and nothing is added before it is accepted.
5. **Several files dropped on a tree**, with the same preview (one file: 0.5).
6. **Retry unreadable change files.** A change file skipped because it was locked or still being copied is read again a few seconds later, not only at the next sync event or when the window is shown ([GOAL.md](docs/GOAL.md#robust-and-secure)).

## 0.10 (to define)
An Android client that reads server vaults: [ANDROID-CONCEPT.md](docs/ANDROID-CONCEPT.md). Builds on 0.8's content on demand and session in Core. No folder vaults.
1. **Sign in and unlock.** Server account and its pass phrase; the key optionally remembered in the Android Keystore.
2. **Vault list.** The account's server vaults, own and shared, with their role; add from the server, remove from the phone.
3. **Sync, downloads only.** On opening and resuming, then every 30 s while shown; status dot. The phone never uploads ([Read only](docs/ANDROID-CONCEPT.md#read-only-safely)).
4. **Search.** The input box's search, offline.
5. **Node details.** Type, name, colour, links; a tap opens the linked node; back through the trail.
6. **Notes.** Markdown and task lists shown, content fetched on demand.
7. **Views.** Flat and tree views as tabs.
8. **Pinned and recent nodes.**
9. **Datasets listed.** Name, type, size, icon; content not opened.

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
11. **Android: add and edit.** Input box, creation screen, notes and task lists, renames, colours, pins ([Later](docs/ANDROID-CONCEPT.md#later)).
12. **Android: datasets.** Open a file on demand, share it to another app, add a file from the phone.
13. **Android: sharing.** Members, roles, share, leave.
14. Stores **statistics** about data exchanges with server. It could be useful for analysing performance problems.
