namespace ErenshorCraftingExpanded
{
    // Reserved id namespace for this mod's custom items - see
    // docs/NATIVE_ITEM_REGISTRY_FINDINGS.md section 4 for the collision-avoidance rationale.
    public static class CraftingExpandedItemIds
    {
        public const long RangeStart = 910000000L;
        public const long RangeEnd = 910999999L;
        // Reserved subrange for mod-owned native Smithing Template Items. Production slots use
        // stable ids from ProductionRecipePlan; keeping resource and recipe-template ids disjoint
        // prevents gathered materials from ever being reused as recipe identities.
        public const long RecipeTemplateRangeStart = 910100000L;
        public const long RecipeTemplateRangeEnd = 910199999L;

        public const string WildHerbId = "910000001";
        public const string CaveMushroomId = "910000002";
        public const string WildBloomId = "910000003";
        public const string CaveMossId = "910000004";
        public const string BlightrootId = "910000005";
        public const string FieldFiberId = "910000006";
        public const string ResinSprigId = "910000007";
        public const string StarleafId = "910000008";
        public const string GhostcapId = "910000009";
        public const string MeadowReedId = "910000010";
        public const string AmberbarkId = "910000011";
        public const string SunpetalId = "910000012";
        public const string IronvineRootId = "910000013";
        public const string DuskleafId = "910000014";
        public const string ElderResinId = "910000015";
        public const string VoidcapId = "910000016";

        public const string WovenFiberCordId = "910010001";
        public const string HerbalBinderId = "910010002";
        public const string ResinSealedGripId = "910010003";
        public const string BloomTinctureId = "910010004";
        public const string CavePasteId = "910010005";
        public const string StarleafInfusionId = "910010006";
        public const string RootTemperId = "910010007";
        public const string GhostcapExtractId = "910010008";

        public const string FieldTonicId = "910030001";
        public const string WayfarerTonicId = "910030002";
        public const string CaveDraughtId = "910030003";
        public const string StarleafTonicId = "910030004";
        public const string RootwardProvisionId = "910030005";

        public const string TrailwoodCudgelId = "910020001";
        public const string BarkguardBucklerId = "910020002";
        public const string BriarKnifeId = "910020003";
        public const string BloomwoodStaffId = "910020004";
        public const string StarleafLongbowId = "910020005";
        public const string BlightrootBladeId = "910020006";
        public const string RootboundGuardId = "910020007";
        public const string GhostcapShivId = "910020008";

        public static bool IsInOwnedRange(string id)
        {
            long numeric;
            if (string.IsNullOrEmpty(id) || !long.TryParse(id, out numeric)) return false;
            return numeric >= RangeStart && numeric <= RangeEnd;
        }

        public static bool IsInRecipeTemplateRange(string id)
        {
            long numeric;
            if (string.IsNullOrEmpty(id) || !long.TryParse(id, out numeric)) return false;
            return numeric >= RecipeTemplateRangeStart && numeric <= RecipeTemplateRangeEnd;
        }
    }
}
