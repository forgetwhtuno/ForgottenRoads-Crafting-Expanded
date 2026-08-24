# Crafting Expanded 0.3.0 Content Manifest

Machine-readable authority: `assets/content-manifest.json`. Icon-specific authority: `assets/item-art-manifest.json`.

## Forage resources

| Resource | ID | Foraging | XP | World | Rarity | Pool | Density/cap | Purpose |
|---|---|---:|---:|---|---|---|---|---|
| Wild Herb | `910000001` | 1 | 20 | Starter | Common | OpenHerbs | 10/3 | baseline herbal preparations and simple restorative recipes |
| Field Fiber | `910000006` | 3 | 24 | Starter | Common | OpenFibers | 8/2 | cordage, bindings, bows, shields, and workshop components |
| Resin Sprig | `910000007` | 6 | 28 | Starter | Common | OpenWood | 7/2 | resin grips, hardened bindings, weapons, and shields |
| Cave Mushroom | `910000002` | 8 | 32 | Starter | Uncommon | CoveredFungi | 6/2 | cave tonics, alchemical reagents, and dungeon-focused consumables |
| Meadow Reed | `910000010` | 9 | 34 | Low-mid | Common | OpenFibers | 6/2 | travel cordage and early crafted tonics |
| Amberbark | `910000011` | 12 | 36 | Low-mid | Uncommon | OpenWood | 5/1 | travel preparations and mid-tier wood/resin recipes |
| Wild Bloom | `910000003` | 14 | 38 | Low-mid | Uncommon | OpenFlowers | 4/1 | pigments, restorative preparations, and light utility elixirs |
| Sunpetal | `910000012` | 16 | 42 | Low-mid | Uncommon | OpenFlowers | 3/1 | mid-tier recovery preparations and Starleaf refinement |
| Starleaf | `910000008` | 18 | 46 | Mid | Rare | OpenHerbs | 2/1 | mid/high-tier infusions, bows, and refined tonics |
| Ironvine Root | `910000013` | 22 | 50 | Mid | Uncommon | OpenRoots | 4/1 | mid/high root temper and durable workshop components |
| Cave Moss | `910000004` | 24 | 52 | Mid | Rare | CoveredMoss | 3/1 | binding agents and higher-tier dungeon remedies |
| Ghostcap | `910000009` | 30 | 62 | Mid-high | Rare | CoveredFungi | 2/1 | specialist cave extracts and rare dagger finishing |
| Blightroot | `910000005` | 36 | 68 | Mid-high | Rare | OpenRoots | 2/1 | late regional reagents and root temper |
| Duskleaf | `910000014` | 42 | 76 | High | Rare | OpenHerbs | 2/1 | late provisions and high-tier herbal work |
| Elder Resin | `910000015` | 46 | 82 | High | Rare | OpenWood | 2/1 | late provisions and hardened high-tier compounds |
| Voidcap | `910000016` | 49 | 90 | High | Rare | CoveredFungi | 1/1 | late specialist provisions where a proven native utility donor exists |

World band controls whether the resource may exist in the scene. Foraging skill separately controls whether the character may harvest it. Covered/region/visual evidence gates remain additional requirements.

## Crafted consumables

| Item | ID | Tier | Native donor family | Max donor level | ItemValue |
|---|---|---:|---|---:|---:|
| Field Tonic | `910030001` | 1 | AnyRecovery | 6 | 1 |
| Wayfarer Tonic | `910030002` | 2 | AnyRecovery | 14 | 1 |
| Cave Draught | `910030003` | 3 | ManaRecovery | 24 | 1 |
| Starleaf Tonic | `910030004` | 4 | AnyRecovery | 32 | 2 |
| Rootward Provision | `910030005` | 5 | Utility | 35 | 1 |

Consumables clone a safe live native commodity donor and preserve its exact `ItemEffectOnClick` and icon. Missing safe donor evidence fails closed; no recovery magnitude is synthesized.

## Production recipes

| Template | Output | Gates | Ingredients |
|---|---|---|---|
| `910110001` | Woven Fiber Cord | C1 / F3 | 2x Field Fiber |
| `910110002` | Herbal Binder | C3 / F3 | 2x Wild Herb, 1x Field Fiber |
| `910110017` | Field Tonic | C4 / F3 | 3x Wild Herb, 2x Field Fiber |
| `910110003` | Resin-Sealed Grip | C5 / F6 | 2x Resin Sprig, 1x Woven Fiber Cord |
| `910110004` | Trailwood Cudgel | C6 / F6 | 1x Resin-Sealed Grip, 1x Herbal Binder |
| `910110005` | Barkguard Maul | C8 / F6 | 2x Woven Fiber Cord, 2x Resin Sprig |
| `910110006` | Bloom Tincture | C10 / F14 | 2x Wild Bloom, 1x Wild Herb |
| `910110007` | Briar Knife | C12 / F14 | 1x Resin-Sealed Grip, 1x Bloom Tincture |
| `910110018` | Wayfarer Tonic | C13 / F14 | 2x Amberbark, 2x Meadow Reed, 1x Herbal Binder |
| `910110008` | Starleaf Infusion | C17 / F18 | 2x Starleaf, 1x Wild Bloom |
| `910110009` | Bloomwood Staff | C18 / F18 | 1x Resin-Sealed Grip, 1x Starleaf Infusion, 2x Resin Sprig |
| `910110010` | Cave Paste | C22 / F24 | 2x Cave Mushroom, 1x Cave Moss |
| `910110019` | Cave Draught | C25 / F24 | 2x Cave Mushroom, 2x Cave Moss, 1x Cave Paste |
| `910110011` | Ghostcap Extract | C28 / F30 | 2x Ghostcap, 1x Cave Mushroom |
| `910110012` | Starleaf Longbow | C29 / F30 | 2x Resin-Sealed Grip, 2x Woven Fiber Cord, 1x Starleaf Infusion |
| `910110013` | Ghostcap Shiv | C30 / F30 | 1x Resin-Sealed Grip, 1x Ghostcap Extract, 1x Cave Paste |
| `910110020` | Starleaf Tonic | C31 / F30 | 2x Starleaf, 2x Sunpetal, 1x Starleaf Infusion |
| `910110014` | Root Temper | C34 / F36 | 2x Blightroot, 1x Ironvine Root |
| `910110015` | Blightroot Blade | C36 / F36 | 1x Root Temper, 1x Resin-Sealed Grip, 1x Starleaf Infusion |
| `910110016` | Rootbound Guard | C38 / F36 | 1x Root Temper, 2x Woven Fiber Cord, 1x Resin Sprig |
| `910110021` | Rootward Provision | C49 / F49 | 1x Duskleaf, 1x Elder Resin, 1x Voidcap |

All rows are physical mod-owned Template identities. Native `Smithing.Combine()` / `DoSuccess()` remain ingredient-consumption/output authority. General material/consumable outputs retain native 1–5 fuel scaling; equipment outputs are one.

## Equipment

The existing eight equipment outputs and conservative current-ItemDB donor-family policy are preserved. Bloomwood Staff remains fail-closed when no safe native TwoHandStaff donor exists at or below its target tier.

## Art

The existing 25 mod-owned icons are unchanged. Newly added 0.3.0 raw identities and consumables intentionally retain proven donor icons instead of adding unsupported palette swaps.
