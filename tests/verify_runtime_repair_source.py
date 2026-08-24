#!/usr/bin/env python3
"""Static regression gates preserving the live forage/runtime interaction repair through 0.3.0.

This intentionally validates source-owned safety contracts without requiring proprietary
Unity/Erenshor assemblies. RUN_TESTS.ps1 remains the executable C# deterministic suite when a
Windows .NET Framework compiler is available.
"""
from __future__ import annotations

import json
import re
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
SRC = ROOT / "src"


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
    in_string = False
    verbatim = False
    in_char = False
    in_line = False
    in_block = False
    escape = False
    i = brace
    while i < len(source):
        c = source[i]
        n = source[i + 1] if i + 1 < len(source) else ""
        if in_line:
            if c == "\n":
                in_line = False
            i += 1
            continue
        if in_block:
            if c == "*" and n == "/":
                in_block = False
                i += 2
            else:
                i += 1
            continue
        if in_string:
            if verbatim:
                if c == '"' and n == '"':
                    i += 2
                    continue
                if c == '"':
                    in_string = False
                    verbatim = False
                i += 1
                continue
            if escape:
                escape = False
            elif c == "\\":
                escape = True
            elif c == '"':
                in_string = False
            i += 1
            continue
        if in_char:
            if escape:
                escape = False
            elif c == "\\":
                escape = True
            elif c == "'":
                in_char = False
            i += 1
            continue
        if c == "/" and n == "/":
            in_line = True
            i += 2
            continue
        if c == "/" and n == "*":
            in_block = True
            i += 2
            continue
        if c == "@" and n == '"':
            in_string = True
            verbatim = True
            i += 2
            continue
        if c == '"':
            in_string = True
            i += 1
            continue
        if c == "'":
            in_char = True
            i += 1
            continue
        if c == "{":
            depth += 1
        elif c == "}":
            depth -= 1
            if depth == 0:
                return source[brace : i + 1]
        i += 1
    raise AssertionError(f"unclosed method body: {signature}")


def validate_balanced_cs(path: Path) -> None:
    # Lightweight lexical brace balance. This catches mechanical edit corruption without pretending
    # to replace the real compiler gate.
    source = path.read_text(encoding="utf-8-sig")
    body = "class __Wrapper {\n" + source + "\n}"
    depth = 0
    in_string = in_char = in_line = in_block = verbatim = escape = False
    i = 0
    while i < len(body):
        c = body[i]
        n = body[i + 1] if i + 1 < len(body) else ""
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
            require(depth >= 0, f"negative brace balance in {path.relative_to(ROOT)}")
        i += 1
    require(depth == 0 and not in_string and not in_char and not in_block,
            f"lexical structure incomplete in {path.relative_to(ROOT)}")


