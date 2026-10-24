# Changelog

All notable changes to kofrenot are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/). Versions follow kofrenot's own `MAJOR.MINOR` scheme ([docs/GOAL.md](docs/GOAL.md#versioning)), not Semantic Versioning. Planned versions: [ROADMAP.md](ROADMAP.md).

## [Unreleased]

### Added
- **Task lists** in notes, as in Google Keep: **Checkboxes** on a note's toolbar shows its lines as items with a checkbox, checked items crossed out and listed below the others; Enter adds an item, Backspace in an empty one removes it. Clicked again, the items become lines of text. A note whose lines are all items opens as a list, and its status says how many are done. Stored as Markdown task lines (`- [ ] text`), so the vault format does not change.
- **Colours**: any node can be given a colour in its details (yellow, green, blue, pink, purple, teal, grey, or none), shown as a dot on its icon in every list and as the background of a note's text. Synced with the vault; chosen differently on two devices before they synced, both are shown, to keep one.
- **Cards**: **Cards** on a list view's bar shows its nodes as cards, as in Google Keep, each in its colour with a preview: a note's first lines or unchecked items, an image's thumbnail, a file's kind and size, what a view shows. A note is read only when its card comes into view. Kept per view on this computer.

### Changed
- **Vault format 7**, for colours: kofrenot asks before upgrading a vault when it opens it; kofrenot 0.7 then asks to be updated to open it.

## [0.7] - 2026-10-24

### Added
- **Sharing**
  - **Share a server vault** (its owner): Settings > Vault > *Members* > *Share...*, with another account on the server found by its email, as a reader or a writer, once the fingerprint of their key is compared with them (theirs: Settings > Server, *Your key*). In *Vault from the server…* it is marked *shared with you*, as reader or writer; they open it with their own pass phrase; a reader cannot change it, and sees *Read only* in the window's title and header. The members are listed with their fingerprint, the owner's included, for them to compare too. Only the owner changes the vault's keys and its members: what it did not sign is ignored, so a writer cannot let anyone else in, nor drop anyone.
  - **Change a member's role**: *Members* > *Change role...* makes a writer a reader, or a reader a writer, after a confirmation; no new key.
  - **Remove a member, or leave**: the owner removes a member (*Members* > *Remove...*); a member leaves (*Leave...*). The vault's key then changes: what is written from now on is unreadable to them, what they had stays theirs. The owner's kofrenot changes it when it removes them, or at its next opening after they left; the server then refuses files sealed with the old key, which the other devices seal again on their own.
- **Keys and pass phrases**
  - **Recovery key**: shown once when a vault is created or upgraded, it opens the vault when its pass phrase is lost (*Lost it? Use the recovery key* when unlocking); a folder vault then asks a new pass phrase.
  - **Change the pass phrase** of a folder vault: Settings > Vault > *Pass phrase* > *Change...*, the vault open. The recovery key and the remembered pass phrase stay valid.
  - **Server account's pass phrase**: one pass phrase, and one recovery key, open all your server vaults. Chosen when offered after signing in, in Settings > Server > *Pass phrase* > *Choose...*, or with your first new server vault (or when an older one moves to it); changed there too (*Change...*). Kept, sealed, in your account on the server, and copied on each computer so server vaults open offline.
  - **Show what you type** in a pass phrase or password box: hold its eye button; released, it is masked again.
- **Server**
  - **Change the server password**: Settings > Server > *Password* > *Change...*, the current one asked again. Your other devices are signed out.
  - **Server too old**: Settings > Server says when the server runs a version older than 0.2; your server vaults still sync, but cannot move to the account's pass phrase until it is upgraded.
- **Link types and input box**
  - **Your own link types**: Settings > Link types > *New link type*, with a name (*wrote*) and how it reads the other way (*written by*), or the same both ways (*knows*). Listed with the built-in ones, marked *yours*; typed in the input box, proposed and labelled like the built-in ones, offered on the creation screen, in *Change link type* and for trees; aliases can be added to them. ✎ renames one in place, keeping the way it reads; × removes one, after asking, once no link has it. Stored in the vault (format 6), so every device has them; renames made on two devices are flagged.
  - **Input box labels and proposals**: the first node and the link type become labels once the link type is recognized, a new node in green with a `+`, homonyms counted; Enter turns the rest into labels before the creation screen; Backspace turns the last label back into text. The rest of a node's name or of a link type is proposed in grey while typing; **Tab** or **→** accepts it, and Tab switches the side of a link only when nothing is proposed.
- **App**
  - **Another set of data**: `kofrenot.exe --data <folder>` keeps the settings, vaults list, remembered pass phrases and server account in that folder, to use another account or test data.

