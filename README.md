# Draft+ (Slay the Spire 2)

**Draft+** is a mod that adds **Custom mode** modifiers: **Starter+** pairs with **Draft**, **Sealed Deck**, or **Insanity** so **starter (Basic) cards** can appear in that Neow flow. Separate modifiers add starters to **combat rewards** and **shops**; optional **Minus Strike & Defend** removes Basic Strike/Defend from those pools when Starter+, Rewards, or Shops are on.

**Download:** [Nexus Mods — Draft+](https://www.nexusmods.com/slaythespire2/mods/861) · **Source:** [github.com/kaitorque/draft-plus-sts2](https://github.com/kaitorque/draft-plus-sts2)

Requires **[BaseLib](https://github.com/Alchyr/BaseLib-StS2)** in your game `mods` folder.

---

## Features

### Starter+ (`DraftPlus`)

- A **companion modifier** for **Draft**, **Sealed Deck**, or **Insanity** (**Custom mode**).
- When enabled, **starter (Basic) cards can appear in that Neow** mode’s card flow.
- **Character Cards** compatible: extra character **card pools** (via game hook) and **starting decks** (via this mod’s starter merge) are included.
- **Pandora’s Box**: normally can’t be offered by **Darv** in Draft/Sealed/Insanity. With Starter+ enabled, it can be offered only if your deck still has any Strike/Defend.

### Starter Rewards (`StarterRewards`)

- Optional **Custom mode** modifier.
- When enabled, **combat card rewards** (`Encounter` source, non-uniform rarity path) can pull from **starter cards** as well as the usual pool:
  - Starters are merged into the candidate pool; when the rolled rarity is **Common**, **Basic** starters are included in the pick list (vanilla never rolls “Basic” as a rarity).
- Does **not** change shops, events, or uniform pools by design.
- Respects **Character Cards** the same way as Draft+ (extra characters’ starters included).

### Starter Shops (`StarterShop`)

- Optional **Custom mode** modifier (**Starter Shops** in the UI).
- Uses the same merged starter decks as **Draft+** / **Starter Rewards** (your character plus **Character Cards** extras; see `StarterDeckHelpers`).
- Vanilla shops strip all **Basic** cards before rarity rolls, which hides starter Strikes/Defends. With this on, **Basic** cards from that merged starter set are kept and can show up when the slot rolls **Common** (other Basics stay excluded); same high‑level idea as **Starter Rewards**.

### Minus Strike & Defend (`MinusStrikeDefendStarters`)

- Optional **Custom mode** modifier.
- Only relevant if you have **Starter+**, **Starter Rewards**, or **Starter Shops** enabled.
- Removes **Basic Strike/Defend** cards from the eligible pools used by those modifiers.

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

1. Install **BaseLib** into your game **`mods/`** folder if you do not already have it (follow BaseLib’s layout; its folder name must match BaseLib’s manifest `id`).

2. **Draft+:** Install from [Nexus Mods — Draft+](https://www.nexusmods.com/slaythespire2/mods/861) (or build from source). The game only cares about this layout—the folder name **`DraftPlus`** matches the manifest **`id`**:

   ```
   …/Slay the Spire 2/mods/DraftPlus/DraftPlus.dll
   …/Slay the Spire 2/mods/DraftPlus/DraftPlus.json
   …/Slay the Spire 2/mods/DraftPlus/DraftPlus.pdb   ← optional
   ```

   Nexus downloads are usually a **flat** zip (those three filenames at the archive root); unpack however you like so the paths above are the result.

3. Launch the game → **Settings → Mod Settings** and confirm **Draft+** (`DraftPlus`) is **enabled**. Then start **Custom mode**, pick **Draft**, **Sealed Deck**, or **Insanity**, and also tick **Starter+**. Restart if the game asks.

If you build from source, **`dotnet build`** already copies the same files into **`mods/DraftPlus/`** when your StS2 install path is detected (see **Build (developers)** below).

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
| `DraftPlusCode/Modifiers/DraftPlus.cs` | Starter+ companion modifier + related Harmony patches. |
| `DraftPlusCode/Modifiers/StarterRewards.cs` | Starter Rewards modifier + encounter reward patch. |
| `DraftPlusCode/Modifiers/StarterShop.cs` | Starter Shops modifier + merchant `CreateForMerchant` patches. |
| `DraftPlusCode/Modifiers/MinusStrikeDefendStarters.cs` | Minus Strike & Defend modifier (filters Strike/Defend from starter-enabled pools). |
| `DraftPlusCode/StarterDeckHelpers.cs` | Shared starter deck merge (Character Cards aware). |
| `DraftPlus/` | Assets folder for future `.pck` content (images, localization JSON, etc.). |

Localization for modifier titles/descriptions is provided via **`ILocalizationProvider`** in code (BaseLib `ModelLocPatch`); you do not need `modifiers.json` in the `.pck` for those strings.

---

## Compatibility notes

- **Vanilla Draft / Sealed Deck / Insanity** are unchanged; use **Starter+** when you want starters in that Neow mode.
- Mods that adjust rewards **via hooks** (e.g. changing options before generation) usually stack cleanly.
- Mods that **replace** `CardFactory.CreateForReward` with incompatible Prefix logic may conflict depending on Harmony order — test with your full mod list.
- **Dynamic Card Rewards** patches a **different overload** (`CreateForReward(Player, int, …)`) with Postfixes for rarity — generally compatible with this mod’s patches on the internal blacklist overload.

---

## Credits

- **MegaCrit** — *Slay the Spire 2*
- **Alchyr** — BaseLib and [ModTemplate-StS2](https://github.com/Alchyr/ModTemplate-StS2)

---

## License

[MIT](LICENSE). **Slay the Spire 2** is trademarked and owned by MegaCrit; this project is a fan mod and is not affiliated with or endorsed by them.
