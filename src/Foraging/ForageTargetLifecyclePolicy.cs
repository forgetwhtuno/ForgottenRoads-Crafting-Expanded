namespace ErenshorCraftingExpanded
{
    public enum ForageNodeLifecycleAction
    {
        Healthy = 0,
        RepairInteractionTarget = 1,
        RetireNode = 2
    }

    // Pure lifecycle policy for Unity-object loss. Label/highlight presentation is intentionally
    // non-authoritative and may disappear without retiring a usable resource. The root visual,
    // definition and runtime state are authoritative for a live node; the raycast target can be
    // rebuilt if its trigger/component is destroyed during a scene transition.
    public static class ForageTargetLifecyclePolicy
    {
        public static ForageNodeLifecycleAction Evaluate(
            bool nodeExists,
            bool definitionExists,
            bool stateExists,
            bool visualAlive,
            bool interactionTargetAlive,
            bool interactionComponentAlive,
            bool colliderAlive,
            bool labelAlive,
            bool highlightAlive)
        {
            // Presentation loss is deliberately tolerated. labelAlive/highlightAlive are accepted
            // only so deterministic tests prove destroyed presentation cannot become gameplay
            // authority; they intentionally do not affect the lifecycle decision.

            if (!nodeExists || !definitionExists || !stateExists || !visualAlive)
                return ForageNodeLifecycleAction.RetireNode;
            if (!interactionTargetAlive || !interactionComponentAlive || !colliderAlive)
                return ForageNodeLifecycleAction.RepairInteractionTarget;
            return ForageNodeLifecycleAction.Healthy;
        }

        internal static string RunSelfTests()
        {
            if (Evaluate(true, true, true, true, true, true, true, true, true) != ForageNodeLifecycleAction.Healthy)
                return "FAIL healthy forage node lifecycle";
            if (Evaluate(false, true, true, true, true, true, true, true, true) != ForageNodeLifecycleAction.RetireNode)
                return "FAIL missing node should retire";
            if (Evaluate(true, false, true, true, true, true, true, true, true) != ForageNodeLifecycleAction.RetireNode)
                return "FAIL missing definition should retire";
            if (Evaluate(true, true, false, true, true, true, true, true, true) != ForageNodeLifecycleAction.RetireNode)
                return "FAIL missing state should retire";
            if (Evaluate(true, true, true, false, true, true, true, true, true) != ForageNodeLifecycleAction.RetireNode)
                return "FAIL destroyed visual should retire";
            if (Evaluate(true, true, true, true, false, true, true, true, true) != ForageNodeLifecycleAction.RepairInteractionTarget)
                return "FAIL destroyed target should repair";
            if (Evaluate(true, true, true, true, true, false, true, true, true) != ForageNodeLifecycleAction.RepairInteractionTarget)
                return "FAIL destroyed interaction component should repair";
            if (Evaluate(true, true, true, true, true, true, false, true, true) != ForageNodeLifecycleAction.RepairInteractionTarget)
                return "FAIL destroyed target collider should repair";
            if (Evaluate(true, true, true, true, true, true, true, false, true) != ForageNodeLifecycleAction.Healthy)
                return "FAIL destroyed label became gameplay-authoritative";
            if (Evaluate(true, true, true, true, true, true, true, true, false) != ForageNodeLifecycleAction.Healthy)
                return "FAIL destroyed highlight became gameplay-authoritative";
            return "PASS forage target lifecycle policy";
        }
    }
}
