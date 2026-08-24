using System.Collections.Generic;

namespace ErenshorCraftingExpanded
{
    internal static class CraftingDiagnostics
    {
        internal static void Report(string modVersion)
        {
            List<string> lines = new List<string>();
            lines.Add("=== Erenshor Crafting Expanded v" + modVersion + " ===");
            lines.Add("Scene: " + SafeSceneName());

            bool forgeOpen = GameCraftingApi.IsForgeOpen();
            CraftRecipeSnapshot recipe = forgeOpen ? GameCraftingApi.TryGetActiveRecipe() : null;
            lines.Add("Forge: state=" + (forgeOpen ? "open" : "closed") +
                " recipe=" + (recipe != null ? recipe.TemplateItemName : "(none)") +
                " canCraft=" + CraftingController.LastCraftableCount +
                " hotkey=" + (CraftingConfig.CraftHotkey != null ? CraftingConfig.CraftHotkey.Value.ToString() : "(unset)") +
                " lastCraft=" + (CraftingController.LastCraft != null ? CraftingController.LastCraft.OutputItemName : "(none)") +
                " autoFillMoved=" + CraftingController.LastAutoFillMovedUnits +
                " lastRejection=" + (string.IsNullOrEmpty(CraftingController.LastRejectionReason) ? "(none)" : CraftingController.LastRejectionReason));

            CustomItemRegistrationOutcome herbOutcome = CraftingExpandedItems.Outcome(CraftingExpandedItemIds.WildHerbId);
            CustomItemRegistrationOutcome fungusOutcome = CraftingExpandedItems.Outcome(CraftingExpandedItemIds.CaveMushroomId);
            lines.Add("Custom items: range=" + CraftingExpandedItemIds.RangeStart + "-" + CraftingExpandedItemIds.RangeEnd +
                " WildHerb=" + DescribeItemOutcome(CraftingExpandedItemIds.WildHerbId, herbOutcome) +
                " inventory=" + CountInventoryItem(CraftingExpandedItemIds.WildHerbId));
            lines.Add("Custom items covered: CaveMushroom=" + DescribeItemOutcome(CraftingExpandedItemIds.CaveMushroomId, fungusOutcome) +
                " inventory=" + CountInventoryItem(CraftingExpandedItemIds.CaveMushroomId) +
                " coveredResources=" + (ForagingConfig.ExperimentalCoveredResources != null && ForagingConfig.ExperimentalCoveredResources.Value ? "on" : "off"));
            lines.Add("Custom items evidence-gated: WildBloom=" +
                DescribeItemOutcome(CraftingExpandedItemIds.WildBloomId, CraftingExpandedItems.Outcome(CraftingExpandedItemIds.WildBloomId)) +
                " CaveMoss=" +
                DescribeItemOutcome(CraftingExpandedItemIds.CaveMossId, CraftingExpandedItems.Outcome(CraftingExpandedItemIds.CaveMossId)) +
                " Blightroot=" +
                DescribeItemOutcome(CraftingExpandedItemIds.BlightrootId, CraftingExpandedItems.Outcome(CraftingExpandedItemIds.BlightrootId)));

            lines.Add("Crafting progression: Lv" + CraftingController.Progress.Level +
                " " + CraftingController.Progress.Xp + "/" + SmithingXpCurve.XpToNextLevel(CraftingController.Progress.Level) +
                " nativeActivity=Smithing scope=" + (CraftingController.CharacterScopeResolved ? "per-character" : "unresolved") +
                (string.IsNullOrEmpty(CraftingProgressionStore.LastRecovery) ? string.Empty : " recoveredFrom=" + CraftingProgressionStore.LastRecovery) +
                (string.IsNullOrEmpty(CraftingProgressionStore.LastError) ? string.Empty : " persistenceError={" + CraftingProgressionStore.LastError + "}"));

            CraftingCommission commission = CommissionController.Current;
            lines.Add("Commissions: enabled=" + (CraftingConfig.EnableCraftingRequests != null && CraftingConfig.EnableCraftingRequests.Value ? "yes" : "no") +
                " state=" + (commission != null ? (commission.SimName + " -> " + commission.RequestedItemName + " [" + commission.State + "]") : "(none)") +
                " coop=" + (CoopCompatibility.IsCoopSession() ? "yes" : "no"));

            string forageScene = ForageNodeController.SafeSceneName();
            lines.Add("Foraging: enabled=" + (ForagingConfig.EnableForaging.Value ? "yes" : "no") +
                " autoPlacement=" + (ForageNodeController.IsAutoPlacementEnabledForScene(forageScene) ? "yes" : "no") +
                " scene=" + forageScene +
                " spawned=" + ForageNodeController.SpawnedCount +
                " available=" + ForageNodeController.AvailableCount() +
                " depleted=" + ForageNodeController.DepletedCount() +
                " cooldowns=" + ForageDepletionLedger.Count +
                " ambiguousQuarantine=" + ForageAmbiguousGrantQuarantine.Count);
            lines.Add("Foraging progression: Lv" + ForagingKnowledge.CurrentLevel +
                " " + ForagingKnowledge.CurrentXp + "/" + ForagingKnowledge.XpToNext +
                " known=" + ForagingProgressionController.DiscoveredCount + "/" + ForageResourceCatalog.All().Count +
                " characterState=" + ForagingProgressionController.PersistenceState +
                (string.IsNullOrEmpty(ForagingProgressionStore.LastRecovery) ? string.Empty : " recoveredFrom=" + ForagingProgressionStore.LastRecovery) +
                (string.IsNullOrEmpty(ForagingProgressionStore.LastError) ? string.Empty : " persistenceError={" + ForagingProgressionStore.LastError + "}"));

            lines.Add("Primary node: " + ForageNodeController.DescribePrimaryNode());
            lines.Add("Foraging interaction: " + ForageNodeController.LastTargetSummary +
                " | " + ForageNodeController.LastEligibilitySummary +
                " | " + ForageNodeController.LastTargetInvariantSummary);
            lines.Add("Foraging gather: " + ForageNodeController.DescribeActiveGather());
            lines.Add("Foraging transaction: " + ForageNodeController.LastGatherTransactionSummary +
                " | lastItem=" + (string.IsNullOrEmpty(ForageNodeController.LastGatherSummary) ? "(none)" : ForageNodeController.LastGatherSummary) +
                " | lastFailure=" + (string.IsNullOrEmpty(ForageNodeController.LastFailureReason) ? "(none)" : ForageNodeController.LastFailureReason));
            lines.Add("Foraging presentation: " + ForageNodeController.LastNameplateSummary + " live={" + ForageNodeWorldLabel.DescribePresentation() + "}");
            lines.Add("Foraging facing: " + ForageNodeWorldLabel.LastFacingDiagnostic);

            lines.Add("Native crafting evidence: " + NativeCraftingRuntimeProbe.Describe());
            lines.Add("Native recipe examples: " + NativeCraftingRuntimeProbe.DescribeExamples());
            lines.Add("Native recipe outputs: " + NativeCraftingRuntimeProbe.DescribeOutputs());
            lines.Add("Expanded content: enabled=" + (CraftingConfig.EnableExpandedContent != null && CraftingConfig.EnableExpandedContent.Value ? "yes" : "no") +
                " items=" + ExpandedContentItemRegistry.AvailableCount + "/" + ExpandedContentItems.All.Count +
                " itemFailures=" + ExpandedContentItemRegistry.FailureCount +
                " icons=" + ItemIconAssetLoader.LoadedCount + "/25" +
                " recipeIdentities=" + ExpandedContentRecipeRegistry.IdentityCount + "/" + ExpandedContentRecipes.All.Count +
                " activeRecipes=" + ExpandedContentRecipeRegistry.ActiveCount + "/" + ExpandedContentRecipes.All.Count +
                (string.IsNullOrEmpty(ExpandedContentRecipeRegistry.LastFailure) ? string.Empty : " recipeStatus={" + ExpandedContentRecipeRegistry.LastFailure + "}"));
            string expandedFailures = ExpandedContentItemRegistry.DescribeFailures(3);
            if (!string.IsNullOrEmpty(expandedFailures)) lines.Add("Expanded item failures: " + expandedFailures);
            lines.Add("Inventory semantics: " + GameItemRegistryApi.DescribeOwnedInventorySemanticsSummary() + " | detail=/craftdiag items");
            lines.Add("Legacy native recipe experiment: catalog=" + ProductionNativeRecipeRegistry.Describe() +
                " gate=" + (CraftingConfig.EnableProductionNativeRecipes != null && CraftingConfig.EnableProductionNativeRecipes.Value ? "ON" : "OFF") +
                " verification={" + ExperimentalNativeRecipeRegistry.Describe() + "}");

            RecipeBookSnapshot recipeBook = RecipeOwnershipController.BuildBookSnapshot(CraftingController.Progress.Level);
            lines.Add("Recipe ownership: registered=" + RecipeOwnershipController.RegisteredRecipeCount +
                " known=" + recipeBook.KnownCount + "/" + recipeBook.TotalCount +
                " persistence={" + RecipeOwnershipController.DescribePersistenceStatus() + "}" +
                " bank={" + RecipeOwnershipController.DescribeBankStatus() + "}" +
                " absenceAuthority={" + RecipeOwnershipController.DescribeAbsenceAuthorityStatus() + "}" +
                (string.IsNullOrEmpty(RecipeOwnershipController.LastError) ? string.Empty : " error={" + RecipeOwnershipController.LastError + "}"));
            lines.Add("Recipe templates: safety=PlayerCannotSell+NoTradeNoDestroy+value0 unique=not-relied-on recovery=inventory/forge+optional authoritative storage probes");
            lines.Add("Recipe dev: /craftdiag recipe status | candidates | trial register | trial grant (production catalog binds automatically and fail-closed)");
            lines.Add("Forage survey: /craftdiag forage pos | /craftdiag forage scan [filter] (see docs/FORAGING_ASSET_SURVEY.md)");

            lines.Add("UI: state=" + CraftingUiStateMachine.Current +
                " panelNormalized=(" + CraftingConfig.PanelX.Value.ToString("F3") + "," + CraftingConfig.PanelY.Value.ToString("F3") + ")" +
                " launcherNormalized=(" + CraftingConfig.LauncherX.Value.ToString("F3") + "," + CraftingConfig.LauncherY.Value.ToString("F3") + ")" +
                " persist=" + (CraftingConfig.PersistWindowPosition.Value ? "on" : "off"));

            try { foreach (string line in lines) UpdateSocialLog.LogAdd(line, "yellow"); } catch { }
        }

