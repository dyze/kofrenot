# kofrenot: goal

## One-liner
A private, self-hosted knowledge system: store, organize, search, version and share notes and files securely across devices, with full offline use of metadata and on-demand access to content.

## Who it's for
Me first: a personal tool, optimised for my own workflow. Later, others who want to host their own.

## Data model
Everything is a **node**, and nodes are joined by **links**.

```
 ┌──────────┐   relates to   ┌───────────────┐
 │   Dune   │────────────────│ Frank Herbert │
 └──────────┘                └───────────────┘
   node (type: generic)        node (type: generic)
```

- A node has a type, a name and a creation date. Later types add their own properties: notes and files will be node types.
- A link has a type and joins two nodes. A link type has a name and aliases ("relates to", "related to"); it is symmetric, or directed with an inverse name ("written by" / "wrote").
- Types are extensible: 0.4 has four built-in node types (*generic*, *note*, *dataset*, *view*) and three link types (*relates to*, *contains*, *is a*). Later, the user defines their own types and properties.

## How it works (target)

```
 Device A (kofrenot)              Vault folder             Device B (kofrenot)
 ┌───────────────────┐       ┌──────────────────────┐       ┌───────────────────┐
 │ full graph in     │──────►│ header + encrypted   │◄──────│ ...               │
 │ memory, offline   │◄──────│ change files, only   │──────►│                   │
 └───────────────────┘ file  │ ever added           │ file  └───────────────────┘
                       access└──────────────────────┘ access
                                  ▲ copied between devices by a separate
                                    sync app (Dropbox-like, designed apart)
```

- A **vault is a folder**: local, or on a file share. kofrenot only reads and writes files there.
- Each change (a node or link added) is a new encrypted file, never modified afterwards, like git's objects. Several apps can write to the same vault at the same time, and a sync tool never has to merge a file.
- Only an app that has the key can read the files. The key is unlocked with a pass phrase.

