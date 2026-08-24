using System;
using System.Collections.Generic;

namespace ErenshorCraftingExpanded
{
    public static class ExpandedContentRecipes
    {
        private static readonly List<CustomRecipeDefinition> Recipes = Build();
        private static readonly IList<CustomRecipeDefinition> ReadOnlyRecipes = Recipes.AsReadOnly();
        public static IList<CustomRecipeDefinition> All { get { return ReadOnlyRecipes; } }

        private static List<CustomRecipeDefinition> Build()
        {
            List<CustomRecipeDefinition> result = new List<CustomRecipeDefinition>();
            result.Add(Recipe("fr.field_cord", "910110001", "Woven Fiber Cord", CraftingExpandedItemIds.WovenFiberCordId, 1, 3,
                Ingredients(CraftingExpandedItemIds.FieldFiberId, 2), Discoveries("field_fiber")));
            result.Add(Recipe("fr.herbal_binder", "910110002", "Herbal Binder", CraftingExpandedItemIds.HerbalBinderId, 3, 3,
                Ingredients(CraftingExpandedItemIds.WildHerbId, 2, CraftingExpandedItemIds.FieldFiberId, 1), Discoveries("wild_herb", "field_fiber")));
            result.Add(Recipe("fr.field_tonic", "910110017", "Field Tonic", CraftingExpandedItemIds.FieldTonicId, 4, 3,
                Ingredients(CraftingExpandedItemIds.WildHerbId, 3, CraftingExpandedItemIds.FieldFiberId, 2), Discoveries("wild_herb", "field_fiber")));
            result.Add(Recipe("fr.resin_grip", "910110003", "Resin-Sealed Grip", CraftingExpandedItemIds.ResinSealedGripId, 5, 6,
                Ingredients(CraftingExpandedItemIds.ResinSprigId, 2, CraftingExpandedItemIds.WovenFiberCordId, 1), Discoveries("resin_sprig", "field_fiber")));
            result.Add(Recipe("fr.trailwood_cudgel", "910110004", "Trailwood Cudgel", CraftingExpandedItemIds.TrailwoodCudgelId, 6, 6,
                Ingredients(CraftingExpandedItemIds.ResinSealedGripId, 1, CraftingExpandedItemIds.HerbalBinderId, 1), Discoveries("resin_sprig", "wild_herb")));
            result.Add(Recipe("fr.barkguard_buckler", "910110005", "Barkguard Maul", CraftingExpandedItemIds.BarkguardBucklerId, 8, 6,
                Ingredients(CraftingExpandedItemIds.WovenFiberCordId, 2, CraftingExpandedItemIds.ResinSprigId, 2), Discoveries("field_fiber", "resin_sprig")));

            result.Add(Recipe("fr.bloom_tincture", "910110006", "Bloom Tincture", CraftingExpandedItemIds.BloomTinctureId, 10, 14,
                Ingredients(CraftingExpandedItemIds.WildBloomId, 2, CraftingExpandedItemIds.WildHerbId, 1), Discoveries("wild_bloom")));
            result.Add(Recipe("fr.briar_knife", "910110007", "Briar Knife", CraftingExpandedItemIds.BriarKnifeId, 12, 14,
                Ingredients(CraftingExpandedItemIds.ResinSealedGripId, 1, CraftingExpandedItemIds.BloomTinctureId, 1), Discoveries("wild_bloom", "resin_sprig")));
            result.Add(Recipe("fr.wayfarer_tonic", "910110018", "Wayfarer Tonic", CraftingExpandedItemIds.WayfarerTonicId, 13, 14,
                Ingredients(CraftingExpandedItemIds.AmberbarkId, 2, CraftingExpandedItemIds.MeadowReedId, 2, CraftingExpandedItemIds.HerbalBinderId, 1), Discoveries("amberbark", "meadow_reed")));
            result.Add(Recipe("fr.starleaf_infusion", "910110008", "Starleaf Infusion", CraftingExpandedItemIds.StarleafInfusionId, 17, 18,
                Ingredients(CraftingExpandedItemIds.StarleafId, 2, CraftingExpandedItemIds.WildBloomId, 1), Discoveries("starleaf", "wild_bloom")));
            result.Add(Recipe("fr.bloomwood_staff", "910110009", "Bloomwood Staff", CraftingExpandedItemIds.BloomwoodStaffId, 18, 18,
                Ingredients(CraftingExpandedItemIds.ResinSealedGripId, 1, CraftingExpandedItemIds.StarleafInfusionId, 1, CraftingExpandedItemIds.ResinSprigId, 2), Discoveries("starleaf", "resin_sprig")));

            result.Add(Recipe("fr.cave_paste", "910110010", "Cave Paste", CraftingExpandedItemIds.CavePasteId, 22, 24,
                Ingredients(CraftingExpandedItemIds.CaveMushroomId, 2, CraftingExpandedItemIds.CaveMossId, 1), Discoveries("cave_mushroom", "cave_moss")));
            result.Add(Recipe("fr.cave_draught", "910110019", "Cave Draught", CraftingExpandedItemIds.CaveDraughtId, 25, 24,
                Ingredients(CraftingExpandedItemIds.CaveMushroomId, 2, CraftingExpandedItemIds.CaveMossId, 2, CraftingExpandedItemIds.CavePasteId, 1), Discoveries("cave_mushroom", "cave_moss")));
            result.Add(Recipe("fr.ghostcap_extract", "910110011", "Ghostcap Extract", CraftingExpandedItemIds.GhostcapExtractId, 28, 30,
                Ingredients(CraftingExpandedItemIds.GhostcapId, 2, CraftingExpandedItemIds.CaveMushroomId, 1), Discoveries("ghostcap", "cave_mushroom")));
            result.Add(Recipe("fr.starleaf_longbow", "910110012", "Starleaf Longbow", CraftingExpandedItemIds.StarleafLongbowId, 29, 30,
                Ingredients(CraftingExpandedItemIds.ResinSealedGripId, 2, CraftingExpandedItemIds.WovenFiberCordId, 2, CraftingExpandedItemIds.StarleafInfusionId, 1), Discoveries("starleaf", "field_fiber")));
            result.Add(Recipe("fr.ghostcap_shiv", "910110013", "Ghostcap Shiv", CraftingExpandedItemIds.GhostcapShivId, 30, 30,
                Ingredients(CraftingExpandedItemIds.ResinSealedGripId, 1, CraftingExpandedItemIds.GhostcapExtractId, 1, CraftingExpandedItemIds.CavePasteId, 1), Discoveries("ghostcap", "cave_moss")));

            result.Add(Recipe("fr.starleaf_tonic", "910110020", "Starleaf Tonic", CraftingExpandedItemIds.StarleafTonicId, 31, 30,
                Ingredients(CraftingExpandedItemIds.StarleafId, 2, CraftingExpandedItemIds.SunpetalId, 2, CraftingExpandedItemIds.StarleafInfusionId, 1), Discoveries("starleaf", "sunpetal")));
            result.Add(Recipe("fr.root_temper", "910110014", "Root Temper", CraftingExpandedItemIds.RootTemperId, 34, 36,
                Ingredients(CraftingExpandedItemIds.BlightrootId, 2, CraftingExpandedItemIds.IronvineRootId, 1), Discoveries("blightroot", "ironvine_root")));
            result.Add(Recipe("fr.blightroot_blade", "910110015", "Blightroot Blade", CraftingExpandedItemIds.BlightrootBladeId, 36, 36,
                Ingredients(CraftingExpandedItemIds.RootTemperId, 1, CraftingExpandedItemIds.ResinSealedGripId, 1, CraftingExpandedItemIds.StarleafInfusionId, 1), Discoveries("blightroot", "starleaf")));
            result.Add(Recipe("fr.rootbound_guard", "910110016", "Rootbound Guard", CraftingExpandedItemIds.RootboundGuardId, 38, 36,
                Ingredients(CraftingExpandedItemIds.RootTemperId, 1, CraftingExpandedItemIds.WovenFiberCordId, 2, CraftingExpandedItemIds.ResinSprigId, 1), Discoveries("blightroot", "field_fiber")));
            result.Add(Recipe("fr.rootward_provision", "910110021", "Rootward Provision", CraftingExpandedItemIds.RootwardProvisionId, 49, 49,
                Ingredients(CraftingExpandedItemIds.DuskleafId, 1, CraftingExpandedItemIds.ElderResinId, 1, CraftingExpandedItemIds.VoidcapId, 1), Discoveries("duskleaf", "elder_resin", "voidcap")));
            return result;
        }

