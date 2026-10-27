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
- Types are extensible: 0.4 has four built-in node types (*generic*, *note*, *dataset*, *view*) and three link types (*relates to*, *contains*, *is a*); the user's own link types since 0.7. The user's own node types come with composition (below).

### Composition: types made of fields
Planned for 0.11, not implemented ([ROADMAP.md](../ROADMAP.md#011)). A node type is a list of fields, each of a kind; built-in and user types are made the same way.

```
field kind (code)                 field (vault)               type (vault)
Text · Number · Date · Url    ─►  "Birth date" : Date   ─►    Person  = Birth date + Website
Checkbox · Choice                 "Website"    : Url          Note    = Body (Markdown)
Markdown · File · View def.       "Body"       : Markdown     Dataset = File (File)

a node's fields = the fields of each of its types  +  its own fields
```

| Topic | Decision |
| --- | --- |
| Field kinds | The only part in code, each written once: Text, Number, Date, Url, Checkbox, Choice to start; Markdown, File and View definition are today's content |
| Types | A name, an icon, a colour and a list of fields; *generic*, *note*, *dataset* and *view* are built-in field lists, shipped with kofrenot |
| Several types | A node has a set of types (Frank Herbert: *Person* and *Author*), each bringing its fields; added on two devices, both are kept, no conflict |
| A node's own fields | Any node can have fields of its own, beside its types' |
| Field added to a type | Its nodes show it empty; nothing is written to them |
| Field removed from a type | Its nodes with a value keep it, as their own field; the same on a retype |
| Same name | A type's field and an own field (or two types' fields) of the same name are both shown, labelled by where they come from (*Birth date (Person)*, *Birth date (own)*); merged only on demand, keeping one value |
| Values | Small values (text, number, date, url, checkbox, choice) are metadata, always local and searchable offline; Markdown and File stay content, fetched on demand |
| Links | A value that names another node is a link, not a field. Qualifiers (fields on links) would later use the same field kinds |
| Filled in | A node's details; in the input box later |

**Existing vaults: format 8.** New changes (fields, types, values, a set of types) break older kofrenots, so the format goes from 7 to 8. Nothing is rewritten: a node's type is read as a set of one, `editNote`, `editDataset` and `editView` versions as the values of their built-in type's field, `retypeNode` as replacing the set. A vault is upgraded after the usual question; from then on kofrenot 0.10, on Windows and Android, closes it and asks to be updated, for every member of a shared vault too. The server does not read the format.

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
- **Content on demand.** A note's or dataset's content is read only when needed, never kept in memory or in the snapshot. A server vault downloads it when first needed (since 0.8); keeping it offline on request comes later.
- **Versioned.** Changes are kept, never overwritten.
- **Conflicts are easy.** Files are only added, so the only conflicts are in the meaning (the same name added twice, or a node renamed on two devices before they synced): they are shown, not resolved silently.
- **Logic in Core.** The domain is plain .NET in `kofrenot.Core`, independent of Windows and of the UI.

## Robust and secure
What could go wrong, and how it is addressed today:

| Risk | Addressed by | Status |
| --- | --- | --- |
| Half-written file (crash, sync tool reading early) | Written as `*.tmp`, then renamed; `.tmp` files ignored ([Folder](ARCHITECTURE.md#folder)) | Implemented |
| Two writers on one file | Each change is a new file named by a random id, never overwritten | Implemented |
| File locked or still being copied | Skipped, logged once, read again 2, 5, 10 and 30 s later, then at each refresh ([Folder](ARCHITECTURE.md#folder)) | Implemented |
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
| A server reading or swapping keys | The server only stores wrapped keys; fingerprints (of both public keys) are compared out of band before sharing, the owner's by its members too ([Sharing](SERVER-CONCEPT.md#sharing)) | Implemented |
| A writer taking over the keys or the members | Key epochs and member records count only when signed by the vault's owner; wrap files have unguessable names ([Sharing](SERVER-CONCEPT.md#sharing)) | Implemented |

## Versioning
- Format `MAJOR.MINOR`, starting at **0.1**, then 0.2, and so on. No `v` prefix, and no 1.0 until I decide.
- **Under development**, the version has a `-dev` suffix: `0.10-dev` is 0.10 being built, not released, older than `0.10` (the update check offers the release to it). Only a release is ever plain `MAJOR.MINOR`:

  ```
  0.9 released ──► 0.10-dev … ──► 0.10 (release commit) ──► 0.11-dev …
  ```

  The release commit drops `-dev`; the next commit sets the next version with `-dev`. Both happen when I ask for a release or a bump.
- Defined once, in `Directory.Build.props`, stamped into every produced assembly, and shown at the bottom right of the main window, `-dev` included. Where only numbers fit (Android's version code, the Windows file version), the number without `-dev` is used (`VersionNumber`).
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
| Notes | A node type; content in Markdown, each save a new version (since 0.2); task lists as Markdown task lines (`- [ ]`), shown as checkboxes (since 0.8) |
| Content on demand | Content files are in the vault folder on every device (the sync tool copies them); kofrenot reads one only when needed: a note shown, a dataset exported (since 0.3). A server vault downloads notes' and datasets' files when first needed, thumbnails with the sync (since 0.8) |
| Datasets | A node type holding one file of any size, each import a new version; encrypted in 1 MB chunks, so memory use does not depend on the size (since 0.3) |
| Server | A generic, blind, write-once store of files with accounts and members, on Linux, knowing nothing of kofrenot so other apps can use it, in its own repository ([kofreserver](https://github.com/dyze/kofreserver)); no email, the admin approves accounts in a JSON file (since 0.6) |
| Sharing | Complete server vaults, with roles reader, writer and one owner; removing a member rotates the vault key (since 0.7) |
| Keys | Vault keys wrapped for an identity (key pair) unlocked by a pass phrase or a recovery key; same model for folder and server vaults, format 6; a server vault's identity is its account's, one for all its server vaults (since 0.7) |
| Clients | Windows (WPF); Android for server vaults only, with Avalonia: read only in 0.9, notes and files edited since 0.10 ([ANDROID-CONCEPT.md](ANDROID-CONCEPT.md)) |
| Types and fields | Composition: a type is a list of fields, a node has several types and fields of its own (planned for 0.11, format 8: [Composition](#composition-types-made-of-fields)) |
| Views | A node type holding a definition (a tree from a root node through a link type, or a list of the nodes of a type), in versions kept in its changes: views work offline (since 0.4) |

## Open questions
Decisions to take with the owner before the matching features:

| Topic | Question |
| --- | --- |
| Sharing | Below a complete vault: a single node or view? |
| Versioning | Show and restore history? |
| Packs | One file per change makes many small files. Pack old changes into bigger files in the vault folder, as git's packs? Who packs, when, and how does it stay safe with several apps and the sync tool? |
| Search | Full-text on content kept offline? |

## Stack and architecture
See [ARCHITECTURE.md](ARCHITECTURE.md).
