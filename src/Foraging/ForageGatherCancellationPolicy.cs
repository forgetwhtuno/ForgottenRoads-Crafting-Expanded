using System;

namespace ErenshorCraftingExpanded
{
    public enum ForageGatherCancelReason
    {
        None = 0,
        GameplayDisabled = 1,
        Typing = 2,
        ZoneChanged = 3,
        CharacterChanged = 4,
        JumpOrFall = 5,
        Movement = 6,
        OutOfRange = 7,
        Occluded = 8,
        Damaged = 9,
        DifferentNodeClick = 10,
        WorldInteraction = 11,
        PluginUnload = 12,
        RuntimeException = 13,
        LocalHostileAggro = 14,
        ButtonReleased = 15, // legacy diagnostic only; RMB release no longer cancels gathering
        AimLost = 16,        // legacy diagnostic only; hover/aim loss no longer cancels gathering
        UiOwned = 17,
        TargetInvalid = 18,
        InputOwnershipUnavailable = 19,
        PlayerDead = 20
    }

    // Pure frame policy for a captured one-click gather channel. Hover/aim and mouse-button state
    // are deliberately absent. The captured node is validated by lifetime, range and true LOS;
    // movement and actual HP decrease are supplied as already-proven local-player facts.
    public static class ForageGatherCancellationPolicy
    {
        public static ForageGatherCancelReason EvaluateFrame(
            bool gameplayEnabled,
            bool typing,
            bool zoneChanged,
            bool characterChanged,
            bool targetValid,
            bool meaningfulMovement,
            float nodeDistance,
            float interactionRange,
            bool occluded,
            int previousHp,
            int currentHp)
        {
            if (!gameplayEnabled) return ForageGatherCancelReason.GameplayDisabled;
            if (typing) return ForageGatherCancelReason.Typing;
            if (zoneChanged) return ForageGatherCancelReason.ZoneChanged;
            if (characterChanged) return ForageGatherCancelReason.CharacterChanged;
            if (!targetValid) return ForageGatherCancelReason.TargetInvalid;
            if (currentHp == 0) return ForageGatherCancelReason.PlayerDead;
            if (meaningfulMovement) return ForageGatherCancelReason.Movement;
            if (!IsFinitePositive(interactionRange) || !IsFiniteNonNegative(nodeDistance) || nodeDistance > interactionRange)
                return ForageGatherCancelReason.OutOfRange;
            if (occluded) return ForageGatherCancelReason.Occluded;
            if (ForageGatherChannelPolicy.HasActualHpDecrease(previousHp, currentHp)) return ForageGatherCancelReason.Damaged;
            return ForageGatherCancelReason.None;
        }

