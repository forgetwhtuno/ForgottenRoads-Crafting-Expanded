namespace ErenshorCraftingExpanded
{
    // Pure presentation constants/policy shared by the live world label and auto-cluster builder.
    // The visual footprint is intentionally resource-like rather than scenery-like: a compact
    // three-clump patch with a single Mineral-Deposit-inspired name-in-bar presentation.
    public static class ForagePresentationPolicy
    {
        public const float LabelWorldScale = 0.0085f;
        public const float LabelWidth = 244f;
        public const float LabelHeight = 54f;
        public const float LabelFontSize = 25f;
        public const float LabelDistanceScaleStart = 3.5f;
        public const float LabelDistanceScaleEnd = 8.0f;
        public const float LabelMaximumDistanceScale = 1.18f;

        public const float BarLeftFraction = 0.02f;
        public const float BarRightFraction = 0.98f;
        public const float BarBottomFraction = 0.06f;
        public const float BarTopFraction = 0.94f;

        public const int PreferredClusterClumpCount = 3;
        public const float ClusterTargetLargestDimension = 0.78f;
        public const float ClusterMinNormalizedScale = 0.10f;
        public const float ClusterMaxNormalizedScale = 1.40f;
        public const float ClusterMaxOffsetRadius = 0.42f;
        public const float GroundingTolerance = 0.06f;
        public const float VisualMinimumLargestDimension = 0.025f;
        public const float VisualMaximumScaleAxis = 8f;
        public const float VisualMaximumAnchorHorizontalOffset = 1.25f;

        private static readonly float[] ClusterOffsetX = new float[] { 0f, 0.35f, -0.31f };
        private static readonly float[] ClusterOffsetZ = new float[] { 0f, 0.15f, -0.22f };
        private static readonly float[] ClusterYaw = new float[] { 0f, 113f, 247f };
        private static readonly float[] ClusterRelativeScale = new float[] { 1f, 0.78f, 0.66f };

        public static float GetClusterOffsetX(int index)
        {
            return index >= 0 && index < ClusterOffsetX.Length ? ClusterOffsetX[index] : 0f;
        }

        public static float GetClusterOffsetZ(int index)
        {
            return index >= 0 && index < ClusterOffsetZ.Length ? ClusterOffsetZ[index] : 0f;
        }

        public static float GetClusterYaw(int index)
        {
            return index >= 0 && index < ClusterYaw.Length ? ClusterYaw[index] : 0f;
        }

        public static float GetClusterRelativeScale(int index)
        {
            return index >= 0 && index < ClusterRelativeScale.Length ? ClusterRelativeScale[index] : 0f;
        }

        public static float CalculateNormalizedClusterScale(float sourceLargestDimension)
        {
            if (float.IsNaN(sourceLargestDimension) ||
                float.IsInfinity(sourceLargestDimension) ||
                sourceLargestDimension <= 0.001f)
                return 0f;

            float scale = ClusterTargetLargestDimension / sourceLargestDimension;
            if (scale < ClusterMinNormalizedScale) scale = ClusterMinNormalizedScale;
            if (scale > ClusterMaxNormalizedScale) scale = ClusterMaxNormalizedScale;
            return scale;
        }

        public static bool IsClusterOffsetWithinBounds(float x, float z)
        {
            if (float.IsNaN(x) || float.IsInfinity(x) || float.IsNaN(z) || float.IsInfinity(z))
                return false;
            return x * x + z * z <= ClusterMaxOffsetRadius * ClusterMaxOffsetRadius;
        }

        public static float EstimateClusterEnvelopeRadius(float sourceLargestDimension)
        {
            float normalizedScale = CalculateNormalizedClusterScale(sourceLargestDimension);
            if (normalizedScale <= 0f) return 0f;
            return ClusterMaxOffsetRadius + sourceLargestDimension * normalizedScale * 0.5f;
        }

        public static bool TryCalculateGroundingOffset(float clusterPlaneY, float minimumRendererY, out float offset)
        {
            offset = clusterPlaneY - minimumRendererY;
            if (float.IsNaN(offset) || float.IsInfinity(offset) || System.Math.Abs(offset) > 2.0f)
            {
                offset = 0f;
                return false;
            }
            return true;
        }

        public static bool ShouldShowResourcePresentation(ForageAvailability availability)
        {
            return availability == ForageAvailability.Available ||
                availability == ForageAvailability.Gathering ||
                availability == ForageAvailability.GrantPending;
        }

        // Existing red resource fill drains as the mod-owned gathering channel advances.
        public static float ResourceBarFill(float gatherProgress01)
        {
            if (float.IsNaN(gatherProgress01) || float.IsInfinity(gatherProgress01)) gatherProgress01 = 0f;
            if (gatherProgress01 < 0f) gatherProgress01 = 0f;
            if (gatherProgress01 > 1f) gatherProgress01 = 1f;
            return 1f - gatherProgress01;
        }

        public static float CompletionFeedbackAlpha(float normalizedRemaining)
        {
            if (float.IsNaN(normalizedRemaining) || float.IsInfinity(normalizedRemaining)) normalizedRemaining = 0f;
            if (normalizedRemaining < 0f) normalizedRemaining = 0f;
            if (normalizedRemaining > 1f) normalizedRemaining = 1f;
            return normalizedRemaining;
        }

        public static float CompletionFeedbackScale(float normalizedRemaining)
        {
            float alpha = CompletionFeedbackAlpha(normalizedRemaining);
            return 0.94f + 0.06f * alpha;
        }

        public static float CalculateLabelDistanceScale(float distance)
        {
            if (float.IsNaN(distance) || float.IsInfinity(distance) || distance <= LabelDistanceScaleStart) return 1f;
            if (distance >= LabelDistanceScaleEnd) return LabelMaximumDistanceScale;
            float t = (distance - LabelDistanceScaleStart) / (LabelDistanceScaleEnd - LabelDistanceScaleStart);
            return 1f + (LabelMaximumDistanceScale - 1f) * t;
        }

        public static float LabelWorldWidth() { return LabelWidth * LabelWorldScale; }
        public static float LabelWorldHeight() { return LabelHeight * LabelWorldScale; }
        public static float BarWorldWidth() { return LabelWidth * (BarRightFraction - BarLeftFraction) * LabelWorldScale; }
        public static float BarWorldHeight() { return LabelHeight * (BarTopFraction - BarBottomFraction) * LabelWorldScale; }

        internal static string RunSelfTests()
        {
            float normalizedPlant = CalculateNormalizedClusterScale(0.8f);
            if (normalizedPlant < 0.95f || normalizedPlant > 1.00f)
                return "FAIL ordinary plant should scale into compact herb patch";

            float normalizedBush = CalculateNormalizedClusterScale(1.6f);
            if (normalizedBush < 0.47f || normalizedBush > 0.50f)
                return "FAIL bush fallback should shrink substantially";

            float normalizedLarge = CalculateNormalizedClusterScale(6f);
            if (normalizedLarge < 0.12f || normalizedLarge > 0.14f)
                return "FAIL giant vegetation normalization should remain bounded";

            if (CalculateNormalizedClusterScale(float.NaN) != 0f ||
                CalculateNormalizedClusterScale(float.PositiveInfinity) != 0f ||
                CalculateNormalizedClusterScale(0f) != 0f)
                return "FAIL invalid cluster dimensions should fail closed";

            if (PreferredClusterClumpCount != 3)
                return "FAIL forage presentation should use three compact clumps";
            for (int i = 0; i < PreferredClusterClumpCount; i++)
            {
                if (!IsClusterOffsetWithinBounds(GetClusterOffsetX(i), GetClusterOffsetZ(i)))
                    return "FAIL cluster clump offset escaped compact footprint at index " + i;
                float relative = GetClusterRelativeScale(i);
                if (relative < 0.60f || relative > 1.05f)
                    return "FAIL cluster clump scale variation at index " + i;
                float yaw = GetClusterYaw(i);
                if (yaw < 0f || yaw >= 360f) return "FAIL cluster yaw at index " + i;
            }
            if (IsClusterOffsetWithinBounds(0.8f, 0f)) return "FAIL oversized cluster offset accepted";

            float groundingOffset;
            if (!TryCalculateGroundingOffset(0f, -0.35f, out groundingOffset) || System.Math.Abs(groundingOffset - 0.35f) > 0.0001f)
                return "FAIL ordinary visual grounding offset";
            if (TryCalculateGroundingOffset(0f, -3f, out groundingOffset))
                return "FAIL extreme visual grounding correction should fail closed";

            if (EstimateClusterEnvelopeRadius(0.8f) > 0.82f ||
                EstimateClusterEnvelopeRadius(1.6f) > 0.82f ||
                EstimateClusterEnvelopeRadius(6f) > 0.82f)
                return "FAIL forage cluster envelope should remain resource-sized";

            if (!ShouldShowResourcePresentation(ForageAvailability.Available)) return "FAIL available presentation";
            if (!ShouldShowResourcePresentation(ForageAvailability.Gathering)) return "FAIL gathering presentation";
            if (!ShouldShowResourcePresentation(ForageAvailability.GrantPending)) return "FAIL grant-pending presentation";
            if (ShouldShowResourcePresentation(ForageAvailability.Depleted)) return "FAIL depleted presentation";
            if (ResourceBarFill(0f) != 1f || ResourceBarFill(1f) != 0f) return "FAIL gather bar endpoints";
            float halfwayFill = ResourceBarFill(0.5f);
            if (halfwayFill < 0.49f || halfwayFill > 0.51f) return "FAIL gather bar midpoint";
            if (ResourceBarFill(-4f) != 1f || ResourceBarFill(4f) != 0f) return "FAIL gather bar clamping";
            if (CompletionFeedbackAlpha(1f) != 1f || CompletionFeedbackAlpha(0f) != 0f) return "FAIL completion alpha endpoints";
            if (CompletionFeedbackScale(1f) < 0.999f || CompletionFeedbackScale(0f) > 0.941f) return "FAIL completion scale feedback";

            float width = LabelWorldWidth();
            float height = LabelWorldHeight();
            float barWidth = BarWorldWidth();
            float barHeight = BarWorldHeight();
            if (width < 2.07f || width > 2.09f || height < 0.45f || height > 0.47f)
                return "FAIL readable nameplate dimensions";
            if (barWidth < 1.98f || barWidth > 2.00f || barHeight < 0.39f || barHeight > 0.41f)
                return "FAIL integrated resource bar dimensions";
            if (CalculateLabelDistanceScale(2f) != 1f) return "FAIL near label distance scale";
            float midScale = CalculateLabelDistanceScale(5.75f);
            if (midScale < 1.08f || midScale > 1.10f) return "FAIL mid label distance scale";
            if (CalculateLabelDistanceScale(12f) != LabelMaximumDistanceScale) return "FAIL far label distance scale clamp";

            return "PASS forage presentation policy";
        }
    }
}
