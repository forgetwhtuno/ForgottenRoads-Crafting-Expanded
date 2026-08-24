# Erenshor Crafting Expanded

Part of the **Forgotten Roads for Erenshor** mod collection.

A horizontal-progression expansion to Erenshor's native crafting: forge quality-of-life, Smithing
progression, a crafting-commission proof-of-concept, and a new Foraging gathering system built on
mod-owned nodes (vanilla Mining and Fishing are untouched). See `docs/` for the full native-API
research this mod is built against, and the top-level implementation plan for scope boundaries.

## Status: 0.2.5 forage-interaction polish candidate

- **Source version 0.2.5 is the one-click RMB Foraging interaction-polish candidate.**
  The live-proven exact-once gather transaction is preserved: one valid right-click captures the exact
  forage node, starts the 1.0–1.5 second channel, and no longer requires a held mouse button or continued
  hover/aim. Camera motion alone is harmless. Meaningful player movement, actual HP loss, range/true-LOS
  loss, player death, scene/character change, incompatible UI ownership, unload, or a genuine conflicting
  world interaction terminates the channel before any reward commit. The current content baseline remains
  nine forage resources, eight processed components, sixteen stable recipe identities, the conservative
  equipment-donor policy, and the 25-icon intent; this interaction pass does not weaken content safety.
- **Native Lunaris plugin.** This version has been migrated off BepInEx 5 onto native Lunaris
  (`LunarisPlugin`/`[LunarisPlugin]`/`[LunarisPermission]`, native `Config`/`Logging`). This is a
  loader/config/logging/lifecycle migration only — no gameplay behavior, recipe logic, Foraging
  scope, or command syntax changed. The migration has been statically verified (real compile
  against the installed game + Lunaris assemblies, zero BepInEx references in the compiled
  output, hot-unload event-cleanup audit, full deterministic test suite) but **has not yet been
  live-tested in a running game with Lunaris**. A legacy BepInEx release remains available in this
  repository's Git history/tags for anyone still on BepInEx.
- **Pre-UI-migration baseline:** compiled cleanly and passed its full deterministic test suite against the actual installed
  Erenshor/Lunaris assemblies. The retained-uGUI candidate in this working handoff has **not** been
  recompiled in this execution environment because the handoff omitted the native reference DLLs.
  Gameplay Harmony targets remain `Smithing.Combine`, `Smithing.DoSuccess`, `ItemDatabase.Start`,
  and `TypeText.CheckCommands`; the former UI-only `PlayerControl.LeftClick` / `csMouseOrbit.LateUpdate`
  patches were removed with the IMGUI panel. Native member assumptions include
  (`ItemDatabase.itemDict`/`ItemDB`/`ItemDBList`, `Smithing.Template`/`Components`/`FuelSource`,
  `Item.TemplateIngredients`/`TemplateRewards`, `ItemIcon.QuickSmith`, `PlayerControl.myTransform`,
  `GameData.PlayerTyping`) were independently re-verified against the installed game assembly in
  that earlier documented pass. This handoff does not contain that binary, so those findings are
  historical supporting evidence rather than a fresh current-assembly verification.
- **Partially runtime verified — under the prior BepInEx build, not yet under native Lunaris.**
  Wild Herb custom-item registration, native inventory grant, stacking, zone leave/return, a full
  game exit/restart with the same character, and the `/craftdiag` inventory count were all
  confirmed live by the human tester before this migration. See
  `docs/NATIVE_ITEM_REGISTRY_FINDINGS.md` for that evidence matrix. None of the underlying
  gameplay code changed in the Lunaris migration, but a fresh live pass under Lunaris is still
  pending. Bank/vendor/trade/Auction House behavior remains unverified and is not claimed safe.
- **Smithing stack QoL and the Foraging scanner rewrite still need a live regression pass.** The
  code keeps native `Smithing.Combine()` authoritative, captures the recipe before `DoSuccess()`
  and awards progression only in the postfix after native success, and wires stack QoL to the
  normal Craft/Combine path by repeatedly invoking native `ItemIcon.QuickSmith()` only for the
  exact missing generic recipe ingredients (the three known special-combine template IDs are
  excluded from auto-fill). None of this has been exercised in a running game yet — see
  `docs/FIRST_RUNTIME_TEST.md` for the checklist.
