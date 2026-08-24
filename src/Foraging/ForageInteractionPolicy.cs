using System;

namespace ErenshorCraftingExpanded
{
    public enum ForageInteractionEligibility
    {
        Ready = 0,
        NoNode = 1,
        Depleted = 2,
        InvalidRange = 3,
        OutOfRange = 4,
        ProgressionUnavailable = 5,
        SkillTooLow = 6
    }

    public struct ForageInteractionEvaluation
    {
        public ForageInteractionEligibility Eligibility;
        public string Reason;
        public bool CanGather;
    }

    // Pure policy for the mod-owned Foraging interaction surface. Target acquisition itself is a
    // bounded pointer raycast against mod-owned trigger hitboxes. Production gathering is one-click
    // RMB: the native PlayerControl.RightClick boundary admits only an exact valid forage target,
    // then the captured node/token owns the channel until completion or a meaningful interruption.
    // There is deliberately no keyboard or nearest-node fallback because those inputs can collide
    // with native Erenshor controls.
    public static class ForageInteractionPolicy
    {
        public const float TargetProbeDistance = 8f;
        public const float TargetProbeIntervalSeconds = 0.12f;
        public const float MinimumHitRadius = 0.58f;
        public const float MaximumHitRadius = 0.95f;
        public const float MinimumHitHeight = 1.35f;
        public const float MaximumHitHeight = 2.10f;
        public const float RayHitTieEpsilon = 0.002f;

        public static ForageInteractionEvaluation Evaluate(
            bool hasNode,
            bool available,
            float distance,
            float interactionRange,
            bool progressionReady,
            int currentSkill,
            int requiredSkill)
        {
            ForageInteractionEvaluation result = new ForageInteractionEvaluation();
            if (!hasNode)
            {
                result.Eligibility = ForageInteractionEligibility.NoNode;
                result.Reason = "No forage resource targeted.";
                return result;
            }
            if (!available)
            {
                result.Eligibility = ForageInteractionEligibility.Depleted;
                result.Reason = "Resource is depleted.";
                return result;
            }
            if (!IsFinitePositive(interactionRange))
            {
                result.Eligibility = ForageInteractionEligibility.InvalidRange;
                result.Reason = "Foraging interaction range is invalid.";
                return result;
            }
            if (!IsFiniteNonNegative(distance) || distance > interactionRange)
            {
                result.Eligibility = ForageInteractionEligibility.OutOfRange;
                result.Reason = "Out of range.";
                return result;
            }
            if (!progressionReady)
            {
                result.Eligibility = ForageInteractionEligibility.ProgressionUnavailable;
                result.Reason = "Foraging progression is waiting for the active character save slot.";
                return result;
            }
            if (requiredSkill < 1) requiredSkill = 1;
            if (currentSkill < requiredSkill)
            {
                result.Eligibility = ForageInteractionEligibility.SkillTooLow;
                result.Reason = "Requires Foraging " + requiredSkill.ToString() + ".";
                return result;
            }
            result.Eligibility = ForageInteractionEligibility.Ready;
            result.Reason = "Ready.";
            result.CanGather = true;
            return result;
        }

        // Click admission is intentionally exact: only the resource hit by the pointer may own the
        // gather transaction. A missing pointer hit never falls back to a nearby node.
        public static int SelectClickedCandidate(int targetedIndex, int nodeCount)
        {
            if (nodeCount <= 0) return -1;
            return targetedIndex >= 0 && targetedIndex < nodeCount ? targetedIndex : -1;
        }

