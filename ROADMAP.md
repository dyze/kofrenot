# kofrenot: roadmap

Scope of each planned version. Implemented features move to [CHANGELOG.md](CHANGELOG.md), where each released version opens with its theme. The goal, principles, non-goals and open questions are in [GOAL.md](docs/GOAL.md). The features are sorted by priority, each tagged with the app that gets it: Windows, Android or Both (what each supports today: [README](README.md#windows-and-android)).

## Later
1. Both · **Events and qualifiers** ([QUALIFIERS-CONCEPT.md](docs/QUALIFIERS-CONCEPT.md)).
	* **Qualifiers** add context to a link: date, place, source, quantity, confidence. *Dune → written by → Frank Herbert (date: 1965)*.
	* **Events** link several nodes, each in a role, with qualifiers of their own. *Publication of Dune (work: Dune, publisher: Chilton Books, date: 1965, country: USA)*.
	* Qualifiers and roles are [fields](README.md#node-types-and-fields), so they are searched offline like any other.
2. Windows · **Pack vault.** Pack a vault to reduce its size and number of files (needs deletes on the server).
3. Windows · **Re-encrypt on pack.** Packed files are encrypted again with the latest vault key, so old key epochs can be dropped ([Rotation](docs/SERVER-CONCEPT.md#rotation)).
4. Windows · **Move a server vault to a folder.** Its files are saved as they are into a folder vault; its id is kept.
5. Windows · **Import/Export a server repo.**
6. Windows · **Share folder vaults.** A member's identity added to the vault's `keys/identities/` by their kofrenot ([Sharing](docs/SERVER-CONCEPT.md#sharing)).
7. Windows · **Rotate key on demand**, not only when a member is removed ([Rotation](docs/SERVER-CONCEPT.md#rotation)).
8. Windows · **Edit a label in place.** A click on a label of the input box turns it back into text there, not only the last one with Backspace.
9. Windows · **Quote a name** containing a link phrase in the input box (`"This is a test"`), so such names can be typed.
10. Windows · **Cycle through proposals.** Several proposals in grey, the next one with a key, instead of typing more to narrow it.
11. Windows · **Propose node types** while typing, in the input box, instead of only on the creation screen.
12. Both · **Faster transfers.** Many small files downloaded in one stream; big files uploaded in parts, resumable.