- **Wild Herb auto-placement is now the first Foraging vertical slice.** The earlier live trial
  proved conservative wall/rock-edge placement can find a small number of useful nodes. Current
  source treats the wall/rock hit only as context, probes outward and downward for nearby reachable
  ground, rejects the anchor/boulder surface, steep/raised/forbidden surfaces, and accepts fewer
  than three rather than forcing bad placement. When a scene has no curated `ForageAuthoredNodes`,
  normal `EnableForaging=true` aims for 2–3 Wild Herb clusters using a plant-like mesh selected
  from safe runtime evidence in that scene, but accepts one safe cluster rather than forcing bad
  placement. The cluster is normalized into a compact three-clump resource patch and ordinary
  bushes rank below explicit herb/plant sources so it reads as a gatherable rather than scenery.
  The explicit `EnablePoCNode`/debug-placeholder path is
  still developer-only and is no longer the normal auto-placement switch. This revised slice needs
  the live acceptance pass in `docs/FIRST_RUNTIME_TEST.md`; no offline result is presented as live.
- **Foraging now has a nine-resource ecology with strict evidence gates.** Wild Herb (Foraging 1),
  Field Fiber (3), Resin Sprig (6), Cave Mushroom (8), Wild Bloom (14), Starleaf (18), Cave Moss (24),
  Ghostcap (30), and Blightroot (36) form the raw progression. Open families require matching current-scene
  visual/environment evidence; covered resources require matching fungus/moss evidence and the normal
  `EnableCoveredResources=true` setting; Blightroot additionally requires exact regional eligibility for
  `The Blight`. Every gather yields one. Missing item/mesh/environment/region evidence fails closed instead
  of substituting unrelated scenery. Scene visual discovery remains cached/bounded rather than per-frame.
- **The default-on major content path is separate from the older six-slot runtime-bound experiment.**
  `Crafting/EnableExpandedContent=true` registers stable 91001xxxx component and 91002xxxx equipment
  identities plus sixteen 910110xxx Template identities. Recipes form a raw -> processed -> gear progression
  and still delegate ingredient validation, consumption, fuel handling, and output creation to native
  `Smithing.Combine()` / `Smithing.DoSuccess()`. The older six-slot `EnableProductionNativeRecipes` catalog
  remains OFF by default and is retained only as the earlier experimental/runtime-bound path.
- **Inventory artwork is now mod-owned and manifest-backed for implemented content.** Twenty-five square
  transparent PNG icons ship under `assets/icons/`; `assets/item-art-manifest.json` records stable item ID,
  display name, category, tier/rarity, visual description, path, and fallback behavior. Runtime loading uses
  current Unity image types through cached reflection; a missing/invalid icon safely retains the verified
  native donor icon instead of making item registration fail.
- **The single-template verification experiment remains separate.**
  `Crafting.Experimental/ExperimentalNativeRecipeRegistration=false`, `/craftdiag recipe candidates`,
  `trial register`, and `trial grant` remain developer-only tools for proving the current native path;
  they are not the normal production catalog and award no production recipe progression.
- **Mod progression is now per-character and player-facing as Crafting.** Native activity remains
  Smithing, while the mod-owned sidecar stores Crafting level, permanent KnownRecipes and resource
  discoveries under a strict slot-qualified character key. The old profile-wide
  `smithing-progress.json` may be claimed by exactly one character through a one-time owner marker.
- **Forage depletion is per-character and persists as remaining cooldown seconds.** Successful
  gathers record a bounded scene+resource cooldown ledger and the Foraging sidecar checkpoints the
  remaining time. Zone hopping, gameplay OFF/ON, logout/login, and process restart therefore do not
  refresh a gathered resource; offline wall-clock time does not silently advance the cooldown.
- **Custom-item hot reload is revalidated rather than trusted from stale static state.** Plugin
  startup clears only managed item-object bindings and rechecks the live ItemDB/ownership marker.
  Late identity recovery runs even if an unrelated gameplay Harmony patch fails closed, waits for
  the native ItemDB backing collection to be populated, and then retries, so installed custom item
  resolution is not unnecessarily coupled to the whole gameplay patch set.
- **Forage visual discovery is scene-cached rather than per-frame.** One bounded renderer scan ranks
  herb and fungus sources together. Stable positive evidence is reused; missing required families
  retry no faster than every eight seconds, and enabling the cave-resource experiment later in the
  same scene reopens a previously unnecessary fungal search.