        // Retained as a pure compatibility helper for older deterministic tests/migration reasoning;
        // production runtime no longer calls this nearest-node fallback.
        public static int SelectGatherCandidate(
            int targetedIndex,
            bool[] available,
            float[] distances,
            float interactionRange)
        {
            if (available == null || distances == null || available.Length != distances.Length || available.Length == 0)
                return -1;
            if (targetedIndex >= 0 && targetedIndex < available.Length) return targetedIndex;
            if (!IsFinitePositive(interactionRange)) return -1;

            int selected = -1;
            float nearest = float.MaxValue;
            for (int i = 0; i < available.Length; i++)
            {
                if (!available[i]) continue;
                float distance = distances[i];
                if (!IsFiniteNonNegative(distance) || distance > interactionRange || distance >= nearest) continue;
                selected = i;
                nearest = distance;
            }
            return selected;
        }

        public static bool IsSolidOcclusion(float targetDistance, float blockerDistance, bool blockerIsLocalPlayer)
        {
            if (blockerIsLocalPlayer) return false;
            if (!IsFinitePositive(targetDistance) || !IsFinitePositive(blockerDistance)) return false;
            return blockerDistance + 0.01f < targetDistance;
        }

        public static float CalculateHitRadius(float visualWidth, float visualDepth)
        {
            if (!IsFinitePositive(visualWidth) || !IsFinitePositive(visualDepth)) return MinimumHitRadius;
            float radius = Math.Max(visualWidth, visualDepth) * 0.62f + 0.08f;
            if (radius < MinimumHitRadius) radius = MinimumHitRadius;
            if (radius > MaximumHitRadius) radius = MaximumHitRadius;
            return radius;
        }

        public static float CalculateHitHeight(float visualHeight, float hitRadius)
        {
            float safeRadius = IsFinitePositive(hitRadius) ? hitRadius : MinimumHitRadius;
            float height = IsFinitePositive(visualHeight) ? visualHeight * 1.30f + 0.25f : MinimumHitHeight;
            float minimumForCapsule = safeRadius * 2f;
            if (height < minimumForCapsule) height = minimumForCapsule;
            if (height < MinimumHitHeight) height = MinimumHitHeight;
            if (height > MaximumHitHeight) height = MaximumHitHeight;
            return height;
        }

        public static bool ShouldPreferPointerHit(float candidateDistance, string candidateId, float selectedDistance, string selectedId)
        {
            if (!IsFiniteNonNegative(candidateDistance)) return false;
            if (!IsFiniteNonNegative(selectedDistance) || float.IsInfinity(selectedDistance)) return true;
            if (candidateDistance + RayHitTieEpsilon < selectedDistance) return true;
            if (Math.Abs(candidateDistance - selectedDistance) > RayHitTieEpsilon) return false;
            return string.Compare(candidateId ?? string.Empty, selectedId ?? string.Empty, StringComparison.Ordinal) < 0;
        }

        public static bool ShouldLogEligibilityTransition(string previousNodeId, ForageInteractionEligibility previous, string nextNodeId, ForageInteractionEligibility next)
        {
            return !string.Equals(previousNodeId ?? string.Empty, nextNodeId ?? string.Empty, StringComparison.Ordinal) || previous != next;
        }

        private static bool IsFinitePositive(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value) && value > 0f;
        }