        internal static void ReportWorldTier()
        {
            try
            {
                WorldThreatSnapshot threat = WorldThreatRuntime.Current;
                if (threat == null)
                {
                    UpdateSocialLog.LogAdd("[Crafting] World tier: scene=" + SafeSceneName() + " state=stabilizing scanned=" + WorldThreatRuntime.LastScanned + " rejected=" + WorldThreatRuntime.LastRejected, "yellow");
                    return;
                }
                UpdateSocialLog.LogAdd("[Crafting] World tier: " + threat.Summary() + " scanned=" + WorldThreatRuntime.LastScanned + " rejected=" + WorldThreatRuntime.LastRejected, "yellow");
                List<string> eligible = new List<string>(); List<string> blocked = new List<string>();
                List<ForageResourceDefinition> resources = ForageResourceCatalog.All();
                for (int i = 0; i < resources.Count; i++)
                {
                    ForageResourceDefinition r = resources[i];
                    (WorldThreatPolicy.Allows(threat.Band, r.WorldBand) ? eligible : blocked).Add(r.DisplayName);
                }
                UpdateSocialLog.LogAdd("[Crafting] World tier eligible: " + string.Join(", ", eligible.ToArray()), "yellow");
                UpdateSocialLog.LogAdd("[Crafting] World tier blocked: " + (blocked.Count == 0 ? "none" : string.Join(", ", blocked.ToArray())), "yellow");
            }
            catch { }
        }