        private static CustomRecipeDefinition Recipe(string key, string templateId, string name, string outputId, int crafting, int foraging,
            List<CustomRecipeIngredient> ingredients, List<string> discoveries)
        {
            CustomRecipeDefinition d = new CustomRecipeDefinition();
            d.RecipeKey = key; d.TemplateItemId = templateId; d.DisplayName = name; d.OutputItemId = outputId;
            d.MinimumCraftingLevel = crafting; d.MinimumForagingLevel = foraging;
            for (int i = 0; i < ingredients.Count; i++) d.Ingredients.Add(ingredients[i]);
            for (int i = 0; i < discoveries.Count; i++) d.RequiredDiscoveries.Add(discoveries[i]);
            return d;
        }

        private static List<CustomRecipeIngredient> Ingredients(params object[] values)
        {
            List<CustomRecipeIngredient> list = new List<CustomRecipeIngredient>();
            for (int i = 0; i + 1 < values.Length; i += 2) list.Add(new CustomRecipeIngredient((string)values[i], (int)values[i + 1]));
            return list;
        }

        private static List<string> Discoveries(params string[] values) { return new List<string>(values); }

        private static int CatalogValue(string itemId)
        {
            CustomItemDefinition raw = CraftingExpandedItems.Registry.Get(itemId);
            if (raw != null) return raw.Value;
            ExpandedItemDefinition expanded = ExpandedContentItems.Get(itemId);
            return expanded == null ? 0 : expanded.Value;
        }