        private static bool IsFiniteNonNegative(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value) && value >= 0f;
        }

        internal static string RunSelfTests()
        {
            ForageInteractionEvaluation ready = Evaluate(true, true, 2f, 3.5f, true, 4, 1);
            if (!ready.CanGather || ready.Eligibility != ForageInteractionEligibility.Ready) return "FAIL ready interaction";
            ForageInteractionEvaluation outOfRange = Evaluate(true, true, 4f, 3.5f, true, 4, 1);
            if (outOfRange.CanGather || outOfRange.Eligibility != ForageInteractionEligibility.OutOfRange) return "FAIL range gate";
            ForageInteractionEvaluation playerUnavailable = Evaluate(true, true, float.PositiveInfinity, 3.5f, true, 4, 1);
            if (playerUnavailable.CanGather || playerUnavailable.Eligibility != ForageInteractionEligibility.OutOfRange) return "FAIL missing-player target evaluation";
            ForageInteractionEvaluation depleted = Evaluate(true, false, 1f, 3.5f, true, 4, 1);
            if (depleted.CanGather || depleted.Eligibility != ForageInteractionEligibility.Depleted) return "FAIL depletion gate";
            ForageInteractionEvaluation waiting = Evaluate(true, true, 1f, 3.5f, false, 1, 1);
            if (waiting.CanGather || waiting.Eligibility != ForageInteractionEligibility.ProgressionUnavailable) return "FAIL progression readiness gate";
            ForageInteractionEvaluation skill = Evaluate(true, true, 1f, 3.5f, true, 7, 8);
            if (skill.CanGather || skill.Eligibility != ForageInteractionEligibility.SkillTooLow || skill.Reason.IndexOf("8", StringComparison.Ordinal) < 0)
                return "FAIL skill gate";

            if (SelectClickedCandidate(1, 3) != 1) return "FAIL clicked target selection";
            if (SelectClickedCandidate(-1, 3) != -1) return "FAIL click must not use nearest-node fallback";
            if (SelectClickedCandidate(4, 3) != -1) return "FAIL out-of-range clicked target index";

            bool[] available = new bool[] { true, true, false };
            float[] distances = new float[] { 2.5f, 1.2f, 0.5f };
            if (SelectGatherCandidate(-1, available, distances, 3.5f) != 1) return "FAIL nearest fallback";
            if (SelectGatherCandidate(0, available, distances, 3.5f) != 0) return "FAIL aimed node preference";
            if (SelectGatherCandidate(2, available, distances, 3.5f) != 2) return "FAIL aimed depleted node should retain interaction ownership";
            if (SelectGatherCandidate(-1, available, distances, 1f) != -1) return "FAIL no nearest node in range";

            if (!IsSolidOcclusion(4f, 2f, false)) return "FAIL nearer world collider should occlude forage click";
            if (IsSolidOcclusion(4f, 2f, true)) return "FAIL local player collider must not occlude its own pointer ray";
            if (IsSolidOcclusion(4f, 3.995f, false)) return "FAIL target-contact epsilon treated as occlusion";

            float small = CalculateHitRadius(0.2f, 0.3f);
            float ordinary = CalculateHitRadius(0.9f, 0.7f);
            float huge = CalculateHitRadius(9f, 9f);
            if (Math.Abs(small - MinimumHitRadius) > 0.001f) return "FAIL minimum interaction hit radius";
            if (ordinary <= MinimumHitRadius || ordinary >= MaximumHitRadius) return "FAIL ordinary interaction hit radius";
            if (Math.Abs(huge - MaximumHitRadius) > 0.001f) return "FAIL maximum interaction hit radius";
            float ordinaryHeight = CalculateHitHeight(0.75f, ordinary);
            if (ordinaryHeight < ordinary * 2f || ordinaryHeight > MaximumHitHeight) return "FAIL nonblocking interaction capsule height";
            if (CalculateHitHeight(float.NaN, MinimumHitRadius) < MinimumHitHeight) return "FAIL invalid interaction height fallback";
            if (!ShouldPreferPointerHit(1.2f, "b", 1.4f, "a")) return "FAIL closest pointer hit priority";
            if (!ShouldPreferPointerHit(1.2f, "a", 1.2f, "b")) return "FAIL stable overlapping pointer tie break";
            if (ShouldPreferPointerHit(1.2f, "b", 1.2f, "a")) return "FAIL overlapping pointer tie break reversed";

            if (!ShouldLogEligibilityTransition("a", ForageInteractionEligibility.Ready, "b", ForageInteractionEligibility.Ready))
                return "FAIL target change diagnostic transition";
            if (ShouldLogEligibilityTransition("a", ForageInteractionEligibility.Ready, "a", ForageInteractionEligibility.Ready))
                return "FAIL unchanged eligibility should not produce diagnostic transition";
            return "PASS forage interaction policy";
        }
    }
}
