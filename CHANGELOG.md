# Changelog

All notable changes to kofrenot are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/). Versions follow kofrenot's own `MAJOR.MINOR` scheme ([docs/GOAL.md](docs/GOAL.md#versioning)), not Semantic Versioning. Planned versions: [docs/ROADMAP.md](docs/ROADMAP.md).

## [Unreleased]

### Added
- **Application shell**, taken from coursee: a main window (placeholder content) that hides on close, a tray icon (Show, Settings, Log window, Quit), and a single running copy.
- **Settings**: Appearance (light, dark or follow Windows), Logging (debug logging, log window) and About (version, links, changelog, files), with a search box.
- **Logging**: local rolling file (3 x 1 MB) and a live log window.
- **Window positions** remembered per monitor.
- **Update check** once a day against GitHub releases, switchable in About.
- **Installer and portable zip** (`build\installer.cmd`), self-contained.

[Unreleased]: https://github.com/dyze/kofrenot/commits/main
