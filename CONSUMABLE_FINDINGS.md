# Native Consumable Findings — Crafting Expanded 0.3.0

## Evidence boundary

The workstream had the current `Assembly-CSharp.dll` and same-snapshot source/IL investigation, but no
running Unity ItemDB. Native **surface and use-path** claims below are current-assembly grounded. Actual
live donor names/IDs and their exact effect magnitudes are intentionally not fabricated; `/craftdiag
consumables` is the live evidence surface for those values.

Current `Assembly-CSharp.dll` SHA-256:

`B840CB8076ED0553F7DC3BEB4042ABA653917882F763181EC0D2C13C26C17847`

## Proven native consumable shape

The current assembly exposes the item/use members needed by the existing Crafting code:

- `Item.RequiredSlot` / `General` semantics;
- `Stackable`;
- `Disposable`;
- `MustBeEquippedToClick`;
- `ItemEffectOnClick`;
- `ItemIcon`;
- `ItemValue` and `ItemLevel`;
- sell/destroy/trade protection flags;
- quest/teach/aura/worn/proc baggage fields.

The click effect is a native `Spell` asset. Current effect fields used only for **classification**, never
for synthesis, include `TargetHealing`, `CasterHealing`, `PercentManaRestoration`,
`LevelScaledManaRestoration`, `TargetDamage`, `BleedDamagePercent`, `Lifetap`, `Spell.Type == Beneficial`, `SelfOnly`,
`ApplyToCaster`, and `InflictOnSelf`.

## Native item-use path

The same-snapshot current-assembly IL investigation in the Practice Duel workstream identified
`ItemIcon.UseConsumable` as a native caller of `PlayerSpells.StartSpell(Spell, Stats)`. Crafting 0.3.0
therefore preserves the donor's exact `ItemEffectOnClick` object and lets the native item-use/spell path
remain authoritative. It does not add a Harmony click handler to heal or restore mana.

Runtime diagnostic wording:

`ItemIcon.UseConsumable -> PlayerSpells.StartSpell(ItemEffectOnClick, Stats)`

The current static environment cannot prove the final inventory decrement/cooldown/effect outcome for a
particular live donor. That remains a targeted live check rather than an inferred claim.

## Safe donor admission

A donor must be an ordinary commodity consumable:

- General slot;
- stackable and disposable;
- not equip-to-click;
- normal sell/destroy/trade semantics;
- positive value and item level;
- real icon and `ItemEffectOnClick`;
- no template/fuel/relic/rare/unique role;
- no quest-read, teach-spell, teach-skill, aura, worn-effect, or weapon-proc baggage.

Recovery donors are rejected if the same native spell carries harmful payload evidence such as target
damage, bleed damage, or lifetap. A utility donor must additionally have native `Spell.Type == Beneficial` and declare
self-application through `SelfOnly`, `ApplyToCaster`, or `InflictOnSelf`.

Donor selection is deterministic: highest safe matching native item level at or below the crafted
item's target, then stable name/ID tie-breaks. A safe same-family donor above the target is diagnostic
information only and is never auto-selected.

## Crafted consumables

| Crafted item | Stable ID | Requested native family | Max donor item level | ItemValue | Registration behavior |
|---|---|---|---:|---:|---|
| Field Tonic | `910030001` | Any native HP/mana recovery | 6 | 1 | Fail closed if no safe donor |
| Wayfarer Tonic | `910030002` | Any native HP/mana recovery | 14 | 1 | Fail closed if no safe donor |
| Cave Draught | `910030003` | Native mana recovery | 24 | 1 | Fail closed if no safe donor |
| Starleaf Tonic | `910030004` | Any native HP/mana recovery | 32 | 2 | Fail closed if no safe donor |
| Rootward Provision | `910030005` | Beneficial self-only native utility | 35 | 1 | Fail closed if no safe donor |

For each successfully registered item, Crafting clones the native donor and preserves the exact donor
`ItemEffectOnClick` and `ItemIcon`. Equipment/stat baggage and quest/teaching roles are neutralized.
No arbitrary recovery magnitude is assigned.

## Economy guard

Native generic Template output quantity can reach 1–5 by fuel tier. The deterministic recipe guard
therefore requires:

`crafted ItemValue * 5 <= total intrinsic ingredient ItemValue`

for all five consumable recipes. This prevents the obvious tested value inversion but is not presented
as a proof against every native vendor/Auction House pricing rule.

## Live proof required

Run `/craftdiag consumables` after ItemDB is live. Record:

1. safe native candidate count;
2. selected donor name/ID/item level/value for each registered crafted item;
3. native effect summary;
4. normal stack behavior;
5. right-click/use behavior;
6. exact one-item consumption;
7. native cooldown/restriction behavior if any;
8. resulting HP/mana/buff effect;
9. sell/destroy behavior as an ordinary commodity.

If any crafted consumable reports `registered=no`, that is a safe evidence failure, not a reason to add
invented effect fields.
