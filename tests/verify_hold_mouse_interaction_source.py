#!/usr/bin/env python3
"""Static/current-source gates for the one-click RMB forage interaction polish."""
from __future__ import annotations

from pathlib import Path
import hashlib

ROOT = Path(__file__).resolve().parents[1]
PROJECT_ROOT = ROOT.parents[1]
ASSEMBLY = PROJECT_ROOT / "CURRENT_GAME_REFERENCES" / "Assembly-CSharp.dll"


def read(rel: str) -> str:
    return (ROOT / rel).read_text(encoding="utf-8-sig")


def require(condition: bool, message: str) -> None:
    if not condition:
        raise AssertionError(message)


def method_body(source: str, signature: str) -> str:
    at = source.find(signature)
    require(at >= 0, f"missing method signature: {signature}")
    brace = source.find("{", at)
    require(brace >= 0, f"missing method body: {signature}")
    depth = 0
    i = brace
    in_string = in_char = in_line = in_block = verbatim = escape = False
    while i < len(source):
        c = source[i]
        n = source[i + 1] if i + 1 < len(source) else ""
        if in_line:
            if c == "\n": in_line = False
            i += 1; continue
        if in_block:
            if c == "*" and n == "/": in_block = False; i += 2
            else: i += 1
            continue
        if in_string:
            if verbatim:
                if c == '"' and n == '"': i += 2; continue
                if c == '"': in_string = False; verbatim = False
                i += 1; continue
            if escape: escape = False
            elif c == "\\": escape = True
            elif c == '"': in_string = False
            i += 1; continue
        if in_char:
            if escape: escape = False
            elif c == "\\": escape = True
            elif c == "'": in_char = False
            i += 1; continue
        if c == "/" and n == "/": in_line = True; i += 2; continue
        if c == "/" and n == "*": in_block = True; i += 2; continue
        if c == "@" and n == '"': in_string = True; verbatim = True; i += 2; continue
        if c == '"': in_string = True; i += 1; continue
        if c == "'": in_char = True; i += 1; continue
        if c == "{": depth += 1
        elif c == "}":
            depth -= 1
            if depth == 0:
                return source[brace:i + 1]
        i += 1
    raise AssertionError(f"unclosed method body: {signature}")


