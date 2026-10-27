# CLAUDE.md

Rules for working on **kofrenot** together. Read first:
- `docs/GOAL.md`: goal, principles, non-goals, open questions
- `ROADMAP.md`: scope of each planned version
- `CHANGELOG.md`: what each version changed
- `docs/ARCHITECTURE.md`: projects, stack, logging, window positions, updates

## Project in one line
A private, self-hosted knowledge system: store, organize, search, version and share notes and files securely across devices, with metadata fully usable offline and content fetched on demand.

## Architecture rules (keep it this way)
- `kofrenot.App` (WPF) holds the UI and all Windows-specific code. `kofrenot.Core` has no Windows dependency.
- Windows access goes behind an interface defined in `Core` and implemented in `App`.
- All domain logic (notes, files, metadata, search, versioning, sharing, sync) lives in `Core`, so it can be reused by a later server or another client.
- Metadata is always available locally; content may be fetched on demand. Do not design anything that needs the network to browse or search metadata.
- **Vault format.** `VaultFormat.Current` versions what is written in a vault (header, change files, encryption); a kofrenot reads formats up to its own and asks to be upgraded beyond ([ARCHITECTURE.md](docs/ARCHITECTURE.md#format)). Before changing code that makes a breaking change to the vault format (something an older kofrenot would misread or not read), tell the owner, and wait for the go-ahead: the change raises `VaultFormat.Current`.
- Storage, sync, conflicts and encryption are decided (`docs/GOAL.md#decisions`): ask before changing them. The rest (`docs/GOAL.md#open-questions`) is open: ask before deciding one.

## Dependencies
Only those listed in `docs/ARCHITECTURE.md#stack`. Ask before adding one.

## Logging rules
- `Core` writes through `ILog`; only `App` knows Serilog. Classes in `Core` take an optional `ILog`.
- Every user-triggered action at **Info**, failures at Warning/Error, details at **Debug**.
- Never log note or file content, search text typed by the user, keys, passwords or other secrets.
- Logs stay local: 3 files x 1 MB, plus 1000 entries in memory.

## Versioning rules
- Format and policy: `docs/GOAL.md#versioning`.
- `<Version>` in `Directory.Build.props` is the only place the version is written: not in code, docs or scripts.
- Bump it only when the owner asks. Between releases it ends in `-dev` (`0.10-dev`): a release commit drops `-dev`, the next commit sets `<next>-dev`.

## Repo layout
`src/`, `tests/`, `docs/`, `build/`, `lib/`, `packages/`, `samples/`, `.config/` (dotnet tools), `artifacts/` (ignored). Do not add top-level folders without asking.

## How we work
- **Concise answers.** Short explanations, diagrams over long text.
- **Plan first.** Before changing any code, present an implementation plan for the developer to review. Change code only after the developer explicitly says to implement it.
- **Do it well.** Prefer the clean, simple solution over the quick hack.
- **Ask before** adding a dependency, changing the architecture, doing a large refactor, or touching anything listed as a non-goal.
- **Tests.** Every change in `Core` comes with xUnit tests.
- **Build honesty.** Claude builds and runs the tests itself (the .NET SDK is installed) and reports the real result, errors and coverage included. Never claim the code builds without having run it.
- **Docs.** One home per fact:
  - user-facing behavior → `README.md`
  - version scope → `ROADMAP.md` (planned features)
  - changes → `CHANGELOG.md`, following [Keep a Changelog](https://keepachangelog.com/en/1.1.0/): when a feature is implemented, it moves from ROADMAP to `[Unreleased]`, under Added, Changed, Deprecated, Removed, Fixed or Security; at release, `[Unreleased]` becomes `[<version>] - <date>`, most recent version first, with its compare link at the bottom
  - design and internals → `docs/ARCHITECTURE.md`
  - goal and principles → `docs/GOAL.md`
  Prefer diagrams and tables to long descriptions.
  Link between them rather than copying. Update them when behavior changes.
- **Don't touch** `LICENSE`.

## Design rules

* Be concise.
* Follow Clean Architecture principles.
* Avoid redundancies in explanations, try to update existing docs instead of creating new ones.
* Ask clarifying questions before making architectural changes.
* Every delete or remove asks for confirmation first (`MessageDialog.Confirm`), naming what goes.
* Mark in documents what is implemented and what is not. In `ROADMAP.md`, only the version table holds a status: no "Done" or "Not implemented" on features.

## Coding rules

* Apply Clean Code principles. e.g. prioritize meaningful variable names rather than long comments.
* Writing Code for Today's Problems, Not Tomorrow's Fantasies.
* Remove dead code.
* Avoid long modules. Move large, monolithic classes or methods into separate `.cs` files or helper classes to keep individual files concise and maintainable.
* Avoid redundant code.
* Validate with a build and test run (see Build honesty).
* Maintain Core library sub-systems code coverage target of **>=95%** (line coverage, enforced by `build\windows.cmd`).
* Reduce build warnings.

## Git
- Commit and push only when asked.
- Create branches only when asked. Otherwise work on `main`.
- Small commits, [Conventional Commits](https://www.conventionalcommits.org/en) with an imperative subject (`feat: add note tags`).
- No AI attribution in commit messages or PR descriptions (no `Co-Authored-By: Claude …`, no "Generated with Claude Code").

## Definition of done
Code is written, tests cover the new `Core` behavior, docs are updated, and the build and tests pass (`build\windows.cmd`).
