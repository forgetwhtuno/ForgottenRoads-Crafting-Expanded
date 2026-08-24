using System;

namespace ErenshorCraftingExpanded
{
    public enum ItemSemanticFamily
    {
        RawResource = 0,
        ProcessedComponent = 1,
        FinishedEquipment = 2
    }

    // Pure-data policy describing the native Item fields Crafting Expanded owns for ordinary
    // inventory commodities. "Disposable" is intentionally false: current item evidence uses that
    // flag for consumables that are consumed on use, not for manual inventory destruction.
    public sealed class ItemSemanticsDecision
    {
        public ItemSemanticFamily Family;
        public bool Stackable;
        public bool PlayerCannotSell;
        public bool NoTradeNoDestroy;
        public bool Disposable;
        public bool MustBeEquippedToClick;
        public bool Unique;
        public bool Relic;
        public bool RareItem;
        public bool SimPlayersCantGet;
        public int ItemValue;
    }

    public static class ItemSemanticsPolicy
    {
        public static ItemSemanticsDecision ForRawResource(int declaredValue)
        {
            return Commodity(ItemSemanticFamily.RawResource, declaredValue);
        }

        public static ItemSemanticsDecision ForProcessedComponent(int declaredValue)
        {
            return Commodity(ItemSemanticFamily.ProcessedComponent, declaredValue);
        }

        public static ItemSemanticsDecision ForFinishedEquipment(int donorItemValue)
        {
            ItemSemanticsDecision d = Commodity(ItemSemanticFamily.FinishedEquipment, donorItemValue);
            d.Stackable = false;
            return d;
        }

        private static ItemSemanticsDecision Commodity(ItemSemanticFamily family, int itemValue)
        {
            ItemSemanticsDecision d = new ItemSemanticsDecision();
            d.Family = family;
            d.Stackable = family != ItemSemanticFamily.FinishedEquipment;
            d.PlayerCannotSell = false;
            d.NoTradeNoDestroy = false;
            d.Disposable = false;
            d.MustBeEquippedToClick = false;
            d.Unique = false;
            d.Relic = false;
            d.RareItem = false;
            d.SimPlayersCantGet = false;
            d.ItemValue = itemValue < 1 ? 1 : itemValue;
            return d;
        }

        // Components deliberately carry no per-unit intrinsic ItemValue markup over their ingredients.
        // Native smithing can produce more than one General-slot output depending on fuel, so this
        // is intentionally only a per-unit guard; full buy/craft/sell economics remain native and
        // must be validated against live fuel costs, output quantity, and merchant payout.
        public static bool IsConservativeProcessedValue(int outputValue, int totalInputValue)
        {
            return outputValue > 0 && totalInputValue > 0 && outputValue == totalInputValue;
        }

        public static bool IsOrdinaryInventorySemantics(
            ItemSemanticsDecision expected,
            bool stackable,
            bool playerCannotSell,
            bool noTradeNoDestroy,
            bool disposable,
            bool mustBeEquippedToClick,
            bool unique,
            bool relic,
            bool rareItem,
            bool simPlayersCantGet,
            int itemValue)
        {
            if (expected == null) return false;
            return stackable == expected.Stackable &&
                playerCannotSell == expected.PlayerCannotSell &&
                noTradeNoDestroy == expected.NoTradeNoDestroy &&
                disposable == expected.Disposable &&
                mustBeEquippedToClick == expected.MustBeEquippedToClick &&
                unique == expected.Unique &&
                relic == expected.Relic &&
                rareItem == expected.RareItem &&
                simPlayersCantGet == expected.SimPlayersCantGet &&
                itemValue == expected.ItemValue;
        }

        internal static string RunSelfTests()
        {
            ItemSemanticsDecision raw = ForRawResource(1);
            if (!raw.Stackable || raw.PlayerCannotSell || raw.NoTradeNoDestroy || raw.Disposable ||
                raw.MustBeEquippedToClick || raw.Unique || raw.Relic || raw.RareItem || raw.ItemValue != 1)
                return "FAIL raw resource inventory semantics";

            ItemSemanticsDecision component = ForProcessedComponent(7);
            if (!component.Stackable || component.PlayerCannotSell || component.NoTradeNoDestroy ||
                component.Disposable || component.ItemValue != 7)
                return "FAIL processed component inventory semantics";

            ItemSemanticsDecision equipment = ForFinishedEquipment(123);
            if (equipment.Stackable || equipment.PlayerCannotSell || equipment.NoTradeNoDestroy ||
                equipment.Disposable || equipment.ItemValue != 123)
                return "FAIL finished equipment inventory semantics";

            if (!IsConservativeProcessedValue(3, 3) || IsConservativeProcessedValue(4, 3) ||
                IsConservativeProcessedValue(2, 3) || IsConservativeProcessedValue(0, 3))
                return "FAIL processed component economy guard";

            if (!IsOrdinaryInventorySemantics(raw, true, false, false, false, false, false, false, false, false, 1))
                return "FAIL ordinary semantic verifier";
            if (IsOrdinaryInventorySemantics(raw, true, true, false, false, false, false, false, false, false, 1))
                return "FAIL quest/no-sell flag leak accepted";
            if (IsOrdinaryInventorySemantics(raw, true, false, true, false, false, false, false, false, false, 1))
                return "FAIL no-destroy flag leak accepted";
            if (IsOrdinaryInventorySemantics(raw, true, false, false, false, false, false, false, false, true, 1))
                return "FAIL SimPlayersCantGet special restriction leak accepted";

            return "PASS item semantics policy";
        }
    }
}
