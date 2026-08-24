using System;

namespace ErenshorCraftingExpanded
{
    // One-click gather channels are not owned by a physical mouse-button hold. Competing UI is
    // still a real interruption, while releasing RMB after the selecting click is explicitly safe.
    public static class ForageGatherChannelPolicy
    {
        public const float MovementCancelDistance = 0.10f;

        public static ForageGatherCancelReason EvaluateUi(bool conflictingUiOwnsPointer)
        {
            return conflictingUiOwnsPointer ? ForageGatherCancelReason.UiOwned : ForageGatherCancelReason.None;
        }

        public static bool HasMeaningfulMovement(
            float startX, float startY, float startZ,
            float currentX, float currentY, float currentZ)
        {
            float dx = currentX - startX;
            float dy = currentY - startY;
            float dz = currentZ - startZ;
            float squared = dx * dx + dy * dy + dz * dz;
            float toleranceSquared = MovementCancelDistance * MovementCancelDistance;
            return !float.IsNaN(squared) && !float.IsInfinity(squared) && squared > toleranceSquared;
        }

        public static bool HasActualHpDecrease(int previousHp, int currentHp)
        {
            return previousHp >= 0 && currentHp >= 0 && currentHp < previousHp;
        }

        internal static string RunSelfTests()
        {
            if (EvaluateUi(false) != ForageGatherCancelReason.None) return "FAIL clean one-click channel cancelled";
            if (EvaluateUi(true) != ForageGatherCancelReason.UiOwned) return "FAIL competing UI ownership did not cancel";
            if (HasMeaningfulMovement(0f, 0f, 0f, 0.03f, 0.01f, 0.02f)) return "FAIL idle jitter cancelled channel";
            if (!HasMeaningfulMovement(0f, 0f, 0f, 0.11f, 0f, 0f)) return "FAIL meaningful movement not detected";
            if (!HasActualHpDecrease(100, 99)) return "FAIL actual HP decrease not detected";
            if (HasActualHpDecrease(100, 100)) return "FAIL zero damage cancelled";
            if (HasActualHpDecrease(100, 105)) return "FAIL healing cancelled";
            return "PASS forage one-click channel interruption policy";
        }
    }
}