        internal static void ReportResources()
        {
            try
            {
                List<ForageResourceDefinition> resources = ForageResourceCatalog.All();
                UpdateSocialLog.LogAdd("[Crafting] Resources: " + resources.Count + " identities; world tier controls existence, Foraging controls harvest.", "yellow");
                for (int i = 0; i < resources.Count; i++)
                {
                    ForageResourceDefinition r = resources[i];
                    UpdateSocialLog.LogAdd("[Crafting] " + r.DisplayName + " | world=" + WorldThreatPolicy.DisplayName(r.WorldBand) + " | Foraging=" + r.MinimumSkill +
                        " | pool=" + r.Pool + " | density=" + r.DensityWeight + "/cap" + r.MaxAutoNodesPerScene + " | item=" +
                        (GameItemRegistryApi.IsCustomItemAvailable(r.RewardItemId) ? "ready" : "unavailable") + " | purpose=" + r.FutureCraftingPurpose, "yellow");
                }
            }
            catch { }
        }

        internal static void ReportConsumables()
        {
            try
            {
                List<string> lines = GameItemRegistryApi.BuildConsumableReferenceLines(8);
                for (int i = 0; i < lines.Count; i++) UpdateSocialLog.LogAdd("[Crafting] " + lines[i], "yellow");
            }
            catch { }
        }

