# kofrenot: goal

## One-liner
A private, self-hosted knowledge system: store, organize, search, version and share notes and files securely across devices, with full offline use of metadata and on-demand access to content.

## Who it's for
Me first: a personal tool, optimised for my own workflow. Later, others who want to host their own.

## How it works (target)

```
 Device A (Windows client)          Self-hosted server (TBD)          Device B
 ┌──────────────────────┐          ┌──────────────────────┐         ┌──────────┐
 │ metadata: full copy  │◄── sync ─►│ metadata + content   │◄─ sync ─►│ ...      │
 │ content: on demand   │          │ versions, shares     │         └──────────┘
 └──────────────────────┘          └──────────────────────┘
   offline: browse, search,
   organize metadata
```

Only the Windows client shell exists today (0.1): see [ROADMAP.md](ROADMAP.md).

## Principles
- **Private and self-hosted.** The data lives on the user's own devices and server. No third-party cloud required.
- **Secure.** Content and metadata are protected in transit and at rest; sharing is explicit.
- **Offline first for metadata.** Every device keeps the full metadata (titles, tags, folders, versions, ...): browsing, searching and organizing work without a network.
- **Content on demand.** A note's or file's content is fetched when opened, and can be kept offline on request.
- **Versioned.** Changes are kept as versions that can be compared and restored.
- **Logic in Core.** The domain is plain .NET in `kofrenot.Core`, independent of Windows and of the UI.

## Versioning
- Format `MAJOR.MINOR`, starting at **0.1**, then 0.2, and so on. No `v` prefix, and no 1.0 until I decide.
- Defined once, in `Directory.Build.props`, stamped into every produced assembly, and shown at the bottom right of the main window.

What each version contains: [ROADMAP.md](ROADMAP.md).

## Non-goals
- A hosted (SaaS) service run by someone else
- Real-time collaborative editing (several people typing in the same note at once)
- Multi-user installs of the Windows client (the installer is per user)

## Open questions
Decisions to take with the owner before the matching features:

| Topic | Question |
| --- | --- |
| Note format | Markdown files, rich text, or both? |
| Local storage | Plain files on disk, a database (SQLite), or both (files + index)? |
| Server | What runs it (ASP.NET Core, an existing product such as WebDAV / Git)? Where is it hosted? |
| Sync | Protocol, conflict handling, what "on demand" means for large files |
| Security | Encryption at rest and end-to-end? Key management, authentication |
| Sharing | With whom (other users of the same server, public links), and at which granularity |
| Versioning | Every save, or explicit snapshots? Retention |
| Search | Metadata only offline; full-text on content kept offline? |
| Clients | Windows only, or also mobile / web later? |

## Stack and architecture
See [ARCHITECTURE.md](ARCHITECTURE.md).
