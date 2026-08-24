namespace ErenshorCraftingExpanded
{
    // Final auto-placement admission. World threat controls existence; player Foraging skill is
    // intentionally absent because it controls harvest eligibility later, not whether the node can exist.
    public static class ForageResourceAvailabilityPolicy
    {
        public static bool CanAutoSpawn(ForageResourceDefinition resource, ForageEnvironmentKind environment, string scene,
            bool coveredResourcesEnabled, ForageWorldBand worldBand, bool itemAvailable, bool visualAvailable, out string reason)
        {
            reason = string.Empty;
            if (resource == null) { reason = "resource-missing"; return false; }
            if (!ForageResourceCatalog.IsRuntimeEnabled(resource, coveredResourcesEnabled)) { reason = "resource-disabled"; return false; }
            if (!ForageEnvironmentPolicy.IsPoolCompatible(environment, resource.Pool)) { reason = "wrong-environment"; return false; }
            if (!ForageRegionalPolicy.IsEligible(resource, scene)) { reason = "wrong-region"; return false; }
            if (!WorldThreatPolicy.Allows(worldBand, resource.WorldBand)) { reason = "world-tier-too-low"; return false; }
            if (!itemAvailable) { reason = "item-donor-unavailable"; return false; }
            if (!visualAvailable) { reason = "scene-visual-unavailable"; return false; }
            return true;
        }

        internal static string RunSelfTests()
        {
            ForageResourceDefinition herb = ForageResourceCatalog.FindByKnowledgeKey("wild_herb");
            ForageResourceDefinition fungus = ForageResourceCatalog.FindByKnowledgeKey("cave_mushroom");
            ForageResourceDefinition starleaf = ForageResourceCatalog.FindByKnowledgeKey("starleaf");
            ForageResourceDefinition root = ForageResourceCatalog.FindByKnowledgeKey("blightroot");
            if (herb == null || fungus == null || starleaf == null || root == null) return "FAIL availability test catalog";
            string reason;
            if (!CanAutoSpawn(herb, ForageEnvironmentKind.Open, "Hidden Hills", false, ForageWorldBand.Starter, true, true, out reason)) return "FAIL starter herb: " + reason;
            if (!CanAutoSpawn(herb, ForageEnvironmentKind.Open, "Hidden Hills", false, ForageWorldBand.Unknown, true, true, out reason)) return "FAIL unknown-world starter baseline: " + reason;
            if (CanAutoSpawn(starleaf, ForageEnvironmentKind.Open, "High Zone", false, ForageWorldBand.Unknown, true, true, out reason) || reason != "world-tier-too-low") return "FAIL unknown-world progression exclusion";
            if (CanAutoSpawn(starleaf, ForageEnvironmentKind.Open, "Faerie's Brake", false, ForageWorldBand.Starter, true, true, out reason) || reason != "world-tier-too-low") return "FAIL Brake-tier Starleaf exclusion";
            if (!CanAutoSpawn(starleaf, ForageEnvironmentKind.Open, "High Zone", false, ForageWorldBand.High, true, true, out reason)) return "FAIL high-world Starleaf visibility: " + reason;
            if (CanAutoSpawn(starleaf, ForageEnvironmentKind.Open, "High Zone", false, ForageWorldBand.High, false, true, out reason) || reason != "item-donor-unavailable") return "FAIL donor gate";
            if (CanAutoSpawn(root, ForageEnvironmentKind.Open, "Hidden Hills", false, ForageWorldBand.High, true, true, out reason) || reason != "wrong-region") return "FAIL region gate";
            if (!CanAutoSpawn(root, ForageEnvironmentKind.Open, "The Blight", false, ForageWorldBand.MidHigh, true, true, out reason)) return "FAIL Blightroot in eligible world";
            if (CanAutoSpawn(fungus, ForageEnvironmentKind.Covered, "Cave", false, ForageWorldBand.High, true, true, out reason) || reason != "resource-disabled") return "FAIL covered gate";
            return "PASS forage resource availability policy";
        }
    }
}