        internal static void ReportNativeConsumables()
        {
            try
            {
                List<string> lines = GameItemRegistryApi.BuildNativeConsumableReferenceLines(16);
                for (int i = 0; i < lines.Count; i++) UpdateSocialLog.LogAdd("[Crafting] " + lines[i], "yellow");
            }
            catch { }
        }

        internal static void ReportRecipes()
        {
            try
            {
                IList<CustomRecipeDefinition> recipes = ExpandedContentRecipes.All;
                UpdateSocialLog.LogAdd("[Crafting] Production recipes: " + recipes.Count + " physical native Smithing templates.", "yellow");
                for (int i = 0; i < recipes.Count; i++)
                    UpdateSocialLog.LogAdd("[Crafting] " + recipes[i].DisplayName + " | " + CraftingKnowledgePresentationPolicy.BuildRecipeRequirementSummary(recipes[i]), "yellow");
            }
            catch { }
        }

        internal static void ReportGiveHerb()
        {
            bool granted = GameItemRegistryApi.GrantRegisteredItem(CraftingExpandedItemIds.WildHerbId, 1);
            string message = granted
                ? "[Erenshor Crafting Expanded] Granted 1x Wild Herb (dev/test)."
                : "[Erenshor Crafting Expanded] Could not grant Wild Herb - state=" + CraftingExpandedItems.WildHerbState() +
                  " error=" + CraftingExpandedItems.LastFailureReason();
            try { UpdateSocialLog.LogAdd(message, "yellow"); } catch { }
        }

        internal static void ReportGiveCaveMushroom()
        {
            bool granted = GameItemRegistryApi.GrantRegisteredItem(CraftingExpandedItemIds.CaveMushroomId, 1);
            string message = granted
                ? "[Erenshor Crafting Expanded] Granted 1x Cave Mushroom (dev/test)."
                : "[Erenshor Crafting Expanded] Could not grant Cave Mushroom - state=" + CraftingExpandedItems.State(CraftingExpandedItemIds.CaveMushroomId) +
                  " error=" + CraftingExpandedItems.FailureReason(CraftingExpandedItemIds.CaveMushroomId);
            try { UpdateSocialLog.LogAdd(message, "yellow"); } catch { }
        }

