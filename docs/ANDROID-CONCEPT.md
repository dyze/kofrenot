# kofrenot: Android concept

An Android client for **server vaults**: the same vaults as on Windows, read on the phone, offline. Planned for 0.10 ([ROADMAP.md](../ROADMAP.md#010-to-define)); nothing here is implemented yet.

```
 Windows (kofrenot.App)          Kofre server            Android (kofrenot.Android)
┌──────────────────────┐       ┌──────────────┐        ┌──────────────────────┐
│ read, add, edit,     │◄─────►│ stores       │───────►│ read, search,        │
│ share, folder vaults │ sync  │ (encrypted)  │  sync  │ offline              │
└──────────────────────┘       └──────────────┘ (down) └──────────────────────┘
```

## Scope

### Supported in 0.10
The first version **reads**: it browses and searches, and never writes to a vault.

| Feature | On Android | As on Windows |
| --- | --- | --- |
| **Sign in** | Server address, email, password; the session kept, renewed as on Windows | ✔ |
| **Account pass phrase** | Unlocks every server vault of the account; optionally remembered (Android Keystore) | ✔ (DPAPI there) |
| **Vault list** | The account's server vaults, own and shared, with their role; add one from the server, remove it from the phone | ✔ |
| **Offline** | Metadata kept whole on the phone: browsing and searching need no network | ✔ |
| **Sync** | Downloads only: when the app opens or comes back, then every 30 s while it is shown; status dot as in the main window (synced, offline, problem) | Downloads ✔, uploads ✘ |
| **Search** | The input box's search: nodes whose name has a word starting with the text, case and accents ignored, exact match first | ✔ (no adding) |
| **Node details** | Type, name, colour, links read from the node's side (*contained by Kitchen*), tap a link to open its node; trail of open nodes, back button | ✔ |
| **Notes** | Content shown as Markdown, task lists as (read-only) checkboxes, colours; fetched on demand ([0.8](../ROADMAP.md#08-to-define)) | ✔ |
| **Views** | Flat and tree views, as tabs; expand and collapse | ✔ |
| **Pinned and recent** | Pinned nodes (from the vault), recent nodes (on this phone) | ✔ |
| **Datasets** | Shown with name, type, size and icon; content not opened | Partly |
| **Conflicts** | Shown as on Windows (two names, two note versions), not resolved | Shown only |
| **Read only** | A banner says so; shared vaults show their role | — |

### Later
Listed in [ROADMAP.md](../ROADMAP.md#later), in that order:

1. Add and edit: input box, creation screen, notes and task lists, renames, colours, pins.
2. Datasets: open a file on demand, share it to another app, add a file from the phone.
3. Sharing: members, roles, share, leave.

### Not supported
| Feature | Why |
| --- | --- |
| **Folder vaults** | A phone has no shared folder to sync; server vaults cover it. Never planned |
| Tray, global hotkeys, window positions, single instance, drag export | Windows desktop features, no Android counterpart |
| Account setup on the phone: choosing the account pass phrase, recovery key, pass phrase change | They write account data: done on Windows first |
| Vault upgrade, attach, key rotation, delete from server | They write to the vault or the store |
| Update check (GitHub releases) | Depends on the distribution ([Open questions](#open-questions)) |

## Read only, safely
A reading client must not leave a vault in a state Windows cannot fix. Rule: **the phone never uploads, and never writes in its mirror anything the server does not have.**

| Case | On Android |
| --- | --- |
| Vault of a newer format | Not opened: *Update kofrenot* (as on Windows) |
| Vault that needs an upgrade or an attach | Not opened: *Open it once on Windows first* |
| Account without a pass phrase yet | Signed in, no vault opens: *Choose your pass phrase in kofrenot on Windows* |
| Pass phrase lost | *Use the recovery key in kofrenot on Windows* |
| Key rotated by the owner | Taken up at the next sync, as another device |

## Screens

```
 Sign in ─► Pass phrase ─► Vault list ─► Home ─────────────► Node details ─► Note
                                         │ search box           │ links, tap ─► Node details
                                         │ pinned, recent       └ back: trail
                                         └ view tabs ─► View (flat / tree) ─► Node details
 Settings: server, account, vaults, remember key, theme, logging, about
```

One window, navigation by back button; light and dark themes follow the phone.

## Architecture
A new project, beside `kofrenot.App`. Everything it shows comes from `kofrenot.Core`, unchanged in its rules ([CLAUDE.md](../CLAUDE.md#architecture-rules-keep-it-this-way)).

```
kofrenot.App      (WPF, net10.0-windows)          kofrenot.Android   (Avalonia, net10.0-android)
│ views, tray, hotkeys,                           │ views (touch), lifecycle (pause, resume)
│ DpapiProtector, VaultWatcher                    │ KeystoreProtector, AndroidLog
└──────────────┐                    ┌─────────────┘
               ▼                    ▼
        kofrenot.Core  (net10.0: graph, storage, encryption, server, sync, search, browse)
```

| Need | Core today | For Android |
| --- | --- | --- |
| Remembered keys, session | `ISecretProtector` | `KeystoreProtector` (Android Keystore, AES key never leaving it) |
| Data folder | `AppFiles` (`%LocalAppData%`, portable) | The app's private files folder |
| Logs | `ILog`, Serilog in App | Same rules, a file in the app's folder |
| Sync timing, sign-in, opening a vault | `SyncService`, `ServerConnection`, `VaultSession` **in App** (WPF timers and dialogs mixed in) | Their logic moved to Core first, timers and dialogs left to each client |
| Vault refresh after a download | `VaultWatcher` (`FileSystemWatcher`) | Sync tells the vault directly: no watcher |
| Thumbnails | `IThumbnailMaker` | Not in 0.10: icons only |

**Step 1 is a refactor on Windows, in 0.8**: the session, account and sync logic (about 2 000 lines in App) moves into Core, behind small interfaces for the timer and the questions asked to the user, with tests. It makes 0.10 possible without copying code, and is worth it for the WPF app alone.

**Depends on 0.8**: content on demand keeps the phone's mirror small, the change files only; a note's content is downloaded when it is shown.

### Dependencies
To add when 0.10 starts ([CLAUDE.md](../CLAUDE.md#dependencies)): `Avalonia`, `Avalonia.Android` (Android project only). Build: the .NET Android workload. Core stays without packages.

## Security
Same vaults, same files, same encryption ([Encryption](ARCHITECTURE.md#encryption)): the phone holds nothing a Windows device does not.

| Risk on a phone | Addressed by |
| --- | --- |
| Phone lost or stolen | Mirror and snapshot encrypted as on Windows; key remembered only in the Keystore, or not at all |
| Other apps reading the files | App-private storage; no file given to other apps in 0.10 |
| Content visible in the recent apps list | [Open question](#open-questions) |
| Cleartext traffic | HTTPS only (Android's network security config) |

## Open questions
| Topic | Question |
| --- | --- |
| Distribution | APK on GitHub releases (with the update check), F-Droid, or Google Play? |
| Minimum Android version | Which, for the Keystore and Avalonia? |
| Lock | Lock the vault after some time in the background? Fingerprint unlock of the remembered key? |
| Privacy | Hide the app's content in the recent apps list and from screenshots (`FLAG_SECURE`)? |
| Background sync | Sync only while the app is shown (0.10), or also in the background (WorkManager) later? |