- **Malformed Crafting sidecar state is normalized on load.** The mod-owned progression record is
  forced to the Crafting profession, level 1–50, and a valid in-level XP range before gameplay uses
  it. No native Erenshor save field is mutated.
- **Crafting commissions remain an experimental PoC and are disabled by default.** Their current
  Sim runtime key is scene-local, not a persistent identity; active requests are invalidated on
  zoning. The final commission system still needs a verified recipe catalog and persistent Sim
  identity contract.
- Use `INSTALL_TEST.ps1` for a reversible test install. It records target-bound backup metadata
  so `REMOVE_TEST.ps1` cannot restore a backup belonging to another profile. `BUILD_AND_INSTALL.ps1`
  now uses the same backup + SHA-256 verification path; `INSTALL_TEST.ps1` remains the preferred
  reversible development flow because `REMOVE_TEST.ps1` can safely restore that recorded session.

## Installation

The source version is **0.2.5** and remains **not release-ready from static evidence alone**. The
underlying 0.2.4 forage reward transaction has current live proof for successful grant → XP → depletion;
0.2.5 changes only the player-facing interaction/channel interruption and highlight ownership around that
transaction. Expanded-content donor/forge/equipment behavior still keeps its existing evidence gates and
must be live-tested separately where prior runtime proof is incomplete.

The older six-slot native-production experiment remains fail-closed and OFF. The new sixteen-recipe
expanded-content path is separately gated by `EnableExpandedContent` and by current live ItemDB/forge
capability checks.

This is a **native Lunaris plugin** — BepInEx is no longer required for this version. Requires
Lunaris installed in your Erenshor install. The compiled DLL is placed directly in
`<Erenshor>\plugins\ErenshorCraftingExpanded.dll`; Lunaris manages enable/disable.

```powershell
powershell -ExecutionPolicy Bypass -File .\BUILD.ps1
powershell -ExecutionPolicy Bypass -File .\INSTALL_TEST.ps1
```

`BUILD.ps1` auto-detects the target Erenshor install, builds against that install's current
`Assembly-CSharp.dll`/Unity assemblies plus current Lunaris references, and prints reference/output
SHA-256 values. `INSTALL_TEST.ps1` and `BUILD_AND_INSTALL.ps1` both create a target-bound backup
session and verify the installed DLL hash byte-for-byte. `REMOVE_TEST.ps1` refuses to remove or
overwrite a DLL that no longer matches the recorded test-session hash, then hash-verifies any prior
DLL it restores.

A legacy BepInEx build of this mod remains available in this repository's Git history for anyone
who hasn't moved to Lunaris yet.

## Configuration

Settings are managed by Lunaris's native config system (typed `[Config]` fields, no separate
`.cfg` file to hand-edit).

Standalone launcher recovery follows the current Suite Aura presence contract: the user's Show
Launcher preference is honored only while Hub reports `status=Ready&uiAvailable=true` and this
module's own bridge is registered. Missing/malformed/unavailable Hub presence forces the launcher
visible so the player cannot be stranded.

