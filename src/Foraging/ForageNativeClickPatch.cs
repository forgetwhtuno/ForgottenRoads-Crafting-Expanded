using HarmonyLib;

namespace ErenshorCraftingExpanded
{
    // Current Assembly-CSharp exposes PlayerControl.RightClick as a native world-RMB boundary and
    // gates it with EventSystem.IsPointerOverGameObject. This prefix is failure-open and consumes
    // only an RMB that the controller resolves to a Forgotten Roads forage interaction target.
    [HarmonyPatch(typeof(PlayerControl), "RightClick")]
    internal static class ForageNativeRightClickPatch
    {
        [HarmonyPrefix]
        [HarmonyPriority(Priority.First)]
        private static bool Prefix()
        {
            try
            {
                return !ForageNodeController.TryHandleNativeRightClick();
            }
            catch
            {
                // A forage failure must never disable ordinary Erenshor right-click behavior.
                try { ForageNodeController.RuntimeExceptionCleanup(); } catch { }
                return true;
            }
        }
    }
}
