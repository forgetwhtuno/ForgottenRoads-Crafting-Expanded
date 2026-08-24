# Major Crafting Expanded Content Implementation Report

**Workstream date:** 2026-08-20  
**Source baseline:** 0.2.4  
**Repository:** `forgetwhtuno/ForgottenRoads-Crafting-Expanded`  
**Status:** implementation complete for static/source handoff; **not release-ready until live gameplay validation**.

## Scope and result

This pass extends the existing 0.2.4 gathering/crafting architecture rather than replacing it. Native Erenshor remains authoritative for Smithing ingredient validation/consumption, fuel handling, output creation, item stats/equipment semantics, and inventory persistence. The worktree adds a coherent four-tier gather -> process -> gear loop, stable mod-owned item/recipe identities, larger foraging ecology, retained-UI presentation for the expanded recipe set, and a runtime icon asset pipeline.

The current bundled `Assembly-CSharp.dll` used as implementation evidence has SHA-256 `B840CB8076ED0553F7DC3BEB4042ABA653917882F763181EC0D2C13C26C17847`. Current symbols rechecked from that binary include `RequiredSlot`, `ThisWeaponType`, `WeaponDmg`, `WeaponDly`, `TemplateIngredients`, `TemplateRewards`, `FuelLevel`, `DoSuccess`, `OneHandMelee`, `OneHandDagger`, `TwoHandStaff`, `TwoHandBow`, `PlayerCannotSell`, and `NoTradeNoDestroy`. Historical slot-number notes were corrected to the current `SlotType` shape used by this source.

## Implementation counts

| Surface | Added in this pass | Current implemented total |
|---|---:|---:|
| Forage resources | 4 | 9 |
| Processed/intermediate materials | 8 | 8 |
| Expanded progression recipes | 16 | 16 |
| Crafted equipment outputs | 8 | 8 |
| Stable icon-bearing mod items | 20 new + 5 existing resources | 25 |
| Generated runtime icon PNGs | 25 | 25 |
| Progression bands | 4 | 4 |

## Foraging improvements

The production gather interaction remains left-click based; no `Press G to gather` path is used. Interaction range is now 4.25 world units. The normalized three-clump node presentation target is larger (~0.78 world-unit largest dimension) and the label world scale is 0.0062, making nodes more legible without turning them into oversized props. Existing terrain/clearance/reachable-ground checks, duplicate spacing, bounded scene placement, strict inventory grant transaction, per-character cooldown persistence, and gather cancellation behavior remain in place.

The resource selector now supports open herbs, fibers, woody sprigs, flowers, roots, covered fungi, and covered moss. Total auto-placement remains bounded by the existing 1–3 scene cluster policy. Current-scene renderer evidence is cached; positive evidence is reused and missing required visual families are retried on the existing bounded cadence rather than scanned each frame.

## Itemization and balance

Eight real equipment outputs are implemented: two shields and six weapons across one-handed melee, dagger, two-handed staff, and two-handed bow families. Combat numbers are deliberately **not hand-authored**. At runtime the item registry finds a conservative safe native donor matching the verified slot/weapon/shield family, restricted to item level at or below the target, then clones it and preserves the donor's native stats, class restrictions, slot behavior, and weapon semantics. Crafting Expanded changes stable identity/name/lore/trade safeguards/art only. If no safe donor exists, that item fails closed and `/craftdiag` reports the reason.

This pass intentionally does not synthesize armor/accessory slots or custom consumable-effect fields: the current evidence was strongest for weapon/shield donor semantics, and inventing unverified equipment/effect behavior would be a worse result than a smaller coherent gear family.

## Native Smithing quantity behavior

The expanded recipe identities configure native `TemplateIngredients`/`TemplateRewards`; they do not replace `Smithing.Combine()` or `DoSuccess()`. Current native findings show non-Primary/Secondary outputs receive `FuelLevel + 1` quantity, so the eight processed General-slot materials are presented as **1–5 output by native fuel tier**. The Primary/Secondary weapon/shield outputs are presented as **1 output**.

## Inventory art pipeline

`assets/item-art-manifest.json` is the art authority for all 25 implemented icon-bearing items. Each entry contains stable item ID, display name, category, tier, rarity, visual description, asset path, and fallback behavior. The actual 128x128 RGBA PNGs are under `assets/icons/`; they are transparent, text-free, and use a consistent simplified fantasy inventory silhouette style.

`ItemIconAssetLoader` resolves only relative paths within approved plugin roots, caches loaded sprites, marks assigned sprites for hot-reload reuse, and fails safely. Missing files, unavailable image types, decode failures, or assignment failures leave the cloned native donor icon intact; item registration does not depend on successful custom art loading.

## Persistence and migration

Original 910000001–910000005 resource IDs are unchanged. New raw resources use 910000006–910000009, components use 910010001–910010008, equipment uses 910020001–910020008, and new recipe Template identities use 910110001–910110016. Existing custom IDs are never overwritten when occupied by an unrelated item; collisions fail closed. Stable item identities register even when expanded gameplay is disabled so old/future saves can still resolve owned items while the plugin is loaded.

Existing Crafting/Foraging sidecar progression is not erased or replaced. The legacy `ExperimentalCoveredResources` setting remains as a migration-compatible facade while `EnableCoveredResources` is authoritative. Expanded recipe identities are inert until the live database and forge shape can be proven and rebound after ItemDB lifecycle replacement, preventing stale Unity database references across reload/zone transitions. No vanilla save-file editing was introduced.

## Install artifact handling

The install/test scripts now treat `assets/` as part of the tested plugin artifact. They validate the art manifest/PNG count, hash the directory, back up a prior installed asset directory, stage/copy/verify the new assets beside the DLL, and restore/remove them only when the recorded test-session hash still matches. This prevents `REMOVE_TEST.ps1` from clobbering later manual icon changes.

## Build status

A fresh plugin DLL could **not** be compiled inside this Linux execution harness because it contains no .NET/Mono/C# compiler or PowerShell runtime, and the environment blocks downloading binary SDK/package archives. The repository does contain current bundled Erenshor/Lunaris reference assemblies for inspection, but a compiler cannot be introduced here. The pre-existing `bin/ErenshorCraftingExpanded.dll` is therefore **stale relative to this workstream and must not be treated as the new build**.

`BUILD.ps1`/`INSTALL_TEST.ps1` remain the authoritative Windows/current-game build path and dynamically enumerate all `src/**/*.cs`, so the newly added files are included automatically. Run the live checklist after a successful local build before considering this content releasable.

## Deliverable authorities

- Human-readable full content table: `docs/CONTENT_MANIFEST.md`
- Machine-readable content: `assets/content-manifest.json`
- Icon manifest: `assets/item-art-manifest.json`
- Icon review sheet: `docs/ITEM_ICON_CONTACT_SHEET.png`
- Test/build status: `docs/TEST_BUILD_RESULTS_MAJOR_CONTENT.md`
- Live acceptance: `docs/LIVE_TEST_CHECKLIST_MAJOR_CONTENT.md`
