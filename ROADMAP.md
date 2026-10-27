# kofrenot: roadmap

Scope of each planned version. Implemented features move to [CHANGELOG.md](CHANGELOG.md). The goal, principles, non-goals and open questions are in [GOAL.md](docs/GOAL.md). The features are sorted by priority, each tagged with the app that gets it: Windows, Android or Both (what each supports today: [README](README.md#windows-and-android)).

| Version | Status    | Theme                                                                              |
| ------- | --------- | ---------------------------------------------------------------------------------- |
| 0.1     | Released  | Application shell; nodes and links typed in one box, stored in an encrypted folder |
| 0.2     | Released  | Notes: link types, notes with content, pinned and recent nodes                     |
| 0.3     | Released  | Datasets: files stored in the vault, read on demand                                |
| 0.4     | Released  | Views: flat and tree views over the graph, shown as tabs                           |
| 0.5     | Released  | Vaults: several vaults, a hot key per vault; change types, link nodes              |
| 0.6     | Released  | Server: own vaults stored on a web server, synced across devices                   |
| 0.7     | Released  | Sharing: identities, recovery key, vaults shared with other users                  |
| 0.8     | Released  | Notes as in Keep; content on demand; session in Core for Android                   |
| 0.9     | Released  | Android: server vaults read and searched offline on the phone                      |
| 0.10    | Implemented | Offline pins, file drops; notes and files added on Android                       |

## 0.10
Every planned feature is implemented: [CHANGELOG.md](CHANGELOG.md#unreleased), until 0.10 is released.

## Later
1. Windows · **Pack vault.** Pack a vault to reduce its size and number of files (needs deletes on the server).
2. Windows · **Re-encrypt on pack.** Packed files are encrypted again with the latest vault key, so old key epochs can be dropped ([Rotation](docs/SERVER-CONCEPT.md#rotation)).
3. Windows · **Move a server vault to a folder.** Its files are saved as they are into a folder vault; its id is kept.
4. Windows · **Import/Export a server repo.**
5. Windows · **Share folder vaults.** A member's identity added to the vault's `keys/identities/` by their kofrenot ([Sharing](docs/SERVER-CONCEPT.md#sharing)).
6. Windows · **Rotate key on demand**, not only when a member is removed ([Rotation](docs/SERVER-CONCEPT.md#rotation)).
7. Windows · **Edit a label in place.** A click on a label of the input box turns it back into text there, not only the last one with Backspace.
8. Windows · **Quote a name** containing a link phrase in the input box (`"This is a test"`), so such names can be typed.
9. Windows · **Cycle through proposals.** Several proposals in grey, the next one with a key, instead of typing more to narrow it.
10. Windows · **Propose node types** while typing, in the input box, instead of only on the creation screen.
11. Android · **Add and edit.** Input box, creation screen, renames, colours, pins (notes and files: 0.10).
12. Android · **Sharing.** Members, roles, share, leave.
13. Both · Stores **statistics** about data exchanges with server. It could be useful for analysing performance problems.
14. Both · **Events and Qualifiers.** :
	* Qualifiers enrich existing relationships (`node → link → node`) with contextual information such as dates, locations, sources, quantities, or confidence levels. Example: Dune → written by → Frank Herbert (date: 1965).
	* Events represent occurrences involving multiple entities, each with a specific role, and their own qualifiers. Example: Publication of Dune (work: Dune, publisher: Chilton Books, date: 1965, country: USA).
	* Together, these features transform Kofrenot from a simple knowledge graph into a richer, contextual knowledge system, enabling more precise searches, historical tracking, and complex relationships while preserving the existing simple model.
15. Both · **Faster transfers.** Many small files downloaded in one stream; big files uploaded in parts, resumable.
16. Both · **Restore previous versions.** A node's versions are listed, so a mistake can be undone by restoring one of them.
