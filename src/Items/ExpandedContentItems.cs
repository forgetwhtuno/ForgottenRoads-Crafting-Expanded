using System;
using System.Collections.Generic;

namespace ErenshorCraftingExpanded
{
    // Stable content manifest. Material identities are inert ordinary commodities. Equipment clones
    // a conservative current-ItemDB donor in the requested family. Consumables clone a proven native
    // usable-item donor and deliberately preserve its ItemEffectOnClick/native use machinery.
    public static class ExpandedContentItems
    {
        private static readonly List<ExpandedItemDefinition> Items = Build();
        private static readonly IList<ExpandedItemDefinition> ReadOnlyItems = Items.AsReadOnly();

        public static IList<ExpandedItemDefinition> All { get { return ReadOnlyItems; } }

        public static ExpandedItemDefinition Get(string id)
        {
            for (int i = 0; i < Items.Count; i++)
                if (string.Equals(Items[i].Id, id, StringComparison.Ordinal)) return Items[i];
            return null;
        }

        private static List<ExpandedItemDefinition> Build()
        {
            List<ExpandedItemDefinition> result = new List<ExpandedItemDefinition>();

            // Raw resources beyond the five original CustomItemDefinition resources. New 0.3.0
            // identities intentionally retain a verified native donor icon rather than inventing
            // palette-swapped art merely to fill the catalog.
            result.Add(Material(CraftingExpandedItemIds.FieldFiberId, "Field Fiber", "A bundle of tough field fibers suited to cordage.", "resource/fiber", 1, "common", 1, "field_fiber.png", "A tied bundle of pale green and straw-colored fantasy plant fibers."));
            result.Add(Material(CraftingExpandedItemIds.ResinSprigId, "Resin Sprig", "A resin-rich woody sprig useful for grips and binding compounds.", "resource/wood", 1, "common", 1, "resin_sprig.png", "A short dark fantasy twig with amber resin beads."));
            result.Add(Material(CraftingExpandedItemIds.StarleafId, "Starleaf", "A scarce luminous-edged leaf used in precise refinements.", "resource/herb", 2, "rare", 3, "starleaf.png", "A single five-point fantasy leaf with faint cool magical veins."));
            result.Add(Material(CraftingExpandedItemIds.GhostcapId, "Ghostcap", "A rare pale cave fungus used in concentrated extracts.", "resource/fungus", 3, "rare", 3, "ghostcap.png", "A pale translucent cave mushroom cap with a faint spectral glow."));
            result.Add(MaterialNoArt(CraftingExpandedItemIds.MeadowReedId, "Meadow Reed", "A springy reed used in travel cordage and field preparations.", "resource/fiber", 1, "common", 1));
            result.Add(MaterialNoArt(CraftingExpandedItemIds.AmberbarkId, "Amberbark", "Thin resinous bark cut from hardy shrubs and saplings.", "resource/wood", 2, "uncommon", 1));
            result.Add(MaterialNoArt(CraftingExpandedItemIds.SunpetalId, "Sunpetal", "A warm-colored flower prized for stable mid-tier preparations.", "resource/flower", 2, "uncommon", 1));
            result.Add(MaterialNoArt(CraftingExpandedItemIds.IronvineRootId, "Ironvine Root", "A dense fibrous root that takes tempering compounds well.", "resource/root", 3, "uncommon", 2));
            result.Add(MaterialNoArt(CraftingExpandedItemIds.DuskleafId, "Duskleaf", "A dark late-growth herb found where ordinary hostile life is dangerous.", "resource/herb", 5, "rare", 2));
            result.Add(MaterialNoArt(CraftingExpandedItemIds.ElderResinId, "Elder Resin", "Heavy resin gathered from mature high-threat growth.", "resource/wood", 5, "rare", 2));
            result.Add(MaterialNoArt(CraftingExpandedItemIds.VoidcapId, "Voidcap", "A rare late cave fungus whose native-world habitat is exceptionally dangerous.", "resource/fungus", 5, "rare", 2));

            // Processed intermediates.
            result.Add(Material(CraftingExpandedItemIds.WovenFiberCordId, "Woven Fiber Cord", "Field fibers twisted into dependable workshop cord.", "component", 1, "common", 2, "woven_fiber_cord.png", "A compact coil of braided natural cord."));
            result.Add(Material(CraftingExpandedItemIds.HerbalBinderId, "Herbal Binder", "A sticky herbal binding compound for simple crafted equipment.", "component", 1, "common", 3, "herbal_binder.png", "A small stoppered ceramic jar with green herbal paste."));
            result.Add(Material(CraftingExpandedItemIds.ResinSealedGripId, "Resin-Sealed Grip", "A cord-wrapped grip hardened with natural resin.", "component", 1, "uncommon", 4, "resin_sealed_grip.png", "A short leather-and-cord weapon grip sealed with amber resin."));
            result.Add(Material(CraftingExpandedItemIds.BloomTinctureId, "Bloom Tincture", "A concentrated preparation that stabilizes flexible crafted materials.", "component", 2, "uncommon", 3, "bloom_tincture.png", "A small fantasy glass vial of warm floral tincture."));
            result.Add(Material(CraftingExpandedItemIds.CavePasteId, "Cave Paste", "Mushroom and moss reduced to a dense cave-working compound.", "component", 3, "uncommon", 3, "cave_paste.png", "A squat stone cup containing pale green-gray cave paste."));
            result.Add(Material(CraftingExpandedItemIds.StarleafInfusionId, "Starleaf Infusion", "A carefully steeped infusion for higher-grade flexible equipment.", "component", 2, "rare", 7, "starleaf_infusion.png", "A narrow fantasy vial with luminous blue-green herbal infusion."));
            result.Add(Material(CraftingExpandedItemIds.RootTemperId, "Root Temper", "Root and vine resin refined into a hardening agent for late workshop pieces.", "component", 4, "rare", 4, "root_temper.png", "A dark iron cup holding deep red-brown root resin."));
            result.Add(Material(CraftingExpandedItemIds.GhostcapExtractId, "Ghostcap Extract", "A volatile concentrated cave-fungus extract used in specialist blades.", "component", 3, "rare", 7, "ghostcap_extract.png", "A small violet-gray fantasy vial with ghostly fungal extract."));

            // Real usable items. Registration is fail-closed: each definition is activated only if
            // the current live ItemDB supplies a proven self-usable native donor in the requested
            // effect family. Positive native item/effect levels are progression ceilings; otherwise
            // the selector uses bounded same-family effect magnitude. The donor Spell, icon and item
            // cast time are preserved verbatim.
            result.Add(Consumable(CraftingExpandedItemIds.FieldTonicId, "Field Tonic", "A simple field tonic that preserves a proven native direct-health recovery effect.", 1, "common", 6, 1, NativeConsumableFamily.HealthRecovery));
            result.Add(Consumable(CraftingExpandedItemIds.WayfarerTonicId, "Wayfarer Tonic", "A travel tonic that preserves a proven native direct-health recovery effect appropriate to its tier.", 2, "uncommon", 14, 1, NativeConsumableFamily.HealthRecovery));
            result.Add(Consumable(CraftingExpandedItemIds.CaveDraughtId, "Cave Draught", "A cave preparation that preserves a proven native direct-mana recovery effect.", 3, "uncommon", 24, 1, NativeConsumableFamily.ManaRecovery));
            result.Add(Consumable(CraftingExpandedItemIds.StarleafTonicId, "Starleaf Tonic", "A refined tonic that preserves a stronger ordinary native direct-health recovery effect.", 4, "rare", 32, 2, NativeConsumableFamily.HealthRecovery));
            result.Add(Consumable(CraftingExpandedItemIds.RootwardProvisionId, "Rootward Provision", "A late-progression provision that preserves a proven native self-only beneficial use effect, including its status payload when present.", 5, "rare", 35, 1, NativeConsumableFamily.Utility));

            // Equipment uses current native donors as conservative balance anchors.
            result.Add(Equipment(CraftingExpandedItemIds.TrailwoodCudgelId, "Trailwood Cudgel", "A practical resin-bound one-handed cudgel for early expeditions.", 1, "common", 6, "PrimaryOrSecondary", "OneHandMelee", false, "trailwood_cudgel.png", "A sturdy fantasy hardwood cudgel wrapped with cord and amber resin."));
            result.Add(Equipment(CraftingExpandedItemIds.BarkguardBucklerId, "Barkguard Maul", "A two-handed field maul reinforced with resin-bound fiber and barkwood.", 1, "common", 8, "Primary", "TwoHandMelee", false, "barkguard_maul.png", "A weathered two-handed barkwood maul with fiber lashings and a heavy iron-capped head."));
            result.Add(Equipment(CraftingExpandedItemIds.BriarKnifeId, "Briar Knife", "A light field knife balanced with bloom-treated bindings.", 2, "uncommon", 12, "PrimaryOrSecondary", "OneHandDagger", false, "briar_knife.png", "A narrow fantasy dagger with thorn-like guard and wrapped natural grip."));
            result.Add(Equipment(CraftingExpandedItemIds.BloomwoodStaffId, "Bloomwood Staff", "A long staff whose flexible bindings are stabilized with bloom and starleaf preparations.", 2, "uncommon", 18, "Primary", "TwoHandStaff", false, "bloomwood_staff.png", "A tall fantasy wooden staff with flowering knots and subtle starleaf accents."));
            result.Add(Equipment(CraftingExpandedItemIds.StarleafLongbowId, "Starleaf Longbow", "A carefully tensioned longbow treated with starleaf infusion.", 3, "rare", 28, "Primary", "TwoHandBow", false, "starleaf_longbow.png", "A graceful fantasy longbow of dark wood with faint cool leaf inlays."));
            result.Add(Equipment(CraftingExpandedItemIds.GhostcapShivId, "Ghostcap Shiv", "A specialist cave knife finished with concentrated Ghostcap extract.", 3, "rare", 30, "PrimaryOrSecondary", "OneHandDagger", false, "ghostcap_shiv.png", "A compact dark fantasy dagger with pale mushroom-like pommel and spectral edge accent."));
            result.Add(Equipment(CraftingExpandedItemIds.BlightrootBladeId, "Blightroot Blade", "A late-progression one-handed blade hardened with refined root resin.", 4, "rare", 32, "PrimaryOrSecondary", "OneHandMelee", false, "blightroot_blade.png", "A dark fantasy one-handed blade with root-wrapped hilt and deep crimson resin veins."));
            result.Add(Equipment(CraftingExpandedItemIds.RootboundGuardId, "Rootbound Guard", "A heavy compact shield reinforced with root temper.", 4, "rare", 32, "Secondary", "", true, "rootbound_guard.png", "A dark wood-and-metal fantasy shield bound by twisted roots."));
            return result;
        }

