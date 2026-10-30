# kofrenot: Android concept

An Android client for **server vaults**: the same vaults as on Windows, read on the phone, offline, their notes edited and files added there since 0.10, their nodes changed there since 0.12. Released in 0.9 ([CHANGELOG.md](../CHANGELOG.md#09---2026-10-26)); what is later or open is marked so.

```
 Windows (kofrenot.App)          Kofre server            Android (kofrenot.Android)
┌──────────────────────┐       ┌──────────────┐        ┌──────────────────────┐
│ read, add, edit,     │◄─────►│ stores       │◄──────►│ read, search, add,   │
│ share, folder vaults,│ sync  │ (encrypted)  │  sync  │ edit, delete nodes,  │
│ types, keys          │       │              │        │ invite members       │
└──────────────────────┘       └──────────────┘        └──────────────────────┘
```

## Scope

### Supported
What the phone does, beside Windows: the [feature matrix](../README.md#windows-and-android); how to use it: [On Android](../README.md#on-android); what comes next: [ROADMAP.md](../ROADMAP.md). The first version (0.9) only read; since 0.10 the phone also edits notes and adds files; since 0.12 it changes the vault's nodes too, and leaves what defines the vault to Windows ([What the phone changes](#what-the-phone-changes)). Below: why it differs, and how.

### Not supported
| Feature | Why |
| --- | --- |
| **Folder vaults** | A phone has no shared folder to sync; server vaults cover it. Never planned |
| Tray, global hotkeys, window positions, single instance, drag export | Windows desktop features, no Android counterpart |
| Account setup on the phone: choosing the account pass phrase, recovery key, pass phrase change | They write account data: done on Windows first |
| Vault upgrade, attach, key rotation, delete from server; a member removed (it rotates the key) | They write to the vault or the store |
| Your node types, link types and aliases; the vault's name | They define the vault: changed on Windows ([What the phone changes](#what-the-phone-changes)) |
| Update check (GitHub releases) | Depends on the distribution ([Open questions](#open-questions)) |

## What the phone changes
The phone must not leave a vault in a state Windows cannot fix. Rule (since 0.12, `ClientMode.Phone`): **the phone changes the vault's nodes, never what defines the vault**. `PhoneChanges` checks every change in Core, not only the screens; its writes go to its mirror, then to the server at the next sync, as on Windows.

| The phone writes | Left to Windows |
| --- | --- |
| Nodes and links added; names, colours, pins; nodes and links deleted (asked first) | Your node types, link types and aliases (their definitions, a removal of one) |
| Fields filled; types added to a node, taken out | The vault's name; upgrade, attach, keys, key rotation |
| Notes, files, photos; a version restored | A member removed (it rotates the vault key) |
| Members invited, roles changed; leaving a vault shared with you | Vaults created, moved, deleted; the account's pass phrase, recovery key |

Before 0.12 the phone wrote content only (`ClientMode.Content`: notes and files).

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
                                         │ search box           │ links, tap ─► Node details (a screen each)
                                         │ pinned, recent       └ Back, ←: the screen before
                                         └ view tabs ─► View (flat / tree) ─► Node details
 Settings: server, account, vaults, members, remember key, theme, logging, about
 Home: the search box's text ─► Creation (types, a note's text) ─► Home (cleared, the nodes added first among the recent)
 Home: ☰ (vault, sync, other vaults) · search box · ⋮ (Settings) · ＋ (files, photo, video)
 Node details: ⋮ (rename, colour, pin, fields, types, delete)
```

One window, navigation by back button; light and dark themes follow the phone, or the theme chosen in Settings > Appearance (what Android draws itself, the status bar and the splash, follows the phone).

### Layout
| Part | How |
| --- | --- |
| Tokens | `App.axaml`: touch target (48 dp), bar height, type sizes (title, section, body, caption), colours; code reads them with `PhoneTheme.Brush` and `PhoneTheme.Size` |
| Insets | Edge to edge (`InsetsManager`): the `Shell`'s padding is the safe area, at the bottom the keyboard (`InputPane`) when it covers more; the focused field brought into view (`Shell.Insets`) |
| Transitions | `TransitioningContentControl` with a `PageSlide`, reversed going back |
| Back | `TopLevel.BackRequested`, with predictive back (`enableOnBackInvokedCallback`) |
| Top bar | `TopBar` (a templated control, its look in `App.axaml`): ← when the `Shell` has a screen before, the title or the screen's own content (the search box, the trail), its actions (⋮); ← raises the same back as Android's Back |
| Icons | `StreamGeometry` paths of our own in `App.axaml` (←, ⋮, ＋, ☰, ✕), shown with `PathIcon` |
| Sheets | `Shell.ShowSheet(title, choices)`: a bottom sheet over the screen, instead of menus; a choice closes it, then runs; Back or a tap outside closes it |
| Back stack | `Shell`: one stack of screens; a deep link (a share from another app) shows the home, then its node over it (`ShowOverHome`) |

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
| Sync timing, sign-in, opening a vault | `SyncService`, `ServerConnection`, `VaultSession` in Core (`Session/`: `TimeProvider` timers, `IUiThread`, `ISessionQuestions`) | `AvaloniaUiThread` on Avalonia's dispatcher; `PreparedAnswers`: Android has no dialog that waits, so the pass phrase is asked on its own screen first, then the session called with it |
| What the phone changes | — | `VaultSession` made with `ClientMode.Phone`: a change that defines the vault refused in Core (`PhoneChanges`), not only hidden by the screens |
| Vault refresh after a download | `VaultWatcher` (`FileSystemWatcher`) | Read after each sync (`VaultSession.AfterSync`): no watcher (`NothingToWatch`) |
| Thumbnails | `IThumbnailMaker` | `AndroidThumbnailMaker` (since 0.10, for the images added on the phone) |

**Step 1, a refactor on Windows, is done (0.8)**: the session, account and sync logic is in Core, behind small interfaces for the timers, the UI thread and the questions asked to the user, with tests. 0.9 reuses it without copying code.

**Depends on 0.8**: content on demand keeps the phone's mirror small, the change files only; a note's content is downloaded when it is shown.

### Dependencies
Added in 0.9 ([ARCHITECTURE.md](ARCHITECTURE.md#stack)): `Avalonia.Android` and `Avalonia.Themes.Fluent` 12.1, and Serilog as on Windows (Android project only). Build: the .NET Android workload, `build\android.cmd`. Core stays without packages.

## Security
Same vaults, same files, same encryption ([Encryption](ARCHITECTURE.md#encryption)): the phone holds nothing a Windows device does not.

| Risk on a phone | Addressed by |
| --- | --- |
| Phone lost or stolen | Mirror and snapshot encrypted as on Windows; key remembered only in the Keystore, or not at all |
| Other apps reading the files | App-private storage; a file leaves only on *Open with…* (a copy in `cache/open/`, given read only to the app picked, deleted at the next start) or *Save as…* (where the user chooses) |
| Content visible in the recent apps list, screenshots | `FLAG_SECURE`: neither shows it, unless the privacy screen is turned off in Settings (to take a screenshot) |
| Cleartext traffic | HTTPS only (Android's network security config), but to the phone itself (a server reached through `adb reverse` while developing) |

## Decided (0.9)
| Topic | Decision |
| --- | --- |
| Distribution | An APK on GitHub releases, for arm64 phones, signed with the owner's key (`build\android.cmd`; `emulator` adds x64, to try it in an emulator); no update check yet |
| Minimum Android version | 8.0 (API 26) |
| Privacy | `FLAG_SECURE`: the app's screens stay out of the recent apps list and of screenshots; Settings > General turns it off, on by default |
| Background sync | Only while the app is shown |

## Decided (0.13)
| Topic | Decision |
| --- | --- |
| Material controls | Top bar, bottom sheets and FAB built in the app on the Fluent theme: no new package |
| Node trail | Each node opened from a link is a screen of the one back stack; the trail at the top is the screens before (`Shell.PagesUpTo`); `NodeBrowser` keeps one level on the phone |

## Open questions
| Topic | Question |
| --- | --- |
| Distribution | F-Droid or Google Play later? An update check of the APK? |
| Lock | Lock the vault after some time in the background? Fingerprint unlock of the remembered key? |
| Background sync | Also in the background (WorkManager) later? |
| Large screens | A list beside the node's details from 600 dp wide (tablets, landscape), or phone layout only for now? |