### Changed
- **Keys and pass phrases**
  - **Vault format 6**: a vault's key is wrapped for an identity (a key pair) that the pass phrase and the recovery key unlock, kept in the vault's `keys/` folder; every file says which key epoch sealed it. An existing vault is upgraded without rewriting a file: its pass phrase is asked again, then its recovery key shown. kofrenot 0.6 and older no longer open an upgraded vault.
  - **Server vaults** open with your server account's pass phrase. A vault from 0.6, or moved from a folder, opens once more with its own, then moves to the account's: its own pass phrase then opens only its older files. Needs a Kofre server from 0.2.
- **Server**
  - **Sync status in the header**: a server vault shows where its sync stands next to the lock, a dot coloured green (synced), grey, amber (offline) or red (sign in, failed), with the files waiting to be uploaded; its tooltip says more, a click syncs now or opens Settings > Server. It is no longer in the footer. A server stopped behind its proxy now counts as offline, not as a failed sync.
- **Link types**
  - **Removing an alias asks first**, as every other removal does.
  - **Aliases are added in place**: *Add alias* at the end of a way's line in Settings > Link types, instead of the *New alias of* form.
- **Dialogs**
  - **The button Enter presses** is drawn in the accent color in every dialog (*Next*, *OK*, *Save*, *Find* then *Share*, ...), so it is clear before pressing.

## [0.6] - 2026-10-21