| Section | Key | Default | What it does |
|---|---|---|---|
| General | EnableMod | true | Master gameplay switch. When false, crafting/foraging behavior is disabled, but installed custom item identities still register so existing owned resource/template save entries can resolve safely while the plugin remains loaded. |
| Crafting | CraftHotkey | None (unbound) | Key that performs one Craft action while the forge window is open and chat isn't focused. |
| Crafting | EnableExpandedContent | true | Enables the Forgotten Roads raw-resource → processed-component → crafted-equipment progression. Stable item identities still register while disabled so existing saves can resolve them safely. |
| Crafting.Experimental | ExperimentalNativeRecipeRegistration | false | Developer-only single-template lifecycle experiment. Keep OFF for normal play. |
| Crafting.Experimental | EnableProductionNativeRecipes | false | Enables the six stable runtime-bound production recipe slots only after current-build native lifecycle proof. OFF by default. |
| Commissions | EnableCraftingRequests | false | Experimental PoC: let local Sims offer a single crafting commission. |
| UI | ShowCraftingToggle | true | Show the on-screen Crafting launcher while the Suite Hub bridge is usable. Hub/bridge failure forces the fallback launcher visible regardless. |
| UI | PersistWindowPosition | true | Remember the retained Crafting panel's normalized dragged position between sessions. |
| UI | LauncherX / LauncherY | -1 | Normalized launcher position; invalid/legacy values recover to a safe default. |
| UI | PanelX / PanelY | -1 | Normalized retained-panel position; invalid/legacy values recover to a safe default. |
| Foraging | EnableForaging | true | Enable Foraging. Curated entries win; scenes without curated entries use the conservative 1–3-cluster resource selection/auto-placement path with level, environment, region, item, and visual evidence gates. |
| Foraging | EnablePoCNode | false | Developer-only switch for the separate survey/PoC candidate. Production authored nodes do not depend on it. |
| Foraging | ForageKey | None | Legacy compatibility field only. Production Foraging uses one valid right-click on the exact resource target and does not read this binding. |
| Foraging | ForagingInteractionRange | 4.25 | Max distance (world units) to gather a node. Mod-owned conservative usability value; no native gather-range constant was found. |
| Foraging | GatherDurationSeconds | 1.25 | Mod-owned gather channel duration. Runtime-normalized to the production 1.0–1.5 second range. |
| Foraging.Experimental | UseNativeGatherAnimation | false | Optional verified `StartLoot`/`EndLoot` trigger adapter. Keep OFF until a live rig/equipment audition confirms the pose is appropriate. Reward authority never depends on animation. |
| Foraging | EnableCoveredResources | true | Enables Cave Mushroom, Cave Moss, and Ghostcap when the current scene supplies explicit matching fungus/moss visual evidence. No covered resource is placed from a scene-name guess alone. |
| Foraging.Experimental | ExperimentalCoveredResources | legacy only | 0.2.x migration key retained for compatibility; `EnableCoveredResources` is authoritative. |
| Foraging.Dev | ForagingScanRadius | 12 | Radius `/craftdiag forage scan` searches. Development diagnostic only. |
| Foraging.Dev | ForagingDebugRespawnSeconds | 0 | If > 0, overrides node respawn time for fast iteration testing. Leave 0 for normal play. |
| Foraging.Dev | AllowDebugPlaceholderVisual | false | Development-only placeholder-sphere fallback. Leave off. |

### Foraging interaction and presentation

### Gather transaction feedback (exact-once transaction from 0.2.4; RMB channel in 0.2.5)

A confirmed in-range **right-click once** starts the **1.25-second mod-owned gather transaction**.
The physical mouse button does not own the duration: releasing RMB and moving the camera do not cancel.
The exact captured node/token remains authoritative until completion or a meaningful interruption. A
player displacement greater than 0.10 world units from the captured starting point, an actual local-player
HP decrease, range loss, a genuine world LOS blocker, player death, typing/conflicting UI ownership,
scene/character change, gameplay disable/unload, or a deliberate non-forage world RMB cancels without
awarding anything. Another forage RMB cannot steal or restart the active transaction.

The reward path is strict and exactly-once: Foraging selects one normal native `AddItemToInv` overload and invokes it at most once, with **no `ForceItemToInv` fallback**. Verified rejection restores the node; only a verified success commits discovery/XP and successful depletion. An exception or unverifiable return after the native inventory invocation begins is quarantined character-locally for one respawn window so zoning/restarting cannot create an automatic duplicate opportunity, but it does not claim XP/discovery or successful depletion. Successful grants play the verified native `Misc.DropItem` sound, then a brief ~0.15-second nameplate/bar scale/fade completes independently of reward authority.

`UseNativeGatherAnimation` is intentionally OFF by default. When explicitly enabled it issues verified `StartLoot` on begin and guarantees `EndLoot` on every terminal path, but the 1.25-second mod transaction—not an animation event—remains authoritative.

Foraging owns a non-blocking trigger hit target for each spawned resource. It does **not** attach native
`MiningNode` gameplay. Production interaction is **right-click the resource once**: a bounded non-alloc
pointer ray runs at the current native `PlayerControl.RightClick` boundary and must hit that exact
mod-owned target. Crafting consumes only that valid forage RMB edge; non-forage RMB and UI-owned RMB
remain native. There is no keyboard or nearest-node fallback. After the click is released, temporary
camera/input ownership is released while the channel continues, so harmless camera motion is not
transaction authority. Character progression readiness is resolved directly from the proven local
player/save-slot identity fields and is no longer gated on unrelated Suite UI/Sim-group managers.

