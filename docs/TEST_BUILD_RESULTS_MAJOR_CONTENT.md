# Major Content Test / Build Results

**Workstream date:** 2026-08-20  
**Source baseline:** 0.2.4  
**Result:** source/static validation passed; fresh C# build and executable deterministic test runner could not be run in this Linux harness.

## Validation performed here

| Check | Result |
|---|---|
| Major-content static validation harness | **PASS 45/45** |
| Item-art manifest | **PASS** — schema v1, 25 unique stable IDs, 25 unique safe relative paths |
| Generated icon files | **PASS** — exactly 25 PNGs, each 128x128 RGBA with transparency |
| Content manifest | **PASS** — 9 resources, 8 components, 8 equipment outputs, 16 recipes |
| Stable identity validation | **PASS** — 25 unique content item IDs; 16 unique recipe Template IDs in reserved range |
| Recipe graph validation | **PASS** — every ingredient/output resolves to implemented content, positive quantities, <=3 distinct ingredient slots, monotonic Crafting gates |
| Interaction policy | **PASS** — no `Press G to gather`, no keyboard gather read, no `OnGUI` retained-UI regression |
| Scanner hot-path policy | **PASS** — renderer enumeration remains isolated to the scanner; controller uses cached/bounded evidence refresh |
| Icon safety policy | **PASS** — path containment guard, donor-icon fallback, hot-reload marker present |
| Build/install asset policy | **PASS** — icon manifest/count validation, deterministic asset hashing, guarded removal/restore contracts present |
| C# delimiter lexical sanity | **PASS** — all `src/**/*.cs` balanced by a comment/string-aware heuristic |
| New-file C#5 compatibility token scan | **PASS** — no interpolation, `nameof`, null-conditional, or expression-body syntax in the six newly introduced production source files |
| Git whitespace check | **PASS** with `cr-at-eol` enabled for this CRLF worktree |

The machine-readable 45-check result is `docs/STATIC_VALIDATION_MAJOR_CONTENT.json`.

## Current assembly evidence

The project-provided current reference assembly inspected for this pass is:

`ErenshorSuiteHub/refs/Assembly-CSharp.dll`  
SHA-256: `B840CB8076ED0553F7DC3BEB4042ABA653917882F763181EC0D2C13C26C17847`

Binary symbol inspection re-confirmed the current item/crafting surface used by this implementation, including `RequiredSlot`, `ThisWeaponType`, `WeaponDmg`, `WeaponDly`, `TemplateIngredients`, `TemplateRewards`, `FuelLevel`, `DoSuccess`, `OneHandMelee`, `OneHandDagger`, `TwoHandStaff`, `TwoHandBow`, `PlayerCannotSell`, and `NoTradeNoDestroy`. The source uses current enum names/native donor objects rather than historical numeric slot guesses.

## Why the fresh DLL build is not marked PASS

This execution environment has no `dotnet`, Mono, `csc`, `mcs`, PowerShell, or Wine runtime/compiler installed. A root-level package-manager attempt could not reach Debian mirrors, and direct SDK/package/archive downloads are blocked by the sandbox binary-download policy. Therefore:

- `BUILD.ps1` could not execute here;
- `tests/RUN_TESTS.ps1` could not compile/run its C# executable here;
- the pre-existing `bin/ErenshorCraftingExpanded.dll` was **not rebuilt** and is stale relative to this workstream;
- no claim is made that the new source has passed a real compiler or a running Erenshor session.

## Required build/test commands on the current Windows install

```powershell
powershell -ExecutionPolicy Bypass -File .\BUILD.ps1
powershell -ExecutionPolicy Bypass -File .\tests\RUN_TESTS.ps1
powershell -ExecutionPolicy Bypass -File .\INSTALL_TEST.ps1
```

`BUILD.ps1` recursively enumerates every `src/**/*.cs`, so the new files are included without a hand-maintained source list. `tests/RUN_TESTS.ps1` includes the expanded item/icon/recipe pure-policy sources and its source-contract checks now validate the 25-icon manifest and the substantial stable recipe catalog.

After those commands pass, complete `docs/LIVE_TEST_CHECKLIST_MAJOR_CONTENT.md`. Only then should release readiness be evaluated.
