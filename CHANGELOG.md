# Changelog

All notable changes to **Draft+** are documented here.

The format is loosely based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).

## [0.2.0] — 2026-05-12

### Added

- **Minus Strike & Defend** — optional modifier: when **Starter+**, **Starter Rewards**, or **Starter Shops** is on, **Basic Strike/Defend** are filtered out of those starter-related pools (including Basics from character pools in the same flows).
- **Starter+** — **Draft+** is now a **companion** to **Draft**, **Sealed Deck**, or **Insanity** (it no longer replaces vanilla Draft as a mutually exclusive mode). Tick **Starter+** with one of those trio modes so starter (**Basic**) cards can appear in that mode’s Neow card flow.

### Changed

- Neow pools, rewards, and shops share **Character Cards**–aware starter merging via **StarterDeckHelpers**; reward `CreateForReward` paths updated for uniform / sealed-style generation where needed.
- Custom mode modifier screen — Harmony on **`NCustomRunModifiersList.AfterModifiersChanged`** so **Starter+** / **Starter Rewards** / **Starter Shops** / **Minus** interact cleanly with the trio modes.
- **Starter Shops** in-mod description shortened; **README** uses **Custom mode** wording and states that Draft+ adds Custom mode modifiers.
- Manifest **`version`**: **v0.2.0**; description aligned with the feature set above.

### Fixed

- Harmony postfix parameter name for **`AfterModifiersChanged`** matches the game method (avoids “parameter not found” / patch skip on some builds).
- **Pandora’s Box** — with Draft / Sealed Deck / Insanity, Darv can offer it only when a player deck still has a **Strike** or **Defend** after the draft-related flow.

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
