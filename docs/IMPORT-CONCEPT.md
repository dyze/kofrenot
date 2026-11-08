# kofrenot: import concept

**Import records** (a list of books, of contacts) from a file as nodes of one of the user's node types, through a **mapping** kept in the vault, and run again whenever the file changes: new records are added, nodes already there are updated. **JSON import is implemented** (Windows, unreleased: [CHANGELOG.md](../CHANGELOG.md#unreleased); how to use it: [README.md](../README.md#imports)); images, vCard and export are not ([Later](#later)).

```
                 ┌──────── Mapping node "Books (JSON)" ───────────┐
                 │ source: json · type: Book · match: ISBN        │
                 │ title_fr, else title → name                    │
                 │ title → Original title (an other name)         │
                 │ isbn → ISBN · year → Published                 │
                 │ authors[] → Person, linked by "wrote"          │
                 └───────────────────────┬────────────────────────┘
books.json ─► JsonRecordReader ─► records ─► ImportPlanner ─► preview ─► Import ─► changes
                                             (to check · new · updated · unchanged · not imported)
```

## Principles
- **A mapping is a node**, of the built-in type `mapping`, as a view is: its definition in versions (`editMapping`), so it syncs, has a history and is shared with the vault's members.
- **Run as often as needed.** A record whose match value (an ISBN) is already on a node of the type **updates** that node; the others are added.
- **Preview first.** Nothing is written before **Import**; **Cancel** writes nothing.
- **Only what exists is written** for the nodes: `addNode`, `renameNode`, `setField`, `addLink`.
- **Windows only.** Android shows a mapping as a node, without running or editing it.

## Mapping