        private static ExpandedItemDefinition Material(string id, string name, string lore, string category, int tier, string rarity, int value, string file, string visual)
        {
            ExpandedItemDefinition d = MaterialNoArt(id, name, lore, category, tier, rarity, value);
            d.IconAssetPath = "assets/icons/" + file; d.IconDescription = visual;
            return d;
        }

        private static ExpandedItemDefinition MaterialNoArt(string id, string name, string lore, string category, int tier, string rarity, int value)
        {
            ExpandedItemDefinition d = new ExpandedItemDefinition();
            d.Id = id; d.Name = name; d.Lore = lore; d.Category = category; d.Tier = tier; d.Rarity = rarity; d.Value = value;
            d.Kind = ExpandedItemKind.Material; d.IconAssetPath = string.Empty; d.IconDescription = "Retains the verified native material donor icon.";
            return d;
        }

        private static ExpandedItemDefinition Consumable(string id, string name, string lore, int tier, string rarity, int itemLevel, int value, NativeConsumableFamily family)
        {
            ExpandedItemDefinition d = new ExpandedItemDefinition();
            d.Id = id; d.Name = name; d.Lore = lore; d.Category = "consumable"; d.Tier = tier; d.Rarity = rarity; d.Value = value;
            d.Kind = ExpandedItemKind.Consumable; d.TargetItemLevel = itemLevel; d.ConsumableFamily = family;
            d.IconAssetPath = string.Empty; d.IconDescription = "Retains the proven native consumable donor icon so its presentation remains truthful.";
            return d;
        }