### Added
- **Server**
  - **Kofre server** support: a server, in its own repository ([kofreserver](https://github.com/dyze/kofreserver)), keeps the files of server vaults, encrypted as they are, for accounts its admin approves.
  - **Account** in Settings > Server: sign up, sign in (kept across restarts, the password is not), sign out.
- **Vaults**
  - **Server vaults**: *New vault on the server* and *Vault from the server…* in Settings > Vault > *Add...*. kofrenot syncs them across your devices itself: every 30 seconds, soon after a change, and when the window is shown. Kept whole on each computer, they work offline; what is added offline is uploaded at the next sync.
  - **Move to server…**: a folder vault goes to the server as it is, with its name, hotkey and remembered pass phrase; its folder is left as it was.
  - **Delete from server…**: the vault's name and your password asked; it goes to the server's trash, for every device.
  - **Sync status** in the main window's footer and in Settings > Vault: synced, offline, sign in to sync, files waiting to be uploaded.
  - **Name asked on creation**, for a folder vault (its folder's name offered) as for a server vault, so every device shows it at once.

## [0.5] - 2026-10-20

### Added
- **Settings**
  - **Key bindings** tab: every shortcut, by area, with the global hotkeys.
- **Vaults**
  - **Several vaults**: add as many as you like in Settings > Vault (*Add...*), order them (*Up*, *Down*), and *Remove* one from the list (its folder is not touched; its remembered pass phrase is forgotten on this computer). One is open at a time, and they never mix.
  - **Vault box** at the top left of the main window, with two vaults or more: switches to the vault chosen, asking its pass phrase unless remembered; cancelled, the previous vault comes back. The last vault chosen opens at start. A vault whose folder cannot be reached is marked *unavailable*.
  - **Vault name**: *Rename* in Settings > Vault. Stored, encrypted, in the vault, so every computer shows it; a locked vault shows the last name seen on this computer. Named differently on two computers before they synced, both names are shown: renaming keeps one.
  - **Hotkey per vault** (optional, Settings > Vault): shows the main window on that vault, switching to it; pressed again while it is in front, it hides it.
  - **Ctrl+1 to Ctrl+9** switch to the vault at that place in the list; the window's title is the vault's name.
  - **Lock** (padlock next to the vault box, or Ctrl+L): the vault stays locked, also when you come back to it and after a restart, until Unlock, which always asks its pass phrase.
- **Node details**
  - **Type combo**: a generic node becomes a note, a dataset or a view, after a warning (it cannot be undone). Changed differently on two computers before they synced, both types are shown, to keep one.
  - **+ Link** (Ctrl+K): links the node to another one, typed as in the input box after its name (`contains Chapter 1`).
  - **Change link type**: right-click a link to change its type or its direction.
- **Views**
  - **Drag a dataset out** of a tree to a folder; its file is exported only when it leaves kofrenot.
  - **Drop a file** on a node of a tree: a new dataset with that file, named after it, under that node.

### Changed
- **Vault**
  - **Vault format 5**, for the vault's name and type changes. kofrenot 0.4 does not open a vault written by this version, and offers the update; this version asks before upgrading a vault of format 4 or older.
  - **Settings > Vault** lists the vaults, instead of one vault folder. The vault folder of 0.4 becomes the first vault of the list.
  - A vault opened before whose folder is missing (a file share offline) is said to be out of reach, instead of offering to create a new vault there.

## [0.4] - 2026-10-19

### Added
- **Views**
  - **Views**: a node type showing the vault another way: a tree from a node, through a link type (*contains* by default), or a list of the nodes of a type. Chosen on its creation step; kept in the vault, so every device has it, and it works offline. In a tree, a node with several parents shows under each, and a cycle is cut. Enter or a double-click opens a node's details; Back returns to the view. A tree keeps the nodes expanded on this computer, between starts too.
  - **Tabs** at the top of the main window: *Search* first, then the views open on this computer, kept between starts. **+** opens a view, or creates one (**New view…**: its name typed on the creation screen, its type fixed), **×** closes its tab; a new view opens in a tab. **Ctrl+Tab** and **Ctrl+Shift+Tab** select the next and the previous tab, as in Settings.
  - **Edit a view**: change what a view shows from its details (**Edit…**) or **⚙** on its tab, when selected. Each change is a new version; edits made on two computers before they synced are resolved by saving.
  - **Find in a view**: a search box over a view goes to the nodes whose name matches, in the whole tree, without hiding the others; Enter (or ↓) goes to the next one, Shift+Enter to the previous, Ctrl+F gets to it.
  - **Details in a view**: a toggle on the bar shows the type, size and last change of each node in columns, as in a file explorer; kept per view on this computer. A node's details also show its last change (*Modified*).
- **Nodes in a view**, from the bar over a view and its right-click menu
  - **New**: adds a node, its name typed on the creation screen: in a tree, under the node selected, linked the tree's way; in a list, of the list's type. It goes first among the recent nodes, as does a view made with **New view…**.
  - **Edit**: opens the node selected in its details, as Enter does.
  - **Rename** (or F2): edits the name of the node selected in place; Enter or a click elsewhere saves it, Esc cancels. As everywhere a name is set, an empty name is refused.
  - **Tree operations**: in a tree view, copy (Ctrl+C), cut (Ctrl+X) and paste (Ctrl+V) a node under another one, or delete it (Del); or drag a node onto another to move it (Ctrl held: copy it). A node cut is dimmed until pasted (Esc cancels). A copy takes the whole subtree as new nodes; a move keeps the node's other parents; a deletion takes the whole subtree. A copy and a deletion show first what they will do.
- **Vault**
  - **Vault upgrade asked first**: a vault written by an older kofrenot is used only once its upgrade is agreed, since the older kofrenot no longer opens it after a change. *Not now* leaves it unused and untouched; *Upgrade and open…* in the main window asks again. Asked once per vault on each computer.
- **App**
  - **Build commit** in Settings > About and the startup log line: `0.4 (4e9c313)`, with `-dirty` for a build with uncommitted changes. The main window still shows the version alone.
  - **Replace a running copy**: starting kofrenot while another build of it runs (another version, commit or uncommitted changes, or another exe) names both, and offers to close the running one and start this one. A copy of 0.3 or older cannot be asked to close: the message says to quit it from the tray.

### Changed
- **Node details**
  - **Rename by clicking the name** in a node's details (or F2); leaving it saves, as Enter does. The ✎ button is gone.
- **Vault**
  - **Vault format 4**, to remove nodes and links, and for views. kofrenot 0.3 does not open a vault written by this version, and offers the update. Vaults of formats 1 to 3 are read as they are.
- **App**
  - **Quit is refused** while a note has unsaved changes or a file is being imported, from the tray or when another copy asks: the window shows why.

## [0.3] - 2026-10-18

### Added
- **Datasets**: a node type holding one file of any kind and size, encrypted in the vault. Chosen on its creation step (or dropped there), then imported again from its details (**Import…**, Ctrl+I, or a file dropped on it) and exported (**Export…**, Ctrl+E, or dragged out). Each import is a new version; files imported on two computers before they synced show a conflict, to keep one version. The file's name, size and date are shown offline; the file itself is read only when viewed or exported. An import shows its progress in percent.
- **Dataset type and viewer**: a dataset's type is its file's extension (*PNG file*, *JSON file*, ...). Texts (txt, md, csv, json, xml, html, yaml, code, ...) are shown in its details, images (png, jpg, gif, bmp, tif, ico) in their own resizable window (**View image**, or a double-click on the file), fitted then zoomed with the mouse wheel or +/- (0 fits again) and moved by dragging, decrypted in memory only: nothing decrypted is written to disk. A long text shows its beginning, and can wrap its lines; other types are exported to be opened.
- **Vault size** in Settings > Vault: its size on disk and its number of files, next to its nodes and links.
- **Thumbnails and icons**: every node shows an icon in the search results, the links and a dataset's details, by its type and for a dataset by its file's kind (image, text, code, audio, video, archive, PDF). An image dataset shows a thumbnail instead, made once at import and stored encrypted in the vault.

### Changed
- **Rows** in the search and the links: highlighted across their width when hovered, the selected one marked by a bar. A click selects a row, a double-click opens its details.
- **Dataset details**: the file next to the node's properties, its buttons under it, the links across the width below.
- **Creation screen**: a new node's type is chosen in a list on the right, each type with its icon, instead of a combo.
- **Vault format 3.** kofrenot 0.2 does not open a vault written by this version, and offers the update. Vaults of formats 1 and 2 are read as they are.

## [0.2] - 2026-10-18

### Added
- **Directed link types** with a reverse: *contains* / *contained by*, *is a* (alias *is an*) / *has instance*. A link can be typed either way and reads from the node it is seen from.
- **Link type aliases** in Settings > Link types: your own phrases for a link type, either way, stored in the vault.
- **Pinned nodes**: pin a node from its details; pinned nodes are listed while the input box is empty. Stored in the vault.
- **Recent nodes**: the last 10 nodes opened, added or linked on this computer are listed after the pinned ones while the input box is empty.
- **Another copy running**: starting a copy of kofrenot while another one runs (another exe or version) now says so, naming both, instead of silently showing the other.
- **Main window position** (Settings > General): centered on the mouse's monitor (default), at the mouse, or where you left it, as in coursee.
- **Notes**: a node type with Markdown content, edited in its details with a toolbar (bold, italic, heading, list, link) and saved as a new version each time. Edits made on two computers before they synced are shown as a conflict, to keep one version or merge them by hand.

### Changed
- **Creation screen in steps**: one step per new node (its type, and for a note its content), then the link. Nothing is added before the last step; Esc cancels everything.
- The same text has the same size in every window (one type scale: node name, window title, content, secondary text, status, footer).
- **Vault format 2.** kofrenot 0.1 does not open a vault written by this version, and offers the update. Vaults of format 1 are read as they are.
- The local snapshot keeps the changes themselves (snapshot format 3): every change is read again once after the update.

## [0.1] - 2026-10-17

### Added
- **Application shell**, taken from coursee: a main window that hides on close, a tray icon (Show, Settings, Log window, Quit), and a single running copy.
- **Settings**: Appearance (light, dark or follow Windows), Logging (debug logging, log window) and About (version, links, changelog, files), with a search box.
- **Logging**: local rolling file (3 x 1 MB) and a live log window.
- **Window positions** remembered per monitor, and the log window's column widths.
- **Vault**: a folder of encrypted files, chosen in Settings > Vault (*Explore to* opens it). An empty folder becomes a new vault, protected by a pass phrase asked twice; *Remember pass phrase* keeps its key in Windows, *Forget* removes it.
- **Vault format**: a vault and each of its change files carry a format number. A vault written by a newer kofrenot is not opened (or is closed when such a change arrives), and the main window offers to get the latest kofrenot.
- **Nodes and links**: nodes of type *generic* (name, creation date), joined by *relates to* links (alias: *related to*). Several apps can use the same vault at once; changes made elsewhere are read without a restart.
- **Input box** in the main window: `Dune` adds a node, `Dune relates to Frank Herbert` a link and the nodes it needs. While typing, the matching nodes and the links of an exact match are listed, and a preview says what Enter adds. Case and accents are ignored. Homonyms are marked `¹`, `²`, and Up/Down picks one (Tab: the other side of a link).
- **Creation screen**: Enter in the input box shows what will be added before adding it: the type of each new node (*Generic* by default) and the link type, both chosen from the built-in types, and what exists already (a name already used, with its nodes and their types; for a link, whether each side is new). Enter adds, Esc cancels.
- **Node details**: select a node found (or one of its links) with Up/Down, then Enter (or its › button) shows its properties and links instead of the search. From link to link, up to 4 levels, with a breadcrumb to go back; Esc goes back one level.
- **Renaming** a node in its details (F2 or ✎). A rename never overwrites: it lists the names it replaces, so a node renamed on two devices before they synced shows a conflict, with each name offered to keep.
- **Hotkey** Ctrl+Alt+K shows kofrenot from anywhere, changeable in Settings > General. Esc clears the input box, then hides the window.
- **Local snapshot**: each computer keeps the loaded vault in an encrypted cache, so it opens fast.
- **Update check** once a day against GitHub releases, switchable in About.
- **Installer and portable zip** (`build\installer.cmd`), self-contained.

[Unreleased]: https://github.com/dyze/kofrenot/compare/0.7...HEAD
[0.7]: https://github.com/dyze/kofrenot/compare/0.6...0.7
[0.6]: https://github.com/dyze/kofrenot/compare/0.5...0.6
[0.5]: https://github.com/dyze/kofrenot/compare/0.4...0.5
[0.4]: https://github.com/dyze/kofrenot/compare/0.3...0.4
[0.3]: https://github.com/dyze/kofrenot/compare/0.2...0.3
[0.2]: https://github.com/dyze/kofrenot/compare/0.1...0.2
[0.1]: https://github.com/dyze/kofrenot/releases/tag/0.1