Idle forage nodes no longer carry a persistent selection ring. Hover may show only the restrained inner
targeting cue. A selected/gathering node owns the stronger two-ring cue plus progress presentation, and
every terminal path clears that selection state before normal hover can reacquire.

The Wild Herb presentation is a compact three-clump native-vegetation patch. Its detached world-space
nameplate places the yellow resource name **inside the red availability bar** and geometrically keeps
the TMP readable face toward the active gameplay camera without inheriting vegetation scale/rotation.
The Crafting panel uses a fixed header/footer plus one masked body `ScrollRect`, so variable knowledge
content scrolls inside the panel while Crafting/Foraging toggles, Pin/Close, and footer help remain
contained. The body is a compact player-facing knowledge view:

- current Crafting and Foraging level/XP;
- discovered and currently available Foraging resources, with concise skill/exploration hints;
- the recipe currently loaded in the native forge, its required materials, live available counts, and
  a concise craftable/missing-material state;
- expanded recipe/template knowledge, lock reasons, physical template location/recovery when a
  production recipe definition exists.

The player knowledge panel consumes the **actual runtime catalog**. With production native recipes
OFF, it remains honest about the absence of active expanded Templates while native Smithing continues
normally. When the production registry successfully binds/activates a stable slot, the same generic
recipe-book surface shows its known/locked state, Crafting/Foraging/discovery requirements, physical
Template state, and safe recovery action without hardcoding recipe-specific UI.

## Commands

- `/craftdiag` — concise diagnostic summary (mod state, persistence errors, forage survey pointer).
- `/craftdiag giveherb` — development helper to grant a Wild Herb for testing.
- `/craftdiag recipe status` — show experimental native Template registration state.
- `/craftdiag recipe candidates` — show bounded conservative live native donor candidates.
- `/craftdiag recipe trial register` — attempt registration only when the experimental config gate is ON.
- `/craftdiag recipe trial grant` — grant one physical verification Template after successful registration.
- `/craftdiag givemushroom` — development helper for the experimental Cave Mushroom item; fails closed if no safe fungus-looking native Item template exists.
- `/craftdiag forage pos` — reports exact scene/position/yaw and emits a paste-ready `ForageNodeDefinition` skeleton with visual-source fields intentionally blank.
- `/craftdiag forage scan [filter]` — scans nearby renderers as forage-node visual-source candidates; see `docs/FORAGING_ASSET_SURVEY.md`.

## Compatibility

- **Required:** native Lunaris and a matching installed Erenshor build. The native findings under
  `docs/` record earlier disassembly/verification work; after an Erenshor update, rebuild and
  re-verify those assumptions against that machine's current `Assembly-CSharp.dll`.
- **Optional integration:** none currently. Adapts UI/input-protection *code patterns* from this
  author's own `ForgottenRoads-PvP` (see Acknowledgements) but has no runtime dependency on it or any
  other mod in the suite.
- Vanilla Mining and Fishing are untouched by design; this mod only adds a new, separate Foraging
  system.

## Known limitations

See the Status section above for the full detail. In short: Bank/vendor/trade/Auction House
interaction with custom items is unverified; the curated authored-node catalog is still empty; the
newer resource families remain strict evidence/region gated (with covered families experimental);
production native recipes are implemented but **OFF by default pending current-runtime lifecycle
acceptance**; and crafting commissions remain an off-by-default PoC.

## Troubleshooting

