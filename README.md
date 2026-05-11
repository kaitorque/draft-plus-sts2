# Draft+ (Slay the Spire 2)

Custom run modifiers that let **starter (Basic) cards** show up where vanilla Draft and combat rewards normally exclude them.

**Source:** [github.com/kaitorque/draft-plus-sts2](https://github.com/kaitorque/draft-plus-sts2)

Requires **[BaseLib](https://github.com/Alchyr/BaseLib-StS2)** in your game `mods` folder.

---

## Features

### Draft+ (`DraftPlus`)

- Appears in **Custom Run** as its own modifier (mutually exclusive with vanilla **Draft**).
- Clears your deck and runs the same **10 × pick 1 of 3** Neow draft as Draft.
- Draft pool = your character card pool **plus** starter decks:
  - Your character’s **StartingDeck**
  - If you use **Character Cards**, each enabled extra character’s **StartingDeck** is merged in too (so their starters can appear in the draft).
- **Pandora’s Box**: removed from relic grab bags **after** the draft **unless** any player ended with a card whose id looks like **Strike** or **Defend** (substring match on `ModelId.Entry`). If you drafted strikes/defends, Pandora’s Box can show up later (e.g. Darv) like a less restricted Draft run.

### Starter Rewards (`StarterRewards`)

- Optional Custom Run modifier.
- When enabled, **combat card rewards** (`Encounter` source, non-uniform rarity path) can pull from **starter cards** as well as the usual pool:
  - Starters are merged into the candidate pool; when the rolled rarity is **Common**, **Basic** starters are included in the pick list (vanilla never rolls “Basic” as a rarity).
- Does **not** change shops, events, or uniform pools by design.
- Respects **Character Cards** the same way as Draft+ (extra characters’ starters included).

### Starter Shops (`StarterShop`)

- Optional Custom Run modifier (**Starter Shops** in the UI).
- Uses the same merged starter decks as **Draft+** / **Starter Rewards** (your character plus **Character Cards** extras; see `StarterDeckHelpers`).
- Vanilla shops strip all **Basic** cards before rarity rolls, which hides starter Strikes/Defends. With this on, **Basic** cards from that merged starter set are kept and can show up when the slot rolls **Common** (other Basics stay excluded); same high‑level idea as **Starter Rewards**.

---

## Requirements

| Requirement | Notes |
|-------------|--------|
| **Slay the Spire 2** | Early Access; game updates can break mods. This repo targets at least **`min_game_version`** in `DraftPlus.json`. |
| **BaseLib** | Install release matching manifest `dependencies` into `…/Slay the Spire 2/mods/BaseLib/`. |
| **.NET SDK** | For building from source (same major as project `TargetFramework`, currently `net9.0`). |

Optional for **publish / `.pck`** (assets): Megadot / Godot version pinned in `Directory.Build.props` — see [ModTemplate Setup](https://github.com/Alchyr/ModTemplate-StS2/wiki/Setup).

---

## Install (players)

1. Install **BaseLib** into `mods/` if you do not already have it.
2. Copy this mod’s folder **`DraftPlus`** (containing `DraftPlus.dll`, `DraftPlus.json`, and optionally `DraftPlus.pck` if you use assets) into:

   `…/Slay the Spire 2/mods/DraftPlus/`

3. Launch the game → **Settings → Mod Settings** → enable **Draft+** (`DraftPlus`). Restart if the game asks.

Manifest notes:

- **`has_pck`**: this shipping layout uses **`false`** — no `.pck` required unless you add Godot assets and publish.
- **`dependencies`**: must use the **object** form `[{"id":"BaseLib","min_version":"…"}]` on current beta branches (old string-array format triggers a migration warning).

---

## Build (developers)

From the repository root:

```powershell
dotnet build .\DraftPlus.sln -c Debug
```

Each successful **`dotnet build`** runs a post-build step that **automatically copies** `DraftPlus.dll`, `DraftPlus.pdb`, and `DraftPlus.json` into the game’s `mods/DraftPlus/` folder when your StS2 install path resolves (see `Sts2PathDiscovery.props`; optional `Directory.Build.props` can set **`Sts2Path`** / **`GodotPath`** if defaults miss your setup).

- **Code-only changes**: `dotnet build` is enough.
- **Localization / images / scenes in `.pck`**: use **Publish** per the [Setup wiki](https://github.com/Alchyr/ModTemplate-StS2/wiki/Setup) and set `has_pck` / ship the `.pck` accordingly.

Close the game before building if Windows locks `DraftPlus.dll`.

---

## Project layout

| Path | Purpose |
|------|---------|
| `DraftPlus.json` | Mod manifest (id, version, BaseLib dependency). |
| `DraftPlus.csproj` | Godot.NET / Harmony / BaseLib references and post-build copy targets. |
| `DraftPlusCode/MainFile.cs` | `[ModInitializer]` + `Harmony.PatchAll()`. |
| `DraftPlusCode/Modifiers/DraftPlus.cs` | Draft+ modifier + related Harmony patches. |
| `DraftPlusCode/Modifiers/StarterRewards.cs` | Starter Rewards modifier + encounter reward patch. |
| `DraftPlusCode/Modifiers/StarterShop.cs` | Starter Shops modifier + merchant `CreateForMerchant` patches. |
| `DraftPlusCode/StarterDeckHelpers.cs` | Shared starter deck merge (Character Cards aware). |
| `DraftPlus/` | Assets folder for future `.pck` content (images, localization JSON, etc.). |

Localization for modifier titles/descriptions is provided via **`ILocalizationProvider`** in code (BaseLib `ModelLocPatch`); you do not need `modifiers.json` in the `.pck` for those strings.

---

## Compatibility notes

- **Vanilla Draft** is unchanged; use **Draft+** when you want starters in the Neow draft.
- Mods that adjust rewards **via hooks** (e.g. changing options before generation) usually stack cleanly.
- Mods that **replace** `CardFactory.CreateForReward` with incompatible Prefix logic may conflict depending on Harmony order — test with your full mod list.
- **Dynamic Card Rewards** patches a **different overload** (`CreateForReward(Player, int, …)`) with Postfixes for rarity — generally compatible with this mod’s patches on the internal blacklist overload.

---

## Credits

- **MegaCrit** — *Slay the Spire 2*
- **[Alchyr](https://github.com/Alchyr/BaseLib-StS2)** — BaseLib and [ModTemplate-StS2](https://github.com/Alchyr/ModTemplate-StS2)

---

## License

[MIT](LICENSE). **Slay the Spire 2** is trademarked and owned by MegaCrit; this project is a fan mod and is not affiliated with or endorsed by them.