| Part | Example | Note |
| --- | --- | --- |
| Source | `json` | the only one in 0.17 |
| Node type | *Book* | one of the user's types |
| Name | `title_fr`, else `title` | the keys giving the node's name, in order: the first present wins; a record with none is not imported |
| Match | *ISBN* | a field identifying a node; the names are always tried after it ([below](#finding-the-node)) |
| Other names | *Original title* | fields also holding a name of the node: compared with the names when finding the node |
| Field rules | `title` → *Original title*, `isbn` → *ISBN*, `year` → *Published*, `publisher.name` → *Publisher* | a key (a dotted path into nested objects) → a small field of the type; a key may give the name and a field |
| Link rules | `authors` → *Person*, linked by *wrote* (from the person) | each value a node's name: found among the type's nodes (`NameKey`), else added |

Sample `books.json`, for the mapping above:

```json
[
  { "isbn": "978-0441013593", "title_fr": "Dune", "title": "Dune", "year": 1965,
    "publisher": { "name": "Ace" }, "authors": ["Frank Herbert"] },
  { "isbn": "978-0441569595", "title_fr": "Neuromancien", "title": "Neuromancer",
    "year": "c. 1984", "authors": ["William Gibson"] },
  { "title_fr": "De bons présages", "title": "Good Omens", "year": 1990,
    "authors": ["Terry Pratchett", "Neil Gaiman"] },
  { "title": "The Dispossessed", "year": 1974, "authors": ["Ursula K. Le Guin"] },
  { "isbn": "978-0000000000", "year": 2001 }
]
```

| Record | Read as |
| --- | --- |
| Dune | all rules apply; `publisher.name` reads the nested object |
| Neuromancien | `year` does not fit a date: not imported, the rest is; no `publisher`: *Publisher* left as it is |
| De bons présages | no `isbn`: found by its names only; two authors: two *wrote* links |
| The Dispossessed | no `title_fr`: named by `title` (renamed when a later file gives the French one) |
| last | no `title_fr` nor `title`: not imported |

- **JSON shape:** the file is an array of objects (`[{…}, {…}]`). A key's value is text, a number or a boolean; an array gives several values (a link rule's names; a field takes the first, the others listed as not imported).
- **Values** are read as the field's kind (`FieldValue`): a date `1965-08-01`, `1965-08` or `1965`; a number; a url. A value that does not fit is not imported, and listed.
- **Not in 0.17:** image fields ([Later](#later)), several node types per mapping.
- At creation, rules are suggested by name: a key matching a field's name (case and accents ignored) goes to it.

## Finding the node

```
record { isbn: 978-0441569595, title_fr: "Neuromancien", title: "Neuromancer" }
   │
   ├─ 1. match value: a Book has ISBN 978-0441569595 ──────► that node (no doubt)
   │
   ├─ 2. else names: one of the record's names               (NameKey)
   │     "Neuromancien" · "Neuromancer"
   │     equals one of a Book's names
   │     its name · its Original title
   │        without an ISBN ─────────────────────────────► that node; it gets the ISBN
   │        with another ISBN ───────────────────────────► that node, to check in the preview
   │
   └─ 3. none ──────────────────────────────────────────► new node
```

- **The record's names:** its name (`title_fr`, else `title`) and its other names (`title`, read for *Original title*). **A Book's names:** its name and its *Original title*.
- So a Book first imported in English ("Neuromancer") is found by the French file through its original title, and renamed to "Neuromancien" (shown in the preview, can be unticked).
- A record without the match value goes straight to 2.
- **To check** (same name, another ISBN): the preview asks, per record, **Update** it (ISBN *old → new*, the default), **Add as new** (another edition) or **Skip**.
- Several nodes at step 1, or several candidates at step 2: not imported, listed as ambiguous.
- Two records of the file for one node (found, or new with the same name or match value): neither is imported, each listed as *same node as record n*.
- Without a match field, only step 2 runs.

## Updating existing nodes

```
record { isbn: 978-0441013593, title: "Dune", year: 1965, authors: ["Frank Herbert"] }
   ├─ no node found ──────────► new node: addNode, addNodeType, setField…, addLink…
   └─ Book "Dune" found ──────► updated: only what differs
                                 Published: 1966 → 1965      setField (replaces the current values)
                                 wrote: + Frank Herbert      addLink (when missing)
```

| Case | What happens |
| --- | --- |
| A value differs | `setField` replacing every current value (a conflict there is resolved); shown as *old → new* |
| A value is the same | nothing written |
| A key absent or empty in the record | the field is left as it is: an import never clears |
| A link missing | added; links already there are never removed |
| The name differs (found by match value or another name) | `renameNode`, shown in the preview |
| Several candidates | not imported: listed as ambiguous ([Finding the node](#finding-the-node)) |

Each updated node can be unticked in the preview, to leave it as it is.

## Vault format 12

**Compatible**: `oldestReader` 10, `oldestWriter` 11. A 0.16 app skips `editMapping` and shows the mapping as a node of an unknown type; it cannot drop what it does not see, the definition living in its own changes ([Compatibility levels](ARCHITECTURE.md#compatibility-levels)).

```json
{ "format": 12, "oldestReader": 10, "oldestWriter": 11, "at": "…", "op": "editMapping",
  "mapping": { "node": "<node id>", "source": "json", "nodeType": "<type id>", "name": ["title_fr", "title"], "match": "<field id> or null",
               "otherNames": ["<field id>"],
               "fields": [{ "key": "isbn", "field": "<field id>" }],
               "links": [{ "key": "authors", "nodeType": "<type id>", "linkType": "wrote", "reverse": true }],
               "replaces": ["<version id>", "…"] } }
```

Versions replace one another as a view's (`NodeVersions`); two made apart are a conflict, the newest shown.

## Screen

```
┌ kofrenot ───────────────────────────────── [⇅ Imports] [⚙] ─ □ ✕ ┐
                                                  │
┌ Imports ────────────────────────────────────────▼───────────────┐
│ Books (JSON)       Book · match ISBN       [Import…] [Edit…]    │
│ Films (JSON)       Film · match by name    [Import…] [Edit…]    │
│                                       [New…]          [Delete…] │
└─────────────────────────────────────────────────────────────────┘
```

- **Imports** sits in the top bar, beside **Settings**.
- **New… / Edit…**: the mapping's name, type, match, name keys (in order), other names and rules; a sample file shows the first records as they would be imported.
- **Import…**: a file picker, then the preview: *12 new · 3 updated · 1 to check · 40 unchanged · 1 not imported*, each updated node with what changes, each one to check with its choice.
- **Delete…** asks first (`MessageDialog.Confirm`), naming the mapping; the nodes it imported stay.

### Preview

```
┌ Import "Books (JSON)" · books.json ─────────────────────────────────┐
│ [1 to check] [12 new] [3 updated] [1 not imported] [40 unchanged]   │
├─────────────────────────────────────────────────────────────────────┤
│ ▼ To check                                                          │
│   Dune         same name, ISBN 978-0441172719 → 978-0441013593      │
│                (• Update) ( Add as new ) ( Skip )                   │
│ ▼ New                                                               │
│   [✓] Neuromancien     ISBN 978-0441569595 · Original title         │
│                        Neuromancer · wrote: + William Gibson (new)  │
│   [✓] …                                                             │
│ ▼ Updated                                                           │
│   [✓] Fondation        Published: 1950 → 1951                       │
│                        wrote: + Isaac Asimov                        │
│   [✓] Les Dépossédés   found by Original title "The Dispossessed"   │
│                        name: The Dispossessed → Les Dépossédés      │
│   [ ] Hypérion         name: Hyperion → Hypérion                    │
│ ▼ Not imported                                                      │
│       record 17        no "title_fr" nor "title"                    │
│       Solaris          2 Books with this name                       │
│ ▶ Unchanged (40)                                                    │
├─────────────────────────────────────────────────────────────────────┤
│ 1 value not imported: Neuromancien · year "c. 1984"                 │
│                                         [Import 15]   [Cancel]      │
└─────────────────────────────────────────────────────────────────────┘
```

| Part | How it works |
| --- | --- |
| Counts | one per group; clicking one scrolls to it |
| **To check** | same name, another match value ([Finding the node](#finding-the-node)): **Update** (default), **Add as new** or **Skip** |
| **New** | each node with its fields and links; a linked node that does not exist yet says *(new Person)*; untick to leave it out |
| **Updated** | only what differs: *old → new*, `+` for an added link, a rename; *found by …* when found through another name; untick to leave the node as it is |
| **Not imported** | with its reason: no name, ambiguous, twice in the file; nothing to choose |
| **Unchanged** | collapsed, for information |
| Values not imported | a value that does not fit its field, or a field's extra values: listed at the bottom, the rest of the record is imported |
| **Import *n*** | *n* = nodes that will be written (ticked new and updated, to check not skipped); writes them in one go, then a summary (*15 written*) |
| **Cancel** | closes, writes nothing |

The file is read and planned again on each **Import…**, so the preview always reflects the vault and the file as they are now.

## Code

The classes and how they work together: [ARCHITECTURE.md](ARCHITECTURE.md#imports). `System.Text.Json` reads the file: no new dependency.

## Later
- **Images** (a book's cover): a key giving the path of a local image file, relative to the JSON file (`"cover": "covers/dune.jpg"`) → an image field, written as a pasted image is (`FieldPlanner.SetImage`, `VaultSession.SaveImage`): no new vault format. On a re-run, the same SHA-256 as the node's image is unchanged. A missing file, not an image or over `DatasetType.MaxImageSize`: not imported, listed. URLs are not read: they would need the network and tell a site which books you have.
- **vCard** (`.vcf`, 3.0 and 4.0) as a second source: `FN`/`N` → name, `EMAIL`, `TEL`, `ADR`, `URL`, `ORG`, `NOTE`, `BDAY` (without a year: not imported), `PHOTO` → an image field; match on `UID` or the name. vCard 2.1 refused.
- **Export** with a mapping: the type's nodes → records → a `.json` file; the rules read backwards, a link rule giving the linked nodes' names.
- Android.
