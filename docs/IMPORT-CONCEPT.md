# kofrenot: import concept

**Import records** (a list of books, of contacts) from a file as nodes of one of the user's node types, through a **mapping** kept in the vault, and run again whenever the file changes: new records are added, nodes already there are updated. Not implemented: planned, Windows only. JSON import comes first (0.17, [ROADMAP.md](../ROADMAP.md)); vCard and export later ([Later](#later)).

```
                 ┌──────── Mapping node "Books (JSON)" ───────────┐
                 │ source: json · type: Book · match: ISBN        │
                 │ title → name · isbn → ISBN · year → Published  │
                 │ authors[] → Person, linked by "wrote"          │
                 └───────────────────────┬────────────────────────┘
books.json ─► JsonRecordReader ─► records ─► ImportPlanner ─► preview ─► Import ─► changes
                                             (new · updated · unchanged · not imported)
```

## Principles
- **A mapping is a node**, of the built-in type `mapping`, as a view is: its definition in versions (`editMapping`), so it syncs, has a history and is shared with the vault's members.
- **Run as often as needed.** A record whose match value (an ISBN) is already on a node of the type **updates** that node; the others are added.
- **Preview first.** Nothing is written before **Import**; **Cancel** writes nothing.
- **Only what exists is written** for the nodes: `addNode`, `addNodeType`, `setField`, `addLink`.
- **Windows only.** Android shows a mapping as a node, without running or editing it.

## Mapping

| Part | Example | Note |
| --- | --- | --- |
| Source | `json` | the only one in 0.17 |
| Node type | *Book* | one of the user's types |
| Name | `title` | the key giving the node's name; a record without it is not imported |
| Match | *ISBN*, or the name | how a record finds its node (below) |
| Field rules | `isbn` → *ISBN*, `year` → *Published*, `publisher.name` → *Publisher* | a key (a dotted path into nested objects) → a small field of the type |
| Link rules | `authors` → *Person*, linked by *wrote* (from the person) | each value a node's name: found among the type's nodes (`NameKey`), else added |

- **JSON shape:** the file is an array of objects (`[{…}, {…}]`). A key's value is text, a number or a boolean; an array gives several values (a link rule's names; a field takes the first, the others listed as not imported).
- **Values** are read as the field's kind (`FieldValue`): a date `1965-08-01`, `1965-08` or `1965`; a number; a url. A value that does not fit is not imported, and listed.
- **Not in 0.17:** image fields (a JSON file holds no picture; a URL would need the network), several node types per mapping.
- At creation, rules are suggested by name: a key matching a field's name (case and accents ignored) goes to it.

## Updating existing nodes

```
record { isbn: 978-0441013593, title: "Dune", year: 1965, authors: ["Frank Herbert"] }
   │ match: ISBN
   ├─ no Book has it ─────────► new node: addNode, addNodeType, setField…, addLink…
   └─ Book "Dune" has it ─────► updated: only what differs
                                 Published: 1966 → 1965      setField (replaces the current values)
                                 wrote: + Frank Herbert      addLink (when missing)
```

| Case | What happens |
| --- | --- |
| A value differs | `setField` replacing every current value (a conflict there is resolved); shown as *old → new* |
| A value is the same | nothing written |
| A key absent or empty in the record | the field is left as it is: an import never clears |
| A link missing | added; links already there are never removed |
| The name differs | `renameNode`, shown in the preview |
| Several nodes have the match value | not imported: listed as ambiguous |
| Match by name | the first node of the type with that name (`NameKey`); homonyms are ambiguous |

Each updated node can be unticked in the preview, to leave it as it is.

## Vault format 12

**Compatible**: `oldestReader` 10, `oldestWriter` 11. A 0.16 app skips `editMapping` and shows the mapping as a node of an unknown type; it cannot drop what it does not see, the definition living in its own changes ([Compatibility levels](ARCHITECTURE.md#compatibility-levels)).

```json
{ "format": 12, "oldestReader": 10, "oldestWriter": 11, "at": "…", "op": "editMapping",
  "mapping": { "node": "<node id>", "source": "json", "nodeType": "<type id>", "name": "title", "match": "<field id> or name",
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
│ Films (JSON)       Film · match name       [Import…] [Edit…]    │
│                                       [New…]          [Delete…] │
└─────────────────────────────────────────────────────────────────┘
```

- **Imports** sits in the top bar, beside **Settings**.
- **New… / Edit…**: the mapping's name, type, match, name key and rules; a sample file shows the first records as they would be imported.
- **Import…**: a file picker, then the preview: *12 new · 3 updated · 40 unchanged · 1 not imported*, each updated node with what changes.
- **Delete…** asks first (`MessageDialog.Confirm`), naming the mapping; the nodes it imported stay.

## Code

| Where | New | Role |
| --- | --- | --- |
| Core `Graph/` | `TypeCatalog.Mapping`, `MappingEdited` | the built-in type and its change |
| Core `Imports/` | `MappingDefinition` | source, type, name key, match, field and link rules |
| | `JsonRecordReader` | a `.json` file → `Record`s: keys (dotted paths) → values |
| | `ImportPlanner` | records + mapping + graph → `ImportPlan`: new, updated (with their differences), unchanged, not imported |
| | `MappingPlanner` | loads and saves a mapping's versions, as `ViewPlanner` |
| Core `Session/` | `VaultSession.ImportRecords` | writes the plan's changes |
| App `Views/` | `ImportsWindow`, `MappingDialog`, `RecordImportDialog` | the list, the editor, the preview |

`System.Text.Json` reads the file: no new dependency. Tests: reader, planner (new, updated, ambiguous, values that do not fit), mapping versions, the format 12 change read by a format 11 reader; Core coverage ≥ 95%.

## Later
- **vCard** (`.vcf`, 3.0 and 4.0) as a second source: `FN`/`N` → name, `EMAIL`, `TEL`, `ADR`, `URL`, `ORG`, `NOTE`, `BDAY` (without a year: not imported), `PHOTO` → an image field; match on `UID` or the name. vCard 2.1 refused.
- **Export** with a mapping: the type's nodes → records → a `.json` file; the rules read backwards, a link rule giving the linked nodes' names.
- Android.