def main() -> None:
    controller = read("src/Foraging/ForageNodeController.cs")
    update_target = method_body(controller, "private static void UpdateTargetedNode()")
    pointer_probe = method_body(controller, "private static bool TryResolvePointerTarget")
    gather_tick = method_body(controller, "private static void TickActiveGather()")
    integrity = method_body(controller, "private static bool EnsureNodeRuntimeIntegrity")
    destroy_node = method_body(controller, "private static void DestroyNodeOwnedObjects")

    # NRE root regression: no presentation dereference through the null local 'next'.
    require("if (next == null)" in update_target, "target clear branch missing")
    require("next.HighlightView" not in update_target and "next.LabelView" not in update_target,
            "null target presentation dereference returned")
    require("SetNodeInteractionStateSafe(previous, false, false)" in update_target,
            "previous target cleanup is not guarded")
    require("GameForagingApi.TryGetPlayerPosition" in update_target,
            "missing-player target evaluation path absent")

    # Camera/destroyed-object/scene transition containment.
    require("camera == null" in pointer_probe and '"camera-unavailable"' in pointer_probe,
            "camera-unavailable fail-closed path missing")
    require("IsRegisteredLiveTarget" in pointer_probe and '"stale-interaction-target"' in pointer_probe,
            "destroyed/stale target rejection missing")
    require("ForageTargetLifecyclePolicy.Evaluate" in integrity and "interaction-target-rebuild-failed" in integrity,
            "per-node lifecycle/target rebuild gate missing")
    require("for (int i = _spawned.Count - 1; i >= 0; i--)" in controller and "RetireSpawnedNodeAt" in controller,
            "single-node failure containment missing")
    require("CancelActiveGather(ForageGatherCancelReason.ZoneChanged)" in controller and "DespawnAll();" in controller,
            "zone/scene teardown contract missing")
    require("RuntimeExceptionCleanup" in controller and "ForageGatherCancelReason.RuntimeException" in controller,
            "transaction exception cleanup missing")

    # Captured one-click RMB semantics and short-lived pointer ownership. Initial targeting is a
    # native RightClick concern; TickActiveGather validates the captured node directly and never
    # reacquires hover/aim or requires a held mouse button.
    require("IsSelectingRightButtonHeld" in gather_tick and
            'ReleaseActiveGatherInputOwnership("right-click-release")' in gather_tick,
            "initiating RMB ownership release proof missing")
    require("ForageGatherChannelPolicy.EvaluateUi" in gather_tick and "_captureState.IsGathering" in gather_tick,
            "captured one-click channel proof missing")
    for forbidden in ("TryResolvePointerTarget", "pointerResolved", "aimedNode", "AimLost", "Input.GetMouseButton(0)"):
        require(forbidden not in gather_tick, f"active gather regressed to live hover/button authority: {forbidden}")
    require("IsChatFocused()" in gather_tick and "ForageGatherCancelReason.Typing" in gather_tick,
            "chat input does not cancel before grant")
    require("CraftingUiPointerOwnership.HasOwners" in controller and "GameData.DraggingUIElement" in controller and
            "EventSystem.current" in controller and "IsPointerOverGameObject" in controller,
            "retained/native UI ownership gates missing")
    require("IsConflictingUiDuringCapturedGather" in gather_tick and "CraftingUiPointerOwnership.Reassert" in gather_tick,
            "short initiating pointer ownership is not revalidated/reasserted")
    require(gather_tick.find("ForageGatherChannelPolicy.EvaluateUi") < gather_tick.find("TryEnterGrantPending"),
            "UI cancellation is evaluated after grant admission")
    require("BeginGather(" not in gather_tick,
            "active channel can begin another gather without native RightClick admission")
    click_admission = method_body(controller, "internal static bool TryHandleNativeRightClick()")
    require(click_admission.count("BeginGather(") == 1,
            "native RightClick is not the single gather admission path")
    require("_captureState.SetHoverCandidate" in click_admission and "IsGatherLineOfSightBlocked" in click_admission,
            "initial RMB target capture/LOS proof missing")
    require("if (!pointsAtForage) return false;" in click_admission,
            "non-forage RightClick may be globally consumed")
    require("ForageInteractionEligibility.OutOfRange" in click_admission and
            'NotifyGatherFeedback("Out of range.")' in click_admission and "return false;" in click_admission,
            "out-of-range forage targeting may still steal native world RMB")
    require("KeyCode.G" not in "\n".join(p.read_text(encoding="utf-8-sig") for p in (SRC / "Foraging").glob("*.cs")),
            "G-key gathering path returned")

    capture = read("src/Foraging/ForageInteractionCaptureState.cs")
    channel = read("src/Foraging/ForageGatherHoldPolicy.cs")
    camera_patch = read("src/CraftingCameraUiOwnershipPatch.cs")
    require(all(token in capture for token in ("HoverCandidate", "Selected", "Gathering", "Completing", "Cancelled",
                                               "TrySelect", "TryBeginGathering", "TryBeginCompleting")),
            "one-click capture state machine incomplete")
    require("HasMeaningfulMovement" in channel and "HasActualHpDecrease" in channel,
            "movement/damage channel guards missing")
    require("MouseLookPrefix" in camera_patch and "OwnsCapturedPointerInput" in camera_patch,
            "initiating PlayerControl.MouseLook suppression missing")
    require("RightClick" in camera_patch and "References(rightClick, pointerOverUi)" in camera_patch,
            "current PlayerControl.RightClick UI-gate relationship is not runtime-verified")
    require("harmony.Patch(pointerOverUi" not in camera_patch,
            "global EventSystem patch would suppress LandMovement/WaterMovement during capture")
    require("LandMovement/WaterMovement" in camera_patch and "global EventSystem gate is not patched" in camera_patch,
            "movement-preserving ownership rationale/proof missing")
    require("Cursor.lockState" not in controller and "Cursor.visible" not in controller,
            "forage capture directly mutates cursor state")

    # Interaction target remains forgiving but query-only/nonblocking and deterministic.
    require("targetObject.layer = 2" in controller, "dedicated interaction layer missing")
    require("CapsuleCollider" in controller and "capsule.isTrigger = true" in controller,
            "interaction collider is not a trigger")
    interaction = read("src/Foraging/ForageInteractionPolicy.cs")
    require("MinimumHitRadius = 0.58f" in interaction and "MaximumHitRadius = 0.95f" in interaction,
            "forgiving hit-radius bounds changed")
    require("ShouldPreferPointerHit" in interaction and "StringComparison.Ordinal" in interaction,
            "overlap resolution is not deterministic")
    require("IsSolidOcclusion" in pointer_probe,
            "ordinary world occlusion is no longer respected")

    # Highlight remains mod-owned and cannot mutate plant shared materials.
    highlight = read("src/Foraging/ForageNodeHighlight.cs")
    require("LineRenderer" in highlight and "line.sharedMaterial = material" in highlight,
            "mod-owned highlight line renderer contract missing")
    require("renderer.material" not in highlight and "renderer.sharedMaterial" not in highlight,
            "highlight touches forage visual renderer material")
    require("private static Material _sharedMaterial" in highlight and "Shutdown()" in highlight,
            "highlight material lifetime is not cached/cleaned")
    require("Update(" not in method_body(highlight, "private void ApplyState()"),
            "highlight state unexpectedly allocates/updates per frame")

    # World label is detached, non-raycast UI and destroyed with node/lifecycle.
    label = read("src/Foraging/ForageNodeWorldLabel.cs")
    require("root.transform.rotation = Quaternion.identity" in label and "ForageNodeLabelBillboard" in label,
            "billboard world-label contract missing")
    require("raycastTarget = false" in label, "world label can steal pointer input")
    require("ForageNodePresentationOwner" in label and "OnDestroy()" in label,
            "world label does not follow node destruction lifetime")
    require("WorldLabel" in destroy_node and "LabelView = null" in destroy_node,
            "explicit world-label cleanup missing")
    require("TryAuditProductionVisual" in controller and "no-active-renderable-mesh" in controller and
            "post-ground-delta=" in controller and "renderer-anchor-offset=" in controller,
            "forage visuals can still bind presentation without active grounded renderer proof")
    require("forage_visual_spawn" in controller and "bindingAllowed=" in controller and
            "forage_visual_spawn_first_" in controller,
            "bounded production visual provenance diagnostics missing")
    require("AddComponent<Outline>" in label and "rendererAnchor" in label,
            "world label lacks hard border or renderer-backed anchor")

    # Content/donor safety. Bloomwood remains fail-closed until live ItemDB proves A/B/C.
    items = read("src/Items/ExpandedContentItems.cs")
    recipes = read("src/Crafting/ExpandedContentRecipes.cs")
    require(items.count("result.Add(") == 32, "expanded item catalog is not 32 entries")
    require(recipes.count("result.Add(") == 21, "expanded recipe catalog is not 21 entries")
    require('"Barkguard Maul"' in items and '"Primary", "TwoHandMelee", false' in items,
            "Barkguard Maul safe family regressed")
    require('"Ghostcap Shiv"' in items and '"PrimaryOrSecondary", "OneHandDagger", false' in items,
            "Ghostcap Shiv safe one-handed family regressed")
    require('"Bloomwood Staff"' in items and '18, "Primary", "TwoHandStaff", false' in items,
            "Bloomwood fail-closed requested family/tier was silently changed")
    donor_api = read("src/Compatibility/GameItemRegistryApi.cs")
    require("MatchesFamily" in donor_api and "nearest safe family donor above target=" in donor_api,
            "live ItemDB same-family floor diagnostic missing")
    require("retier evidence only; not auto-selected" in donor_api and "return nearestAbove" not in donor_api,
            "higher-level donor is being auto-selected")
    require("output unavailable for " in read("src/Crafting/ExpandedContentRecipeRegistry.cs"),
            "unavailable recipe output no longer fails closed")

    # Icon manifest must be complete with no orphan/missing PNGs.
    manifest_path = ROOT / "assets/item-art-manifest.json"
    data = json.loads(manifest_path.read_text(encoding="utf-8"))
    entries = data.get("items") or []
    require(data.get("schema_version") == 1 and len(entries) == 25,
            "item-art manifest must contain schema v1 and 25 entries")
    manifest_files = set()
    manifest_ids = set()
    for entry in entries:
        stable = str(entry.get("stable_item_id") or "")
        rel = str(entry.get("icon_asset_path") or "")
        require(stable and stable not in manifest_ids, f"duplicate/empty icon stable id: {stable}")
        manifest_ids.add(stable)
        require(rel.startswith("assets/icons/") and ".." not in rel, f"unsafe icon path: {rel}")
        p = ROOT / rel
        require(p.is_file(), f"manifest icon missing: {rel}")
        require(p.read_bytes()[:8] == b"\x89PNG\r\n\x1a\n", f"manifest icon is not PNG: {rel}")
        manifest_files.add(p.name)
    disk_files = {p.name for p in (ROOT / "assets/icons").glob("*.png")}
    require(manifest_files == disk_files, f"orphan/missing icon files: manifest={len(manifest_files)} disk={len(disk_files)}")

    # All changed C# source must at least survive a lexical structure gate here; RUN_TESTS.ps1 does
    # the real compiler/test execution on a Windows build host.
    for cs in SRC.rglob("*.cs"):
        validate_balanced_cs(cs)

    print("PASS runtime repair source/static contracts")
    print("PASS catalogs: expandedItems=32 productionRecipes=21 iconManifest=25 iconFiles=25")
    print("PASS Bloomwood: conservative Primary/TwoHandStaff remains fail-closed with live floor diagnostics")


if __name__ == "__main__":
    main()