        internal static string RunSelfTests()
        {
            if (Recipes.Count != 21) return "FAIL expanded recipe count";
            HashSet<string> keys = new HashSet<string>(StringComparer.Ordinal); HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
            int lastCrafting = 0;
            for (int i = 0; i < Recipes.Count; i++)
            {
                CustomRecipeDefinition d = Recipes[i];
                if (CustomRecipeCatalog.ValidateShape(d) != CustomRecipeRejectReason.None) return "FAIL expanded recipe shape " + d.RecipeKey + " " + CustomRecipeCatalog.ValidateShape(d);
                if (!keys.Add(d.RecipeKey) || !ids.Add(d.TemplateItemId)) return "FAIL expanded recipe duplicate identity";
                if (d.Ingredients.Count > 3) return "FAIL expanded recipe distinct ingredient bound";
                if (d.MinimumCraftingLevel < lastCrafting) return "FAIL expanded recipe crafting order";
                lastCrafting = d.MinimumCraftingLevel;
                ExpandedItemDefinition output = ExpandedContentItems.Get(d.OutputItemId);
                if (output == null) return "FAIL expanded recipe output not mod-owned content";
                int totalInputValue = 0;
                for (int j = 0; j < d.Ingredients.Count; j++)
                {
                    string ingredientId = d.Ingredients[j].ItemId;
                    if (ForageResourceCatalog.FindByRewardItemId(ingredientId) == null && ExpandedContentItems.Get(ingredientId) == null)
                        return "FAIL expanded recipe ingredient missing " + ingredientId;
                    int inputValue = CatalogValue(ingredientId);
                    if (inputValue <= 0) return "FAIL expanded recipe ingredient value missing " + ingredientId;
                    totalInputValue += inputValue * d.Ingredients[j].Quantity;
                }
                if (output.Kind == ExpandedItemKind.Material && string.Equals(output.Category, "component", StringComparison.Ordinal) &&
                    !ItemSemanticsPolicy.IsConservativeProcessedValue(output.Value, totalInputValue))
                    return "FAIL processed component value must equal ingredient value " + d.RecipeKey;
                if (output.Kind == ExpandedItemKind.Consumable && !ConsumableEconomyPolicy.HasNoObviousVendorValueInversion(output.Value, totalInputValue))
                    return "FAIL obvious consumable vendor-value inversion " + d.RecipeKey;
            }
            return "PASS expanded content recipes";
        }
    }
}