        public static string Describe(ForageGatherCancelReason reason)
        {
            switch (reason)
            {
                case ForageGatherCancelReason.GameplayDisabled: return "gameplay-disabled";
                case ForageGatherCancelReason.Typing: return "ui-owned";
                case ForageGatherCancelReason.ZoneChanged: return "scene-change";
                case ForageGatherCancelReason.CharacterChanged: return "player-state";
                case ForageGatherCancelReason.JumpOrFall: return "player-moved";
                case ForageGatherCancelReason.Movement: return "player-moved";
                case ForageGatherCancelReason.OutOfRange: return "out-of-range";
                case ForageGatherCancelReason.Occluded: return "true-los-blocked";
                case ForageGatherCancelReason.Damaged: return "damage-taken";
                case ForageGatherCancelReason.DifferentNodeClick: return "different-node";
                case ForageGatherCancelReason.WorldInteraction: return "world-interaction-conflict";
                case ForageGatherCancelReason.PluginUnload: return "plugin-unload";
                case ForageGatherCancelReason.RuntimeException: return "exception";
                case ForageGatherCancelReason.LocalHostileAggro: return "hostile-engaged";
                case ForageGatherCancelReason.ButtonReleased: return "legacy-button-release";
                case ForageGatherCancelReason.AimLost: return "legacy-aim-lost";
                case ForageGatherCancelReason.UiOwned: return "ui-owned";
                case ForageGatherCancelReason.TargetInvalid: return "node-invalid";
                case ForageGatherCancelReason.InputOwnershipUnavailable: return "input-ownership-unavailable";
                case ForageGatherCancelReason.PlayerDead: return "player-dead";
                default: return "none";
            }
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static bool IsFinitePositive(float value)
        {
            return IsFinite(value) && value > 0f;
        }

        private static bool IsFiniteNonNegative(float value)
        {
            return IsFinite(value) && value >= 0f;
        }

        internal static string RunSelfTests()
        {
            if (EvaluateFrame(true, false, false, false, true, false, 2f, 3.5f, false, 100, 100) != ForageGatherCancelReason.None) return "FAIL valid captured gather cancelled";
            if (EvaluateFrame(false, false, false, false, true, false, 2f, 3.5f, false, 100, 100) != ForageGatherCancelReason.GameplayDisabled) return "FAIL disable cancel";
            if (EvaluateFrame(true, true, false, false, true, false, 2f, 3.5f, false, 100, 100) != ForageGatherCancelReason.Typing) return "FAIL typing cancel";
            if (EvaluateFrame(true, false, true, false, true, false, 2f, 3.5f, false, 100, 100) != ForageGatherCancelReason.ZoneChanged) return "FAIL zone cancel";
            if (EvaluateFrame(true, false, false, true, true, false, 2f, 3.5f, false, 100, 100) != ForageGatherCancelReason.CharacterChanged) return "FAIL character cancel";
            if (EvaluateFrame(true, false, false, false, false, false, 2f, 3.5f, false, 100, 100) != ForageGatherCancelReason.TargetInvalid) return "FAIL invalid captured node cancel";
            if (EvaluateFrame(true, false, false, false, true, true, 2f, 3.5f, false, 100, 100) != ForageGatherCancelReason.Movement) return "FAIL movement cancel";
            if (EvaluateFrame(true, false, false, false, true, false, 3.6f, 3.5f, false, 100, 100) != ForageGatherCancelReason.OutOfRange) return "FAIL range cancel";
            if (EvaluateFrame(true, false, false, false, true, false, 2f, 3.5f, true, 100, 100) != ForageGatherCancelReason.Occluded) return "FAIL LOS cancel";
            if (EvaluateFrame(true, false, false, false, true, false, 2f, 3.5f, false, 100, 99) != ForageGatherCancelReason.Damaged) return "FAIL damage cancel";
            if (EvaluateFrame(true, false, false, false, true, false, 2f, 3.5f, false, 100, 105) != ForageGatherCancelReason.None) return "FAIL healing cancelled";
            if (EvaluateFrame(true, false, false, false, true, false, 2f, 3.5f, false, 1, 0) != ForageGatherCancelReason.PlayerDead) return "FAIL death cancel";
            if (Describe(ForageGatherCancelReason.Movement) != "player-moved" ||
                Describe(ForageGatherCancelReason.Damaged) != "damage-taken" ||
                Describe(ForageGatherCancelReason.OutOfRange) != "out-of-range" ||
                Describe(ForageGatherCancelReason.Occluded) != "true-los-blocked" ||
                Describe(ForageGatherCancelReason.TargetInvalid) != "node-invalid" ||
                Describe(ForageGatherCancelReason.ZoneChanged) != "scene-change" ||
                Describe(ForageGatherCancelReason.PlayerDead) != "player-dead")
                return "FAIL channel cancellation descriptions";
            // Completion-boundary requirement: the guard runs before GrantPending, so a real
            // blocker still wins even on the exact frame the duration reaches completion.
            if (EvaluateFrame(true, false, false, false, true, false, 2f, 3.5f, true, 100, 100) == ForageGatherCancelReason.None)
                return "FAIL completion-boundary cancellation precedence";
            return "PASS forage one-click gather cancellation policy";
        }
    }
}
