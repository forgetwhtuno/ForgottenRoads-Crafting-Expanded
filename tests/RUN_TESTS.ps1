$ErrorActionPreference = "Stop"
$ScriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path

function Find-Csc {
    foreach ($path in @(
        "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe",
        "$env:WINDIR\Microsoft.NET\Framework\v4.0.30319\csc.exe"
    )) {
        if (Test-Path $path) { return $path }
    }
    throw "csc.exe not found. Install the .NET Framework Developer Pack or Visual Studio Build Tools."
}

$csc = Find-Csc
$out = Join-Path $env:TEMP "ErenshorCraftingExpanded.Tests.exe"

& $csc /nologo /target:exe ("/out:{0}" -f $out) `
    (Join-Path $ScriptRoot "..\src\Persistence\AtomicTextSidecar.cs") `
    (Join-Path $ScriptRoot "..\src\Crafting\CraftingRecipeInfo.cs") `
    (Join-Path $ScriptRoot "..\src\Crafting\CraftableCountPolicy.cs") `
    (Join-Path $ScriptRoot "..\src\Crafting\CraftingProgression.cs") `
    (Join-Path $ScriptRoot "..\src\Crafting\CraftSuccessAwardPolicy.cs") `
    (Join-Path $ScriptRoot "..\src\Crafting\RecipeProgressionPolicy.cs") `
    (Join-Path $ScriptRoot "..\src\Crafting\CraftingCharacterKey.cs") `
    (Join-Path $ScriptRoot "..\src\Crafting\CraftingPersistencePolicy.cs") `
    (Join-Path $ScriptRoot "..\src\Crafting\RecipeDiscoveryPolicy.cs") `
    (Join-Path $ScriptRoot "..\src\Crafting\NativeCraftingEvidencePolicy.cs") `
    (Join-Path $ScriptRoot "..\src\Crafting\CustomRecipePolicy.cs") `
    (Join-Path $ScriptRoot "..\src\Crafting\ProductionRecipePlan.cs") `
    (Join-Path $ScriptRoot "..\src\Crafting\ProductionRecipeRetryPolicy.cs") `
    (Join-Path $ScriptRoot "..\src\Crafting\NativeRecipeContentPolicy.cs") `
    (Join-Path $ScriptRoot "..\src\Crafting\ProductionRecipeDefinitionFactory.cs") `
    (Join-Path $ScriptRoot "..\src\Crafting\ProductionRecipeSelectionPolicy.cs") `
    (Join-Path $ScriptRoot "..\src\Crafting\ProductionRecipeBinding.cs") `
    (Join-Path $ScriptRoot "..\src\Crafting\ProductionRecipeRegistrationState.cs") `
    (Join-Path $ScriptRoot "..\src\Crafting\CraftingRecipeCatalog.cs") `
    (Join-Path $ScriptRoot "..\src\Crafting\ExperimentalRecipeDonorPolicy.cs") `
    (Join-Path $ScriptRoot "..\src\Crafting\RecipeOwnershipModels.cs") `
    (Join-Path $ScriptRoot "..\src\Crafting\KnownRecipeLedger.cs") `
    (Join-Path $ScriptRoot "..\src\Crafting\KnownRecipePersistence.cs") `
    (Join-Path $ScriptRoot "..\src\Crafting\RecipeTemplateRecoveryPolicy.cs") `
    (Join-Path $ScriptRoot "..\src\Crafting\RecipeTemplateStoragePolicy.cs") `
    (Join-Path $ScriptRoot "..\src\Crafting\RecipeTemplateItemPolicy.cs") `
    (Join-Path $ScriptRoot "..\src\Crafting\RecipeBookViewPolicy.cs") `
    (Join-Path $ScriptRoot "..\src\Compatibility\RecipeOwnershipIntegration.cs") `
    (Join-Path $ScriptRoot "..\src\Compatibility\SimIdentitySnapshot.cs") `
    (Join-Path $ScriptRoot "..\src\Commissions\CommissionPolicy.cs") `
    (Join-Path $ScriptRoot "..\src\Commissions\CommissionCadencePolicy.cs") `
    (Join-Path $ScriptRoot "..\src\UI\CraftingPanelPositioning.cs") `
    (Join-Path $ScriptRoot "..\src\UI\CraftingPanelLayoutPolicy.cs") `
    (Join-Path $ScriptRoot "..\src\UI\CraftingKnowledgePresentationPolicy.cs") `
    (Join-Path $ScriptRoot "..\src\UI\CraftingUiState.cs") `
    (Join-Path $ScriptRoot "..\src\SuiteUiPolicies.cs") `
    (Join-Path $ScriptRoot "..\src\CraftingPointerOwnershipState.cs") `
    (Join-Path $ScriptRoot "..\src\CraftingCameraUiPolicy.cs") `
    (Join-Path $ScriptRoot "..\src\Foraging\ForageNodeDefinition.cs") `
    (Join-Path $ScriptRoot "..\src\Foraging\ForageNodeCatalog.cs") `
    (Join-Path $ScriptRoot "..\src\Foraging\ForageNodeState.cs") `
    (Join-Path $ScriptRoot "..\src\Foraging\ForagingInventoryGrantResult.cs") `
    (Join-Path $ScriptRoot "..\src\Foraging\ForageGatherCancellationPolicy.cs") `
    (Join-Path $ScriptRoot "..\src\Foraging\ForageGatherHoldPolicy.cs") `
    (Join-Path $ScriptRoot "..\src\Foraging\ForageInteractionCaptureState.cs") `
    (Join-Path $ScriptRoot "..\src\Foraging\ForageCombatEligibilityPolicy.cs") `
    (Join-Path $ScriptRoot "..\src\Foraging\ForageActiveGatherClickPolicy.cs") `
    (Join-Path $ScriptRoot "..\src\Foraging\ForageInteractionPolicy.cs") `
    (Join-Path $ScriptRoot "..\src\Foraging\ForageHighlightPolicy.cs") `
    (Join-Path $ScriptRoot "..\src\Foraging\ForageDepletionLedger.cs") `
    (Join-Path $ScriptRoot "..\src\Foraging\ForageAmbiguousGrantQuarantine.cs") `
    (Join-Path $ScriptRoot "..\src\Foraging\ForagingCharacterKey.cs") `
    (Join-Path $ScriptRoot "..\src\Foraging\ForagingCharacterReadinessPolicy.cs") `
    (Join-Path $ScriptRoot "..\src\Foraging\ForageRegionalPolicy.cs") `
    (Join-Path $ScriptRoot "..\src\Foraging\ForagingProgression.cs") `
    (Join-Path $ScriptRoot "..\src\Foraging\ForagingProgressionCodec.cs") `
    (Join-Path $ScriptRoot "..\src\Foraging\ForagingProgressionStore.cs") `
    (Join-Path $ScriptRoot "..\src\Foraging\ForageBillboardPolicy.cs") `
    (Join-Path $ScriptRoot "..\src\Foraging\ForagePresentationPolicy.cs") `
    (Join-Path $ScriptRoot "..\src\Foraging\ForagingRuntimeConfigValidation.cs") `
    (Join-Path $ScriptRoot "..\src\Foraging\ForagingScanPolicy.cs") `
    (Join-Path $ScriptRoot "..\src\Foraging\ForagePlacementPolicy.cs") `
    (Join-Path $ScriptRoot "..\src\Foraging\ForageEnvironmentPolicy.cs") `
    (Join-Path $ScriptRoot "..\src\Foraging\WorldThreatPolicy.cs") `
    (Join-Path $ScriptRoot "..\src\Foraging\ForageResourceCatalog.cs") `
    (Join-Path $ScriptRoot "..\src\Foraging\ForageResourceSelectionPolicy.cs") `
    (Join-Path $ScriptRoot "..\src\Foraging\ForageResourceAvailabilityPolicy.cs") `
    (Join-Path $ScriptRoot "..\src\Foraging\ResourceObtainabilityCatalog.cs") `
    (Join-Path $ScriptRoot "..\src\Foraging\ForageVisualPolicy.cs") `
    (Join-Path $ScriptRoot "..\src\Items\OrganicItemBasePolicy.cs") `
    (Join-Path $ScriptRoot "..\src\Items\CraftingExpandedItemIds.cs") `
    (Join-Path $ScriptRoot "CraftingExpandedItemsTestShim.cs") `
    (Join-Path $ScriptRoot "..\src\Items\ExpandedItemDefinition.cs") `
    (Join-Path $ScriptRoot "..\src\Items\ItemSemanticsPolicy.cs") `
    (Join-Path $ScriptRoot "..\src\Items\NativeConsumablePolicy.cs") `
    (Join-Path $ScriptRoot "..\src\Items\ExpandedEquipmentDonorPolicy.cs") `
    (Join-Path $ScriptRoot "..\src\Items\ExpandedContentItems.cs") `
    (Join-Path $ScriptRoot "..\src\Items\ItemIconAssetLoader.cs") `
    (Join-Path $ScriptRoot "..\src\Crafting\ExpandedContentRecipes.cs") `
    (Join-Path $ScriptRoot "..\src\Items\CustomItemDefinition.cs") `
    (Join-Path $ScriptRoot "..\src\Items\CustomItemRegistry.cs") `
    (Join-Path $ScriptRoot "ForageGatherTransactionTests.cs") `
    (Join-Path $ScriptRoot "RunAllTests.cs")
if ($LASTEXITCODE -ne 0) { throw "Test compilation failed." }

try {
    & $out
    if ($LASTEXITCODE -ne 0) { throw "Erenshor Crafting Expanded tests failed with exit code $LASTEXITCODE." }

    $placementSource = Get-Content -LiteralPath (Join-Path $ScriptRoot "..\src\Foraging\ForageAutoPlacementTrial.cs") -Raw
    if ($placementSource -match '\bother\.ClosestPoint\s*\(') { throw "Foraging regression: Collider.ClosestPoint reintroduced into placement clearance." }

    $clickPatch = Get-Content -LiteralPath (Join-Path $ScriptRoot "..\src\Foraging\ForageNativeClickPatch.cs") -Raw
    if ($clickPatch -notmatch 'PlayerControl' -or $clickPatch -notmatch 'RightClick') { throw "Foraging regression: native RightClick patch missing." }
    $productionForaging = Get-ChildItem -LiteralPath (Join-Path $ScriptRoot "..\src\Foraging") -Filter '*.cs' -Recurse | ForEach-Object { Get-Content -LiteralPath $_.FullName -Raw }
    if (($productionForaging -join "`n") -match 'GetKey(?:Down|Up)?\s*\([^\)]*(?:KeyCode\.G|ForageKey)') { throw "Foraging regression: keyboard gathering path reintroduced." }

    $registrySource = Get-Content -LiteralPath (Join-Path $ScriptRoot "..\src\Crafting\ProductionNativeRecipeRegistry.cs") -Raw
    $actionableAt = $registrySource.IndexOf('ProductionRecipeRetryPolicy.IsActionable', [System.StringComparison]::Ordinal)
    $consumeAt = $registrySource.IndexOf('ProductionRecipeRetryPolicy.ConsumeAttempt', [System.StringComparison]::Ordinal)
    if ($actionableAt -lt 0 -or $consumeAt -lt 0 -or $consumeAt -lt $actionableAt) { throw "Production recipe regression: retry budget can be consumed before live forge evidence." }
    if ($registrySource -notmatch 'ResetAfterGameplayDisable') { throw "Production recipe regression: disable/re-enable retry reset missing." }

    $sidecarSource = Get-Content -LiteralPath (Join-Path $ScriptRoot "..\src\Persistence\AtomicTextSidecar.cs") -Raw
    if ($sidecarSource -notmatch 'Flush\(true\)' -or $sidecarSource -notmatch '\.tmp' -or $sidecarSource -notmatch '\.bak') { throw "Persistence regression: durable temp/backup recovery primitive incomplete." }

    $controllerSource = Get-Content -LiteralPath (Join-Path $ScriptRoot "..\src\Foraging\ForageNodeController.cs") -Raw
    foreach ($required in @('Available','Gathering','GrantPending','Depleted','gather_begin','gather_cancel','grant_attempt','grant_success','UnknownAfterInvoke','forage_capture_begin','forage_capture_cancel','forage_capture_complete','forage_capture_release','forage_input_owned','forage_input_released')) {
        if ($controllerSource -notmatch [regex]::Escape($required)) { throw "Foraging gather regression: missing $required transaction contract." }
    }
    if (($controllerSource | Select-String -Pattern 'GrantRegisteredItemForForaging\(' -AllMatches).Matches.Count -ne 1) { throw "Foraging gather regression: custom strict grant call count is not exactly one in controller source." }
    if (($controllerSource | Select-String -Pattern 'GrantVanillaItemForForaging\(' -AllMatches).Matches.Count -ne 1) { throw "Foraging gather regression: vanilla strict grant call count is not exactly one in controller source." }
    if ($controllerSource -match 'GrantRegisteredItem\(_activeRewardItemId' -or $controllerSource -match 'GrantVanillaItem\([^\r\n]*_activeReward') { throw "Foraging gather regression: force-capable generic grant reintroduced." }
    $customGrant = Get-Content -LiteralPath (Join-Path $ScriptRoot "..\src\Compatibility\GameItemRegistryApi.cs") -Raw
    $vanillaGrant = Get-Content -LiteralPath (Join-Path $ScriptRoot "..\src\Compatibility\GameForagingApi.cs") -Raw
    $strictCustomStart = $customGrant.IndexOf('GrantRegisteredItemForForaging', [System.StringComparison]::Ordinal)
    $strictCustomEnd = $customGrant.IndexOf('TryApplyRecipeTemplateSafety', $strictCustomStart, [System.StringComparison]::Ordinal)
    if ($strictCustomStart -lt 0 -or $strictCustomEnd -lt 0 -or $customGrant.Substring($strictCustomStart, $strictCustomEnd-$strictCustomStart) -match 'ForceItemToInv') { throw "Foraging strict custom grant may force inventory insertion." }
    $strictVanillaStart = $vanillaGrant.IndexOf('GrantVanillaItemForForaging', [System.StringComparison]::Ordinal)
    $strictVanillaEnd = $vanillaGrant.IndexOf('TryPlaySuccessfulForageSound', $strictVanillaStart, [System.StringComparison]::Ordinal)
    if ($strictVanillaStart -lt 0 -or $strictVanillaEnd -lt 0 -or $vanillaGrant.Substring($strictVanillaStart, $strictVanillaEnd-$strictVanillaStart) -match 'ForceItemToInv') { throw "Foraging strict vanilla grant may force inventory insertion." }
    if ($vanillaGrant -notmatch 'Misc' -or $vanillaGrant -notmatch 'DropItem' -or $vanillaGrant -notmatch 'PlayerAud\.volume\s*/\s*2f\s*\*\s*GameData\.UIVolume\s*\*\s*GameData\.MasterVol') { throw "Foraging successful-loot sound evidence path missing." }
    if ($vanillaGrant -notmatch 'StartLoot' -or $vanillaGrant -notmatch 'EndLoot') { throw "Foraging optional native animation cleanup adapter incomplete." }

    $settingsSource = Get-Content -LiteralPath (Join-Path $ScriptRoot "..\src\CraftingExpandedSettings.cs") -Raw
    if ($settingsSource -notmatch 'GatherDurationSeconds\s*=\s*1\.25f' -or $settingsSource -notmatch 'UseNativeGatherAnimation\s*=\s*false') { throw "Foraging gather production defaults changed unexpectedly." }

    $labelSource = Get-Content -LiteralPath (Join-Path $ScriptRoot "..\src\Foraging\ForageNodeWorldLabel.cs") -Raw
    if ($labelSource -notmatch 'SetFillFraction\(ForagePresentationPolicy\.ResourceBarFill' -or $labelSource -notmatch 'scale\.x\s*\*=\s*fraction' -or $labelSource -notmatch 'barFill\.pivot\s*=\s*new Vector2\(0f, 0\.5f\)') { throw "Foraging resource-bar gather progress presentation missing." }
    if ($labelSource -notmatch 'AddComponent<Outline>' -or $labelSource -notmatch 'effectDistance\s*=\s*new Vector2\(2\.4f, -2\.4f\)' -or $labelSource -notmatch 'rendererAnchor') { throw "Foraging world-label hard-border/renderer-anchor contract missing." }
    if ($labelSource -match 'new\s+Material\s*\(') { throw "Foraging completion feedback must not allocate/mutate a material." }
    $highlightSource = Get-Content -LiteralPath (Join-Path $ScriptRoot "..\src\Foraging\ForageNodeHighlight.cs") -Raw
    $highlightPolicy = Get-Content -LiteralPath (Join-Path $ScriptRoot "..\src\Foraging\ForageHighlightPolicy.cs") -Raw
    if ($highlightSource -notmatch 'sharedMaterial' -or $highlightSource -notmatch 'Shutdown' -or $highlightSource -match 'Update\s*\(') { throw "Foraging highlight lifecycle/material contract failed." }
    if ($highlightPolicy -notmatch 'MinimumRadius' -or $highlightPolicy -notmatch 'ReadyWidthMultiplier') { throw "Foraging highlight policy contract failed." }
    if ($controllerSource -notmatch 'ForageNodeHighlight\.Create' -or $controllerSource -notmatch 'Destroy\(node\.Highlight\)') { throw "Foraging highlight ownership/cleanup contract failed." }
    if ($controllerSource -notmatch 'CapsuleCollider' -or $controllerSource -notmatch 'isTrigger\s*=\s*true' -or $controllerSource -notmatch 'ShouldPreferPointerHit') { throw "Foraging interaction target contract failed." }
    foreach ($required in @('TryAuditProductionVisual', 'no-active-renderable-mesh', 'post-ground-delta=', 'renderer-anchor-offset=', 'LogAutoVisualAttempt', 'forage_visual_spawn', 'bindingAllowed=', 'AttachInteractionTarget(spawned, presentationBounds)')) {
        if ($controllerSource.IndexOf($required, [System.StringComparison]::Ordinal) -lt 0) { throw "Foraging visual-validity contract missing: $required" }
    }
    $itemSource = Get-Content -LiteralPath (Join-Path $ScriptRoot "..\src\Items\ExpandedContentItems.cs") -Raw
    if ($itemSource -notmatch 'Barkguard Maul' -or $itemSource -notmatch 'TwoHandMelee' -or $itemSource -notmatch 'PrimaryOrSecondary') { throw "Expanded equipment semantic reconciliation missing." }

    $semanticsSource = Get-Content -LiteralPath (Join-Path $ScriptRoot "..\src\Items\ItemSemanticsPolicy.cs") -Raw
    foreach ($required in @('RawResource', 'ProcessedComponent', 'FinishedEquipment', 'PlayerCannotSell = false', 'NoTradeNoDestroy = false', 'IsConservativeProcessedValue')) {
        if ($semanticsSource.IndexOf($required, [System.StringComparison]::Ordinal) -lt 0) { throw "Inventory semantics policy contract missing: $required" }
    }
    $itemRegistrySource = Get-Content -LiteralPath (Join-Path $ScriptRoot "..\src\Compatibility\GameItemRegistryApi.cs") -Raw
    foreach ($required in @('ApplyOrdinaryItemSemantics', 'SetField(item, "AssignQuestOnRead", null)', 'SetField(item, "CompleteOnRead", null)', 'BuildOwnedInventorySemanticsLines')) {
        if ($itemRegistrySource.IndexOf($required, [System.StringComparison]::Ordinal) -lt 0) { throw "Inventory semantics registry contract missing: $required" }
    }
    $allCraftingSource = Get-ChildItem -LiteralPath (Join-Path $ScriptRoot "..\src") -Filter '*.cs' -Recurse | ForEach-Object { Get-Content -LiteralPath $_.FullName -Raw }
    $allCraftingJoined = $allCraftingSource -join "`n"
    foreach ($forbidden in @('[HarmonyPatch(typeof(VendorWindow)', '[HarmonyPatch(typeof(TrashSlot)', 'ConfirmDestroyWindow', 'SubmitDelete(', 'Gold +=', 'RemoveItemFromInv(', 'RemoveStackFromInv(')) {
        if ($allCraftingJoined.IndexOf($forbidden, [System.StringComparison]::Ordinal) -ge 0) { throw "Inventory semantics must leave native sell/delete transaction authority untouched: $forbidden" }
    }
    $recipeTemplatePolicy = Get-Content -LiteralPath (Join-Path $ScriptRoot "..\src\Crafting\RecipeTemplateItemPolicy.cs") -Raw
    if ($recipeTemplatePolicy -notmatch 'PlayerCannotSell = true' -or $recipeTemplatePolicy -notmatch 'NoTradeNoDestroy = true' -or $recipeTemplatePolicy -notmatch 'SafeVendorValue = 0') { throw "Protected recipe-template inventory semantics regressed." }
    $expandedRecipeEconomy = Get-Content -LiteralPath (Join-Path $ScriptRoot "..\src\Crafting\ExpandedContentRecipes.cs") -Raw
    if ($expandedRecipeEconomy -notmatch 'IsConservativeProcessedValue') { throw "Processed-component per-unit intrinsic-value guard missing." }

    $dragSource = Get-Content -LiteralPath (Join-Path $ScriptRoot "..\src\RetainedUiKit.cs") -Raw
    foreach ($required in @('OnPointerDown(', 'CraftingUiPointerOwnership.Acquire', 'Input.GetMouseButton(0)', 'OnApplicationFocus', 'OnApplicationPause', 'forgetwhtuno.erenshor.ui.drag.owners.v1', 'forgetwhtuno.erenshor.ui.drag.nativeBaseline.v1', 'forgetwhtuno.erenshor.ui.drag.nativeBaselineCaptured.v1')) {
        if ($dragSource.IndexOf($required, [System.StringComparison]::Ordinal) -lt 0) { throw "Crafting retained drag ownership lifecycle/coordination incomplete: $required" }
    }
    $cameraPatch = Get-Content -LiteralPath (Join-Path $ScriptRoot "..\src\CraftingCameraUiOwnershipPatch.cs") -Raw
    foreach ($required in @('TryVerify', 'UIWindows', 'ModernControls', 'releaseMouse', 'GetAxis', 'DraggingUIElement', 'EventSystem', 'IsPointerOverGameObject', 'PlayerControl', 'LeftClick', 'RightClick', 'MouseLook', 'MouseLookPrefix', 'harmony.Patch', 'CraftingCameraUiPolicy.PromoteUsingUi', 'global EventSystem gate is not patched')) {
        if ($cameraPatch.IndexOf($required, [System.StringComparison]::Ordinal) -lt 0) { throw "Crafting verified captured-input/camera ownership contract incomplete: $required" }
    }
    if ($cameraPatch -match '\[HarmonyPatch\s*\(\s*typeof\(CameraController\)') { throw "Crafting camera containment must not install by unverified attribute target." }
    if ($cameraPatch -match 'harmony\.Patch\s*\(\s*pointerOverUi') { throw "Crafting forage capture must not globally patch EventSystem pointer ownership; that would suppress native movement." }

    $captureSource = Get-Content -LiteralPath (Join-Path $ScriptRoot "..\src\Foraging\ForageInteractionCaptureState.cs") -Raw
    foreach ($required in @('Idle', 'HoverCandidate', 'Selected', 'Gathering', 'Completing', 'Cancelled', 'TrySelect', 'TryBeginGathering', 'TryBeginCompleting', 'Release')) {
        if ($captureSource.IndexOf($required, [System.StringComparison]::Ordinal) -lt 0) { throw "Foraging capture state machine incomplete: $required" }
    }
    $channelSource = Get-Content -LiteralPath (Join-Path $ScriptRoot "..\src\Foraging\ForageGatherHoldPolicy.cs") -Raw
    foreach ($required in @('ForageGatherChannelPolicy', 'MovementCancelDistance = 0.10f', 'HasMeaningfulMovement', 'HasActualHpDecrease')) {
        if ($channelSource.IndexOf($required, [System.StringComparison]::Ordinal) -lt 0) { throw "One-click gather interruption policy incomplete: $required" }
    }
    foreach ($required in @('AcquireActiveGatherInputOwnership', 'ReleaseCapturedInteraction', 'OwnsCapturedPointerInput', 'IsConflictingUiDuringCapturedGather', 'capture=locked')) {
        if ($controllerSource.IndexOf($required, [System.StringComparison]::Ordinal) -lt 0) { throw "Foraging captured-input controller contract missing: $required" }
    }
    $tickAt = $controllerSource.IndexOf('private static void TickActiveGather()', [System.StringComparison]::Ordinal)
    $grantAt = $controllerSource.IndexOf('private static void CompleteActiveGatherGrant()', [System.StringComparison]::Ordinal)
    if ($tickAt -lt 0 -or $grantAt -le $tickAt) { throw "Foraging active-gather method boundaries unavailable." }
    $tickSource = $controllerSource.Substring($tickAt, $grantAt - $tickAt)
    foreach ($forbidden in @('TryResolvePointerTarget', 'pointerResolved', 'aimedNode', 'AimLost', 'Input.GetMouseButton(0)')) {
        if ($tickSource.IndexOf($forbidden, [System.StringComparison]::Ordinal) -ge 0) { throw "Captured gather must not depend on live hover/held-button authority: $forbidden" }
    }
    foreach ($required in @('ForageGatherChannelPolicy.EvaluateUi', 'HasMeaningfulMovement', '_activeGatherLastHp', 'ReleaseActiveGatherInputOwnership("right-click-release")')) {
        if ($tickSource.IndexOf($required, [System.StringComparison]::Ordinal) -lt 0) { throw "One-click gather active-channel contract missing: $required" }
    }

    foreach ($required in @('_activeNativeGrantInvokeStarted', 'out _activeNativeGrantInvokeStarted', 'RecordAmbiguousGrantQuarantine', 'RuntimeExceptionCleanup')) {
        if ($controllerSource.IndexOf($required, [System.StringComparison]::Ordinal) -lt 0) { throw "Foraging post-invoke ambiguity/runtime cleanup contract incomplete: $required" }
    }
    if ($customGrant.IndexOf('out bool nativeInvokeStarted', [System.StringComparison]::Ordinal) -lt 0 -or $vanillaGrant.IndexOf('out bool nativeInvokeStarted', [System.StringComparison]::Ordinal) -lt 0) { throw "Foraging strict adapters must expose native-invoke-started authority." }
    $codecSource = Get-Content -LiteralPath (Join-Path $ScriptRoot "..\src\Foraging\ForagingProgressionCodec.cs") -Raw
    if ($codecSource -notmatch 'quarantine=' -or $codecSource -notmatch 'AmbiguousGrants') { throw "Foraging ambiguous-grant quarantine persistence missing." }

    $worldThreatApi = Get-Content -LiteralPath (Join-Path $ScriptRoot "..\src\Compatibility\GameWorldThreatApi.cs") -Raw
    foreach ($required in @('FindObjectsOfType<NPC>', 'actor.Master != null', 'actor.Invulnerable', 'actor.isVendor', 'actor.BossXp > 0f', 'npc.SimPlayer', 'npc.ThisSim != null', 'npc.NeverAggro', 'npc.MiningNode', 'npc.TreasureChest', 'npc.SummonedByPlayer', 'MyDialog', 'MyQuests', 'questToAssign', 'RareSpawns', 'PvP_TemporaryClone', 'actor.MyStats.Level')) {
        if ($worldThreatApi.IndexOf($required, [System.StringComparison]::Ordinal) -lt 0) { throw "World-tier hostile filter regression: missing $required" }
    }
    if ($worldThreatApi -match '\bPlayer(?:Level|Lvl)\s*[=:.(]' -or $worldThreatApi -match '\bCharacterLevel\s*[=:.(]') { throw "World-tier regression: player-level scaling introduced." }
    $consumableApi = Get-Content -LiteralPath (Join-Path $ScriptRoot "..\src\Compatibility\GameItemRegistryApi.cs") -Raw
    foreach ($required in @('ItemEffectOnClick', 'Disposable', 'SpellCastTime', 'FindSafeConsumableDonor', 'ApplyConsumableSemantics', 'native consumable donor', 'DirectHp', 'FlatMana', 'PercentManaRestoration', 'TargetDamage', 'BeneficialType', 'SelfOnly', 'IsProgressionCompatible', 'BuildNativeConsumableReferenceLines')) {
        if ($consumableApi.IndexOf($required, [System.StringComparison]::Ordinal) -lt 0) { throw "Consumable regression: missing native-donor contract $required" }
    }
    if ($consumableApi -match 'ForceItemToInv[^\r\n]*FieldTonic' -or $consumableApi -match 'HP\s*\+=' -or $consumableApi -match 'CurrentMana\s*\+=') { throw "Consumable regression: fake/manual recovery path introduced." }
    $consumeStart = $consumableApi.IndexOf('private static bool ApplyConsumableSemantics', [System.StringComparison]::Ordinal)
    $consumeEnd = $consumableApi.IndexOf('private static object FindSafeEquipmentDonor', [System.StringComparison]::Ordinal)
    if ($consumeStart -lt 0 -or $consumeEnd -le $consumeStart) { throw "Consumable regression: apply-semantics boundary missing." }
    $consumeChunk = $consumableApi.Substring($consumeStart, $consumeEnd - $consumeStart)
    foreach ($required in @('SetField(item, "ItemEffectOnClick", donorEffect)', 'SetField(item, "ItemIcon", donorIcon)', 'SetField(item, "SpellCastTime", donorCastTime)', 'object.ReferenceEquals')) {
        if ($consumeChunk.IndexOf($required, [System.StringComparison]::Ordinal) -lt 0) { throw "Consumable regression: donor-exact behavior field missing $required" }
    }
    if ($consumeChunk.IndexOf('ClearClassRestrictions(item)', [System.StringComparison]::Ordinal) -ge 0) { throw "Consumable regression: clone rewrites native eligibility metadata." }

    $allSource = Get-ChildItem -LiteralPath (Join-Path $ScriptRoot "..\src") -Filter '*.cs' -Recurse | ForEach-Object { Get-Content -LiteralPath $_.FullName -Raw }
    if (($allSource -join "`n") -match '\bOnGUI\s*\(') { throw "Retained UI regression: OnGUI found in Crafting source." }

    $launcherVisual = Get-Content -LiteralPath (Join-Path $ScriptRoot "..\src\StandaloneLauncherVisual.cs") -Raw
    $launcherSource = Get-Content -LiteralPath (Join-Path $ScriptRoot "..\src\UI\CraftingLauncher.cs") -Raw
    $windowSource = Get-Content -LiteralPath (Join-Path $ScriptRoot "..\src\UI\CraftingWindow.cs") -Raw
    if ($launcherVisual -notmatch 'Width\s*=\s*154f' -or $launcherVisual -notmatch 'Height\s*=\s*32f' -or
        $launcherVisual -notmatch 'GripWidth\s*=\s*20f' -or $launcherVisual -notmatch '"GripDot"' -or
        $launcherSource -notmatch 'StyleGrip\(grip\)' -or $windowSource -notmatch 'AddVerticalChevron\(_collapseChevron, !collapsed\)') {
        throw "Crafting Forgotten Roads launcher/chevron visual contract failed."
    }

    if ($windowSource.Contains([char]0x25B2) -or $windowSource.Contains([char]0x25BC) -or $windowSource.Contains([char]0x25BE) -or $windowSource.Contains([char]0x25B8)) { throw "Crafting header guard failed: Unicode collapse glyph dependency introduced." }
    if ($windowSource -notmatch 'AddHeaderLeftButton\(header, "Collapse"' -or
        $windowSource -notmatch 'HeaderTitleLeftInset' -or
        $windowSource -notmatch 'HeaderRightControlsWidth') {
        throw "Crafting header guard failed: collapse control is not immediately left of the title."
    }
    if ([regex]::Matches($windowSource, 'AddVerticalChevron\(_collapseChevron').Count -ne 2) {
        throw "Crafting header guard failed: collapse graphic creation/update path changed unexpectedly."
    }
    if ($windowSource -notmatch 'AddHeaderButton\(header, "Reset", "R", -38f' -or
        $windowSource -notmatch 'AddHeaderButton\(header, "Close", "X", -6f') {
        throw "Crafting header guard failed: real Reset/X right-side chrome regressed."
    }


    $stalePromptMatches = Get-ChildItem -LiteralPath (Join-Path $ScriptRoot "..\src") -Filter '*.cs' -Recurse | Select-String -Pattern 'Press G to gather' -SimpleMatch
    if ($stalePromptMatches) { throw "Foraging regression: stale Press G to gather prompt returned." }
    $expandedRecipeSource = Get-Content -LiteralPath (Join-Path $ScriptRoot "..\src\Crafting\ExpandedContentRecipes.cs") -Raw
    if ([regex]::Matches($expandedRecipeSource, '9101100').Count -lt 21) { throw "Expanded content regression: expected 21 stable production recipe identities." }

    $assetRoot = Join-Path $ScriptRoot "..\assets"
    $manifestPath = Join-Path $assetRoot 'item-art-manifest.json'
    if (-not (Test-Path $manifestPath -PathType Leaf)) { throw "Expanded content regression: item-art manifest missing." }
    $manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
    if ($manifest.schema_version -ne 1 -or @($manifest.items).Count -ne 25) { throw "Expanded content regression: item-art manifest must contain schema v1 and exactly 25 implemented item icons." }
    $manifestIds = @{}
    foreach ($entry in @($manifest.items)) {
        $id = [string]$entry.stable_item_id
        $iconPath = [string]$entry.icon_asset_path
        if (-not $id -or $manifestIds.ContainsKey($id)) { throw "Expanded content regression: duplicate/empty item-art stable ID: $id" }
        $manifestIds[$id] = $true
        if (-not $iconPath -or [IO.Path]::IsPathRooted($iconPath) -or $iconPath.Contains('..')) { throw "Expanded content regression: unsafe icon manifest path for $id" }
        $fullIcon = Join-Path (Join-Path $ScriptRoot '..') $iconPath
        if (-not (Test-Path $fullIcon -PathType Leaf)) { throw "Expanded content regression: manifest icon missing for $id at $iconPath" }
        $bytes = [IO.File]::ReadAllBytes((Resolve-Path $fullIcon).Path)
        if ($bytes.Length -lt 24 -or $bytes[0] -ne 137 -or $bytes[1] -ne 80 -or $bytes[2] -ne 78 -or $bytes[3] -ne 71) { throw "Expanded content regression: icon is not a PNG for $id" }
    }
    $iconCount = @(Get-ChildItem -LiteralPath (Join-Path $assetRoot 'icons') -Filter '*.png' -File).Count
    if ($iconCount -ne 25) { throw "Expanded content regression: expected exactly 25 implemented icon PNGs, found $iconCount." }

    Write-Host "Source contract checks: PASS" -ForegroundColor Green
}
finally {
    Remove-Item $out -Force -ErrorAction SilentlyContinue
}
