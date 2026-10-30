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
| 0.10    | Released  | Offline pins, file drops; notes and files added on Android                         |
| 0.11    | Released  | Composition: node types made of fields, several per node; fields of a node's own   |
| 0.12    | Released  | History: versions restored; the phone adds and changes nodes, links, fields and members |

## 0.13
Android navigation and layouts, as Android's [principles of navigation](https://developer.android.com/guide/navigation/principles) and Material 3. Open points: [Android open questions](docs/ANDROID-CONCEPT.md#open-questions).
1. Android · **Edge to edge.** Content kept clear of the status and navigation bars and of the keyboard (edge to edge is enforced from API 35); Back checked with predictive back (on by default from API 36).
2. Android · **Spacing.** Margins and gaps as shared resources too, as the touch sizes, type and colours already are.
3. Android · **Material layout.** A top bar with ← and ⋮, icons instead of text glyphs; node actions and adding in bottom sheets; a ＋ button (FAB) on Home; screens slide forward and back.
4. Android · **One back stack.** Back and ← both go to the screen before. A share from another app opens on Home › node, as a deep link would.
5. Android · **Lighter Home.** The search box in the top bar; the vault and its sync in a sheet.

## 0.14
1. Both · **Events and qualifiers.**
	* **Qualifiers** add context to a link: date, place, source, quantity, confidence. *Dune → written by → Frank Herbert (date: 1965)*.
	* **Events** link several nodes, each in a role, with qualifiers of their own. *Publication of Dune (work: Dune, publisher: Chilton Books, date: 1965, country: USA)*.
	* Qualifiers and roles are [fields](README.md#node-types-and-fields), so they are searched offline like any other.

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
11. Both · **Faster transfers.** Many small files downloaded in one stream; big files uploaded in parts, resumable.


