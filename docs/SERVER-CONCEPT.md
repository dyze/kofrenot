# kofrenot: server concept

The server is **Kofre** (`Kofre.Server`): generic, kofrenot is one of its clients.

Decisions: [GOAL.md](GOAL.md#decisions). Built in steps ([ROADMAP.md](../ROADMAP.md)); what is implemented is documented in [kofreserver](https://github.com/dyze/kofreserver) (the server) and [ARCHITECTURE.md](ARCHITECTURE.md#server-vaults) (kofrenot):

| Version | Parts of this design | Status |
| --- | --- | --- |
| 0.6 | [Accounts](#accounts), stores with their owner only, [Delete](#delete), files and feed of the [REST API](#rest-api-sketch); [Client sync](#client-sync) with every file downloaded; today's keys (format 5) | Implemented |
| 0.7 | [Keys](#keys): identities, recovery key, pass phrase change, [Format 6](#format-6); the account's identity in its account data, server vaults attached to it, the server's version checked | Implemented ([ARCHITECTURE.md](ARCHITECTURE.md#encryption)); account data, members, fence in [kofreserver](https://github.com/dyze/kofreserver) 0.2 |
| 0.7 | [Sharing](#sharing) | Implemented ([ARCHITECTURE.md](ARCHITECTURE.md#sharing)) |
| 0.7 | Members removed or leaving, [Rotation](#rotation) with the [fence](#fence) | Implemented ([ARCHITECTURE.md](ARCHITECTURE.md#members-leaving)) |
| 0.8 | Content on demand ([Client sync](#client-sync)) | Planned |
| 0.9 | Pinned nodes ([Client sync](#client-sync)), batch download, uploads in parts | Planned |

## Principles
- **Generic and blind.** The server stores *stores*: sets of files addressed by path, with their members. It never sees a key, a name or a content, and knows nothing of kofrenot: other apps can use it.
- **Write-once.** A file, once uploaded, is never replaced or deleted: an upload on an existing path is refused. Only a whole store can be deleted, into a trash first ([Delete](#delete)). (Packing, later, will need deletes: [ROADMAP.md](../ROADMAP.md).)
- **Offline first, unchanged.** A server vault is mirrored in a local folder: every change file is downloaded, content only when needed or pinned. `Vault` keeps reading a folder.
- **Folder vaults stay.** A vault is a folder (as today) or a server store; both use the same keys and files ([Keys](#keys)).
- **REST only.** No web site, no admin UI: the admin edits the server's JSON files.

## Generic by design
The server is a product of its own, used by kofrenot as one client among others, in its own repository: [kofreserver](https://github.com/dyze/kofreserver).

| Rule | So that |
| --- | --- |
| No kofrenot term in its API, code, files or docs (no vault, node, identity, key epoch): only accounts, stores, members, files, feed, fence | Any app can use it |
| Paths, file contents and account data are opaque: no format read, no file name pattern assumed | It never depends on a client's format |
| Every behavior a client may not want is optional per store (the [fence](#fence)) | A simple client ignores it |
| Its own project, solution folder, tests and docs; no reference to `kofrenot.Core`, and no shared code with it | It moves to another repository as is |
| kofrenot only talks to it over HTTP (`ServerStore`, in Core) | Server and client evolve apart, through the API |
| The API is versioned (`/v1/…`) | Clients of other versions keep working |

What is kofrenot's (vault layout, identity, keys, rotation) is in this document for kofrenot, not in the server.

```
 kofrenot                                  Server (Linux, Kestrel behind nginx/Caddy for HTTPS)
┌──────────────────────────────┐        ┌──────────────────────────────────────┐
│ Vault ─ reads a local folder │        │ accounts.json  (admin approves here) │
│   ▲                          │ HTTPS  │ stores/<id>/                         │
│ IVaultStore                  │ REST   │   store.json   members, fence        │
│   ├ FolderStore (as today)   │◄──────►│   feed.jsonl   seq → path, size      │
│   └ ServerStore: mirror +    │  JWT   │   files/...    the client's files    │
│     outbox, content on demand│        └──────────────────────────────────────┘
└──────────────────────────────┘
```

## Accounts
Two secrets, never mixed:

| Secret | Unlocks | Server sees |
| --- | --- | --- |
| **Password** | Signing in (tokens) | A PBKDF2 hash |
| **Pass phrase** (+ recovery key) | kofrenot's identity, kept wrapped in the account data ([Keys](#keys)) | Never |

- **Sign up:** email + password. The account waits until the admin sets `"approved": true` in `accounts.json`. No email is sent.
- **Sign in:** approved accounts only. Returns an **access token** (JWT, 15 min: the user id only) and a **refresh token** (30 days, stored hashed, revoked by a password change or a disabled account).
- **Access is checked at every request** against the store's members, not in the token: a removed member gets `403` at once.

## Stores and members

| Role | Read | Upload | Members, fence, delete, restore |
| --- | --- | --- | --- |
| reader | ✔ | | |
| writer | ✔ | ✔ | |
| owner | ✔ | ✔ | ✔ |

- The creator is the owner, the only one: not changed, not removed. A member can leave (`DELETE …/members/me`). The server knows a store by its id: the vault's name stays in its encrypted changes.
- **Fence:** optional, see [Fence](#fence).
- **Statistics:** disk space, number of files.

### Delete
No single call destroys data for good:

```
DELETE /v1/stores/{id}          + password  ─► in the trash: 403 for members, hidden from lists
POST   /v1/stores/{id}/restore  + password  ─► back as it was (the admin can also restore in the JSON)
after N days (config, 30 by default)        ─► purged from disk
```

- **Password again** (`X-Password` header) for every destructive call: a stolen token alone cannot delete.
- **"Are you sure?" is the client's**: kofrenot asks the user to type the vault's name. The server knows nothing of it.
- No *clear*: delete, create and add the members again.

### Fence
An optional number per store, to stop writers that are behind: the server compares numbers and knows nothing of what they mean.

| Store fence | Upload without `Store-Fence` | Upload with `Store-Fence: n` |
| --- | --- | --- |
| Off (default) | Accepted | Accepted, header ignored |
| `f` | Refused (`409`) | Accepted if `n ≥ f`, else refused (`409`, current fence returned) |

- An owner turns it on (`PUT …/fence` with a first value), raises it (never lowers it) or turns it off (`DELETE …/fence`).
- kofrenot turns it on for each server vault and raises it to the new key epoch at each [rotation](#rotation): a device that has not seen the latest epoch cannot add files under the old key. Another app may use it for anything else, or never.

## REST API (sketch)

| Method and path                                                                              | Does                                                                                                                                                                  |
| -------------------------------------------------------------------------------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `GET /v1/version` | The server's version: kofrenot says *server too old* below the one it needs |
| `POST /v1/accounts`                                                                          | Sign up                                                                                                                                                               |
| `POST /v1/sessions`, `POST /v1/sessions/refresh`                                             | Sign in, refresh                                                                                                                                                      |
| `PUT /v1/accounts/me/password`                                                               | Change password (revokes refresh tokens)                                                                                                                              |
| `GET/PUT /v1/accounts/me/data`                                                               | Opaque account data: a public part other users can read, a private part only its owner reads (kofrenot: the identity's public key, and its private key wrapped twice) |
| `GET /v1/accounts/{id}/public`, `GET /v1/accounts?email=` (exact)                                 | Find a user to share with, and read their public part                                                                                                                 |
| `GET/POST /v1/stores`                                                                        | List (owned and shared), create                                                                                                                                       |
| `GET /v1/stores/{id}/stats`                                                                  | Size, file count                                                                                                                                                      |
| `DELETE /v1/stores/{id}`, `POST /v1/stores/{id}/restore` | To the trash, back from it (password again): [Delete](#delete) |
| `PUT/DELETE /v1/stores/{id}/members/{user}`                                                  | Add, change role, remove, leave                                                                                                                                           |
| `PUT/DELETE /v1/stores/{id}/fence`                                                           | Turn on or raise, turn off the [fence](#fence)                                                                                                                        |
| `GET /v1/stores/{id}/feed?since={seq}`                                                       | Files added since `seq`                                                                                                                                               |
| `GET/PUT /v1/stores/{id}/files/{path}`                                                       | Download, upload (create only; optional `Store-Fence` header)                                                                                                         |
| `POST /v1/stores/{id}/batch`                                                                 | Download many small files in one stream                                                                                                                               |
| `POST /v1/stores/{id}/uploads`, `PUT …/{upload}/{part}`, `POST …/{upload}/commit`            | Big file in parts, resumable; the file appears at commit only                                                                                                         |

## Keys
Same model for folder and server vaults: only where the files are differs.

```
pass phrase ──PBKDF2──┐                 recovery key (random, shown once) ──┐
                      ▼                                                     ▼
            identity private key  ◄── unwraps ── two wraps of the same key ─┘
                      │  ECDH P-256 + HKDF + AES-GCM
                      ▼
 keys/epoch-N/<identity>.<random>.kfw   vault key N, wrapped for that identity, signed by its writer: every epoch, for each identity
                      │
                      ▼  key id in every file
          changes/, content/ (AES-256-GCM, as today)
```

|                                 | Folder vault                                              | Server vault                                  |
| ------------------------------- | --------------------------------------------------------- | --------------------------------------------- |
| Identity (public key + 2 wraps) | In the vault: `keys/identities/<id>.json`, one per member | In the account: one for all its server vaults |
| Pass phrase, recovery key       | One per vault (as today)                                  | One per account                               |
| Vault keys                      | `keys/epoch-N/…` in the vault                             | Same, in the store                            |
| Removing a member               | Rotation only (and the user stops sharing the folder)     | Server access removed, then rotation          |
| Reader role                     | Not enforceable                                           | Enforced by the server                        |

- **Open:** unwrap the identity, then each epoch wrapped for it: a table *epoch → key* in memory. A file access only looks up its epoch.
- **Change pass phrase:** wrap the identity again; nothing else changes. A folder vault's identity file is rewritten (`.tmp`, then renamed), like the header today.
- **Lost pass phrase:** the recovery key unwraps the identity; choose a new pass phrase. Both lost: the data is lost, unless another member shares the vault again with a new identity.
- **Pass phrase or recovery key leaked:** new identity (key pair), then rotation of every vault it can open. Changing the password only stops access through the server.
- **Remember pass phrase** (DPAPI) keeps the identity's private key per vault or account; the master keys remembered in `keys.json` (format 5) are replaced at upgrade.
- Built into .NET (`ECDiffieHellman`, `HKDF`, `AesGcm`, `Rfc2898DeriveBytes`): no package.

### Sharing
1. The owner looks the member up by exact email; the server returns their account id and the public part of their data: their identity's id and public key.
2. kofrenot shows the key's **fingerprint**: owner and member compare it out of band (phone, in person), so a server cannot swap keys.
3. **Every** epoch's key is wrapped for the member (`keys/epoch-N/<identity>`), a `addMember` change records them in the vault (account, identity, public key, email, role; the owner too, the first time), and the server makes them a member. The records let every device know the members' keys: a rotation never asks the server for them, so it cannot swap one later.
4. **Only the owner** changes who reads the vault: it signs the epoch wraps (`KFW2`) and the member records (`addMember`, `removeMember`) with a signing key derived from its identity. Devices use only what the owner signed: a writer can neither plant a key nor add or drop a member. Each wrap file has a random part in its name, so no member can take the name the owner writes next (files on the server are written once).
5. **The owner** is, on its own devices, this account; on a member's, the store's owner on the server, read once with its public keys and kept. Its fingerprint is listed among the members: the member can compare it with the owner too.

Server vaults only in 0.7. A folder vault (later) is shared the same way, with the member's identity added to `keys/identities/` by their kofrenot.

### Rotation
Done by the owner's kofrenot when a member is removed, or at its next opening after a member left (recorded in the vault, no longer a member on the server); on demand, *Rotate key*, later:

```
1. server: remove the member (server vault)  ─► 403 from now on
2. new vault key N+1, wrapped for each remaining member (their keys from the vault's records)
3. sync uploads the new keys, then server: fence = N+1 ─► uploads naming less refused
4. every device takes up N+1 once synced; a file it wrote under N, refused, is sealed again with N+1 (same id: it never reached the server)
```

- Files on the server are never re-encrypted: what the removed member already had stays readable to them; what is added after is not.
- The key of N+1 is derived from the owner's private key: two of its devices rotating at once write the same. An upload names the epoch in its file's header (`Store-Fence`), or the vault's latest for its header and keys.
- Cost: a 100-byte file per member and epoch.

### Format 6
A breaking change, agreed with the owner (2026-10-20): `VaultFormat.Current` becomes 6.
- New: `keys/` (identities, epochs) and the key epoch in every change and content file.
- An existing vault is upgraded without rewriting a file: its master key becomes **epoch 0**, a file without a key id is read with it, and `keys/` is added (identity, epoch 0 wrapped for it, recovery key shown). Asked through the existing upgrade question ([ARCHITECTURE.md](ARCHITECTURE.md#upgrade)).
- **Server vaults** (one pass phrase each in 0.6, or a vault identity of their own when made by an early 0.7 or moved from a folder): opened once with that pass phrase, then **attached** to the account's identity (`VaultKeys.Attach`): every key wrapped for it, then a new epoch for it only, its key derived from the identity's private key so two devices attaching at once write the same. The old pass phrase then opens only the files written before. The account's pass phrase and recovery key are chosen at the first need (a new server vault, or the first attach), or earlier when offered after signing in (Settings > Server), so it can be shared with before having a vault.
- **Move to the server:** the folder vault's files are copied as they are; it is attached at its next opening.

## Client sync
- **Mirror:** a server vault is mirrored in `%LocalAppData%\kofrenot\stores\<id>\` with the folder vault layout; `Vault` reads it as a folder.
- **Download:** the feed since the last `seq`: every file under `keys/` and `changes/` at once (batched), `content/` when shown or exported, or ahead for pinned nodes.
- **Upload:** written to the mirror first; the mirror's files the store lacks are its outbox, sent when online: adding works offline.
- **Name of a vault not yet opened on this device:** shown as a placeholder (id, size) until the account's identity is unlocked; then read from its `nameVault` changes like any vault.

## Server storage
Implemented (0.6): files on disk, configuration, admin and API in [kofreserver](https://github.com/dyze/kofreserver), the server's own repository. Each later part moves there once implemented; what stays here is kofrenot's use of it, and what is not built yet.
