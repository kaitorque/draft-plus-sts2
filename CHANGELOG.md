# Changelog

All notable changes to **Draft+** are documented here.

The format is loosely based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).

## [0.1.0] — 2026-05-11

### Added

- **Draft+** — Custom run modifier (mutually exclusive with vanilla Draft): Neow draft includes merged starter decks and optional **Character Cards** starters; Pandora’s Box handling after the draft when no Strike/Defend-style cards remain.
- **Starter Rewards** — Combat encounter rewards can include starter (**Basic**) cards; Character Cards starters merged when applicable.
- **Starter Shops** — Merchant offers can include starter Basics on **Common** rolls (same starter merge as Draft+ / Starter Rewards).
- **StarterDeckHelpers** — Shared Character Cards–aware starter deck merge for the modifiers above.
- **MIT License**; `README.md` and this changelog.
- Manifest **`DraftPlus`** → install folder `mods/DraftPlus/` (`DraftPlus.dll`, `DraftPlus.json`); BaseLib dependency object form; `has_pck: false`; `min_game_version` as shipped.

### Fixed

- Draft+ Pandora check: detect Strike/Defend via `Deck.Cards` after the draft.

### Notes

- Modifier titles/descriptions use BaseLib **`ILocalizationProvider`** (no `.pck` required for those strings).
- If you still have an old **`DraftPlusMod`** folder from a prior fork name, remove it and use **`mods/DraftPlus/`** only.
