# kofrenot: vCard import concept

**Import contacts** from a vCard file (`.vcf`) as nodes of one of the user's node types, from time to time. Not implemented: planned ([ROADMAP.md](../ROADMAP.md)), Windows only.

```
contacts.vcf ─► VCardReader ─► VCardImportPlanner ◄── ImportRules (import-rules.json, this device)
                 (Core)          suggest / apply rules         ▲
                                     │                         │ saved on Import
                                     ▼                         │
                         VCardImportDialog (WPF) ──────────────┘
                         target type · rules · preview
                                     │ Import
                                     ▼
                  VaultSession.ImportContacts ─► addNode · addNodeType · setField (+ image files)
```

## Principles
- **An occasional import, not a sync.** Each import adds nodes; nothing ties them to the file afterwards (no source id kept, no re-import that updates).
- **No new vault format.** Only changes that exist today are written: `addNode`, `addNodeType`, `setField`, an image field's files.
- **Preview first.** Nothing is written before **Import**; **Cancel** writes nothing, not even the rules.
- **Rules remembered on this device**, never in the vault: the last rules per vault and target type, offered at the next import.

## Rules
A rule maps a vCard property (with an optional `TYPE`, `EMAIL;TYPE=work`) to a field of the target type. At the first import, rules are suggested by field kind; the user adjusts them in the dialog.

| Property | Goes to | Note |
| --- | --- | --- |
| `FN`, else `N` (given + family) | the node's name | a card without a name is skipped |
| `EMAIL` | email field | |
| `TEL` | phone field | |
| `ADR` | address field | its parts joined in one line |
| `URL` | url field | |
| `ORG` | text field | no organization node |
| `NOTE` | text field | |
| `BDAY` | date field | without a year (`--0812`) not imported: a date needs one ([FieldValue](ARCHITECTURE.md#types-and-fields)) |
| `PHOTO` | image field | base64 or `data:` URI, stored as an image field's file (`VaultSession.SaveImage`), up to `DatasetType.MaxImageSize` |
| others | ignored | |

- A property with several values (two `EMAIL`) fills the first matching rule only; the others are listed as not imported.
- Versions **3.0 and 4.0** are read (folded lines, parameters, escaping). A **2.1** file (quoted-printable, charsets) is refused with a message.

## Preview

```
┌ Import contacts: contacts.vcf ──────────────────────────────────┐
│ Type: [Person ▾]                                                │
│ Rules:  EMAIL → Email · TEL → Phone · ORG → Company · …         │
├─────────────────────────────────────────────────────────────────┤
│ ✓ Jane Doe        jane@…  +33 6…  Acme   📷                      │
│ ⚠ John Smith      already exists     ( skip ▾ | add anyway )     │
│ ✓ Ana Lopez       BDAY --0812 not imported (no year)            │
│ ✗ Card 14         no name: skipped                              │
├─────────────────────────────────────────────────────────────────┤
│ 42 new · 3 existing (skip) · 1 skipped       [Cancel] [Import]  │
└─────────────────────────────────────────────────────────────────┘
```

- Changing the type or a rule recomputes the preview in memory.
- **Already exists**: a node of the target type with the same name, as duplicates are found today (`NameKey`); skipped by default, or added anyway (then marked as a duplicate, as any).

## Code

| Where | New | Role |
| --- | --- | --- |
| Core `Contacts/` | `VCardReader` | `.vcf` text → `VCard` records (properties, parameters, values) |
| | `ImportRule` | a property (and `TYPE`) → a field id of the target type |
| | `ImportRules` | the last rules per vault and target type, through `LocalJsonFile` (`import-rules.json`) |
| | `VCardImportPlanner` | suggests rules by field kind, applies them → `ContactImportPlan`: new nodes, values, photos, existing names, what is not imported |
| Core `Session/` | `VaultSession.ImportContacts` | writes the plan's changes, then each photo off the UI thread |
| App `Views/` | `VCardImportDialog` | **Import contacts…**: file picker, type, rules, preview |

Tests: real exports (Google, Android, Outlook) for the reader; planner, rules file and session in xUnit, Core coverage ≥ 95%.

## Not planned
- Re-import that updates the nodes it made (needs a source id: the vCard `UID`, kept as a link qualifier).
- An organization node linked from `ORG`.
- vCard 2.1, export to vCard, Android.
