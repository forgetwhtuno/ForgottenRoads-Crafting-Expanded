using System;

namespace ErenshorCraftingExpanded
{
    public static class ForageHighlightPolicy
    {
        public const int SegmentCount = 28;
        public const float MinimumRadius = 0.34f;
        public const float MaximumRadius = 0.86f;
        public const float FootprintPadding = 0.10f;
        public const float OuterWidth = 0.070f;
        public const float InnerWidth = 0.030f;
        public const float HoverWidthMultiplier = 0.82f;
        public const float SelectedWidthMultiplier = 1.10f;
        public const float ReadyWidthMultiplier = 1.18f;

        public static float CalculateRadius(float visualDimension)
        {
            if (float.IsNaN(visualDimension) || float.IsInfinity(visualDimension) || visualDimension <= 0f) return MinimumRadius;
            float radius = visualDimension * 0.5f + FootprintPadding;
            if (radius < MinimumRadius) radius = MinimumRadius;
            if (radius > MaximumRadius) radius = MaximumRadius;
            return radius;
        }

        public static float WidthMultiplier(bool hovered, bool selected, bool ready)
        {
            if (selected && ready) return ReadyWidthMultiplier;
            if (selected) return SelectedWidthMultiplier;
            if (hovered) return HoverWidthMultiplier;
            return 1f;
        }

        public static bool ShowOuter(bool available, bool selected)
        {
            return available && selected;
        }

        public static bool ShowInner(bool available, bool hovered, bool selected)
        {
            return available && (selected || hovered);
        }

        internal static string RunSelfTests()
        {
            if (SegmentCount < 20 || SegmentCount > 40) return "FAIL highlight segment budget";
            if (Math.Abs(CalculateRadius(0.2f) - MinimumRadius) > 0.001f) return "FAIL highlight minimum radius";
            float normal = CalculateRadius(0.9f);
            if (normal <= MinimumRadius || normal >= MaximumRadius) return "FAIL highlight ordinary radius";
            if (Math.Abs(CalculateRadius(9f) - MaximumRadius) > 0.001f) return "FAIL highlight maximum radius";
            if (ShowOuter(true, false) || ShowInner(true, false, false)) return "FAIL idle node ring must be off";
            if (ShowOuter(true, false) || !ShowInner(true, true, false)) return "FAIL hover cue must be restrained inner-only";
            if (!ShowOuter(true, true) || !ShowInner(true, false, true)) return "FAIL selected ring visibility";
            if (WidthMultiplier(true, false, false) >= WidthMultiplier(false, true, true)) return "FAIL hover stronger than selected";
            if (ShowInner(false, true, true) || ShowOuter(false, true)) return "FAIL unavailable highlight visible";
            return "PASS forage selected-only highlight policy";
        }
    }
}
