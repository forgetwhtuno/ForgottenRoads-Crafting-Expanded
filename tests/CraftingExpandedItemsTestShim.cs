namespace ErenshorCraftingExpanded
{
    // Test-only catalog surface used by ExpandedContentRecipes. The runtime implementation
    // is Harmony-backed and intentionally excluded from the pure-logic test compilation.
    internal static class CraftingExpandedItems
    {
        internal static readonly CustomItemRegistry Registry = Build();

        private static CustomItemRegistry Build()
        {
            CustomItemRegistry registry = new CustomItemRegistry();
            Add(registry, CraftingExpandedItemIds.WildHerbId, "Wild Herb");
            Add(registry, CraftingExpandedItemIds.CaveMushroomId, "Cave Mushroom");
            Add(registry, CraftingExpandedItemIds.WildBloomId, "Wild Bloom");
            Add(registry, CraftingExpandedItemIds.CaveMossId, "Cave Moss");
            Add(registry, CraftingExpandedItemIds.BlightrootId, "Blightroot");
            return registry;
        }

        private static void Add(CustomItemRegistry registry, string id, string name)
        {
            registry.TryDefine(new CustomItemDefinition
            {
                Id = id,
                Name = name,
                Value = 1,
                DefaultGrantQuantity = 1
            });
        }
    }
}