def main() -> None:
    controller = read("src/Foraging/ForageNodeController.cs")
    admission = method_body(controller, "internal static bool TryHandleNativeRightClick()")
    tick = method_body(controller, "private static void TickActiveGather()")
    target = method_body(controller, "private static void UpdateTargetedNode()")
    begin = method_body(controller, "private static void BeginGather(")
    cancel = method_body(controller, "private static void CancelActiveGather(")
    clear = method_body(controller, "private static void ClearActiveGatherSnapshot()")
    destroy = method_body(controller, "private static void DestroyNodeOwnedObjects(")
    los = method_body(controller, "private static bool IsGatherLineOfSightBlocked(")
    conflict = method_body(controller, "private static bool IsConflictingUiDuringCapturedGather()")

    capture = read("src/Foraging/ForageInteractionCaptureState.cs")
    channel = read("src/Foraging/ForageGatherHoldPolicy.cs")
    cancel_policy = read("src/Foraging/ForageGatherCancellationPolicy.cs")
    click_policy = read("src/Foraging/ForageActiveGatherClickPolicy.cs")
    camera = read("src/CraftingCameraUiOwnershipPatch.cs")
    native_click = read("src/Foraging/ForageNativeClickPatch.cs")
    highlight = read("src/Foraging/ForageNodeHighlight.cs")
    highlight_policy = read("src/Foraging/ForageHighlightPolicy.cs")
    transaction_tests = read("tests/ForageGatherTransactionTests.cs")

    # Current binary surface. The source runtime verifier additionally proves RightClick -> UI gate
    # relationship before camera/input containment is installed.
    require(ASSEMBLY.is_file(), "current Assembly-CSharp.dll missing")
    assembly_bytes = ASSEMBLY.read_bytes()
    for symbol in ("CameraController", "UsingUI", "ModernControls", "Controls", "PlayerControl",
                   "LeftClick", "RightClick", "MouseLook", "LandMovement", "WaterMovement",
                   "DraggingUIElement", "IsPointerOverGameObject", "CurrentAggroTarget"):
        require(symbol.encode("ascii") in assembly_bytes, f"current assembly input symbol missing: {symbol}")
    print("Assembly-CSharp SHA256:", hashlib.sha256(assembly_bytes).hexdigest().upper())

    # RMB admission is narrow and fail-open.
    require('HarmonyPatch(typeof(PlayerControl), "RightClick")' in native_click,
            "native PlayerControl.RightClick admission hook missing")
    require("TryHandleNativeRightClick" in native_click and "return true;" in native_click,
            "RightClick patch is not failure-open")
    require(admission.count("BeginGather(") == 1, "RMB admission does not have exactly one BeginGather path")
    require("TryResolvePointerTarget" in admission and "_captureState.SetHoverCandidate" in admission,
            "RMB does not resolve/store exact forage candidate")
    require("if (!pointsAtForage) return false;" in admission,
            "non-forage RMB may be globally consumed")
    require("IsPointerOwnedByUi()" in admission and "return false;" in admission,
            "UI RMB does not remain native")
    require("ForageInteractionEligibility.OutOfRange" in admission and
            'NotifyGatherFeedback("Out of range.")' in admission,
            "out-of-range targeting feedback missing")
    require("IsGatherLineOfSightBlocked" in admission and "OutOfRange" in admission,
            "initial range/LOS validation missing")
    require("Input.GetMouseButton(0)" not in admission,
            "legacy LMB hold admission still present")

    # Explicit selection -> gathering state machine and exact captured identity.
    for token in ("Idle", "HoverCandidate", "Selected", "Gathering", "Completing", "Cancelled",
                  "TrySelect", "TryBeginGathering", "TryBeginCompleting", "Release"):
        require(token in capture, f"capture state missing: {token}")
    require("_captureState.TrySelect" in begin and "_captureState.TryBeginGathering" in begin,
            "BeginGather does not explicitly transition Selected -> Gathering")
    require(begin.find("TrySelect") < begin.find("TryBeginGather"),
            "transaction selection must precede elapsed gather timer")
    require("forage_selected" in begin and "forage_gather_begin" in begin,
            "selection/gather diagnostics missing")
    require("capture=locked" in target and "_activeGatherNode" in target,
            "hover presentation does not lock to captured node")
    for forbidden in ("TryResolvePointerTarget", "pointerResolved", "aimedNode", "CurrentHoveredNode", "AimLost"):
        require(forbidden not in tick, f"captured channel still depends on hover/aim: {forbidden}")

    # Physical RMB release only releases short camera ownership; it never cancels the timer.
    require("IsSelectingRightButtonHeld" in tick and
            'ReleaseActiveGatherInputOwnership("right-click-release")' in tick,
            "initiating RMB ownership is not released independently")
    require("ButtonReleased" not in tick and "GetMouseButton(0)" not in tick,
            "physical mouse release still cancels one-click channel")
    require("get { return _activeGatherInputOwned; }" in controller,
            "camera ownership incorrectly lasts for whole captured transaction")
    require("MouseLookPrefix" in camera and "OwnsCapturedPointerInput" in camera,
            "short RMB camera containment missing")
    require("RightClick" in camera and "References(rightClick, pointerOverUi)" in camera,
            "current RightClick UI-gate relationship is not runtime-verified")
    require("harmony.Patch(pointerOverUi" not in camera,
            "global EventSystem ownership would break unrelated input")

    # Movement/damage are real local-player interruptions. Camera/hover are not.
    require("MovementCancelDistance = 0.10f" in channel,
            "bounded movement tolerance missing")
    require("HasMeaningfulMovement" in tick and "_activeGatherStartPosition" in tick,
            "player displacement interruption missing")
    require("_activeGatherLastHp" in tick and "HasActualHpDecrease" in cancel_policy,
            "actual HP decrease interruption missing")
    require("GameForagingApi.TryGetPlayerCurrentHp" in tick and "Character.DamageMe" not in controller,
            "damage interruption must observe local resolved HP rather than attack attempts/other actors")
    require('return "player-moved"' in cancel_policy and 'return "damage-taken"' in cancel_policy,
            "movement/damage diagnostics are not precise")
    require("currentHp == 0" in cancel_policy and 'return "player-dead"' in cancel_policy,
            "player death interruption missing")
    require("TryResolvePointerTarget" not in tick and "Input.mousePosition" not in tick,
            "camera/pointer motion still affects active gather")
    require("OutOfRange" in cancel_policy and "Occluded" in cancel_policy and "targetValid" in tick,
            "captured node range/LOS/lifetime guards missing")

    # Active RMB semantics: forage cannot steal/restart; real world RMB passes native.
    require("ConsumeForageClick" in click_policy and "CancelWorldPassThrough" in click_policy,
            "active RMB policy missing forage-vs-world distinction")
    require("CancelActiveGather(ForageGatherCancelReason.WorldInteraction)" in admission,
            "non-forage active RMB does not cancel/pass through")
    require("duplicate-forage-rmb=consumed" in admission,
            "second forage node/RMB can replace active transaction")

    # LOS and exact-once transaction architecture remain untouched.
    require("IsColliderOwnedByNodeVisual(node, blocker)" in los,
            "own forage visual collider exclusion missing")
    require("ForageInteractionPolicy.IsSolidOcclusion" in los,
            "real world LOS blocker policy missing")
    require("same-frame double click restarted" in transaction_tests and
            "cancelled gather committed reward/progression" in transaction_tests,
            "exact-once/cancel-zero transaction regression coverage missing")

    # Selected-only ring: idle off, hover inner-only, selected both, terminal clear.
    require("showSelected" in highlight and "showHover" in highlight,
            "highlight state does not distinguish hover from selected")
    require("_outer.enabled = showSelected" in highlight and
            "_inner.enabled = showSelected || showHover" in highlight,
            "idle/hover/selected ring visibility contract missing")
    require("ShowOuter(true, false)" in highlight_policy and
            "ShowInner(true, true, false)" in highlight_policy,
            "highlight deterministic idle/hover tests missing")
    require("forage_highlight_" in controller and "HighlightSelectionActive" in controller,
            "bounded highlight on/off diagnostics missing")
    require("SetNodeInteractionStateSafe(completedNode, false, false)" in clear,
            "terminal cleanup does not clear strong selection ring")
    require("node.Highlight" in destroy and "UnityEngine.Object.Destroy(node.Highlight)" in destroy,
            "scene/node teardown does not destroy selection highlight")

    # Every terminal/lifecycle path still releases token, presentation and temporary input ownership.
    require("ReleaseCapturedInteraction" in cancel and "forage_gather_cancel" in cancel,
            "cancel path does not release/log channel")
    require('ReleaseCapturedInteraction("snapshot-clear")' in clear,
            "final cleanup safety net missing")
    for lifecycle in ("ZoneChanged", "GameplayDisabled", "PluginUnload", "RuntimeException"):
        require(f"CancelActiveGather(ForageGatherCancelReason.{lifecycle})" in controller,
                f"missing lifecycle cancel: {lifecycle}")
    require("forage_gather_complete" in controller,
            "completion diagnostic missing")

    print("PASS: one-click RMB forage admission/capture/interruption/visual contracts")
    print("PASS: non-forage/UI RMB remains native; camera motion is not gather authority")
    print("PASS: movement, actual HP decrease, range/LOS, death and cleanup guards present")


if __name__ == "__main__":
    main()
