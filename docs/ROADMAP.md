# kofrenot: roadmap

Scope of each planned version. Implemented features move to [CHANGELOG.md](../CHANGELOG.md). The goal, principles, non-goals and open questions are in [GOAL.md](GOAL.md). The features are sorted by priority.

| Version | Status      | Theme                                                                              |
| ------- | ----------- | ---------------------------------------------------------------------------------- |
| 0.1     | In progress | Application shell; nodes and links typed in one box, stored in an encrypted folder |
| 0.2     | To define   | Notes as nodes                                                                     |

## 0.1
Implemented: see `[Unreleased]` in [CHANGELOG.md](../CHANGELOG.md). Left: the feature below, then the owner's test of the whole version.

1. **Node details.** Browse a node and its links from the search, inside the main window.

**Search.** Results are listed as today. **Down** selects a node row, **Up/Down** move the selection, and typing clears it. **Enter** on a selected row, or its **›** button (on every node row), opens the node's details. With no row selected, Enter opens the creation screen as today. While a link is typed, Up/Down keep choosing among homonyms, so no row can be selected with the keys; the **›** buttons still work.

```
┌ kofrenot ─────────────────────────────────────── ⚙ ┐
│ [ dune                                           ] │
│ “dune” exists already. Enter to add another one.   │
│ › Dune                                         [›] │  ← selected: Enter opens its details
│     relates to Frank Herbert                       │
│   Dune Messiah                                 [›] │
└────────────────────────────────────────────────────┘
```

**Details.** They replace the search area. They show the node's properties (name, with its `¹` `²` mark among homonyms; type; creation date), then its links, oldest first: the link type and the other node.

```
┌ kofrenot ─────────────────────────────────────── ⚙ ┐
│ ⌂ Search › Dune                                    │  ← breadcrumb: click an item to go back to it
│                                                    │
│ Dune                                               │
│ Type      Generic                                  │
│ Created   2026-10-16 09:12                         │
│                                                    │
│ Links (2)                                          │
│ › relates to   Frank Herbert                   [›] │  ← Up/Down select, Enter or › opens it
│   relates to   Arrakis                         [›] │
└────────────────────────────────────────────────────┘
```

**Going deeper.** A link's other node opens one level deeper: at most **4 levels** below the search. At the last level, and for a node already in the breadcrumb, the row is greyed and has no **›**: the breadcrumb is the way back to it.

```
│ ⌂ Search › Dune › Frank Herbert › Arrakis › Fremen │  ← level 4: the limit
│ Links (2)                                          │
│   relates to   Arrakis                             │  ← already in the breadcrumb: greyed
│   relates to   Stilgar                             │  ← level 4: no deeper
```

**Back.** **Esc** goes up one level. From level 1, it goes back to the search box with its text and selection kept. **⌂ Search**, the breadcrumb's first item, goes there directly. The details follow changes made elsewhere: a node or link added on another device shows up without reopening.

Not in 0.1: renaming, deleting, editing properties, user-defined types.

## 0.2 (to define)
1. **Notes** as a node type, with content.
2. **Collections** as a node type. Used to order nodes at top level. By default, a collection is created and all new nodes are attached to it. Current collection is shown at top of Main Window.
3. **Containers as node type.** Used to order nodes in a tree. Useful for datasets. By default, all new nodes are attached at root.
4. **Datasets** as node type
	- Load/Save content
5. Link types have **aliases**. e.g "relates to" is equivalent to "relates" or simply "-".
	- A screen is available and allow user to display and edit those aliases.
6. Link types have **reverse** versions. "relates to" is the reverse of "related to"

## 0.3 (to define)
1. **Multiple vaults.** User can register several vaults on his app. Current vault is selected using a combo in main window. Selection is memorized. Each vault has his passphrase.
2. **Dedicated hot keys.** User can assign a hot key per vault. e.g. CTRL ALT I for private vault and CTRL ALT P for personal one.
3. **Web server.** Vaults are stored on a dedicated web server. See dedicated concept  [SERVER-CONCEPT.md](SERVER-CONCEPT.md).