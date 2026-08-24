using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace ErenshorCraftingExpanded
{
    // One live spawned node: definition + pure runtime state + scene-bound visual metadata.
    // Scene objects themselves are never persisted. A bounded logical depletion ledger tracks
    // successful gathers by scene + resource family; Foraging progression persists only remaining
    // cooldown seconds per verified local character, never transient GameObjects or Erenshor save fields.
    internal sealed class SpawnedForageNode
    {
        internal ForageNodeDefinition Definition;
        internal ForageNodeRuntimeState State;
        internal GameObject Visual;
        internal GameObject WorldLabel;
        internal ForageNodeWorldLabelView LabelView;
        internal GameObject Highlight;
        internal ForageNodeHighlightView HighlightView;
        internal Renderer[] VisualRenderers;
        internal GameObject InteractionTarget;
        internal ForageNodeInteractionTarget InteractionComponent;
        internal bool PresentationInitialized;
        internal ForageAvailability LastPresentedAvailability;
        internal bool LastPresentedCompletionFeedback;
        internal float CompletionFeedbackRemaining;
        internal bool CompletionFeedbackLogged;
        internal bool PresentationFailureLogged;
        internal bool HighlightSelectionActive;
        internal bool IsDebugPlaceholder;
        internal bool IsAutoTrial;
        internal bool IsCoveredTrial;
        internal string TrialWallName = string.Empty;
        internal string TrialVisualSummary = string.Empty;
        internal string ResolvedMeshName = string.Empty;
        internal string ResolvedShaderName = string.Empty;
        internal bool TintApplied;
        internal Vector3 AppliedScale = Vector3.one;
    }

    internal static class ForageNodeController
    {
        internal static readonly ForageNodeCatalog Catalog = new ForageNodeCatalog();
        private static readonly List<SpawnedForageNode> _spawned = new List<SpawnedForageNode>();
        private static string _spawnedScene = string.Empty;
        private static bool _lastPoCNodeEnabled;
        private static bool _lastDebugPlaceholderEnabled;
        private static bool _lastExperimentalCoveredResources;
        private static int _autoTrialGeneration;
        private static int _autoTrialAttemptCount;
        private static float _autoTrialRetrySeconds;
        private static string _cachedVisualScene = string.Empty;
        private static ForageVisualSourceSet _cachedVisualSources;
        private static float _nextNegativeVisualRescanTime;
        private static Camera _interactionCamera;
        private static float _nextInteractionCameraProbe;
        private static float _nextTargetProbe;
        private static SpawnedForageNode _targetedNode;
        private static readonly RaycastHit[] _targetRayHits = new RaycastHit[8];
        private static readonly RaycastHit[] _occlusionRayHits = new RaycastHit[12];
        private static readonly HashSet<string> _visualFamilySuccessLogged = new HashSet<string>();
        private static readonly HashSet<string> _visualFamilyFailureLogged = new HashSet<string>();
        private static string _lastEligibilityNodeId = string.Empty;
        private static ForageInteractionEligibility _lastEligibility = ForageInteractionEligibility.NoNode;
        private static string _lastTargetInvariantKey = string.Empty;
        private static float _lastTargetInvariantLogAt = -999f;

        private const float CompletionFeedbackSeconds = 0.15f;
        private const float TargetInvariantLogCooldownSeconds = 5f;
        private const float GatherFeedbackCooldownSeconds = 0.75f;
        private static readonly object _forageInputOwner = new object();
        private static readonly ForageInteractionCaptureState _captureState = new ForageInteractionCaptureState();
        private static SpawnedForageNode _activeGatherNode;
        private static long _activeGatherToken;
        private static long _nextGatherToken;
        private static int _activeGatherStartHp = -1;
        private static int _activeGatherLastHp = -1;
        private static Vector3 _activeGatherStartPosition = Vector3.zero;
        private static string _activeGatherScene = string.Empty;
        private static string _activeGatherCharacterKey = string.Empty;
        private static string _activeRewardItemId = string.Empty;
        private static int _activeRewardQuantity;
        private static float _activeRespawnSeconds;
        private static bool _activeGatherAnimationStarted;
        private static bool _activeNativeGrantInvokeStarted;
        private static bool _activeGatherInputOwned;
        private static string _lastGatherFeedback = string.Empty;
        private static float _nextGatherFeedbackAt;
        // First vertical slice: when a scene has no curated authored forage entries, normal
        // Foraging uses the conservative runtime edge-placement policy. This is no longer tied to
        // the legacy PoC/debug-placeholder switches.
        private const bool AutoPlacementVerticalSliceEnabled = true;

        // The one Wild Herb node this mod tracks. It intentionally remains invalid until a human
        // supplies real /craftdiag forage scan + forage pos evidence. Catalog.Validate is the
        // authoritative gate that prevents placeholder data from ever spawning.
        private static readonly ForageNodeDefinition _candidateDefinition = BuildCandidateDefinition();
        private static readonly ForageDefinitionRejectReason _candidateRejectReason;

        internal static string LastSpawnFailureReason = string.Empty;
        internal static string LastGatherSummary = string.Empty;
        internal static string LastFailureReason = string.Empty;
        internal static string LastAutoTrialSummary = string.Empty;
        internal static string LastTargetSummary = "target=(none)";
        internal static string LastEligibilitySummary = "eligibility=no-target";
        internal static string LastGatherTransactionSummary = "gather=(none)";
        internal static string LastGatherCancelReason = "none";
        internal static string LastGrantResult = "none";
        internal static string LastNameplateSummary = "nameplate=(none)";
        internal static string LastTargetInvariantSummary = "targetInvariant=healthy";

        internal static bool OwnsCapturedPointerInput
        {
            // Pointer/camera ownership lasts only through the initiating RMB press. The gather
            // transaction remains captured after release so normal camera movement can resume.
            get { return _activeGatherInputOwned; }
        }

        static ForageNodeController()
        {
            // Production content is a separate curated catalog. The developer candidate is
            // validated but never registered as production content; EnablePoCNode therefore
            // cannot accidentally gate real authored nodes.
            ForageAuthoredNodes.RegisterAll(Catalog);
            _candidateRejectReason = ForageNodeCatalog.Validate(_candidateDefinition, Catalog);
        }

        private static ForageNodeDefinition BuildCandidateDefinition()
        {
            return new ForageNodeDefinition
            {
                Id = "WildHerb_001",
                DisplayName = "Wild Herb",
                Scene = ForagingConfig.UnsurveyedLabel,
                PositionSet = false,
                VisualSourceScene = string.Empty,
                VisualSourceHierarchyPath = string.Empty,
                Scale = 1.3f,
                TintEnabled = false,
                RespawnSeconds = 300f,
                RewardItemId = CraftingExpandedItemIds.WildHerbId,
                RewardQuantity = 1
            };
        }

        internal static void Tick(float deltaSeconds)
        {
            if (!ForagingConfig.EnableForaging.Value)
            {
                CancelActiveGather(ForageGatherCancelReason.GameplayDisabled);
                if (_spawned.Count > 0) DespawnAll();
                return;
            }

            string scene = SafeSceneName();
            WorldThreatRuntime.Tick(scene, Time.unscaledTime);
            bool pocNodeEnabled = ForagingConfig.EnablePoCNode.Value;
            bool debugPlaceholderEnabled = ForagingConfig.AllowDebugPlaceholderVisual.Value;
            bool experimentalCoveredResources = ForagingConfig.ExperimentalCoveredResources != null && ForagingConfig.ExperimentalCoveredResources.Value;
            if (!string.Equals(scene, _spawnedScene, System.StringComparison.OrdinalIgnoreCase) ||
                pocNodeEnabled != _lastPoCNodeEnabled ||
                debugPlaceholderEnabled != _lastDebugPlaceholderEnabled ||
                experimentalCoveredResources != _lastExperimentalCoveredResources)
            {
                _lastPoCNodeEnabled = pocNodeEnabled;
                _lastDebugPlaceholderEnabled = debugPlaceholderEnabled;
                _lastExperimentalCoveredResources = experimentalCoveredResources;
                CancelActiveGather(ForageGatherCancelReason.ZoneChanged);
                RespawnForScene(scene);
            }

            if (IsAutoPlacementEnabledForScene(scene) && WorldThreatRuntime.Ready &&
                AutoTrialCount() == 0 && _autoTrialAttemptCount > 0 && _autoTrialAttemptCount < 5)
            {
                _autoTrialRetrySeconds -= deltaSeconds;
                if (_autoTrialRetrySeconds <= 0f)
                {
                    _autoTrialAttemptCount++;
                    _autoTrialGeneration++;
                    if (TrySpawnAutoTrial(scene) > 0) _autoTrialAttemptCount = 0;
                    else _autoTrialRetrySeconds = 2f;
                }
            }

            // A single destroyed/half-unloaded scene object must never abort the entire Crafting
            // Update. Validate from the back so an invalid node can be retired without invalidating
            // iteration, and contain presentation failures to that one node.
            for (int i = _spawned.Count - 1; i >= 0; i--)
            {
                SpawnedForageNode node = _spawned[i];
                string invariantReason;
                if (!EnsureNodeRuntimeIntegrity(node, out invariantReason))
                {
                    RetireSpawnedNodeAt(i, invariantReason);
                    continue;
                }
                try
                {
                    node.State.Tick(deltaSeconds);
                    TickCompletionFeedback(node, deltaSeconds);
                    UpdateVisualForState(node);
                    UpdateDynamicGatherPresentation(node);
                }
                catch (System.Exception ex)
                {
                    RetireSpawnedNodeAt(i, "node-tick-" + ex.GetType().Name);
                }
            }

            // Guard evaluation deliberately runs after elapsed time advances but before the node is
            // allowed into GrantPending. Movement/damage/player-state/range/LOS cancellation on the
            // exact duration boundary therefore wins and can never race a reward grant.
            TickActiveGather();
            UpdateTargetedNode();
        }

        internal static bool TryHandleNativeRightClick()
        {
            // PlayerControl.RightClick is the current native world-RMB boundary. This prefix
            // independently respects real UI/chat ownership because it executes before native
            // RightClick's own EventSystem guard.
            if (ForagingConfig.EnableForaging == null || !ForagingConfig.EnableForaging.Value) return false;

            bool typing = IsChatFocused();
            if (IsPointerOwnedByUi())
            {
                if (_activeGatherNode != null) CancelActiveGather(ForageGatherCancelReason.UiOwned);
                return false;
            }
            if (typing)
            {
                if (_activeGatherNode != null) CancelActiveGather(ForageGatherCancelReason.Typing);
                return false;
            }

            SpawnedForageNode selected;
            float pointerRayDistance;
            bool pointsAtForage = TryResolvePointerTarget(out selected, out pointerRayDistance) && selected != null;

            if (_activeGatherNode != null)
            {
                ForageActiveGatherClickAction activeAction = ForageActiveGatherClickPolicy.Evaluate(
                    false, IsConflictingUiDuringCapturedGather(), pointsAtForage);
                if (activeAction == ForageActiveGatherClickAction.CancelUiPassThrough)
                {
                    CancelActiveGather(ForageGatherCancelReason.UiOwned);
                    return false;
                }
                if (activeAction == ForageActiveGatherClickAction.CancelTypingPassThrough)
                {
                    CancelActiveGather(ForageGatherCancelReason.Typing);
                    return false;
                }
                if (activeAction == ForageActiveGatherClickAction.CancelWorldPassThrough)
                {
                    CancelActiveGather(ForageGatherCancelReason.WorldInteraction);
                    return false;
                }

                LastGatherTransactionSummary = "gather=active node=" + SafeNodeId(_activeGatherNode) +
                    " token=" + _activeGatherToken.ToString() + " duplicate-forage-rmb=consumed";
                return true;
            }

            // Non-forage RMB is never consumed by Crafting.
            if (!pointsAtForage) return false;

            float interactionRange = ForagingConfig.InteractionRange.Value;
            if (!ForagingRuntimeConfigValidation.IsValidInteractionRange(interactionRange))
            {
                LastFailureReason = "Configured ForagingInteractionRange is invalid; gather suppressed.";
                LastGatherTransactionSummary = "attempt=suppressed reason=invalid-range";
                return true;
            }

            Vector3 playerPos;
            if (!GameForagingApi.TryGetPlayerPosition(out playerPos))
            {
                LastFailureReason = "Could not resolve player position.";
                LastGatherTransactionSummary = "attempt=suppressed reason=player-position-unavailable";
                return true;
            }

            float playerDistance = selected.Visual == null
                ? float.PositiveInfinity
                : Vector3.Distance(playerPos, selected.Visual.transform.position);
            ForageResourceDefinition selectedResource = selected.Definition == null
                ? null
                : ForageResourceCatalog.FindByRewardItemId(selected.Definition.RewardItemId);
            int requiredSkill = selectedResource == null ? 1 : selectedResource.MinimumSkill;
            ForageInteractionEvaluation eligibility = ForageInteractionPolicy.Evaluate(
                true,
                selected.State != null && selected.State.Availability == ForageAvailability.Available,
                playerDistance,
                interactionRange,
                ForagingProgressionController.IsReady,
                ForagingProgressionController.CurrentLevel,
                requiredSkill);

            _targetedNode = selected;
            _captureState.SetHoverCandidate(SafeNodeId(selected));
            RecordEligibility(selected, playerDistance, eligibility);
            LastTargetSummary = "target=" + SafeNodeId(selected) +
                " name={" + SafeDisplayName(selected) + "}" +
                " pointerRay=" + pointerRayDistance.ToString("F2") + "m" +
                " player=" + playerDistance.ToString("F2") + "m";

            if (!eligibility.CanGather)
            {
                LastFailureReason = eligibility.Reason;
                LastGatherTransactionSummary = "attempt=rejected node=" + SafeNodeId(selected) + " reason={" + eligibility.Reason + "}";
                if (selectedResource != null &&
                    (eligibility.Eligibility == ForageInteractionEligibility.SkillTooLow ||
                     eligibility.Eligibility == ForageInteractionEligibility.ProgressionUnavailable))
                    ForagingProgressionController.NotifyRequirement(selectedResource);
                else if (eligibility.Eligibility == ForageInteractionEligibility.OutOfRange)
                {
                    NotifyGatherFeedback("Out of range.");
                    // Preserve native RMB when the enlarged interaction volume is not actually
                    // usable; this avoids stealing a nearby NPC/world interaction.
                    return false;
                }
                return true;
            }

            if (IsGatherLineOfSightBlocked(selected, playerPos))
            {
                LastFailureReason = "A world obstruction blocks this forage resource.";
                LastGatherTransactionSummary = "attempt=rejected node=" + SafeNodeId(selected) + " reason=true-los-blocked";
                NotifyGatherFeedback("Something blocks the resource.");
                return true;
            }

            bool localAggro;
            string localAggroDetail;
            bool localAggroKnown = GameForagingApi.TryGetLocalHostileAggro(out localAggro, out localAggroDetail);
            bool globalCombat = GameForagingApi.IsGlobalCombat();
            if (!ForageCombatEligibilityPolicy.CanBeginOrContinue(localAggroKnown, localAggro))
            {
                LastFailureReason = "Cannot forage while a hostile has you engaged.";
                LastGatherTransactionSummary = "attempt=rejected node=" + SafeNodeId(selected) +
                    " reason=" + ForageCombatEligibilityPolicy.DiagnosticToken(globalCombat, localAggroKnown, localAggro) +
                    " probe=" + localAggroDetail;
                NotifyGatherFeedback("Cannot forage while a hostile has you engaged.");
                return true;
            }

            if (!CraftingCameraUiOwnershipPatch.IsInstalled)
            {
                LastFailureReason = "Verified camera/input ownership is unavailable for this game build.";
                LastGatherTransactionSummary = "attempt=rejected node=" + SafeNodeId(selected) + " reason=input-ownership-unavailable";
                NotifyGatherFeedback("Foraging input ownership is unavailable.");
                return true;
            }

            BeginGather(selected, playerPos, selectedResource);
            return true;
        }

        private static void UpdateTargetedNode()
        {
            float now = Time.unscaledTime;
            if (now < _nextTargetProbe) return;
            _nextTargetProbe = now + ForageInteractionPolicy.TargetProbeIntervalSeconds;

            // Selected/Gathering owns an exact node identity. Hover rays are no longer
            // transaction authority until that captured channel has terminated.
            if (_activeGatherNode != null && _captureState.IsCaptured(_activeGatherToken))
            {
                SpawnedForageNode captured = _activeGatherNode;
                if (IsRegisteredLiveTarget(captured))
                {
                    SpawnedForageNode previousCapturedTarget = _targetedNode;
                    if (previousCapturedTarget != captured) SetNodeInteractionStateSafe(previousCapturedTarget, false, false);
                    _targetedNode = captured;
                    SetNodeInteractionStateSafe(captured, true, true);
                    LastTargetSummary = "target=" + SafeNodeId(captured) + " capture=locked";
                    MarkTargetInvariantHealthy();
                    return;
                }
            }

            // Title/loading/scene teardown normally has no live forage nodes. Do not probe a camera
            // (or emit camera-unavailable diagnostics) until there is actually a node to target.
            if (_spawned.Count == 0)
            {
                SetNodeInteractionStateSafe(_targetedNode, false, false);
                _targetedNode = null;
                _captureState.SetHoverCandidate(string.Empty);
                LastTargetSummary = "target=(none)";
                _lastEligibilityNodeId = string.Empty;
                _lastEligibility = ForageInteractionEligibility.NoNode;
                LastEligibilitySummary = "node=(none) state=NoNode reason={No forage resource under pointer.}";
                MarkTargetInvariantHealthy();
                return;
            }

            SpawnedForageNode previous = _targetedNode;
            SpawnedForageNode next;
            float hitDistance;
            if (IsPointerOwnedByUi() || !TryResolvePointerTarget(out next, out hitDistance))
            {
                next = null;
                hitDistance = float.PositiveInfinity;
            }

            if (next != null && !IsRegisteredLiveTarget(next))
            {
                RecordTargetInvariantFailure("resolved-target-not-live", next);
                next = null;
                hitDistance = float.PositiveInfinity;
            }

            bool targetChanged = previous != next;
            if (targetChanged) SetNodeInteractionStateSafe(previous, false, false);
            _targetedNode = next;
            _captureState.SetHoverCandidate(next == null ? string.Empty : SafeNodeId(next));
            if (next == null)
            {
                LastTargetSummary = "target=(none)";
                if (!string.IsNullOrEmpty(_lastEligibilityNodeId) || _lastEligibility != ForageInteractionEligibility.NoNode)
                {
                    _lastEligibilityNodeId = string.Empty;
                    _lastEligibility = ForageInteractionEligibility.NoNode;
                    LastEligibilitySummary = "node=(none) state=NoNode reason={No forage resource under pointer.}";
                }
                return;
            }

            Vector3 playerPos;
            float playerDistance = GameForagingApi.TryGetPlayerPosition(out playerPos) && next.Visual != null
                ? Vector3.Distance(playerPos, next.Visual.transform.position)
                : float.PositiveInfinity;
            ForageResourceDefinition resource = next.Definition == null ? null : ForageResourceCatalog.FindByRewardItemId(next.Definition.RewardItemId);
            ForageInteractionEvaluation evaluation = ForageInteractionPolicy.Evaluate(
                true,
                next.State != null && next.State.Availability == ForageAvailability.Available,
                playerDistance,
                ForagingConfig.InteractionRange == null ? 0f : ForagingConfig.InteractionRange.Value,
                ForagingProgressionController.IsReady,
                ForagingProgressionController.CurrentLevel,
                resource == null ? 1 : resource.MinimumSkill);
            LastTargetSummary = "target=" + SafeNodeId(next) +
                " name={" + SafeDisplayName(next) + "}" +
                " pointerRay=" + (float.IsInfinity(hitDistance) ? "?" : hitDistance.ToString("F2") + "m") +
                " player=" + (float.IsInfinity(playerDistance) ? "?" : playerDistance.ToString("F2") + "m");
            if (targetChanged) CraftingController.LogInfo("Foraging forage_hover_acquired node=" + SafeNodeId(next));
            SetNodeInteractionStateSafe(next, true, evaluation.CanGather);
            RecordEligibility(next, playerDistance, evaluation);
            MarkTargetInvariantHealthy();
        }

        private static bool TryResolvePointerTarget(out SpawnedForageNode node, out float hitDistance)
        {
            node = null;
            hitDistance = float.PositiveInfinity;
            try
            {
                Camera camera = ResolveInteractionCamera(Time.unscaledTime);
                if (camera == null)
                {
                    RecordTargetInvariantFailure("camera-unavailable", null);
                    return false;
                }
                Ray ray = camera.ScreenPointToRay(Input.mousePosition);
                int mask = 1 << 2; // Dedicated Ignore Raycast layer, explicitly queried only by this mod.
                int hitCount = Physics.RaycastNonAlloc(ray, _targetRayHits, ForageInteractionPolicy.TargetProbeDistance, mask, QueryTriggerInteraction.Collide);
                for (int i = 0; i < hitCount; i++)
                {
                    RaycastHit hit = _targetRayHits[i];
                    Collider hitCollider = hit.collider;
                    if (hitCollider == null) continue;
                    ForageNodeInteractionTarget target = null;
                    try { target = hitCollider.GetComponent<ForageNodeInteractionTarget>(); }
                    catch (System.Exception ex)
                    {
                        RecordTargetInvariantFailure("target-component-" + ex.GetType().Name, null);
                        continue;
                    }
                    if (target == null || target.Node == null || !IsRegisteredLiveTarget(target.Node))
                    {
                        if (target != null) RecordTargetInvariantFailure("stale-interaction-target", target.Node);
                        continue;
                    }
                    string candidateId = SafeNodeId(target.Node);
                    string selectedId = node == null ? string.Empty : SafeNodeId(node);
                    if (!ForageInteractionPolicy.ShouldPreferPointerHit(hit.distance, candidateId, hitDistance, selectedId)) continue;
                    node = target.Node;
                    hitDistance = hit.distance;
                }
                if (node == null) return false;

                // The dedicated target layer makes vanilla rays ignore forage hitboxes, but our
                // click must still respect ordinary world occlusion. Reject a target hidden behind
                // a nearer solid collider instead of allowing click-through harvesting.
                int worldMask = ~(1 << 2);
                int blockerCount = Physics.RaycastNonAlloc(ray, _occlusionRayHits, hitDistance, worldMask, QueryTriggerInteraction.Ignore);
                Transform playerTransform;
                GameForagingApi.TryGetPlayerTransform(out playerTransform);
                for (int i = 0; i < blockerCount; i++)
                {
                    RaycastHit blocker = _occlusionRayHits[i];
                    Collider blockerCollider = blocker.collider;
                    if (blockerCollider == null) continue;
                    bool localPlayer = false;
                    try
                    {
                        localPlayer = playerTransform != null &&
                            (blockerCollider.transform == playerTransform || blockerCollider.transform.IsChildOf(playerTransform));
                    }
                    catch { localPlayer = false; }
                    // A cloned native forage visual can carry its own ordinary collider. That
                    // collider sits in front of our enlarged Ignore-RayCast interaction target and
                    // must not make the node occlude itself. Only unrelated world geometry should
                    // cancel pointer ownership here.
                    if (IsColliderOwnedByNodeVisual(node, blockerCollider)) continue;
                    if (ForageInteractionPolicy.IsSolidOcclusion(hitDistance, blocker.distance, localPlayer))
                    {
                        node = null;
                        hitDistance = float.PositiveInfinity;
                        return false;
                    }
                }
                return true;
            }
            catch (System.Exception ex)
            {
                RecordTargetInvariantFailure("pointer-probe-" + ex.GetType().Name, node);
                node = null;
                hitDistance = float.PositiveInfinity;
                return false;
            }
        }

        private static Camera ResolveInteractionCamera(float now)
        {
            if (_interactionCamera != null && _interactionCamera.isActiveAndEnabled && now < _nextInteractionCameraProbe)
                return _interactionCamera;
            _nextInteractionCameraProbe = now + 0.35f;
            // Pointer targeting has no resource layer before the ray resolves, so select the best
            // active screen/world camera without a culling-layer assumption.
            _interactionCamera = ForageGameplayCameraResolver.Resolve(_interactionCamera, -1);
            return _interactionCamera;
        }

        private static bool IsPointerOwnedByUi()
        {
            try { if (CraftingUiPointerOwnership.HasOwners) return true; } catch { }
            try
            {
                if (GameData.DraggingUIElement) return true;
            }
            catch { }
            try
            {
                EventSystem eventSystem = EventSystem.current;
                if (eventSystem != null && eventSystem.IsPointerOverGameObject()) return true;
            }
            catch { }
            return false;
        }

        private static bool IsConflictingUiDuringCapturedGather()
        {
            // Do not treat this gather's own DraggingUIElement/UsingUI promotion as foreign UI.
            // A second retained-Crafting owner, a real EventSystem hit, or the raw native UsingUI
            // answer is a genuine competing owner and cancels the captured gesture.
            try { if (CraftingUiPointerOwnership.HasOtherOwners(_forageInputOwner)) return true; } catch { }
            try
            {
                EventSystem eventSystem = EventSystem.current;
                if (eventSystem != null && eventSystem.IsPointerOverGameObject()) return true;
            }
            catch { }
            try { if (CraftingCameraUiOwnershipPatch.NativeUiRecentlyActive(Time.unscaledTime)) return true; } catch { }
            return false;
        }

        private static bool AcquireActiveGatherInputOwnership()
        {
            if (_activeGatherInputOwned) return true;
            if (!CraftingCameraUiOwnershipPatch.IsInstalled) return false;
            try
            {
                CraftingUiPointerOwnership.Acquire(_forageInputOwner);
                _activeGatherInputOwned = CraftingUiPointerOwnership.HasOwner(_forageInputOwner);
            }
            catch
            {
                _activeGatherInputOwned = false;
            }
            if (_activeGatherInputOwned)
                CraftingController.LogInfo("Foraging forage_input_owned node=" + SafeNodeId(_activeGatherNode) +
                    " token=" + _activeGatherToken.ToString());
            return _activeGatherInputOwned;
        }

        private static void ReleaseActiveGatherInputOwnership(string terminal)
        {
            bool owned = _activeGatherInputOwned;
            try { owned = owned || CraftingUiPointerOwnership.HasOwner(_forageInputOwner); } catch { }
            _activeGatherInputOwned = false;
            if (!owned) return;
            try { CraftingUiPointerOwnership.Release(_forageInputOwner); } catch { }
            CraftingController.LogInfo("Foraging forage_input_released node=" + SafeNodeId(_activeGatherNode) +
                " token=" + _activeGatherToken.ToString() + " terminal=" + (terminal ?? "unknown"));
        }

        private static void ReleaseCapturedInteraction(string terminal)
        {
            bool hadCapture = _activeGatherInputOwned ||
                _captureState.Phase == ForageInteractionCapturePhase.Selected ||
                _captureState.Phase == ForageInteractionCapturePhase.Gathering ||
                _captureState.Phase == ForageInteractionCapturePhase.Completing ||
                _captureState.Phase == ForageInteractionCapturePhase.Cancelled;
            if (!hadCapture) return;

            string nodeId = SafeNodeId(_activeGatherNode);
            long token = _activeGatherToken;
            ReleaseActiveGatherInputOwnership(terminal);
            _captureState.Release(terminal);
            CraftingController.LogInfo("Foraging forage_capture_release node=" + nodeId +
                " token=" + token.ToString() + " terminal=" + (terminal ?? "unknown"));
        }

        private static bool IsSelectingRightButtonHeld()
        {
            try { return Input.GetMouseButton(1); }
            catch { return false; }
        }

        private static bool EnsureNodeRuntimeIntegrity(SpawnedForageNode node, out string reason)
        {
            reason = string.Empty;
            bool visualAlive = false;
            bool targetAlive = false;
            bool componentAlive = false;
            bool colliderAlive = false;
            bool labelAlive = false;
            bool highlightAlive = false;
            string visualIntegrity;
            Bounds ignoredVisualBounds;
            try { visualAlive = node != null && node.Visual != null && TryAuditProductionVisual(node.Visual, node.IsAutoTrial, out ignoredVisualBounds, out visualIntegrity); } catch { visualAlive = false; }
            try { targetAlive = node != null && node.InteractionTarget != null; } catch { targetAlive = false; }
            try { componentAlive = node != null && node.InteractionComponent != null; } catch { componentAlive = false; }
            try { colliderAlive = componentAlive && node.InteractionComponent.HitCollider != null; } catch { colliderAlive = false; }
            try { labelAlive = node != null && node.WorldLabel != null && node.LabelView != null; } catch { labelAlive = false; }
            try { highlightAlive = node != null && (node.Highlight == null || node.HighlightView != null); } catch { highlightAlive = false; }

            ForageNodeLifecycleAction action = ForageTargetLifecyclePolicy.Evaluate(
                node != null,
                node != null && node.Definition != null,
                node != null && node.State != null,
                visualAlive,
                targetAlive,
                componentAlive,
                colliderAlive,
                labelAlive,
                highlightAlive);
            if (action == ForageNodeLifecycleAction.Healthy) return true;
            if (action == ForageNodeLifecycleAction.RetireNode)
            {
                reason = !visualAlive ? "visual-invalid-or-destroyed" : "core-state-invalid";
                return false;
            }

            // The interaction target is entirely mod-owned and query-only, so it is safe to rebuild
            // after a destroyed collider/component without touching the visual/resource state.
            try
            {
                if (node.InteractionTarget != null) UnityEngine.Object.Destroy(node.InteractionTarget);
            }
            catch { }
            try
            {
                if (node.Highlight != null) UnityEngine.Object.Destroy(node.Highlight);
            }
            catch { }
            node.InteractionTarget = null;
            node.InteractionComponent = null;
            node.Highlight = null;
            node.HighlightView = null;
            node.PresentationInitialized = false;
            Bounds repairedBounds;
            string repairedVisualReason;
            if (!TryAuditProductionVisual(node.Visual, node.IsAutoTrial, out repairedBounds, out repairedVisualReason) ||
                !AttachInteractionTarget(node, repairedBounds))
            {
                reason = "interaction-target-rebuild-visual-invalid:" + repairedVisualReason;
                return false;
            }
            try
            {
                if (node.InteractionTarget != null && node.InteractionComponent != null && node.InteractionComponent.HitCollider != null)
                {
                    RecordTargetInvariantFailure("interaction-target-rebuilt", node);
                    return true;
                }
            }
            catch { }
            reason = "interaction-target-rebuild-failed";
            return false;
        }

        private static bool IsRegisteredLiveTarget(SpawnedForageNode node)
        {
            if (node == null || node.Definition == null || node.State == null) return false;
            try
            {
                return _spawned.Contains(node) &&
                    node.Visual != null &&
                    node.InteractionTarget != null &&
                    node.InteractionComponent != null &&
                    node.InteractionComponent.HitCollider != null &&
                    node.InteractionComponent.HitCollider.enabled;
            }
            catch { return false; }
        }

        private static void SetNodeInteractionStateSafe(SpawnedForageNode node, bool targeted, bool ready)
        {
            if (node == null) return;
            bool selected = node == _activeGatherNode && _captureState.IsCaptured(_activeGatherToken);
            try
            {
                if (node.HighlightView != null) node.HighlightView.SetInteractionState(targeted, selected, ready);
                if (node.HighlightSelectionActive != selected)
                {
                    node.HighlightSelectionActive = selected;
                    CraftingController.LogInfo("Foraging forage_highlight_" + (selected ? "on" : "off") +
                        " node=" + SafeNodeId(node));
                }
            }
            catch (System.Exception ex)
            {
                RecordTargetInvariantFailure("highlight-" + ex.GetType().Name, node);
            }
            try { if (node.LabelView != null) node.LabelView.SetInteractionState(targeted || selected, ready && selected); }
            catch (System.Exception ex)
            {
                RecordTargetInvariantFailure("label-state-" + ex.GetType().Name, node);
            }
        }

        private static void RetireSpawnedNodeAt(int index, string reason)
        {
            if (index < 0 || index >= _spawned.Count) return;
            SpawnedForageNode node = _spawned[index];
            if (_activeGatherNode == node) CancelActiveGather(ForageGatherCancelReason.TargetInvalid);
            if (_targetedNode == node)
            {
                SetNodeInteractionStateSafe(node, false, false);
                _targetedNode = null;
                LastTargetSummary = "target=(none)";
            }
            RecordTargetInvariantFailure("node-retired-" + (reason ?? "invalid"), node);
            DestroyNodeOwnedObjects(node);
            _spawned.RemoveAt(index);
        }

        private static void DestroyNodeOwnedObjects(SpawnedForageNode node)
        {
            if (node == null) return;
            try { if (node.WorldLabel != null) UnityEngine.Object.Destroy(node.WorldLabel); } catch { }
            try { if (node.InteractionTarget != null) UnityEngine.Object.Destroy(node.InteractionTarget); } catch { }
            try { if (node.Highlight != null) UnityEngine.Object.Destroy(node.Highlight); } catch { }
            try { if (node.Visual != null) UnityEngine.Object.Destroy(node.Visual); } catch { }
            node.WorldLabel = null;
            node.LabelView = null;
            node.InteractionTarget = null;
            node.InteractionComponent = null;
            node.Highlight = null;
            node.HighlightView = null;
            node.Visual = null;
            node.VisualRenderers = null;
        }

        private static void RecordTargetInvariantFailure(string reason, SpawnedForageNode node)
        {
            string key = (reason ?? "unknown") + ":" + SafeNodeId(node);
            LastTargetInvariantSummary = "targetInvariant=" + (reason ?? "unknown") + " node=" + SafeNodeId(node);
            float now;
            try { now = Time.unscaledTime; } catch { now = _lastTargetInvariantLogAt + TargetInvariantLogCooldownSeconds; }
            if (string.Equals(_lastTargetInvariantKey, key, System.StringComparison.Ordinal) &&
                now < _lastTargetInvariantLogAt + TargetInvariantLogCooldownSeconds)
                return;
            _lastTargetInvariantKey = key;
            _lastTargetInvariantLogAt = now;
            CraftingController.LogInfo("Foraging target_invariant " + LastTargetInvariantSummary);
        }

        private static void MarkTargetInvariantHealthy()
        {
            LastTargetInvariantSummary = "targetInvariant=healthy";
        }

        private static void RecordEligibility(SpawnedForageNode node, float distance, ForageInteractionEvaluation evaluation)
        {
            string nodeId = SafeNodeId(node);
            if (ForageInteractionPolicy.ShouldLogEligibilityTransition(_lastEligibilityNodeId, _lastEligibility, nodeId, evaluation.Eligibility))
            {
                _lastEligibilityNodeId = nodeId;
                _lastEligibility = evaluation.Eligibility;
                LastEligibilitySummary = "node=" + nodeId +
                    " state=" + evaluation.Eligibility +
                    " distance=" + (float.IsInfinity(distance) ? "?" : distance.ToString("F2") + "m") +
                    " reason={" + evaluation.Reason + "}";
            }
        }

        private static string SafeNodeId(SpawnedForageNode node)
        {
            return node == null || node.Definition == null || string.IsNullOrEmpty(node.Definition.Id) ? "(none)" : node.Definition.Id;
        }

        private static string SafeDisplayName(SpawnedForageNode node)
        {
            return node == null || node.Definition == null || string.IsNullOrEmpty(node.Definition.DisplayName) ? "Forage Resource" : node.Definition.DisplayName;
        }

        private static bool IsChatFocused()
        {
            try { return GameData.PlayerTyping; }
            catch { return true; }
        }

        private static void BeginGather(SpawnedForageNode node, Vector3 playerPos, ForageResourceDefinition resource)
        {
            string nodeId = SafeNodeId(node);
            if (node == null || node.Definition == null || node.State == null || node.State.Availability != ForageAvailability.Available)
            {
                LastFailureReason = "Resource is not available.";
                LastGatherTransactionSummary = "gather=rejected node=" + nodeId + " reason=not-available";
                return;
            }

            int startHp;
            if (!GameForagingApi.TryGetPlayerCurrentHp(out startHp))
            {
                LastFailureReason = "Player state is unavailable.";
                LastGatherTransactionSummary = "gather=rejected node=" + nodeId + " reason=player-state-unavailable";
                NotifyGatherFeedback("Cannot gather right now.");
                return;
            }

            string characterKey = ForagingProgressionController.CurrentCharacterKey;
            if (string.IsNullOrEmpty(characterKey))
            {
                LastFailureReason = "Foraging progression is waiting for the active character save slot.";
                LastGatherTransactionSummary = "gather=rejected node=" + nodeId + " reason=character-unavailable";
                return;
            }

            float configuredDuration = ForagingConfig.GatherDurationSeconds == null
                ? ForagingRuntimeConfigValidation.DefaultGatherDurationSeconds
                : ForagingConfig.GatherDurationSeconds.Value;
            float duration = ForagingRuntimeConfigValidation.NormalizeGatherDuration(configuredDuration);
            float effectiveRespawn = EffectiveRespawnSeconds(node);
            long token = NextGatherToken();

            if (!_captureState.TrySelect(nodeId, token))
            {
                LastFailureReason = "Another forage interaction is already selected.";
                LastGatherTransactionSummary = "gather=rejected node=" + nodeId + " reason=selection-state";
                return;
            }

            _activeGatherNode = node;
            _activeGatherToken = token;
            _activeGatherStartHp = startHp;
            _activeGatherLastHp = startHp;
            _activeGatherStartPosition = playerPos;
            _activeGatherScene = SafeSceneName();
            _activeGatherCharacterKey = characterKey;
            _activeRewardItemId = node.Definition.RewardItemId ?? string.Empty;
            _activeRewardQuantity = node.Definition.RewardQuantity;
            _activeRespawnSeconds = effectiveRespawn;
            _activeGatherAnimationStarted = false;
            _activeNativeGrantInvokeStarted = false;
            _activeGatherInputOwned = false;
            LastGatherCancelReason = "none";
            LastGrantResult = "none";
            LastFailureReason = string.Empty;
            node.CompletionFeedbackRemaining = 0f;
            node.CompletionFeedbackLogged = false;
            node.PresentationFailureLogged = false;

            SetNodeInteractionStateSafe(node, true, true);
            CraftingController.LogInfo("Foraging forage_selected node=" + nodeId + " token=" + token.ToString());

            // Own only the initiating RMB press so CameraController cannot treat the selecting
            // click as orbit input. Ownership is released as soon as RMB is physically released;
            // the transaction continues independently and camera movement is then allowed.
            if (!AcquireActiveGatherInputOwnership())
            {
                _captureState.MarkCancelled(token, "input-ownership-unavailable");
                _captureState.Release("input-ownership-unavailable");
                LastFailureReason = "Verified camera/input ownership could not be acquired.";
                LastGatherCancelReason = ForageGatherCancellationPolicy.Describe(ForageGatherCancelReason.InputOwnershipUnavailable);
                LastGatherTransactionSummary = "gather=rejected node=" + nodeId +
                    " token=" + token.ToString() + " reason=" + LastGatherCancelReason;
                NotifyGatherFeedback("Foraging input ownership is unavailable.");
                ClearActiveGatherSnapshot();
                return;
            }

            if (!node.State.TryBeginGather(token, duration) || !_captureState.TryBeginGathering(token))
            {
                try { node.State.CancelGather(token); } catch { }
                _captureState.MarkCancelled(token, "state-transition");
                LastFailureReason = "Resource is already being gathered or depleted.";
                LastGatherCancelReason = "node-invalid";
                LastGatherTransactionSummary = "gather=rejected node=" + nodeId + " reason=state-transition";
                ReleaseCapturedInteraction("state-transition");
                ClearActiveGatherSnapshot();
                return;
            }

            bool animationRequested = ForagingConfig.UseNativeGatherAnimation != null && ForagingConfig.UseNativeGatherAnimation.Value;
            bool animationStarted = false;
            if (animationRequested)
            {
                _activeGatherAnimationStarted = true;
                animationStarted = GameForagingApi.TryStartNativeGatherAnimation();
            }

            UpdateVisualForState(node);
            SetNodeInteractionStateSafe(node, true, true);
            UpdateDynamicGatherPresentation(node);
            LastGatherTransactionSummary = "gather=Gathering node=" + nodeId +
                " token=" + token.ToString() +
                " elapsed=0.00 duration=" + duration.ToString("F2") +
                " capture=channel input=rmb-edge-owned" +
                " animation=" + (animationRequested ? (animationStarted ? "started" : "requested-unavailable") : "off");
            CraftingController.LogInfo("Foraging forage_capture_begin node=" + nodeId +
                " token=" + token.ToString() + " duration=" + duration.ToString("F2"));
            CraftingController.LogInfo("Foraging forage_gather_begin node=" + nodeId +
                " token=" + token.ToString() + " duration=" + duration.ToString("F2"));
            // Preserve the established diagnostic token used by existing live-log tooling.
            CraftingController.LogInfo("Foraging gather_begin node=" + nodeId +
                " token=" + token.ToString() + " duration=" + duration.ToString("F2"));
        }

        private static void TickActiveGather()
        {
            SpawnedForageNode node = _activeGatherNode;
            if (node == null) return;
            if (node.State == null || !node.State.IsTokenActive(_activeGatherToken) ||
                !_captureState.IsGathering(_activeGatherToken))
            {
                CancelActiveGather(ForageGatherCancelReason.TargetInvalid);
                return;
            }
            if (!IsRegisteredLiveTarget(node))
            {
                CancelActiveGather(ForageGatherCancelReason.TargetInvalid);
                return;
            }

            if (IsChatFocused())
            {
                CancelActiveGather(ForageGatherCancelReason.Typing);
                return;
            }

            ForageGatherCancelReason uiCancel = ForageGatherChannelPolicy.EvaluateUi(
                IsConflictingUiDuringCapturedGather());
            if (uiCancel != ForageGatherCancelReason.None)
            {
                CancelActiveGather(uiCancel);
                return;
            }

            // The selecting RMB only owns camera input until the physical click is released. The
            // channel itself does not depend on the button and camera movement after release is safe.
            if (_activeGatherInputOwned)
            {
                if (IsSelectingRightButtonHeld())
                {
                    try { CraftingUiPointerOwnership.Reassert(); } catch { }
                }
                else
                {
                    ReleaseActiveGatherInputOwnership("right-click-release");
                }
            }

            Vector3 playerPos;
            if (!GameForagingApi.TryGetPlayerPosition(out playerPos))
            {
                CancelActiveGather(ForageGatherCancelReason.CharacterChanged);
                return;
            }

            int currentHp;
            if (!GameForagingApi.TryGetPlayerCurrentHp(out currentHp)) currentHp = -1;

            bool localAggro;
            string localAggroDetail;
            bool localAggroKnown = GameForagingApi.TryGetLocalHostileAggro(out localAggro, out localAggroDetail);
            if (!ForageCombatEligibilityPolicy.CanBeginOrContinue(localAggroKnown, localAggro))
            {
                LastGatherTransactionSummary = "gather_cancel node=" + SafeNodeId(node) +
                    " reason=hostile-engaged probe=" + localAggroDetail;
                CancelActiveGather(ForageGatherCancelReason.LocalHostileAggro);
                return;
            }

            string currentScene = SafeSceneName();
            string currentCharacterKey = ForagingProgressionController.CurrentCharacterKey;
            bool zoneChanged = !string.Equals(currentScene, _activeGatherScene, System.StringComparison.OrdinalIgnoreCase);
            bool characterChanged = string.IsNullOrEmpty(currentCharacterKey) ||
                !string.Equals(currentCharacterKey, _activeGatherCharacterKey, System.StringComparison.Ordinal);
            bool targetValid = IsRegisteredLiveTarget(node) &&
                node.Visual != null &&
                node.State != null &&
                (node.State.Availability == ForageAvailability.Gathering ||
                 node.State.Availability == ForageAvailability.GrantPending);
            bool meaningfulMovement = ForageGatherChannelPolicy.HasMeaningfulMovement(
                _activeGatherStartPosition.x, _activeGatherStartPosition.y, _activeGatherStartPosition.z,
                playerPos.x, playerPos.y, playerPos.z);
            float nodeDistance = node.Visual == null ? float.PositiveInfinity : Vector3.Distance(playerPos, node.Visual.transform.position);
            float interactionRange = ForagingConfig.InteractionRange == null ? 0f : ForagingConfig.InteractionRange.Value;
            bool occluded = IsGatherLineOfSightBlocked(node, playerPos);
            ForageGatherCancelReason cancel = ForageGatherCancellationPolicy.EvaluateFrame(
                ForagingConfig.EnableForaging != null && ForagingConfig.EnableForaging.Value,
                false,
                zoneChanged,
                characterChanged,
                targetValid,
                meaningfulMovement,
                nodeDistance,
                interactionRange,
                occluded,
                _activeGatherLastHp,
                currentHp);
            if (cancel != ForageGatherCancelReason.None)
            {
                CancelActiveGather(cancel);
                return;
            }

            // Healing raises the comparison baseline; zero/no change does not. A later actual
            // decrease from that most-recent observed HP therefore cancels even after a heal.
            if (currentHp >= 0) _activeGatherLastHp = currentHp;

            if (node.State.IsGatherReady(_activeGatherToken))
            {
                if (!_captureState.TryBeginCompleting(_activeGatherToken) ||
                    !node.State.TryEnterGrantPending(_activeGatherToken))
                {
                    CancelActiveGather(ForageGatherCancelReason.TargetInvalid);
                    return;
                }
                UpdateDynamicGatherPresentation(node);
                CompleteActiveGatherGrant();
                return;
            }
        }

        private static void CompleteActiveGatherGrant()
        {
            try
            {
                CompleteActiveGatherGrantCore();
            }
            catch (System.Exception ex)
            {
                SpawnedForageNode node = _activeGatherNode;
                long token = _activeGatherToken;
                bool failClosed = _activeNativeGrantInvokeStarted;
                if (node != null && node.State != null &&
                    node.State.Availability == ForageAvailability.GrantPending && node.State.IsTokenActive(token))
                {
                    if (failClosed)
                    {
                        try { ForagingProgressionController.RecordAmbiguousGrantQuarantine(_activeGatherScene, _activeRewardItemId, _activeRespawnSeconds); } catch { }
                        node.State.FailClosedUnknownAfterInvoke(token, _activeRespawnSeconds);
                    }
                    else
                    {
                        node.State.RejectGrant(token);
                    }
                    UpdateVisualForState(node);
                }
                EndActiveGatherAnimation();
                LastGrantResult = failClosed
                    ? ForagingInventoryGrantResult.UnknownAfterInvoke.ToString()
                    : ForagingInventoryGrantResult.NativeGrantUnavailable.ToString();
                LastFailureReason = failClosed
                    ? "Gather transaction failed after the native inventory call began."
                    : "Gather transaction failed before the native inventory call began.";
                LastGatherTransactionSummary = "gather=" + (failClosed ? "failed-closed" : "failed") + " node=" + SafeNodeId(node) +
                    " token=" + token.ToString() + " exception=" + ex.GetType().Name +
                    " nativeInvoke=" + (failClosed ? "started" : "no") + " retry=" + (failClosed ? "no" : "allowed") +
                    " xp=no depletionLedger=no";
                CraftingController.LogError("Foraging gather transaction exception node=" + SafeNodeId(node) +
                    " token=" + token.ToString() + " type=" + ex.GetType().Name +
                    " nativeInvoke=" + (failClosed ? "started" : "no") + " retry=" + (failClosed ? "fail-closed" : "available"));
                ReleaseCapturedInteraction("grant-exception");
                ClearActiveGatherSnapshot();
            }
        }

        private static void CompleteActiveGatherGrantCore()
        {
            SpawnedForageNode node = _activeGatherNode;
            long token = _activeGatherToken;
            if (node == null || node.State == null || node.State.Availability != ForageAvailability.GrantPending || !node.State.IsTokenActive(token))
            {
                CancelActiveGather(ForageGatherCancelReason.CharacterChanged);
                return;
            }

            string nodeId = SafeNodeId(node);
            CraftingController.LogInfo("Foraging grant_attempt node=" + nodeId + " token=" + token.ToString());
            ForagingInventoryGrantResult grantResult;
            if (CraftingExpandedItemIds.IsInOwnedRange(_activeRewardItemId))
            {
                grantResult = GameItemRegistryApi.GrantRegisteredItemForForaging(_activeRewardItemId, _activeRewardQuantity, out _activeNativeGrantInvokeStarted);
            }
            else
            {
                object item = GameForagingApi.TryGetVanillaItemById(_activeRewardItemId);
                grantResult = item == null
                    ? ForagingInventoryGrantResult.ItemUnavailable
                    : GameForagingApi.GrantVanillaItemForForaging(item, _activeRewardQuantity, out _activeNativeGrantInvokeStarted);
            }
            LastGrantResult = grantResult.ToString();

            if (grantResult == ForagingInventoryGrantResult.Success)
            {
                if (!node.State.CompleteGrantSuccess(token, _activeRespawnSeconds))
                {
                    // The native grant is already authoritative. Never reopen/regrant because a
                    // later mod-owned transition failed unexpectedly.
                    if (node.State.Availability == ForageAvailability.GrantPending)
                        node.State.FailClosedUnknownAfterInvoke(token, _activeRespawnSeconds);
                    EndActiveGatherAnimation();
                    ReleaseCapturedInteraction("grant-success-local-transition-failed");
                    ClearActiveGatherSnapshot();
                    CraftingController.LogError("Foraging grant_success but local depletion transition failed; node kept fail-closed.");
                    return;
                }

                bool depletionCommitted = false;
                bool xpCommitted = false;
                int appliedXp = 0;
                try
                {
                    ForageDepletionLedger.Record(_activeGatherScene, _activeRewardItemId, Time.unscaledTime, _activeRespawnSeconds);
                    depletionCommitted = true;
                }
                catch (System.Exception ex)
                {
                    CraftingController.LogError("Foraging depletion commit failed after authoritative grant: " + ex.GetType().Name);
                }

                ForageResourceDefinition resource = ForageResourceCatalog.FindByRewardItemId(_activeRewardItemId);
                try
                {
                    ForagingGatherProgressionResult progression = resource == null ? null : ForagingProgressionController.OnSuccessfulGather(resource);
                    xpCommitted = progression != null && progression.Applied;
                    appliedXp = progression == null || progression.XpAward == null ? 0 : progression.XpAward.AppliedXp;
                }
                catch (System.Exception ex)
                {
                    CraftingController.LogError("Foraging XP/discovery commit failed after authoritative grant: " + ex.GetType().Name);
                }

                node.CompletionFeedbackRemaining = CompletionFeedbackSeconds;
                node.CompletionFeedbackLogged = false;
                UpdateVisualForState(node);
                UpdateDynamicGatherPresentation(node);
                bool soundPlayed = GameForagingApi.TryPlaySuccessfulForageSound();
                EndActiveGatherAnimation();

                LastGatherSummary = SafeDisplayName(node) + " x" + _activeRewardQuantity.ToString();
                LastFailureReason = string.Empty;
                LastGatherCancelReason = "none";
                LastGatherTransactionSummary = "gather=success node=" + nodeId +
                    " token=" + token.ToString() +
                    " grant=Success depletion=" + (depletionCommitted ? "yes" : "no") +
                    " xp=" + (xpCommitted ? ("yes+" + appliedXp.ToString()) : "no") +
                    " sound=" + (soundPlayed ? "played" : "unavailable") +
                    " feedback=" + CompletionFeedbackSeconds.ToString("F2") + "s";
                CraftingController.LogInfo("Foraging grant_success node=" + nodeId + " token=" + token.ToString());
                CraftingController.LogInfo("Foraging xp_commit " + (xpCommitted ? "yes" : "no") +
                    " depletion_commit " + (depletionCommitted ? "yes" : "no"));
                CraftingController.LogInfo("Foraging forage_capture_complete node=" + nodeId +
                    " token=" + token.ToString());
                CraftingController.LogInfo("Foraging forage_gather_complete node=" + nodeId +
                    " token=" + token.ToString());
                ReleaseCapturedInteraction("complete");
                ClearActiveGatherSnapshot();
                return;
            }

            if (ForagingInventoryGrantPolicy.RestoresAvailability(grantResult))
            {
                node.State.RejectGrant(token);
                UpdateVisualForState(node);
                UpdateDynamicGatherPresentation(node);
                EndActiveGatherAnimation();
                string resultName = grantResult.ToString();
                LastFailureReason = grantResult == ForagingInventoryGrantResult.InventoryRejected
                    ? "Inventory rejected the gathered item."
                    : "Gather reward is currently unavailable.";
                LastGatherTransactionSummary = "gather=failed node=" + nodeId + " token=" + token.ToString() +
                    " grant=" + resultName + " rollback=available xp=no depletion=no";
                CraftingController.LogInfo("Foraging grant_" +
                    (grantResult == ForagingInventoryGrantResult.InventoryRejected ? "rejected" : "unavailable") +
                    " node=" + nodeId + " result=" + resultName + " xp_commit=no depletion_commit=no");
                if (grantResult == ForagingInventoryGrantResult.InventoryRejected)
                    NotifyGatherFeedback("Make room in your inventory to gather this resource.");
                else
                    NotifyGatherFeedback("This resource cannot be gathered right now.");
                ReleaseCapturedInteraction("grant-" + resultName);
                ClearActiveGatherSnapshot();
                return;
            }

            // UnknownAfterInvoke may already have inserted the item. Quarantine it separately
            // from successful depletion authority so zoning/restart cannot immediately offer an
            // automatic duplicate retry, while still refusing to claim XP/discovery/success.
            bool quarantineCommitted = false;
            try { quarantineCommitted = ForagingProgressionController.RecordAmbiguousGrantQuarantine(_activeGatherScene, _activeRewardItemId, _activeRespawnSeconds); }
            catch { quarantineCommitted = false; }
            node.State.FailClosedUnknownAfterInvoke(token, _activeRespawnSeconds);
            UpdateVisualForState(node);
            EndActiveGatherAnimation();
            LastFailureReason = "Inventory result could not be verified after the native grant call.";
            LastGatherTransactionSummary = "gather=failed-closed node=" + nodeId + " token=" + token.ToString() +
                " grant=UnknownAfterInvoke retry=no xp=no depletionLedger=no quarantine=" + (quarantineCommitted ? "yes" : "no");
            CraftingController.LogError("Foraging grant_unknown node=" + nodeId + " token=" + token.ToString() +
                " xp_commit=no depletion_commit=no retry=fail-closed");
            NotifyGatherFeedback("Gather result could not be verified; this resource will recover later.");
            ReleaseCapturedInteraction("grant-unknown");
            ClearActiveGatherSnapshot();
        }

        private static void CancelActiveGather(ForageGatherCancelReason reason)
        {
            SpawnedForageNode node = _activeGatherNode;
            long token = _activeGatherToken;
            string reasonText = ForageGatherCancellationPolicy.Describe(reason);
            if (node == null)
            {
                _captureState.MarkCancelled(token, reasonText);
                ReleaseCapturedInteraction("cancel:" + reasonText);
                EndActiveGatherAnimation();
                ClearActiveGatherSnapshot();
                return;
            }

            bool restored = false;
            if (node.State != null)
            {
                if (node.State.Availability == ForageAvailability.Gathering)
                    restored = node.State.CancelGather(token);
                else if (node.State.Availability == ForageAvailability.GrantPending && node.State.IsTokenActive(token))
                    node.State.FailClosedUnknownAfterInvoke(token, _activeRespawnSeconds);
            }
            if (restored && node.LabelView != null) node.LabelView.ResetAvailable();
            UpdateVisualForState(node);
            EndActiveGatherAnimation();
            LastGatherCancelReason = reasonText;
            _captureState.MarkCancelled(token, reasonText);
            LastGatherTransactionSummary = "gather=cancelled node=" + SafeNodeId(node) +
                " token=" + token.ToString() + " reason=" + LastGatherCancelReason +
                " restored=" + (restored ? "yes" : "no");
            CraftingController.LogInfo("Foraging forage_capture_cancel node=" + SafeNodeId(node) +
                " token=" + token.ToString() + " reason=" + LastGatherCancelReason);
            CraftingController.LogInfo("Foraging gather_cancel node=" + SafeNodeId(node) +
                " token=" + token.ToString() + " reason=" + LastGatherCancelReason);
            CraftingController.LogInfo("Foraging forage_gather_cancel node=" + SafeNodeId(node) +
                " token=" + token.ToString() + " reason=" + LastGatherCancelReason);
            ReleaseCapturedInteraction("cancel:" + reasonText);
            ClearActiveGatherSnapshot();
        }

        private static void EndActiveGatherAnimation()
        {
            if (!_activeGatherAnimationStarted) return;
            _activeGatherAnimationStarted = false;
            GameForagingApi.EndNativeGatherAnimation();
        }

        private static void ClearActiveGatherSnapshot()
        {
            SpawnedForageNode completedNode = _activeGatherNode;
            // Final idempotent ownership safety net for every exception/teardown path.
            if (_activeGatherInputOwned ||
                _captureState.Phase == ForageInteractionCapturePhase.Selected ||
                _captureState.Phase == ForageInteractionCapturePhase.Gathering ||
                _captureState.Phase == ForageInteractionCapturePhase.Completing ||
                _captureState.Phase == ForageInteractionCapturePhase.Cancelled)
                ReleaseCapturedInteraction("snapshot-clear");
            _activeGatherNode = null;
            _activeGatherToken = 0;
            _activeGatherStartHp = -1;
            _activeGatherLastHp = -1;
            _activeGatherStartPosition = Vector3.zero;
            _activeGatherScene = string.Empty;
            _activeGatherCharacterKey = string.Empty;
            _activeRewardItemId = string.Empty;
            _activeRewardQuantity = 0;
            _activeRespawnSeconds = 0f;
            _activeGatherAnimationStarted = false;
            _activeNativeGrantInvokeStarted = false;
            _activeGatherInputOwned = false;
            // Selected ring/progress ownership is terminal. Hover may reacquire on the next normal
            // target probe, but no completion/cancel path may leave the strong ring stuck on.
            SetNodeInteractionStateSafe(completedNode, false, false);
        }

        private static long NextGatherToken()
        {
            if (_nextGatherToken >= long.MaxValue - 1) _nextGatherToken = 0;
            _nextGatherToken++;
            if (_nextGatherToken <= 0) _nextGatherToken = 1;
            return _nextGatherToken;
        }

        private static float EffectiveRespawnSeconds(SpawnedForageNode node)
        {
            float effective = node == null || node.Definition == null ? 0f : node.Definition.RespawnSeconds;
            float debugOverride = ForagingConfig.DebugRespawnSecondsOverride == null ? 0f : ForagingConfig.DebugRespawnSecondsOverride.Value;
            if (ForagingRuntimeConfigValidation.IsValidDebugRespawnOverride(debugOverride) && debugOverride > 0f)
                effective = debugOverride;
            return effective > 0f ? effective : 0f;
        }

        private static bool IsGatherLineOfSightBlocked(SpawnedForageNode node, Vector3 playerPosition)
        {
            if (node == null || node.Visual == null) return true;
            try
            {
                Vector3 origin = playerPosition + Vector3.up * 0.80f;
                Vector3 target = node.InteractionTarget != null
                    ? node.InteractionTarget.transform.position
                    : node.Visual.transform.position + Vector3.up * 0.35f;
                Vector3 delta = target - origin;
                float distance = delta.magnitude;
                if (distance <= 0.05f) return false;
                Ray ray = new Ray(origin, delta / distance);
                int worldMask = ~(1 << 2);
                int blockerCount = Physics.RaycastNonAlloc(ray, _occlusionRayHits, distance, worldMask, QueryTriggerInteraction.Ignore);
                Transform playerTransform;
                GameForagingApi.TryGetPlayerTransform(out playerTransform);
                for (int i = 0; i < blockerCount; i++)
                {
                    Collider blocker = _occlusionRayHits[i].collider;
                    if (blocker == null) continue;
                    bool localPlayer = playerTransform != null &&
                        (blocker.transform == playerTransform || blocker.transform.IsChildOf(playerTransform));
                    if (localPlayer) continue;
                    // Preserve wall/terrain LOS while allowing the node's own native visual collider
                    // to exist between the player and the mod-owned interaction target.
                    if (IsColliderOwnedByNodeVisual(node, blocker)) continue;
                    if (ForageInteractionPolicy.IsSolidOcclusion(distance, _occlusionRayHits[i].distance, false)) return true;
                }
                return false;
            }
            catch { return true; }
        }

        private static bool IsColliderOwnedByNodeVisual(SpawnedForageNode node, Collider collider)
        {
            if (node == null || collider == null) return false;
            GameObject visual = null;
            try { visual = node.Visual; } catch { visual = null; }
            if (visual == null) return false;
            try
            {
                Transform visualRoot = visual.transform;
                Transform hit = collider.transform;
                return visualRoot != null && hit != null && (hit == visualRoot || hit.IsChildOf(visualRoot));
            }
            catch { return false; }
        }

        private static void TickCompletionFeedback(SpawnedForageNode node, float deltaSeconds)
        {
            if (node == null || node.CompletionFeedbackRemaining <= 0f) return;
            if (float.IsNaN(deltaSeconds) || float.IsInfinity(deltaSeconds) || deltaSeconds < 0f) deltaSeconds = 0f;
            node.CompletionFeedbackRemaining -= deltaSeconds;
            if (node.CompletionFeedbackRemaining > 0f) return;
            node.CompletionFeedbackRemaining = 0f;
            if (!node.CompletionFeedbackLogged)
            {
                node.CompletionFeedbackLogged = true;
                CraftingController.LogInfo("Foraging presentation_complete node=" + SafeNodeId(node));
            }
        }

        private static void UpdateDynamicGatherPresentation(SpawnedForageNode node)
        {
            if (node == null || node.State == null || node.LabelView == null) return;
            try
            {
                if (node.State.Availability == ForageAvailability.Gathering || node.State.Availability == ForageAvailability.GrantPending)
                {
                    node.LabelView.SetGatherProgress(node.State.GatherProgress01);
                }
                else if (node.State.Availability == ForageAvailability.Depleted && node.CompletionFeedbackRemaining > 0f)
                {
                    node.LabelView.SetCompletionFeedback(node.CompletionFeedbackRemaining / CompletionFeedbackSeconds);
                }
            }
            catch (System.Exception ex)
            {
                // Presentation is never reward authority. A successful grant stays depleted even if
                // fill/fade manipulation fails. Keep this event bounded: one diagnostic per gather.
                if (!node.PresentationFailureLogged)
                {
                    node.PresentationFailureLogged = true;
                    CraftingController.LogInfo("Foraging presentation_failure node=" + SafeNodeId(node) + " reason=" + ex.GetType().Name);
                }
            }
        }

        private static void NotifyGatherFeedback(string message)
        {
            if (string.IsNullOrEmpty(message)) return;
            float now = Time.unscaledTime;
            if (string.Equals(_lastGatherFeedback, message, System.StringComparison.Ordinal) && now < _nextGatherFeedbackAt) return;
            _lastGatherFeedback = message;
            _nextGatherFeedbackAt = now + GatherFeedbackCooldownSeconds;
            ForagingProgressionController.NotifyGatherFeedback(message);
        }

        internal static string DescribeActiveGather()
        {
            SpawnedForageNode node = _activeGatherNode;
            if (node == null || node.State == null)
                return "state=none capture=" + _captureState.Phase +
                    " inputOwned=" + (_activeGatherInputOwned ? "yes" : "no") +
                    " lastCapture={" + (_captureState.LastTerminalReason ?? string.Empty) + "}" +
                    " lastCancel=" + LastGatherCancelReason + " lastGrant=" + LastGrantResult;
            float remaining = System.Math.Max(0f, node.State.GatherDurationSeconds - node.State.GatherElapsedSeconds);
            return "state=" + node.State.Availability +
                " node=" + SafeNodeId(node) +
                " token=" + _activeGatherToken.ToString() +
                " capture=" + _captureState.Phase +
                " inputOwned=" + (_activeGatherInputOwned ? "yes" : "no") +
                " elapsed=" + node.State.GatherElapsedSeconds.ToString("F2") +
                " remaining=" + remaining.ToString("F2") +
                " lastCancel=" + LastGatherCancelReason +
                " lastGrant=" + LastGrantResult;
        }

        private static void UpdateVisualForState(SpawnedForageNode node)
        {
            if (node == null || node.Visual == null || node.State == null) return;
            ForageAvailability availability = node.State.Availability;
            bool completionFeedback = availability == ForageAvailability.Depleted && node.CompletionFeedbackRemaining > 0f;
            if (node.PresentationInitialized &&
                node.LastPresentedAvailability == availability &&
                node.LastPresentedCompletionFeedback == completionFeedback) return;

            bool visible = ForagePresentationPolicy.ShouldShowResourcePresentation(availability) || completionFeedback;
            bool interactionAvailable = availability == ForageAvailability.Available || availability == ForageAvailability.Gathering;
            try
            {
                if (node.VisualRenderers == null) node.VisualRenderers = CacheVisualRenderers(node.Visual);
                Renderer[] renderers = node.VisualRenderers;
                if (renderers != null)
                {
                    foreach (Renderer renderer in renderers)
                        if (renderer != null && renderer.enabled != visible) renderer.enabled = visible;
                }
                if (node.WorldLabel != null && node.WorldLabel.activeSelf != visible) node.WorldLabel.SetActive(visible);
                if (node.InteractionComponent != null) node.InteractionComponent.SetAvailable(interactionAvailable);
                if (node.HighlightView != null) node.HighlightView.SetAvailable(visible);
                if (availability == ForageAvailability.Available && node.LabelView != null) node.LabelView.ResetAvailable();
                if (availability == ForageAvailability.GrantPending && node.LabelView != null) node.LabelView.SetGatherProgress(1f);
                node.LastPresentedAvailability = availability;
                node.LastPresentedCompletionFeedback = completionFeedback;
                node.PresentationInitialized = true;
            }
            catch
            {
                // Do not mark the presentation synchronized after a failed Unity operation; a
                // later frame may run after the scene transition has settled and can retry safely.
                node.PresentationInitialized = false;
            }
        }

        private static Renderer[] CacheVisualRenderers(GameObject visual)
        {
            if (visual == null) return new Renderer[0];
            try { return visual.GetComponentsInChildren<Renderer>(true); }
            catch { return new Renderer[0]; }
        }

        private static void RespawnForScene(string scene)
        {
            DespawnAll();
            _spawnedScene = scene;
            WorldThreatRuntime.BeginScene(scene, Time.unscaledTime);
            if (string.IsNullOrEmpty(scene)) return;

            foreach (ForageNodeDefinition def in Catalog.GetForScene(scene))
                SpawnDefinition(def);

            // Curated/authored content wins. The first vertical slice uses conservative runtime
            // auto-placement only in scenes where no authored forage entries exist.
            if (IsAutoPlacementEnabledForScene(scene))
            {
                // Wait until the scene threat survey freezes. This avoids placing resource tiers
                // from a half-loaded hostile population and keeps the classification fixed for the visit.
                _autoTrialAttemptCount = 1;
                _autoTrialRetrySeconds = 0.25f;
            }

            // The older survey candidate remains an explicit developer-only path. It is separate
            // from normal auto placement and may still use the debug placeholder if requested.
            if (ForagingConfig.EnablePoCNode.Value &&
                _candidateRejectReason == ForageDefinitionRejectReason.None &&
                string.Equals(_candidateDefinition.Scene, scene, System.StringComparison.OrdinalIgnoreCase))
            {
                SpawnDefinition(_candidateDefinition);
            }

            ApplySessionDepletion(scene, Time.unscaledTime);
        }

        private static void ApplySessionDepletion(string scene, float now)
        {
            if (string.IsNullOrEmpty(scene) || _spawned.Count == 0) return;
            HashSet<string> processed = new HashSet<string>();
            for (int i = 0; i < _spawned.Count; i++)
            {
                SpawnedForageNode sample = _spawned[i];
                if (sample == null || sample.Definition == null || string.IsNullOrEmpty(sample.Definition.RewardItemId)) continue;
                string itemId = sample.Definition.RewardItemId;
                if (!processed.Add(itemId)) continue;

                List<float> remaining = ForageDepletionLedger.GetActiveRemainingSeconds(scene, itemId, now);
                List<float> quarantined = ForageAmbiguousGrantQuarantine.GetActiveRemainingSeconds(scene, itemId, now);
                if (quarantined.Count > 0)
                {
                    remaining.AddRange(quarantined);
                    remaining.Sort();
                }
                if (remaining.Count == 0) continue;
                int next = 0;
                for (int n = 0; n < _spawned.Count && next < remaining.Count; n++)
                {
                    SpawnedForageNode node = _spawned[n];
                    if (node == null || node.Definition == null || node.State == null) continue;
                    if (!string.Equals(node.Definition.RewardItemId, itemId, System.StringComparison.Ordinal)) continue;
                    if (node.State.Availability != ForageAvailability.Available) continue;
                    if (!node.State.RestorePersistedDepletion(remaining[next])) continue;
                    UpdateVisualForState(node);
                    next++;
                }
            }
        }

        internal static bool IsAutoPlacementEnabledForScene(string scene)
        {
            return AutoPlacementVerticalSliceEnabled &&
                ForagingConfig.EnableForaging != null &&
                ForagingConfig.EnableForaging.Value &&
                !string.IsNullOrEmpty(scene) &&
                Catalog.CountForScene(scene) == 0;
        }

        private static int TrySpawnAutoTrial(string scene)
        {
            if (string.IsNullOrEmpty(scene)) return 0;
            WorldThreatSnapshot threat = WorldThreatRuntime.Current;
            if (threat == null || !threat.Frozen)
            {
                LastAutoTrialSummary = "waiting: world threat snapshot stabilizing";
                return 0;
            }
            Vector3 playerPos;
            if (!GameForagingApi.TryGetPlayerPosition(out playerPos))
            {
                LastAutoTrialSummary = "waiting: player position unavailable";
                return 0;
            }

            string placementSummary;
            List<ForageAutoTrialPoint> points = ForageAutoPlacementTrial.FindPoints(scene, playerPos, _autoTrialGeneration, out placementSummary);
            LastAutoTrialSummary = placementSummary;
            if (points == null || points.Count == 0)
            {
                LastSpawnFailureReason = "auto placement found no safe wall-adjacent ground points (" + placementSummary + ")";
                return 0;
            }

            bool coveredEnabled = ForagingConfig.ExperimentalCoveredResources != null && ForagingConfig.ExperimentalCoveredResources.Value;

            // Determine only the visual families that could actually be used in this scene. A
            // resource with no registered item donor cannot spawn, so it should not keep forcing
            // expensive renderer rescans for a mesh that cannot yet produce an obtainable node.
            List<ForageResourcePool> requiredPools = new List<ForageResourcePool>();
            for (int pointIndex = 0; pointIndex < points.Count; pointIndex++)
            {
                ForageEnvironmentKind environment = ForageEnvironmentPolicy.Classify(points[pointIndex].Covered);
                List<ForageResourceDefinition> possible = ForageResourceCatalog.ForEnvironmentAll(environment, scene, coveredEnabled);
                for (int i = 0; i < possible.Count; i++)
                {
                    ForageResourceDefinition resource = possible[i];
                    if (!WorldThreatPolicy.Allows(threat.Band, resource.WorldBand)) continue;
                    if (!GameItemRegistryApi.IsCustomItemAvailable(resource.RewardItemId)) continue;
                    if (!requiredPools.Contains(resource.Pool)) requiredPools.Add(resource.Pool);
                }
            }

            ForageVisualSourceSet visualSources = GetForageVisualSources(scene, playerPos, requiredPools);
            Dictionary<string, int> resourceCounts = new Dictionary<string, int>(System.StringComparer.Ordinal);
            int skippedDisabled = 0;
            int skippedItem = 0;
            int skippedVisual = 0;
            int skippedDensity = 0;
            int rejectedDensity = 0;
            int rejectedWorldBand = 0;
            int rejectedProgression = 0;
            int rejectedRegion = 0;
            int rejectedEvidence = 0;
            int rejectedCap = 0;
            int eligibleStarter = 0;
            int spawnedIndex = 0;

            for (int pointIndex = 0; pointIndex < points.Count; pointIndex++)
            {
                ForageAutoTrialPoint point = points[pointIndex];
                ForageEnvironmentKind environment = ForageEnvironmentPolicy.Classify(point.Covered);
                List<ForageResourceDefinition> possible = ForageResourceCatalog.ForEnvironmentAll(environment, scene, coveredEnabled);
                if (possible.Count == 0)
                {
                    skippedDisabled++;
                    continue;
                }

                List<ForageResourceDefinition> proven = new List<ForageResourceDefinition>();
                for (int i = 0; i < possible.Count; i++)
                {
                    ForageResourceDefinition candidate = possible[i];
                    bool itemAvailable = GameItemRegistryApi.IsCustomItemAvailable(candidate.RewardItemId);
                    bool visualAvailable = visualSources.Has(candidate.Pool);
                    string availabilityReason;
                    if (!ForageResourceAvailabilityPolicy.CanAutoSpawn(
                        candidate, environment, scene, coveredEnabled, threat.Band, itemAvailable, visualAvailable, out availabilityReason))
                    {
                        if (availabilityReason == "item-donor-unavailable") skippedItem++;
                        else if (availabilityReason == "scene-visual-unavailable") skippedVisual++;
                        else if (availabilityReason == "world-tier-too-low") rejectedWorldBand++;
                        else if (availabilityReason == "wrong-region") rejectedRegion++;
                        else if (availabilityReason == "wrong-environment" || availabilityReason == "resource-disabled") rejectedEvidence++;
                        else skippedDisabled++;
                        continue;
                    }
                    if (candidate.WorldBand == ForageWorldBand.Starter) eligibleStarter++;
                    proven.Add(candidate);
                }

                if (proven.Count == 0)
                {
                    // Selection never ran; this is an eligibility/evidence outcome, not density.
                    continue;
                }

                ForageResourceDefinition resource = ForageResourceSelectionPolicy.Select(
                    proven,
                    resourceCounts,
                    scene,
                    _autoTrialGeneration,
                    spawnedIndex);
                if (resource == null)
                {
                    if (ForageResourceSelectionPolicy.DescribeNoSelection(proven, resourceCounts) == "cap") rejectedCap++;
                    else
                    {
                        rejectedDensity++;
                        skippedDensity++;
                    }
                    continue;
                }

                RendererScanResult visualSource = visualSources.Get(resource.Pool);
                string visualSourceSummary = visualSources.GetSummary(resource.Pool);
                if (visualSource == null)
                {
                    skippedVisual++;
                    continue;
                }

                spawnedIndex++;
                ForageNodeDefinition def = new ForageNodeDefinition
                {
                    Id = resource.NodeIdPrefix + _autoTrialGeneration + "_" + spawnedIndex,
                    DisplayName = resource.DisplayName,
                    Scene = scene,
                    Position = new ForagePosition(point.Position.x, point.Position.y, point.Position.z),
                    PositionSet = true,
                    RotationY = point.RotationY,
                    VisualSourceScene = string.Empty,
                    VisualSourceHierarchyPath = string.Empty,
                    Scale = 1f,
                    TintEnabled = false,
                    RespawnSeconds = resource.RespawnSeconds,
                    RewardItemId = resource.RewardItemId,
                    RewardQuantity = resource.BaseYield
                };
                if (SpawnAutoTrialDefinition(def, point, visualSource, visualSourceSummary, resource))
                    ForageResourceSelectionPolicy.Record(resourceCounts, resource);
                else
                    skippedVisual++;
            }

            int trialCount = AutoTrialCount();
            if (!ForagePlacementPolicy.IsUsableClusterCount(trialCount))
            {
                RemoveAutoTrialNodes();
                LastSpawnFailureReason = "auto placement produced no usable resource cluster; points=" + points.Count +
                    " worldBand=" + WorldThreatPolicy.DisplayName(threat.Band) +
                    " coveredExperimental=" + (coveredEnabled ? "on" : "off") +
                    " skippedDisabled=" + skippedDisabled +
                    " skippedItem=" + skippedItem +
                    " skippedVisual=" + skippedVisual +
                    " rejectedWorldBand=" + rejectedWorldBand +
                    " rejectedProgression=" + rejectedProgression +
                    " rejectedRegion=" + rejectedRegion +
                    " rejectedEvidence=" + rejectedEvidence +
                    " rejectedCap=" + rejectedCap +
                    " rejectedDensity=" + rejectedDensity +
                    " eligibleStarter=" + eligibleStarter +
                    " skippedDensity=" + skippedDensity +
                    " visualEvidence={" + DescribeRequiredVisuals(requiredPools, visualSources) + "}";
                LastAutoTrialSummary += " resources=none";
                return 0;
            }

            List<string> resourceSummary = new List<string>();
            foreach (KeyValuePair<string, int> pair in resourceCounts)
            {
                ForageResourceDefinition resource = ForageResourceCatalog.FindByKnowledgeKey(pair.Key);
                resourceSummary.Add((resource == null ? pair.Key : resource.DisplayName) + ":" + pair.Value);
            }
            resourceSummary.Sort(System.StringComparer.OrdinalIgnoreCase);

            LastSpawnFailureReason = string.Empty;
            LastAutoTrialSummary += " worldBand=" + WorldThreatPolicy.DisplayName(threat.Band) +
                " resources=" + (resourceSummary.Count == 0 ? "none" : string.Join(",", resourceSummary.ToArray())) +
                " skippedDisabled=" + skippedDisabled +
                " skippedItem=" + skippedItem +
                " skippedVisual=" + skippedVisual +
                " rejectedWorldBand=" + rejectedWorldBand +
                " rejectedProgression=" + rejectedProgression +
                " rejectedRegion=" + rejectedRegion +
                " rejectedEvidence=" + rejectedEvidence +
                " rejectedCap=" + rejectedCap +
                " rejectedDensity=" + rejectedDensity +
                " eligibleStarter=" + eligibleStarter +
                " skippedDensity=" + skippedDensity;
            CraftingController.LogInfo("Foraging auto-placement: scene=" + scene + " " + LastAutoTrialSummary);
            return trialCount;
        }

        private static string DescribeRequiredVisuals(List<ForageResourcePool> requiredPools, ForageVisualSourceSet sources)
        {
            if (requiredPools == null || requiredPools.Count == 0) return "none";
            List<string> parts = new List<string>();
            for (int i = 0; i < requiredPools.Count; i++)
            {
                ForageResourcePool pool = requiredPools[i];
                parts.Add(pool + "=" + (sources == null ? "(scan unavailable)" : sources.GetSummary(pool)));
            }
            return string.Join(" | ", parts.ToArray());
        }

        private static void RemoveAutoTrialNodes()
        {
            for (int i = _spawned.Count - 1; i >= 0; i--)
            {
                SpawnedForageNode node = _spawned[i];
                if (node == null || !node.IsAutoTrial) continue;
                try { if (node.WorldLabel != null) UnityEngine.Object.Destroy(node.WorldLabel); } catch { }
                try { if (node.InteractionTarget != null) UnityEngine.Object.Destroy(node.InteractionTarget); } catch { }
                try { if (node.Highlight != null) UnityEngine.Object.Destroy(node.Highlight); } catch { }
                try { if (node.Visual != null) UnityEngine.Object.Destroy(node.Visual); } catch { }
                _spawned.RemoveAt(i);
            }
        }

        private static ForageVisualSourceSet GetForageVisualSources(
            string scene,
            Vector3 origin,
            List<ForageResourcePool> requiredPools)
        {
            bool sameScene = string.Equals(_cachedVisualScene, scene, System.StringComparison.OrdinalIgnoreCase);
            if (sameScene && _cachedVisualSources != null)
            {
                bool hasEveryRequiredFamily = true;
                if (requiredPools != null)
                {
                    for (int i = 0; i < requiredPools.Count; i++)
                    {
                        if (_cachedVisualSources.Has(requiredPools[i])) continue;
                        hasEveryRequiredFamily = false;
                        break;
                    }
                }
                if (hasEveryRequiredFamily) return _cachedVisualSources;

                // A new evidence-gated item can become available after the first scene scan (for
                // example after late ItemDB registration). Reopen a missing-family search without
                // turning renderer enumeration into Update-loop work.
                if (_nextNegativeVisualRescanTime == float.PositiveInfinity)
                    _nextNegativeVisualRescanTime = 0f;
                if (Time.unscaledTime < _nextNegativeVisualRescanTime) return _cachedVisualSources;
            }

            _cachedVisualScene = scene ?? string.Empty;
            _cachedVisualSources = ForagingAssetScanApi.FindBestForageClusterSources(origin, 75f);

            bool missingRequired = false;
            if (requiredPools != null)
            {
                for (int i = 0; i < requiredPools.Count; i++)
                {
                    if (_cachedVisualSources != null && _cachedVisualSources.Has(requiredPools[i])) continue;
                    missingRequired = true;
                    break;
                }
            }

            // Positive family evidence is scene-stable and cached. Missing required families retry
            // at the existing bounded eight-second cadence; unrelated families do not force scans.
            _nextNegativeVisualRescanTime = missingRequired
                ? Time.unscaledTime + 8f
                : float.PositiveInfinity;
            return _cachedVisualSources ?? new ForageVisualSourceSet();
        }

        private static void ClearVisualSourceCache()
        {
            _cachedVisualScene = string.Empty;
            _cachedVisualSources = null;
            _nextNegativeVisualRescanTime = 0f;
        }

        private static bool SpawnAutoTrialDefinition(
            ForageNodeDefinition def,
            ForageAutoTrialPoint point,
            RendererScanResult visualSource,
            string visualSourceSummary,
            ForageResourceDefinition resource)
        {
            string clusterSummary;
            GameObject visual = ForageAutoPlacementTrial.BuildTrialClusterVisual(def.Id, visualSource, resource, point.Position, point.RotationY, out clusterSummary);
            if (visual == null)
            {
                LastSpawnFailureReason = def.Id + ": native vegetation cluster creation failed (" + clusterSummary + ").";
                LogAutoVisualAttempt(scene: def.Scene, resource: resource, source: visualSource, detail: clusterSummary, bindingAllowed: false);
                return false;
            }

            // BuildTrialClusterVisual established this root's final world pose before its
            // renderer-bounds grounding proof. Do not translate/rotate/scale it afterward.
            string visualFailure;
            Bounds presentationBounds;
            if (!TryAuditProductionVisual(visual, true, out presentationBounds, out visualFailure))
            {
                LastSpawnFailureReason = def.Id + ": visual clone rejected (" + visualFailure + ").";
                LogAutoVisualAttempt(def.Scene, resource, visualSource, visualFailure, false);
                try { UnityEngine.Object.Destroy(visual); } catch { }
                return false;
            }
            GameObject label = ForageNodeWorldLabel.Create(visual, def.DisplayName, presentationBounds);
            if (label == null)
            {
                LastSpawnFailureReason = def.Id + ": world resource label could not be created.";
                try { UnityEngine.Object.Destroy(visual); } catch { }
                return false;
            }

            string shader = "(native vegetation)";
            if (visualSource != null && visualSource.ShaderNames != null && visualSource.ShaderNames.Count > 0)
                shader = visualSource.ShaderNames[0];

            SpawnedForageNode spawned = new SpawnedForageNode
            {
                Definition = def,
                State = new ForageNodeRuntimeState(),
                Visual = visual,
                WorldLabel = label,
                LabelView = label.GetComponent<ForageNodeWorldLabelView>(),
                VisualRenderers = CacheVisualRenderers(visual),
                IsDebugPlaceholder = false,
                IsAutoTrial = true,
                IsCoveredTrial = point.Covered,
                TrialWallName = point.WallName ?? string.Empty,
                TrialVisualSummary = clusterSummary + " | " + (visualSourceSummary ?? string.Empty),
                ResolvedMeshName = visualSource == null ? "(unresolved)" : visualSource.MeshName,
                ResolvedShaderName = shader,
                TintApplied = false,
                AppliedScale = Vector3.one
            };
            if (!AttachInteractionTarget(spawned, presentationBounds))
            {
                LastSpawnFailureReason = def.Id + ": visual presentation binding rejected.";
                try { UnityEngine.Object.Destroy(label); } catch { }
                try { UnityEngine.Object.Destroy(visual); } catch { }
                return false;
            }
            _spawned.Add(spawned);
            LogAutoVisualAttempt(def.Scene, resource, visualSource, visualFailure, true);
            LastNameplateSummary = "bound node=" + def.Id + " " + ForageNodeWorldLabel.DescribePresentation();
            return true;
        }

        // A label/collider is only legal after a mod-owned vegetation clone proves it can render.
        private static bool HasMaterializedAutoVisual(GameObject visual, out string reason)
        {
            Bounds bounds;
            return TryAuditProductionVisual(visual, true, out bounds, out reason);
        }

        private static void LogAutoVisualAttempt(string scene, ForageResourceDefinition resource, RendererScanResult source, string detail, bool bindingAllowed)
        {
            string family = resource == null ? "unknown" : resource.Pool.ToString();
            string key = (scene ?? string.Empty) + ":" + family;
            string donor = source == null ? "(none)" : (source.GameObjectName + "/" + source.MeshName);
            CraftingController.LogInfo("Foraging forage_visual_spawn scene=" + (scene ?? string.Empty) +
                " family=" + family + " donor=" + donor + " bindingAllowed=" + bindingAllowed + " " + (detail ?? string.Empty));
            HashSet<string> seen = bindingAllowed ? _visualFamilySuccessLogged : _visualFamilyFailureLogged;
            if (seen.Add(key))
                CraftingController.LogInfo("Foraging forage_visual_spawn_first_" + (bindingAllowed ? "success" : "failure") +
                    " scene=" + (scene ?? string.Empty) + " family=" + family + " donor=" + donor + " " + (detail ?? string.Empty));
        }

        // The presentation authority is the actual final cloned hierarchy, not the donor scan or
        // a label fallback. A resource may bind interaction only when at least one active mesh can
        // draw with a usable material, its aggregate bounds are finite/nontrivial, and auto-cluster
        // grounding/anchor remain coherent with the accepted placement pose.
        private static bool TryAuditProductionVisual(GameObject visual, bool requireGrounding, out Bounds combined, out string reason)
        {
            combined = new Bounds(); reason = "unknown";
            if (visual == null || !visual.activeInHierarchy) { reason = "root-inactive"; return false; }
            Renderer[] renderers = CacheVisualRenderers(visual);
            bool found = false; int active = 0;
            for (int i = 0; i < renderers.Length; i++)
            {
                MeshRenderer renderer = renderers[i] as MeshRenderer;
                MeshFilter filter = renderer == null ? null : renderer.GetComponent<MeshFilter>();
                if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy || filter == null || filter.sharedMesh == null) continue;
                Material[] materials = renderer.sharedMaterials;
                bool material = false;
                if (materials != null) for (int m = 0; m < materials.Length; m++) if (materials[m] != null && materials[m].shader != null) { material = true; break; }
                Bounds bounds = renderer.bounds; Vector3 scale = renderer.transform.lossyScale;
                if (!material || !IsFinite(bounds.min) || !IsFinite(bounds.max) || !IsFinite(scale) ||
                    Mathf.Abs(scale.x) < 0.001f || Mathf.Abs(scale.y) < 0.001f || Mathf.Abs(scale.z) < 0.001f ||
                    Mathf.Abs(scale.x) > ForagePresentationPolicy.VisualMaximumScaleAxis || Mathf.Abs(scale.y) > ForagePresentationPolicy.VisualMaximumScaleAxis || Mathf.Abs(scale.z) > ForagePresentationPolicy.VisualMaximumScaleAxis ||
                    Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z)) < ForagePresentationPolicy.VisualMinimumLargestDimension) continue;
                if (!found) { combined = bounds; found = true; } else combined.Encapsulate(bounds);
                active++;
            }
            if (!found) { reason = "no-active-renderable-mesh"; return false; }
            Vector3 root = visual.transform.position;
            float horizontal = new Vector2(combined.center.x - root.x, combined.center.z - root.z).magnitude;
            float groundingDelta = Mathf.Abs(combined.min.y - root.y);
            if (horizontal > ForagePresentationPolicy.VisualMaximumAnchorHorizontalOffset) { reason = "renderer-anchor-offset=" + horizontal.ToString("F2"); return false; }
            if (requireGrounding && groundingDelta > ForagePresentationPolicy.GroundingTolerance) { reason = "post-ground-delta=" + groundingDelta.ToString("F3"); return false; }
            reason = "activeRenderers=" + active + " bounds=" + combined.size.ToString("F2") + " groundDelta=" + groundingDelta.ToString("F3") + " anchorOffset=" + horizontal.ToString("F2");
            return true;
        }

        private static bool IsFinite(Vector3 value)
        {
            return !float.IsNaN(value.x) && !float.IsInfinity(value.x) && !float.IsNaN(value.y) && !float.IsInfinity(value.y) && !float.IsNaN(value.z) && !float.IsInfinity(value.z);
        }

        private static void SpawnDefinition(ForageNodeDefinition def)
        {
                GameForagingApi.VisualResolution resolution = GameForagingApi.TryResolveVisualSource(def);
                GameObject visual;
                bool isDebugPlaceholder = false;

                if (resolution.Source != null)
                {
                    visual = GameForagingApi.BuildVisualClone(resolution.Source, def.Id);
                }
                else if (ForagingConfig.AllowDebugPlaceholderVisual.Value)
                {
                    visual = GameForagingApi.BuildDebugPlaceholderVisual(def.Id);
                    isDebugPlaceholder = true;
                    CraftingController.LogInfo("Foraging: '" + def.Id + "' visual source unresolved (" + resolution.FailureReason + ") - using DEBUG placeholder because AllowDebugPlaceholderVisual=true.");
                }
                else
                {
                    LastSpawnFailureReason = def.Id + ": " + (resolution.FailureReason ?? "visual source did not resolve");
                    CraftingController.LogError("Foraging: '" + def.Id + "' did not spawn - " + LastSpawnFailureReason);
                    return;
                }

                if (visual == null)
                {
                    LastSpawnFailureReason = def.Id + ": visual clone produced no renderable geometry.";
                    return;
                }

                visual.transform.position = new Vector3(def.Position.X, def.Position.Y, def.Position.Z);
                visual.transform.rotation = Quaternion.Euler(0f, def.RotationY, 0f);

                // Definition.Scale is a multiplier, not an absolute replacement. The regular
                // clone's VisualRoot already preserves the native source lossy scale; the outer
                // mod-owned root applies only the authored multiplier. Debug placeholders have no
                // native scale, so the same multiplier naturally applies to their unit sphere.
                visual.transform.localScale = Vector3.one * def.Scale;
                Vector3 baseScale = isDebugPlaceholder ? Vector3.one : resolution.SourceLossyScale;
                Vector3 appliedScale = new Vector3(baseScale.x * def.Scale, baseScale.y * def.Scale, baseScale.z * def.Scale);

                bool tintApplied = false;
                if (def.TintEnabled)
                    tintApplied = GameForagingApi.TryApplyTint(visual, new Color(def.TintR, def.TintG, def.TintB), def.TintColorProperty);

                Bounds presentationBounds;
                string presentationFailure;
                if (!TryAuditProductionVisual(visual, false, out presentationBounds, out presentationFailure))
                {
                    LastSpawnFailureReason = def.Id + ": visual clone rejected (" + presentationFailure + ").";
                    try { UnityEngine.Object.Destroy(visual); } catch { }
                    return;
                }
                GameObject label = ForageNodeWorldLabel.Create(visual, def.DisplayName, presentationBounds);
                if (label == null)
                {
                    LastSpawnFailureReason = def.Id + ": mod-owned world label could not be created.";
                    try { UnityEngine.Object.Destroy(visual); } catch { }
                    CraftingController.LogError("Foraging: '" + def.Id + "' did not spawn - " + LastSpawnFailureReason);
                    return;
                }

                SpawnedForageNode spawned = new SpawnedForageNode
                {
                    Definition = def,
                    State = new ForageNodeRuntimeState(),
                    Visual = visual,
                    WorldLabel = label,
                    LabelView = label.GetComponent<ForageNodeWorldLabelView>(),
                    VisualRenderers = CacheVisualRenderers(visual),
                    IsDebugPlaceholder = isDebugPlaceholder,
                    ResolvedMeshName = string.IsNullOrEmpty(resolution.MeshName) ? (isDebugPlaceholder ? "(debug placeholder)" : "(none)") : resolution.MeshName,
                    ResolvedShaderName = string.IsNullOrEmpty(resolution.ShaderName) ? (isDebugPlaceholder ? "(debug placeholder)" : "(none)") : resolution.ShaderName,
                    TintApplied = tintApplied,
                    AppliedScale = appliedScale
                };
                if (!AttachInteractionTarget(spawned, presentationBounds))
                {
                    LastSpawnFailureReason = def.Id + ": visual presentation binding rejected.";
                    try { UnityEngine.Object.Destroy(label); } catch { }
                    try { UnityEngine.Object.Destroy(visual); } catch { }
                    return;
                }
                _spawned.Add(spawned);
                LastNameplateSummary = "bound node=" + def.Id + " " + ForageNodeWorldLabel.DescribePresentation();
                LastSpawnFailureReason = string.Empty;
        }

        private static bool AttachInteractionTarget(SpawnedForageNode node, Bounds auditedBounds)
        {
            if (node == null || node.Visual == null) return false;
            try
            {
                Bounds combined = auditedBounds;
                Vector3 center = new Vector3(combined.center.x, combined.min.y + combined.size.y * 0.45f, combined.center.z);
                float radius = ForageInteractionPolicy.CalculateHitRadius(combined.size.x, combined.size.z);
                float hitHeight = ForageInteractionPolicy.CalculateHitHeight(combined.size.y, radius);

                GameObject targetObject = new GameObject("ForageInteractionTarget_" + SafeNodeId(node));
                targetObject.layer = 2; // Ignore Raycast to vanilla/default rays; our explicit mask owns it.
                targetObject.transform.position = new Vector3(combined.center.x, combined.min.y + hitHeight * 0.5f, combined.center.z);
                targetObject.transform.rotation = Quaternion.identity;
                targetObject.transform.localScale = Vector3.one;
                CapsuleCollider capsule = targetObject.AddComponent<CapsuleCollider>();
                capsule.isTrigger = true;
                capsule.direction = 1;
                capsule.radius = radius;
                capsule.height = hitHeight;
                ForageNodeInteractionTarget target = targetObject.AddComponent<ForageNodeInteractionTarget>();
                target.Node = node;
                target.HitCollider = capsule;
                node.InteractionTarget = targetObject;
                node.InteractionComponent = target;
                ForageNodeHighlightView highlightView;
                GameObject highlight = ForageNodeHighlight.Create(combined, SafeNodeId(node), out highlightView);
                node.Highlight = highlight;
                node.HighlightView = highlightView;
                return true;
            }
            catch (System.Exception ex)
            {
                LastTargetSummary = "target-binding-failed node=" + SafeNodeId(node) + " reason={" + ex.Message + "}";
                return false;
            }
        }

        internal static void SceneTransition()
        {
            WorldThreatRuntime.Reset();
            CancelActiveGather(ForageGatherCancelReason.ZoneChanged);
            DespawnAll();
            ClearVisualSourceCache();
        }

        internal static void DisableGameplay()
        {
            // Gameplay toggles should despawn scene objects without erasing successful-gather
            // cooldowns. Otherwise EnableMod OFF -> ON would become a free resource respawn.
            CancelActiveGather(ForageGatherCancelReason.GameplayDisabled);
            DespawnAll();
        }

        internal static void Shutdown()
        {
            WorldThreatRuntime.Reset();
            CancelActiveGather(ForageGatherCancelReason.PluginUnload);
            DespawnAll();
            ForageNodeHighlight.Shutdown();
            ClearVisualSourceCache();
            ForageDepletionLedger.Clear();
        }

        internal static void RuntimeExceptionCleanup()
        {
            CancelActiveGather(ForageGatherCancelReason.RuntimeException);
        }

        private static void DespawnAll()
        {
            // All normal lifecycle callers cancel first with a meaningful reason. This is an
            // idempotent final guard for rebuild/error paths so an optional StartLoot pose can
            // never survive destruction of its node.
            if (_activeGatherNode != null) CancelActiveGather(ForageGatherCancelReason.WorldInteraction);
            foreach (SpawnedForageNode node in _spawned) DestroyNodeOwnedObjects(node);
            _spawned.Clear();
            _targetedNode = null;
            _interactionCamera = null;
            _nextInteractionCameraProbe = 0f;
            ForageGameplayCameraResolver.Reset();
            ForageNodeWorldLabel.ResetDiagnostics();
            _visualFamilySuccessLogged.Clear();
            _visualFamilyFailureLogged.Clear();
            _nextTargetProbe = 0f;
            _lastEligibilityNodeId = string.Empty;
            _lastEligibility = ForageInteractionEligibility.NoNode;
            LastTargetSummary = "target=(none)";
            LastTargetInvariantSummary = "targetInvariant=healthy";
            _lastTargetInvariantKey = string.Empty;
            _lastTargetInvariantLogAt = -999f;
            _spawnedScene = string.Empty;
            _autoTrialAttemptCount = 0;
            _autoTrialRetrySeconds = 0f;
            LastAutoTrialSummary = string.Empty;
        }

        internal static string SafeSceneName()
        {
            // The player GameObject lives under DontDestroyOnLoad in the current game build, so
            // its Unity scene is not the gameplay zone. For the procedural trial use the game's
            // own logical zone name when available; fall back only if that native value is absent.
            try
            {
                string logicalScene = GameData.SceneName;
                if (!string.IsNullOrWhiteSpace(logicalScene)) return logicalScene;
            }
            catch { }
            return GameForagingApi.SafeSceneName();
        }

        internal static int AvailableCount()
        {
            int count = 0;
            foreach (SpawnedForageNode node in _spawned) if (node.State.Availability == ForageAvailability.Available) count++;
            return count;
        }

        internal static int AvailableCountForItem(string itemId)
        {
            if (string.IsNullOrEmpty(itemId)) return 0;
            int count = 0;
            foreach (SpawnedForageNode node in _spawned)
                if (node != null && node.Definition != null &&
                    string.Equals(node.Definition.RewardItemId, itemId, System.StringComparison.Ordinal) &&
                    node.State.Availability == ForageAvailability.Available) count++;
            return count;
        }

        internal static int DepletedCount()
        {
            int count = 0;
            foreach (SpawnedForageNode node in _spawned) if (node.State.Availability == ForageAvailability.Depleted) count++;
            return count;
        }

        internal static int SpawnedCount { get { return _spawned.Count; } }

        internal static int AutoTrialCount()
        {
            int count = 0;
            foreach (SpawnedForageNode node in _spawned) if (node.IsAutoTrial) count++;
            return count;
        }

        internal static string DescribeCompactStatus()
        {
            if (ForagingConfig.EnableForaging == null || !ForagingConfig.EnableForaging.Value) return "OFF";
            int available = AvailableCount();
            if (available > 0) return available.ToString() + " available";
            if (_spawned.Count > 0) return "0 available";
            if (IsAutoPlacementEnabledForScene(SafeSceneName()) && _autoTrialAttemptCount > 0 && _autoTrialAttemptCount < 5) return "searching";
            return "none here";
        }

        internal static string DescribePlayerStatus()
        {
            if (ForagingConfig.EnableForaging == null || !ForagingConfig.EnableForaging.Value) return "OFF";
            string scene = SafeSceneName();
            if (_spawned.Count > 0)
            {
                List<string> availableResources = new List<string>();
                List<ForageResourceDefinition> resources = ForageResourceCatalog.All();
                for (int i = 0; i < resources.Count; i++)
                {
                    ForageResourceDefinition resource = resources[i];
                    int count = AvailableCountForItem(resource.RewardItemId);
                    if (count <= 0) continue;
                    availableResources.Add(resource.DisplayName + " " + count);
                }
                if (availableResources.Count == 0) return "No forage nodes available right now.";
                return string.Join(" | ", availableResources.ToArray()) + " available.";
            }
            if (IsAutoPlacementEnabledForScene(scene) && _autoTrialAttemptCount > 0 && _autoTrialAttemptCount < 5)
                return "Forage auto-placement is looking for safe resource ground beside environmental edges.";
            if (Catalog.CountForScene(scene) == 0) return "No forage nodes in this zone.";
            return "Forage nodes are authored here but unavailable.";
        }

        internal static string DescribePrimaryNode()
        {
            if (_spawned.Count > 0)
            {
                SpawnedForageNode node = _spawned[0];
                return "id=" + node.Definition.Id + " valid=true" +
                    " trial=" + node.IsAutoTrial +
                    (node.IsAutoTrial ? (" environment=" + ForageEnvironmentPolicy.Classify(node.IsCoveredTrial).ToString().ToLowerInvariant() +
                        " wall=" + node.TrialWallName) : string.Empty) +
                    " pos=(" + node.Definition.Position.X + "," + node.Definition.Position.Y + "," + node.Definition.Position.Z + ")" +
                    " source=" + (node.IsAutoTrial ? "auto-ground-beside-edge" : (node.Definition.VisualSourceScene + ":" + node.Definition.VisualSourceHierarchyPath)) +
                    " sourceResolved=" + (!node.IsDebugPlaceholder) +
                    " mesh=" + node.ResolvedMeshName +
                    " shader=" + node.ResolvedShaderName +
                    " scaleMultiplier=" + node.Definition.Scale +
                    " appliedScale=(" + node.AppliedScale.x.ToString("F2") + "," + node.AppliedScale.y.ToString("F2") + "," + node.AppliedScale.z.ToString("F2") + ")" +
                    " tintProperty=" + (node.Definition.TintEnabled ? node.Definition.TintColorProperty : "(off)") +
                    " tintApplied=" + node.TintApplied +
                    " state=" + node.State.Availability +
                    (node.State.Availability == ForageAvailability.Gathering || node.State.Availability == ForageAvailability.GrantPending
                        ? (" gather=" + node.State.GatherElapsedSeconds.ToString("F2") + "/" + node.State.GatherDurationSeconds.ToString("F2"))
                        : string.Empty) +
                    " respawnRemaining=" + node.State.RemainingRespawnSeconds.ToString("F0") + "s" +
                    " " + ForageNodeWorldLabel.DescribePresentation() +
                    (node.IsAutoTrial && !string.IsNullOrEmpty(node.TrialVisualSummary) ? " visual=" + node.TrialVisualSummary : string.Empty) +
                    (node.IsAutoTrial && !string.IsNullOrEmpty(LastAutoTrialSummary) ? " placement=" + LastAutoTrialSummary : string.Empty);
            }

            if (Catalog.Count == 0 && _candidateRejectReason != ForageDefinitionRejectReason.None)
            {
                return "productionCatalog=empty autoPlacement=" + IsAutoPlacementEnabledForScene(SafeSceneName()) +
                    " candidateId=" + _candidateDefinition.Id +
                    " candidateValid=false reason=" + _candidateRejectReason +
                    (string.IsNullOrEmpty(LastAutoTrialSummary) ? string.Empty : " autoTrial=" + LastAutoTrialSummary) +
                    (string.IsNullOrEmpty(LastSpawnFailureReason) ? string.Empty : " lastSpawnFailure=" + LastSpawnFailureReason);
            }

            return "productionCatalog=" + Catalog.Count.ToString() + " spawned=false" +
                (string.IsNullOrEmpty(LastAutoTrialSummary) ? string.Empty : " autoTrial=" + LastAutoTrialSummary) +
                (string.IsNullOrEmpty(LastSpawnFailureReason) ? " (no authored node spawned in this scene)" : " reason=" + LastSpawnFailureReason);
        }
    }
}
