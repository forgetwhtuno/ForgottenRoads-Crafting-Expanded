#!/usr/bin/env python3
"""Focused 0.3.2 world-tier/native-consumable static/current-assembly gate.

This test is intentionally honest about its boundary: it verifies source contracts and exact
Assembly-CSharp symbol presence from the supplied current snapshot. It does not pretend to execute
Unity, enumerate the live ItemDB, or survey a live scene.
"""
from pathlib import Path
import hashlib
import re

ROOT = Path(__file__).resolve().parents[1]
PROJECT = ROOT.parents[1]
ASSEMBLY = PROJECT / "CURRENT_GAME_REFERENCES" / "Assembly-CSharp.dll"


def text(rel):
    return (ROOT / rel).read_text(encoding="utf-8-sig")


def req(cond, msg):
    if not cond:
        raise AssertionError(msg)


def count(pattern, source):
    return len(re.findall(pattern, source, flags=re.M))


def main():
    req(ASSEMBLY.is_file(), "current Assembly-CSharp.dll missing")
    raw = ASSEMBLY.read_bytes()
    sha = hashlib.sha256(raw).hexdigest().upper()
    req(sha == "B840CB8076ED0553F7DC3BEB4042ABA653917882F763181EC0D2C13C26C17847", "unexpected current Assembly-CSharp hash")
    for sym in (
        b"MyStats", b"Level", b"NeverAggro", b"MiningNode", b"TreasureChest", b"SummonedByPlayer",
        b"BossXp", b"MyDialog", b"MyQuests", b"questToAssign", b"MySpawnPoint", b"RareSpawns",
        b"ItemEffectOnClick", b"Disposable", b"MustBeEquippedToClick", b"UseConsumable",
        b"HP", b"Mana", b"TargetHealing", b"CasterHealing", b"PercentManaRestoration", b"LevelScaledManaRestoration",
        b"TargetDamage", b"Beneficial", b"SelfOnly", b"ApplyToCaster", b"InflictOnSelf", b"SpellCastTime", b"RequiredLevel", b"ManaCost",
        b"TemplateIngredients", b"TemplateRewards", b"DoSuccess", b"QuickSmith",
    ):
        req(sym in raw, f"current assembly evidence missing: {sym.decode(errors='ignore')}")

    world = text("src/Foraging/WorldThreatPolicy.cs")
    api = text("src/Compatibility/GameWorldThreatApi.cs")
    avail = text("src/Foraging/ForageResourceAvailabilityPolicy.cs")
    controller = text("src/Foraging/ForageNodeController.cs")
    catalog = text("src/Foraging/ForageResourceCatalog.cs")
    ids = text("src/Items/CraftingExpandedItemIds.cs")
    items = text("src/Items/ExpandedContentItems.cs")
    consumable = text("src/Items/NativeConsumablePolicy.cs")
    registry = text("src/Compatibility/GameItemRegistryApi.cs")
    recipes = text("src/Crafting/ExpandedContentRecipes.cs")
    plugin = text("src/ErenshorCraftingExpandedPlugin.cs")

    # World model: deterministic, outlier-resistant, fixed after stabilization and independent of player level.
    for token in ("MinimumRobustSample = 5", "MedianLevel", "UpperOrdinaryLevel", "PercentileNearestRank", "ForageWorldBand.Unknown", "Faerie's Brake"):
        req(token in world, f"world policy missing {token}")
    req("MaximumLevel" in world, "outlier diagnostic maximum absent")
    req("PlayerLevel" not in world and "playerLevel" not in world, "world policy takes player level")
    req("result.Band = ForageWorldBand.Unknown" in world and "fail closed for auto-placement" in world, "sparse fallback is not fail closed")

    for token in (
        "FindObjectsOfType<NPC>()", "actor.Master != null", "actor.Invulnerable", "actor.isVendor", "actor.BossXp > 0f",
        "npc.SimPlayer", "npc.ThisSim != null", "npc.NeverAggro", "npc.MiningNode", "npc.TreasureChest", "npc.SummonedByPlayer",
        'AccessTools.Field(typeof(NPC), "MyDialog")', 'AccessTools.Field(typeof(NPC), "MyQuests")',
        'AccessTools.Field(typeof(NPC), "questToAssign")', 'AccessTools.Field(typeof(NPC), "RM")',
        'AccessTools.Field(typeof(NPC), "MySpawnPoint")', "RareSpawns", 'StartsWith("PvP_TemporaryClone"', "actor.MyStats.Level",
    ):
        req(token in api, f"ordinary-hostile filter missing {token}")
    req("Player" + "Level" not in api and "CharacterLevel" not in api, "hostile survey scales to player level")
    req("if (_snapshot != null" in api and "StabilizeSeconds" in api and "SparseFallbackSeconds" in api, "scene snapshot freeze/stabilization missing")
    req("no-proven-native-zone-level-metadata" in api, "fallback provenance missing")
    req("WorldThreatPolicy.Allows" in avail and "world-tier-too-low" in avail, "resource world-band gate missing")
    req("sceneBand == ForageWorldBand.Unknown" in world and "resourceBand == ForageWorldBand.Starter" in world, "unknown-world Starter baseline repair missing")
    req("unknown-world starter baseline" in avail and "unknown-world progression exclusion" in avail, "unknown-world regression assertions missing")
    for token in ("rejectedWorldBand=", "rejectedProgression=", "rejectedRegion=", "rejectedEvidence=", "skippedDensity=", "rejectedCap=", "eligibleStarter="):
        req(token in controller, f"forage selection diagnostic missing {token}")
    req("WorldThreatRuntime.Tick" in controller and "WorldThreatRuntime.Ready" in controller and "WorldThreatRuntime.Reset" in controller, "world snapshot lifecycle not wired into foraging")

    # 16 stable progression resources, including systemic Brake-sensitive Starleaf and 40s coverage.
    req("all.Count != 16" in catalog, "catalog self-test does not require 16 resources")
    for name in ("Meadow Reed", "Amberbark", "Sunpetal", "Ironvine Root", "Duskleaf", "Elder Resin", "Voidcap"):
        req(name in catalog, f"new forage identity missing: {name}")
    req('"Starleaf"' in catalog and "18, 46" in catalog and "ForageWorldBand.Mid" in catalog, "Starleaf progression/world band missing")
    req('"Duskleaf"' in catalog and "42, 76" in catalog and "ForageWorldBand.High" in catalog, "Duskleaf high-tier gate missing")
    req('"Elder Resin"' in catalog and "46, 82" in catalog and "ForageWorldBand.High" in catalog, "Elder Resin high-tier gate missing")
    req('"Voidcap"' in catalog and "49, 90" in catalog and "ForageWorldBand.High" in catalog, "Voidcap high-tier gate missing")

    # Five custom consumables are donor-backed; exact native effect is copied, never synthesized.
    for item_id in ("910030001", "910030002", "910030003", "910030004", "910030005"):
        req(item_id in ids, f"consumable id missing: {item_id}")
    for name in ("Field Tonic", "Wayfarer Tonic", "Cave Draught", "Starleaf Tonic", "Rootward Provision"):
        req(name in items, f"crafted consumable missing: {name}")
    for token in ("GeneralSlot", "Stackable", "Disposable", "HasClickEffect", "DirectHp", "FlatMana", "PercentManaRestoration", "TargetDamage", "BeneficialType", "SelfOnly", "IsHarmful", "IsProgressionCompatible", "PowerMagnitude"):
        req(token in consumable, f"consumable policy missing {token}")
    req("return facts.BeneficialType;" in consumable, "utility consumables not constrained to proven Beneficial type")
    req("facts.ManaCost > 0.0" in consumable and "!facts.SelfOnly" in consumable, "native consume-on-failed-cast guards missing")
    req("facts.ItemLevel > 0 && facts.ItemLevel > targetLevel" in consumable, "positive native item-level progression ceiling missing")
    req("facts.EffectRequiredLevel > 0 && facts.EffectRequiredLevel > targetLevel" in consumable, "positive effect-level progression ceiling missing")
    for token in ("FindSafeConsumableDonor", "ApplyConsumableSemantics", 'SetField(item, "ItemEffectOnClick", donorEffect)', 'SetField(item, "ItemIcon", donorIcon)', 'SetField(item, "SpellCastTime", donorCastTime)', "object.ReferenceEquals", "BuildNativeConsumableReferenceLines"):
        req(token in registry, f"native consumable donor preservation missing {token}")
    req("HP +=" not in registry and "Mana +=" not in registry, "manual fake recovery path introduced")
    req("ItemIcon.UseConsumable -> CastSpell.StartSpell(ItemEffectOnClick, targetStats, Item.SpellCastTime)" in registry, "native item-use diagnostic path missing")

    # 21 native physical template rows, including five consumables and late diverse inputs.
    req('Recipes.Count != 21' in recipes, "recipe self-test does not require 21 rows")
    for key in ("fr.field_tonic", "fr.wayfarer_tonic", "fr.cave_draught", "fr.starleaf_tonic", "fr.rootward_provision"):
        req(key in recipes, f"consumable recipe missing {key}")
    req("IronvineRootId" in recipes and "BlightrootId" in recipes, "late Root Temper did not deepen regional/root progression")
    req("ConsumableEconomyPolicy.HasNoObviousVendorValueInversion" in recipes, "consumable recipe vendor-value guard missing")

    for cmd in ('"/craftdiag worldtier"', '"/craftdiag resources"', '"/craftdiag consumables"', '"/craftdiag consumables native"', '"/craftdiag recipes"'):
        req(cmd in plugin, f"diagnostic command missing {cmd}")
    req('Version = "0.3.5"' in plugin, "version is not 0.3.5")

    # Preserve exact one-click RMB gathering and native recipe authority source anchors.
    req("TryHandleNativeRightClick" in controller and "BeginGather(" in controller, "RMB gather path missing")
    all_src = "\n".join(p.read_text(encoding="utf-8-sig") for p in (ROOT / "src").rglob("*.cs"))
    req("KeyCode.G" not in all_src, "G gather path returned")
    req("Smithing.Combine" in all_src and "DoSuccess" in all_src and "QuickSmith" in all_src, "native Smithing authority anchors missing")

    print("PASS world-tier/consumables static gate")
    print("Assembly-CSharp SHA256=" + sha)
    print("resources=16 consumables=5 productionRecipes=21 version=0.3.5")


if __name__ == "__main__":
    main()
