# kofrenot: qualifiers concept

**Qualifiers** add context to a link: date, place, source, quantity, confidence. They come with **compatibility levels**, so that an older kofrenot still opens a vault with qualifiers, read-only, instead of refusing it. The compatibility levels are implemented (0.15, [Format](ARCHITECTURE.md#compatibility-levels)), and qualifiers (0.16, [CHANGELOG](../CHANGELOG.md#unreleased), [README](../README.md#node-types-and-fields)); events are planned ([ROADMAP.md](../ROADMAP.md)). Events, the other half of the roadmap item, are left for later ([Events](#events-later)).

```
Dune ──written by──► Frank Herbert
        ├ date: 1965
        └ source: ISFDB
```

## Principles
- **A qualifier is a field on a link.** It has the same kinds, editors, conflicts, history and offline search as a node's fields ([Node types and fields](../README.md#node-types-and-fields)).
- **It hangs on the link's id, not on a reading of it.** *Dune written by Herbert* and *Herbert wrote Dune* reach the same qualifiers.
- **The link type defines its qualifiers**, as a node type defines its fields, a built-in one too (its names kept). A link can also have qualifiers of its own.
- **Same paths as today.** Qualifiers are typed in the input box, edited in the details panel, and found by search. There is no new screen to learn.
- **Older apps degrade, not refuse.** A kofrenot that does not know qualifiers shows links without them, read-only ([Compatibility levels](#compatibility-levels)).

## Releases

| Version | Brings | Vault format |
| --- | --- | --- |
| 0.15 | Compatibility levels: the reading rules only (no user feature) | 10, unchanged |
| 0.16 | Qualifiers | 11: `oldestReader` 10, `oldestWriter` 11 |

The rules must ship one version **before** the first format that uses them, so that the version before can already read it. With this order, 0.15 opens a format 11 vault read-only, and 0.14 and older refuse it (as they refuse any newer format today).

## Compatibility levels
Today, one number (`format`) decides everything: a newer one closes the vault. From 0.15, two more numbers say what an older kofrenot may still do:

| Number | Where | Meaning |
| --- | --- | --- |
| `format` | header, each change file | the format it was written in (as today) |
| `oldestReader` | header, each change file | the oldest format that can read it, skipping what it does not know |
| `oldestWriter` | header, each change file | the oldest format that can write to the vault |

Missing (written before 0.15): `oldestReader` = `oldestWriter` = `format`, so nothing changes for existing vaults. `oldestWriter` is in each change file too: the header is written once, so the first change of format 11 is what raises the vault (`Vault.OldestWriter` is the highest of all its files).

```
this kofrenot's format F, opening a vault:

oldestReader > F  ─► refuse, as today ("Update kofrenot")              incompatible
oldestWriter > F  ─► open READ-ONLY: "Changed by a newer kofrenot:   read-only compatible
                      some details are hidden. Update to change it."
otherwise         ─► open, reading and writing                          compatible

a change file arriving (sync, refresh):
oldestReader > F  ─► close the vault, as today
file.format > F   ─► read it with the skip rules; the vault turns read-only if its oldestWriter > F
```

### Skip rules
A reader whose `format` is older than a file's, but at least its `oldestReader`:

| Found | Does |
| --- | --- |
| an unknown `op` | skips the change |
| a known `op` whose target it does not know (`setField` with `link` instead of `node`) | skips the change |
| an unknown property | ignores it |
| an unknown field kind | keeps the field as `FieldKind.Unknown`, its value shown as read-only text, never dropped (the format 9 trap) |
| any of these in a change of its own format or older | the file is damaged, as before |

- **Skipped files are not kept in the snapshot** ([Local snapshot](ARCHITECTURE.md#local-snapshot)), and not read again until kofrenot is updated.
- **The snapshot keeps the format of the kofrenot that saved it.** A change it read with the skip rules lost what it did not know (a `defineLinkType`'s `fields`), so a kofrenot of another format does not use a snapshot holding changes newer than its reader: it reads every change again, whole.
- **Logging:** the count of changes skipped at Debug, their ids never at Info.

### Why read-only, not full access
Writes are append-only, so an older app cannot break a file. It can still lose what it does not see: renaming a link type writes a `defineLinkType` without `fields`, which replaces the definition and drops its qualifiers. Read-only avoids that whole class of bug, at the cost of nothing a user would miss for long: they update kofrenot to edit.

- In read-only mode, every write is refused in `Core`, by the vault itself (`Vault.IsReadOnly`, `Vault.EnsureCanWrite`), not only in the screens: pins, colours and graph positions too. `VaultSession.IsReadOnly` covers it with a reader's role, so the screens already hiding a reader's changes hide these too.
- Sync keeps downloading, so the vault stays current.

### Deciding a future format's level
Each new format states its `oldestReader` and `oldestWriter` in the format table ([Format](ARCHITECTURE.md#format)), with the reason:

| Level | `oldestReader` | `oldestWriter` | When |
| --- | --- | --- | --- |
| compatible | older | older | the addition can be ignored, and an older writer cannot overwrite it |
| read-only compatible | older | new | it can be ignored when reading, but an older writer could drop it |
| incompatible | new | new | ignoring it would show something wrong: keys, encryption, removals |

Each level comes with a test: a vault written by the new format, read by the reader of the old one, shows what it should.

## Data

### Definitions: `defineLinkType` + `fields`
```json
{ "format": 11, "oldestReader": 10, "at": "…", "op": "defineLinkType",
  "linkType": { "id": "…", "name": "wrote", "reverse": "written by",
                "fields": [{ "id": "…", "name": "Date", "kind": "date" },
                           { "id": "…", "name": "Source", "kind": "web" }],
                "replaces": ["…"] } }
```
- The field kinds are the node types' kinds, **image** included (a cover, a scan of the source).
- A built-in link type (*relates to*, *contains*, *is a*) is given fields by a `defineLinkType` of its id: its names are written, but its own are kept. A 0.15 reader ignores it, as any definition of a built-in id.
- A field taken out of a link type stays on its links with its value, as their own, as with node types.
- A 0.15 reader ignores `fields`, and keeps the type's name.

### Values: `setField` on a link
```json
{ "format": 11, "oldestReader": 10, "at": "…", "op": "setField",
  "field": { "link": "<link id>", "owner": "<link type id> or own", "id": "<field id>",
             "name": "Date", "kind": "date", "value": "1965", "replaces": ["<value id>"] } }
```
- `link` instead of `node`: exactly one of the two. The values of a link and of its copies (the same link added on two devices before they synced) are merged: set on both, they are a conflict. Everything else is `setField` as it is: `replaces` for history and conflicts, an empty value to clear.
- A link removed (`remove`) hides its qualifiers, as a node's fields; nothing is deleted.
- A 0.15 reader skips it (known `op`, unknown target).

### Separate links: `addLink` + `separate`
```json
{ "format": 11, "oldestReader": 10, "op": "addLink",
  "link": { "id": "…", "type": "married-to", "from": "…", "to": "…", "created": "…", "separate": true } }
```
- Links of one type between the same nodes are copies of one link, the oldest shown. A link added with **+ a new link** ([Several links](#several-links-of-a-type-between-the-same-two-nodes)) is `separate`: no copy, shown beside the others, with qualifiers of its own.
- A 0.15 reader ignores `separate`: it shows the oldest of them, read only.

### Format 11 header
```json
{ "format": 11, "oldestReader": 10, "oldestWriter": 11, "id": "…" }
```

### Core
| Today | Becomes |
| --- | --- |
| `FieldValues` keyed by node | keyed by a **field holder**: a node or a link (`FieldHolder`) |
| `CustomLinkType` | + `Fields` (`FieldDefinition`s), shared with `CustomNodeType` |
| `FieldPlanner` (a node's fields) | the same planner for a link's (`FieldPlanner.ForLink`) |
| `KnowledgeGraph` search on node values | + link values ([Search](#search)) |
| `VaultFormat.Current` | 11, its files written with `oldestReader` 10, `oldestWriter` 11 (`VaultHeader`, `ChangeData` read them since 0.15) |
| `LinkIndex`: copies keyed by type and nodes | + the link's id when `Link.IsSeparate` (`KnowledgeGraph.LinksBetween`) |

## Adding qualifiers

### Input box
```
[Dune] [written by] [Frank Herbert], [Date] 1965, [Source] isfdb.org▏
 node    link         node            qualifier     qualifier
```
- After a complete link sentence, `,` followed by a qualifier name of that link type (or its first letters) **forms a label**, as link phrases do today: whole words, proposed in grey, **Tab** accepts.
- A `,` not followed by a qualifier name stays in the name. So *Herbert, Frank* is still a name, and the planned *Quote a name* item covers the rest.
- **Enter**: the creation screen, the qualifiers filled in.
- Android: the same in the search box; **Enter** or a tap on the proposal accepts.

### Creation screen
Once the link type is chosen, its qualifiers show on the link step, under it, all optional (not a step of their own: the link type chosen there changes them). With no qualifier defined, there are none. On a link updated, each says its value now; an empty box leaves it as it is.

### Settings > Link types (Windows)
Each type gets a field list, edited as a node type's (`NodeTypeForm`). Android leaves it to Windows: it defines the vault ([What the phone changes](ANDROID-CONCEPT.md#what-the-phone-changes)).

## Changing them afterwards

### Input box: the sentence again
`AlreadyLinked` no longer ends there: the plan becomes an **update of that link**.

```
[Dune] [written by] [Frank Herbert], [Date] 1966
        ▼
Link exists ─► "Date: 1965 → 1966"  [Update]
```
- A qualifier absent is added, one different shows *old → new*, all confirmed in one step.
- `, Date` with no value removes it, asked first, naming what goes.
- Either reading works: *Herbert wrote Dune, Date 1966*.

### Several links of a type between the same two nodes
Possible with a separate link (`addLink` + `separate`). The sentence is then ambiguous, so the input box asks which one, by its shown qualifier, as soon as qualifiers are typed for nodes linked so already (one link included, so a second can be added):

```
Ann married to Bob, End 2010
  ▸ married to Bob · 1995
  ▸ married to Bob · 2015
  + a new link
```

### Details panel and graph
- Each link row gets a chevron at its right, beside its other buttons, which unfolds its qualifiers in the fields' editor: **Enter** or leaving saves, **Esc** reverts, empty clears, **Add qualifier…** adds one of its own, **×** removes (asked first).
- Android: a tap on a link with qualifiers (its own, or its type's) opens the same sheet, with *Open* its other node first; another opens its node, as before. A long press still removes the link, and adds one of its own. The phone updates the first of several links (no choice).
- Graph view: a click on an edge opens it.

### History and conflicts
Each qualifier version is listed and restorable in the link's sheet, as a node's ([History](GOAL.md#decisions)). Values set on two devices before they synced show both, to keep one.

## Reading them

### Shown
- In link rows: `written by Frank Herbert · 1965`, the first qualifier of the link type's list.
- Graph edges: beside the link name when **Link names** is on.

### Search
Same sentence shape, offline as every field:

| Typed | Finds |
| --- | --- |
| `written by, Date 1965` | links of that type with that value |
| `Frank Herbert wrote, Date 1960..1970` | his links in that range, so the books |
| `, Source isfdb` | any link with that value |

The `..` range is for dates and numbers, node fields' too (`Born 1900..1950`). A date's end is a year, a month or a day, included whole.

### Views
A tree or list can sort a node's children by a qualifier of the link (books by date). The view definition gains an optional `order` (`{ "field": "<field id>" }`), written in format 11.

## Events (later)
An event links several nodes, each in a role: *Publication of Dune (work: Dune, publisher: Chilton Books)*. The planned shape needs **no further format**:
- An event is a node of a node type (*Publication*), its plain values are fields (*Date*).
- Its roles are link types (*work*, *publisher*), which may carry qualifiers.
- **Turn into event…** on a link would replace it with an event node, carrying its qualifiers, when it needs a third participant. Duplicate links of a type between two nodes are a hint that they should be events.

## Decisions
Taken with the owner for 0.16:

| Topic | Decision |
| --- | --- |
| Separator | `,` before a qualifier's name |
| Ranges | `..` for node fields too, in the same version |
| Image qualifiers | Included, as a node type's image field |
| Shown qualifier | The first with a value, in the type's list first; no mark |
| Several links of a type between two nodes | Now: `addLink` with `separate` ([Separate links](#separate-links-addlink--separate)) |
| Built-in link types | Their qualifiers are defined too, in Settings > Link types ([Definitions](#definitions-definelinktype--fields)) |
| Scope | All of this concept in 0.16, events excepted |
