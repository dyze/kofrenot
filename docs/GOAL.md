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
- Types are extensible: 0.1 has one built-in node type (*generic*) and one link type (*relates to*). Later, the user defines their own types and properties.

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
- **Secure.** Everything in the vault, names included, is encrypted; only the header (format, key parameters) is plain.
- **Offline first.** Every device has the full vault: browsing, searching and adding work without a network.
- **Content on demand** (later). A note's or file's content is fetched when opened, and can be kept offline on request.
- **Versioned.** Changes are kept, never overwritten.
- **Conflicts are easy.** Files are only added, so the only conflicts are in the meaning (the same name added twice on two devices): they are shown, not resolved silently.
- **Logic in Core.** The domain is plain .NET in `kofrenot.Core`, independent of Windows and of the UI.

## Versioning
- Format `MAJOR.MINOR`, starting at **0.1**, then 0.2, and so on. No `v` prefix, and no 1.0 until I decide.
- Defined once, in `Directory.Build.props`, stamped into every produced assembly, and shown at the bottom right of the main window.
- The vault has its own **format** number, raised only by a breaking change. A kofrenot reads the formats up to its own, and asks to be upgraded for a newer one: [ARCHITECTURE.md](ARCHITECTURE.md#format).

What each version contains: [ROADMAP.md](ROADMAP.md).

## Non-goals
- A hosted (SaaS) service run by someone else
- Real-time collaborative editing (several people typing in the same note at once)
- Multi-user installs of the Windows client (the installer is per user)
- Syncing files between devices inside kofrenot: a separate app does it

## Decisions

| Topic | Decision |
| --- | --- |
| Local storage | A folder of encrypted change files, read into memory ([ARCHITECTURE.md](ARCHITECTURE.md#vault)) |
| Sync | Outside kofrenot, by copying files. No server for now |
| Conflicts | Concurrent duplicates are flagged, not merged |
| Security | AES-256-GCM, random master key wrapped by a pass-phrase key (PBKDF2-SHA256); optionally remembered by Windows (DPAPI) |
| Notes | Will be a node type |

## Open questions
Decisions to take with the owner before the matching features:

| Topic | Question |
| --- | --- |
| Note format | Markdown files, rich text, or both? |
| Server | Later: a web server (REST API) holding the same files? |
| Content on demand | What "on demand" means for large files |
| Sharing | With whom, and at which granularity |
| Versioning | Show and restore history? |
| Packs | One file per change makes many small files. Pack old changes into bigger files in the vault folder, as git's packs? Who packs, when, and how does it stay safe with several apps and the sync tool? |
| Search | Full-text on content kept offline? |
| Clients | Windows only, or also mobile / web later? |

## Stack and architecture
See [ARCHITECTURE.md](ARCHITECTURE.md).