Details: [ARCHITECTURE.md](ARCHITECTURE.md#vault).

## Principles
- **Private and self-hosted.** The data lives in the user's own folders. No third-party cloud required.
- **Robust and secure, first.** The vault holds what matters to the user: no data lost, corrupted or readable by others, whatever the sync tool, the other apps or a crash do. A feature that weakens this is not done. Everything in the vault, names included, is encrypted; only the header (format, key parameters) is plain. How: [Robust and secure](#robust-and-secure).
- **Offline first.** Every device has the full vault: browsing, searching and adding work without a network.
- **Content on demand.** A note's or dataset's content is read only when needed, never kept in memory or in the snapshot. Fetching it from a server, and keeping it offline on request, come with the server.
- **Versioned.** Changes are kept, never overwritten.
- **Conflicts are easy.** Files are only added, so the only conflicts are in the meaning (the same name added twice, or a node renamed on two devices before they synced): they are shown, not resolved silently.
- **Logic in Core.** The domain is plain .NET in `kofrenot.Core`, independent of Windows and of the UI.

## Robust and secure
What could go wrong, and how it is addressed today:

| Risk | Addressed by | Status |
| --- | --- | --- |
| Half-written file (crash, sync tool reading early) | Written as `*.tmp`, then renamed; `.tmp` files ignored ([Folder](ARCHITECTURE.md#folder)) | Implemented |
| Two writers on one file | Each change is a new file named by a random id, never overwritten | Implemented |
| File locked or still being copied | Skipped, logged once, read again at the next refresh ([Folder](ARCHITECTURE.md#folder)) | Implemented; retry on a timer: [ROADMAP.md](../ROADMAP.md#09-to-define) |
| Files arriving in any order (a link before its node, a change before its content) | Kept until what they refer to arrives; the result does not depend on the order, and a removal wins ([Local snapshot](ARCHITECTURE.md#local-snapshot)) | Implemented |
| Concurrent edits on two devices | Shown as conflicts, never merged silently ([Renames](ARCHITECTURE.md#renames), [Notes](ARCHITECTURE.md#notes)) | Implemented |
| A vault written by a newer kofrenot | Not misread: the vault closes and asks for an upgrade ([Format](ARCHITECTURE.md#format)) | Implemented |
| Two copies of the app writing the same local files | One copy per Windows session ([Startup](ARCHITECTURE.md#startup-and-windows)) | Implemented |
| Someone reading or changing the files | AES-256-GCM per file, the file id as AAD: a changed, swapped or cut file fails to open ([Encryption](ARCHITECTURE.md#encryption)) | Implemented |
| Pass phrase guessed | PBKDF2-SHA256, 600 000 iterations; the remembered key protected by Windows (DPAPI); the pass phrase can be changed | Implemented |
| Secrets leaking through logs or caches | No content, search text, key or pass phrase logged; the snapshot is encrypted and stays local ([Logging](ARCHITECTURE.md#logging)) | Implemented |
| The sync tool holding a `.tmp` during its rename | The save fails with an error, nothing lost | Not tested with Dropbox |
| A server reading the vaults | It stores the files as they are, encrypted with the pass phrase, which never leaves the devices ([Server vaults](ARCHITECTURE.md#server-vaults)) | Implemented |
| The server's password guessed, or a token stolen | Passwords hashed (PBKDF2-SHA256); 10 sign-ins a minute per address; access tokens last 15 minutes, the account checked at every request; deleting a vault asks the password again ([Kofre server](https://github.com/dyze/kofreserver)) | Implemented |
| A server sending a file outside the vault | Only a vault's file names are accepted from it | Implemented |
| Pass phrase lost | A recovery key, shown once, unwraps the identity too ([Keys](SERVER-CONCEPT.md#keys)) | Implemented: a folder vault's, or the server account's for its vaults; a new pass phrase is then chosen |
| A member removed from a shared vault | Server access removed at once, then key rotation: files added later are unreadable to them ([Rotation](SERVER-CONCEPT.md#rotation)) | Implemented |
| A server reading or swapping keys | The server only stores wrapped keys; fingerprints are compared out of band before sharing ([Sharing](SERVER-CONCEPT.md#sharing)) | Implemented |

## Versioning
- Format `MAJOR.MINOR`, starting at **0.1**, then 0.2, and so on. No `v` prefix, and no 1.0 until I decide.
- Defined once, in `Directory.Build.props`, stamped into every produced assembly, and shown at the bottom right of the main window.
- Each build is identified by its git commit (no build number), shown in Settings > About and the log: `0.4 (4e9c313)`, `-dirty` for a build with uncommitted changes.
- The vault has its own **format** number, raised only by a breaking change. A kofrenot reads the formats up to its own, and asks to be upgraded for a newer one: [ARCHITECTURE.md](ARCHITECTURE.md#format).

What each version contains: [ROADMAP.md](../ROADMAP.md).

## Non-goals
- A hosted (SaaS) service run by someone else
- Real-time collaborative editing (several people typing in the same note at once)
- Multi-user installs of the Windows client (the installer is per user)
- Syncing folder vaults inside kofrenot: a separate app copies their files (server vaults are synced by kofrenot, since 0.6)

## Decisions

| Topic | Decision |
| --- | --- |
| Local storage | A folder of encrypted change files, read into memory ([ARCHITECTURE.md](ARCHITECTURE.md#vault)) |
| Sync | Folder vaults: outside kofrenot, by copying files. Server vaults: kofrenot mirrors them in a local folder (since 0.6: [ARCHITECTURE.md](ARCHITECTURE.md#server-vaults)) |
| Conflicts | Concurrent duplicates, renames and note edits are flagged, not merged |
| Security | AES-256-GCM, random master key wrapped by a pass-phrase key (PBKDF2-SHA256); optionally remembered by Windows (DPAPI) |
| Notes | A node type; content in Markdown, each save a new version (since 0.2) |
| Content on demand | Content files are in the vault folder on every device (the sync tool copies them); kofrenot reads one only when needed: a note shown, a dataset exported (since 0.3) |
| Datasets | A node type holding one file of any size, each import a new version; encrypted in 1 MB chunks, so memory use does not depend on the size (since 0.3) |
| Server | A generic, blind, write-once store of files with accounts and members, on Linux, knowing nothing of kofrenot so other apps can use it, in its own repository ([kofreserver](https://github.com/dyze/kofreserver)); no email, the admin approves accounts in a JSON file (since 0.6) |
| Sharing | Complete server vaults, with roles reader, writer and one owner; removing a member rotates the vault key (since 0.7) |
| Keys | Vault keys wrapped for an identity (key pair) unlocked by a pass phrase or a recovery key; same model for folder and server vaults, format 6; a server vault's identity is its account's, one for all its server vaults (since 0.7) |
| Views | A node type holding a definition (a tree from a root node through a link type, or a list of the nodes of a type), in versions kept in its changes: views work offline (since 0.4) |

## Open questions
Decisions to take with the owner before the matching features:

| Topic | Question |
| --- | --- |
| Note format | Markdown files, rich text, or both? |
| Sharing | Below a complete vault: a single node or view? |
| Versioning | Show and restore history? |
| Packs | One file per change makes many small files. Pack old changes into bigger files in the vault folder, as git's packs? Who packs, when, and how does it stay safe with several apps and the sync tool? |
| Search | Full-text on content kept offline? |
| Clients | Windows only, or also mobile / web later? |

## Stack and architecture
See [ARCHITECTURE.md](ARCHITECTURE.md).