        private static ExpandedItemDefinition Equipment(string id, string name, string lore, int tier, string rarity, int itemLevel,
            string slot, string weaponType, bool shield, string file, string visual)
        {
            ExpandedItemDefinition d = new ExpandedItemDefinition();
            d.Id = id; d.Name = name; d.Lore = lore; d.Category = "equipment"; d.Tier = tier; d.Rarity = rarity; d.Value = 0;
            d.Kind = ExpandedItemKind.Equipment; d.TargetItemLevel = itemLevel; d.RequiredSlot = slot; d.WeaponType = weaponType; d.RequireShield = shield;
            d.IconAssetPath = "assets/icons/" + file; d.IconDescription = visual;
            return d;
        }

        internal static string RunSelfTests()
        {
            if (Items.Count != 32) return "FAIL expanded item count";
            HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
            HashSet<string> paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            int materials = 0; int equipment = 0; int consumables = 0;
            for (int i = 0; i < Items.Count; i++)
            {
                ExpandedItemDefinition d = Items[i];
                if (d == null || string.IsNullOrEmpty(d.Name) || !CraftingExpandedItemIds.IsInOwnedRange(d.Id)) return "FAIL expanded item identity";
                if (CraftingExpandedItemIds.IsInRecipeTemplateRange(d.Id)) return "FAIL expanded item overlaps recipe range";
                if (!ids.Add(d.Id)) return "FAIL expanded item duplicate id";
                if (!string.IsNullOrEmpty(d.IconAssetPath))
                {
                    if (!d.IconAssetPath.StartsWith("assets/icons/", StringComparison.Ordinal) || !d.IconAssetPath.EndsWith(".png", StringComparison.OrdinalIgnoreCase)) return "FAIL expanded item icon path";
                    if (!paths.Add(d.IconAssetPath)) return "FAIL expanded item duplicate icon path";
                }
                if (d.Value < 0) return "FAIL expanded item value";
                if (d.Kind == ExpandedItemKind.Material) materials++;
                else if (d.Kind == ExpandedItemKind.Equipment)
                {
                    equipment++;
                    if (d.TargetItemLevel < 1 || d.TargetItemLevel > 35 || string.IsNullOrEmpty(d.RequiredSlot)) return "FAIL expanded equipment donor target";
                }
                else if (d.Kind == ExpandedItemKind.Consumable)
                {
                    consumables++;
                    if (d.TargetItemLevel < 1 || d.TargetItemLevel > 35 || d.Value < 1) return "FAIL expanded consumable donor target";
                }
                else return "FAIL expanded item kind";
            }
            if (materials != 19 || equipment != 8 || consumables != 5) return "FAIL expanded item family counts";
            if (Get(CraftingExpandedItemIds.FieldTonicId).ConsumableFamily != NativeConsumableFamily.HealthRecovery ||
                Get(CraftingExpandedItemIds.WayfarerTonicId).ConsumableFamily != NativeConsumableFamily.HealthRecovery ||
                Get(CraftingExpandedItemIds.CaveDraughtId).ConsumableFamily != NativeConsumableFamily.ManaRecovery ||
                Get(CraftingExpandedItemIds.StarleafTonicId).ConsumableFamily != NativeConsumableFamily.HealthRecovery ||
                Get(CraftingExpandedItemIds.RootwardProvisionId).ConsumableFamily != NativeConsumableFamily.Utility)
                return "FAIL consumable family policy";
            if (Get(CraftingExpandedItemIds.TrailwoodCudgelId).RequiredSlot != "PrimaryOrSecondary" ||
                Get(CraftingExpandedItemIds.BriarKnifeId).RequiredSlot != "PrimaryOrSecondary" ||
                Get(CraftingExpandedItemIds.GhostcapShivId).RequiredSlot != "PrimaryOrSecondary" ||
                Get(CraftingExpandedItemIds.BlightrootBladeId).RequiredSlot != "PrimaryOrSecondary")
                return "FAIL one-hand donor slot semantics";
            ExpandedItemDefinition barkguard = Get(CraftingExpandedItemIds.BarkguardBucklerId);
            if (barkguard == null || barkguard.RequireShield || barkguard.RequiredSlot != "Primary" || barkguard.WeaponType != "TwoHandMelee" || barkguard.TargetItemLevel != 8)
                return "FAIL Barkguard safe-family rework";
            return "PASS expanded content items";
        }
    }
}