- If the mod doesn't load: verify Lunaris itself loads, verify `ErenshorCraftingExpanded.dll` is
  under `<Erenshor>\plugins\`, and check the Lunaris log for a Harmony patch-target error — a
  missing target fails closed with a log line rather than silently running partial features.
- Rebuild against the current `Assembly-CSharp.dll` after any Erenshor update; native method/field
  shapes are re-verified each pass but can change with a patch.
- Run `/craftdiag` for a quick state summary, including any progression `persistenceError`.

## Development / build information

This project is guided through design, testing, playtesting, and audits against the actual installed
game assembly. Bug reports, code
review, corrections, and contributions from experienced Erenshor modders are welcome.

Build/test procedure and architecture boundaries for contributors (human or AI) are documented in
[`AGENTS.md`](AGENTS.md).

This is an unofficial, community-made mod for Erenshor and is not affiliated with or endorsed by
the game's developer.

## License

Apache License 2.0 — see [`LICENSE`](LICENSE) and [`NOTICE`](NOTICE).

## Acknowledgements

Crafting Expanded builds on research and examples from the Erenshor modding community.

- **Arcanism — cammaron**
  Referenced as an existing, currently-maintained Erenshor implementation of custom item
  creation and `ItemDatabase` registration. No source was copied (no license file is published
  in that repository); the registration technique was independently reimplemented against this
  build's own disassembled `Assembly-CSharp.dll`.

- **ForgottenRoads-PvP — forgetwhtuno**
  This mod's own sibling project (same author, Apache-2.0-licensed, independent checkout in this
  development environment). Its earlier panel/input-protection patterns informed development; the current player-facing Crafting panel is retained Unity uGUI and no longer uses UI-only `PlayerControl.LeftClick` / `csMouseOrbit.LateUpdate` Harmony prefixes. Gameplay Harmony hooks remain limited to the actual crafting/item/command surfaces described above and were adapted for Crafting Expanded's own panel.

- **Erenshor-Party-Tools — forgetwhtuno**
  Same author/license as above. Established the panel offset-anchoring convention `ForgottenRoads-PvP`
  (and in turn this mod) follows, and its `RUN_TESTS.ps1` shape is the template for this mod's
  own pure-logic test runner.

Detailed technical attribution, including exactly which files were inspected and what was
copied versus independently reimplemented, is in [`docs/ATTRIBUTION.md`](docs/ATTRIBUTION.md).

No endorsement by any credited project or author is implied.


## Optional Suite Hub integration

Forgotten Roads Hub is **optional**. When it is installed, this mod can expose its normal player-facing controls there through the versioned public `CraftingControlApi` surface. The mod remains independently usable without the Hub and does not compile against Hub types or assume Hub load order.

Crafting keeps its dedicated retained-uGUI Crafting panel and fallback launcher. `/craftdiag` remains a developer/diagnostic surface rather than a normal-player requirement.

Hub can show Smithing/Foraging status, enable or disable the mod-owned features, and open the Crafting panel. Forage scan, asset survey, debug placeholders, and spawn probes remain developer-only.

The shared control/API and fully-in-world UI policy in this handoff are source-validated but **not yet live-tested under Lunaris hot reload**.

### Content/UI migration candidate

The current source replaces the Crafting player panel with retained Unity uGUI, adds a Suite-style fallback launcher plus normalized position persistence/reset, and keeps `/craftdiag` developer-only. Existing `Crafting Expanded` and `Foraging` bool descriptors remain valid, with a new **Show Crafting launcher** descriptor and Open/Close/Reset actions. Recipe, item-registration, persistence, foraging spawn/gathering, clone, and diagnostic gameplay paths were not redesigned. Native compile and live Lunaris UI/reload verification remain required for this candidate.


## Reconciled profession progression (2026-08-14)

This development branch now combines the three parallel profession workstreams:

- **Crafting 1-50** remains per-character and observes successful native Smithing crafts.
- **Foraging 1-50** is a separate per-character gathering skill. Current milestones are Wild Herb 1, Field Fiber 3, Resin Sprig 6, Cave Mushroom 8, Wild Bloom 14, Starleaf 18, Cave Moss 24, Ghostcap 30, and regional Blightroot 36; every family remains fail-closed without the item/scene evidence its catalog entry requires.
- **Resource discovery** is permanent per character and is the discovery authority used by future recipe unlock predicates.
- **Known recipes** are permanent per-character knowledge owned by the recipe-ownership layer, separate from the physical native Template item.
- Physical recipe Templates use defensive `PlayerCannotSell` + `NoTradeNoDestroy` + zero-value policy when the owned custom Template exists.
- Lost/consumed physical Templates can be restored only when the recovery policy can do so without creating an unsafe duplicate. Inventory-full failure never erases recipe knowledge.
- The Crafting panel now has profession summaries and a recipe-book/recovery surface.
- Native Smithing still owns ingredient validation, ingredient consumption, and output creation.

### Important current gate

The **six older runtime-bound production slots** remain an OFF-by-default compatibility/experiment path.
The **sixteen expanded-content recipes** are a separate default-on progression and use stable Template IDs
`910110001` through `910110016`; they activate only when their mod-owned ingredients/outputs resolve and the
live forge exposes sufficient component capacity. The separate `ExperimentalNativeRecipeRegistration` path
remains a one-template developer verification tool. Stable Template IDs must never be silently rebound to
different outputs after players can own them.
