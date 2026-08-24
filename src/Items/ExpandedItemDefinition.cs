namespace ErenshorCraftingExpanded
{
    public enum ExpandedItemKind
    {
        Material = 0,
        Equipment = 1,
        Consumable = 2
    }

    public enum NativeConsumableFamily
    {
        AnyRecovery = 0,
        HealthRecovery = 1,
        ManaRecovery = 2,
        Utility = 3
    }

    public sealed class ExpandedItemDefinition
    {
        public string Id = string.Empty;
        public string Name = string.Empty;
        public string Lore = string.Empty;
        public string Category = string.Empty;
        public string Rarity = string.Empty;
        public int Tier = 1;
        public int Value = 1;
        public ExpandedItemKind Kind = ExpandedItemKind.Material;
        public string RequiredSlot = string.Empty;
        public string WeaponType = string.Empty;
        public bool RequireShield;
        public int TargetItemLevel;
        public NativeConsumableFamily ConsumableFamily = NativeConsumableFamily.AnyRecovery;
        public string IconAssetPath = string.Empty;
        public string IconDescription = string.Empty;
        public string FallbackBehavior = "Retain the verified native donor icon if the mod icon cannot be loaded.";
    }
}