        internal static void ReportItemSemantics()
        {
            try
            {
                UpdateSocialLog.LogAdd("[Crafting] Inventory semantics: " + GameItemRegistryApi.DescribeOwnedInventorySemanticsSummary(), "yellow");
                List<string> itemLines = GameItemRegistryApi.BuildOwnedInventorySemanticsLines(25);
                for (int i = 0; i < itemLines.Count; i++) UpdateSocialLog.LogAdd("[Crafting] " + itemLines[i], "yellow");
                List<string> nativeLines = GameItemRegistryApi.BuildNativeInventorySemanticsReferenceLines();
                for (int i = 0; i < nativeLines.Count; i++) UpdateSocialLog.LogAdd("[Crafting] " + nativeLines[i], "yellow");
            }
            catch { }
        }

        internal static void ReportRecipeExperimentStatus()
        {
            try
            {
                UpdateSocialLog.LogAdd("[Crafting] Production native catalog: gate=" + (CraftingConfig.EnableProductionNativeRecipes != null && CraftingConfig.EnableProductionNativeRecipes.Value ? "ON" : "OFF") + " " + ProductionNativeRecipeRegistry.Describe(), "yellow");
                UpdateSocialLog.LogAdd("[Crafting] Native recipe experiment: " + ExperimentalNativeRecipeRegistry.Describe(), "yellow");
            }
            catch { }
        }

        internal static void ReportRecipeCandidates()
        {
            try
            {
                UpdateSocialLog.LogAdd("[Crafting] Production-safe native candidates: " + ProductionNativeRecipeRegistry.DescribeCandidates(), "yellow");
                UpdateSocialLog.LogAdd("[Crafting] Experimental donor candidates: " + ExperimentalNativeRecipeRegistry.DescribeCandidates(), "yellow");
            }
            catch { }
        }

        internal static void ReportRecipeTrialRegister()
        {
            bool registered = ExperimentalNativeRecipeRegistry.TryRegisterFromLiveDatabase();
            string text = registered ? "registration succeeded" : "registration did not succeed";
            try { UpdateSocialLog.LogAdd("[Crafting] Experimental recipe " + text + ": " + ExperimentalNativeRecipeRegistry.Describe(), "yellow"); } catch { }
        }

        internal static void ReportRecipeTrialGrant()
        {
            bool granted = ExperimentalNativeRecipeRegistry.GrantVerificationTemplate();
            string text = granted
                ? "Granted Recipe: Herbal Preparation (Verification). Load it into the native Smithing Template slot; native Smithing owns the craft."
                : "Verification template not granted: " + ExperimentalNativeRecipeRegistry.Describe();
            try { UpdateSocialLog.LogAdd("[Crafting] " + text, "yellow"); } catch { }
        }

        private static int CountInventoryItem(string itemId)
        {
            try
            {
                int total = 0;
                foreach (InventoryAvailability slot in GameCraftingApi.ReadInventoryAvailability())
                    if (slot.ItemId == itemId) total += slot.Quantity;
                return total;
            }
            catch { return 0; }
        }

        private static string DescribeItemOutcome(string id, CustomItemRegistrationOutcome outcome)
        {
            if (outcome == null) return id + ":Uninitialized";
            string text = id + ":" + outcome.State;
            if (!string.IsNullOrEmpty(outcome.BaseItemName)) text += " base=" + outcome.BaseItemName + "#" + outcome.BaseItemId;
            if (!string.IsNullOrEmpty(outcome.BaseSelectionReason)) text += " visual={" + outcome.BaseSelectionReason + "}";
            if (!string.IsNullOrEmpty(outcome.ConflictingExistingItemName)) text += " collision=" + outcome.ConflictingExistingItemName;
            if (!string.IsNullOrEmpty(outcome.FailureReason)) text += " error={" + outcome.FailureReason + "}";
            return text;
        }

        private static string SafeSceneName()
        {
            try { return GameData.SceneName ?? "(unknown)"; } catch { return "(unknown)"; }
        }
    }
}