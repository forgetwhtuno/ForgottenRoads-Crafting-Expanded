namespace ErenshorCraftingExpanded
{
    public enum ForageActiveGatherClickAction
    {
        ConsumeForageClick = 0,
        CancelTypingPassThrough = 1,
        CancelUiPassThrough = 2,
        CancelWorldPassThrough = 3
    }

    // During an active one-click channel, a later RMB on a forage node cannot steal the captured
    // transaction. A genuine non-forage world RMB cancels the channel and passes through to native
    // Erenshor; UI/chat remain native owners.
    public static class ForageActiveGatherClickPolicy
    {
        public static ForageActiveGatherClickAction Evaluate(bool typing, bool conflictingUiOwnsPointer, bool pointsAtForage)
        {
            if (typing) return ForageActiveGatherClickAction.CancelTypingPassThrough;
            if (conflictingUiOwnsPointer) return ForageActiveGatherClickAction.CancelUiPassThrough;
            if (!pointsAtForage) return ForageActiveGatherClickAction.CancelWorldPassThrough;
            return ForageActiveGatherClickAction.ConsumeForageClick;
        }

        internal static string RunSelfTests()
        {
            if (Evaluate(false, false, true) != ForageActiveGatherClickAction.ConsumeForageClick)
                return "FAIL captured forage interaction should consume duplicate RMB";
            if (Evaluate(false, false, false) != ForageActiveGatherClickAction.CancelWorldPassThrough)
                return "FAIL non-forage RMB should cancel and remain native";
            if (Evaluate(true, false, true) != ForageActiveGatherClickAction.CancelTypingPassThrough)
                return "FAIL typing should cancel/pass through";
            if (Evaluate(false, true, true) != ForageActiveGatherClickAction.CancelUiPassThrough)
                return "FAIL UI should cancel/pass through";
            return "PASS active one-click gather RMB policy";
        }
    }
}
