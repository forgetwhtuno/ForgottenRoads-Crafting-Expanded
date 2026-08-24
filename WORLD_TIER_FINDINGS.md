# World Tier Findings — Crafting Expanded 0.3.0

## Evidence boundary

This workstream had the current project snapshot and the bundled current `Assembly-CSharp.dll`
(SHA-256 `B840CB8076ED0553F7DC3BEB4042ABA653917882F763181EC0D2C13C26C17847`), but no running
Erenshor process. The implementation therefore distinguishes **current-assembly/source proof** from
**live scene evidence**. No zone level is inferred from a scene name.

## Current native hostile surfaces used

The current assembly/project proves the ordinary-hostile survey can read:

- `Object.FindObjectsOfType<NPC>()`;
- `NPC.SimPlayer`, `ThisSim`, `NeverAggro`, `MiningNode`, `TreasureChest`, `SummonedByPlayer`;
- `Character.Alive`, `Master`, `Invulnerable`, `isVendor`, `BossXp`, `MyFaction`, `MyStats.Level`;
- current NPC structural identity wiring used by the same-snapshot Contracts code: `MyDialog`,
  `MyQuests`, `questToAssign`, `RM`, `MySpawnPoint.RareSpawns`, plus quest/achievement death/spawn
  hooks;
- the current same-snapshot PvP temporary-clone marker `PvP_TemporaryClone...` as a negative filter.

The survey excludes Sims, owned/summoned actors, protected/noncombat actors, friendly factions,
boss-reward actors, structurally unique quest/dialog/raid/rare actors, and the PvP proxy before using
`MyStats.Level`.

## Scene threat algorithm

A scene visit starts unclassified. After 2.25 seconds of stabilization, the mod samples ordinary
hostile levels. A robust classification requires at least five samples. It records min, median,
75th-percentile `upper ordinary`, and max, but **max does not determine the band**.

| World band | Required robust distribution |
|---|---|
| Starter | median <= 6 and p75 <= 9 |
| Low-mid | median <= 11 and p75 <= 16 |
| Mid | median <= 18 and p75 <= 24 |
| Mid-high | median <= 26 and p75 <= 32 |
| High | anything above the Mid-high thresholds |

The first robust classification is frozen for that scene visit. Ordinary spawn/despawn churn does not
reroll it every frame. A new scene visit resets the snapshot.

Player level is not an input to `Compute`, `Classify`, `Allows`, the runtime survey, or resource
availability.

## Sparse evidence / fallback

The supplied current snapshot did not establish a trustworthy native zone-difficulty metadata member.
After six seconds, a scene with fewer than five ordinary hostile samples freezes as **Unknown** with
fallback source `no-proven-native-zone-level-metadata`. Unknown fails closed for automatic resource
placement. This is intentionally stricter than guessing a low tier from a zone name or from player
level.

## Resource world bands

| Resource | Foraging | World band |
|---|---:|---|
| Wild Herb | 1 | Starter |
| Field Fiber | 3 | Starter |
| Resin Sprig | 6 | Starter |
| Cave Mushroom* | 8 | Starter |
| Meadow Reed | 9 | Low-mid |
| Amberbark | 12 | Low-mid |
| Wild Bloom | 14 | Low-mid |
| Sunpetal | 16 | Low-mid |
| Starleaf | 18 | Mid |
| Ironvine Root | 22 | Mid |
| Cave Moss* | 24 | Mid |
| Ghostcap* | 30 | Mid-high |
| Blightroot | 36 | Mid-high + exact The Blight regional rule |
| Duskleaf | 42 | High |
| Elder Resin | 46 | High |
| Voidcap* | 49 | High |

`*` Covered families still require the existing covered-resource setting and their explicit visual
evidence. World band is an additional gate, not a replacement for ecology/visual/region gates.

## Faerie's Brake regression

The deterministic regression fixture intentionally uses no Brake-specific code. A Brake-like ordinary
hostile distribution `[4, 5, 5, 6, 6, 7, 7]` resolves to Starter, so Mid-band Starleaf is rejected. A
single level-50 outlier added to a low distribution also cannot promote the scene because classification
uses median/p75 rather than maximum.

**Not yet claimed:** the actual live Faerie's Brake hostile distribution was not available in this
o-running-game environment. Live QA must run `/craftdiag worldtier` in Brake. If its verified ordinary
population is Starter or Low-mid, Starleaf will be blocked systemically; if the real population proves a
higher band, the result should follow that evidence rather than a scene-name special case.
