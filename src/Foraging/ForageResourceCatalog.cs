using System;
using System.Collections.Generic;

namespace ErenshorCraftingExpanded
{
    public enum ForageResourceKind
    {
        WildHerb = 0, CaveMushroom = 1, WildBloom = 2, CaveMoss = 3, Blightroot = 4,
        FieldFiber = 5, ResinSprig = 6, Starleaf = 7, Ghostcap = 8,
        MeadowReed = 9, Amberbark = 10, Sunpetal = 11, IronvineRoot = 12,
        Duskleaf = 13, ElderResin = 14, Voidcap = 15
    }

    public enum ForageResourceRarity { Common = 0, Uncommon = 1, Rare = 2 }
    public enum ForageDiscoveryRule { FirstSuccessfulGather = 0 }

    public sealed class ForageResourceDefinition
    {
        public ForageResourceKind Kind;
        public ForageResourcePool Pool;
        public string KnowledgeKey;
        public string NodeIdPrefix;
        public string DisplayName;
        public string RewardItemId;
        public float RespawnSeconds;
        public int MinimumSkill;
        public int GatherXp;
        public int BaseYield;
        public ForageResourceRarity Rarity;
        public ForageWorldBand WorldBand;
        public bool Experimental;
        public bool EnabledByDefault;
        public ForageRegionalRule RegionalRule;
        public string[] EligibleScenes;
        public int DensityWeight;
        public int MaxAutoNodesPerScene;
        public int VisualClumpCount;
        public float VisualScaleMultiplier;
        public float VisualSpreadMultiplier;
        public string VisualEvidenceRequirement;
        public string ItemDonorEvidenceRequirement;
        public ForageDiscoveryRule DiscoveryRule;
        public string FutureCraftingPurpose;
    }

    // World existence and character harvesting are deliberately separate. ForEnvironmentAll only
    // describes ecology/region candidates; world threat is applied later by availability policy.
    public static class ForageResourceCatalog
    {
        private static readonly List<ForageResourceDefinition> Catalog = Build();

        private static List<ForageResourceDefinition> Build()
        {
            List<ForageResourceDefinition> r = new List<ForageResourceDefinition>();
            r.Add(R(ForageResourceKind.WildHerb, ForageResourcePool.OpenHerbs, "wild_herb", "AutoHerb_", "Wild Herb", CraftingExpandedItemIds.WildHerbId, 300f, 1, 20, ForageResourceRarity.Common, ForageWorldBand.Starter, false, 10, 3, 1.00f, 1.00f,
                "safe plant/herb/fern/bush scene mesh; TFF_Bush_01A is live-proven", "safe organic ItemDB donor; Fernallan Willow Seed is live-proven", "baseline herbal preparations and simple restorative recipes"));
            r.Add(R(ForageResourceKind.FieldFiber, ForageResourcePool.OpenFibers, "field_fiber", "AutoFiber_", "Field Fiber", CraftingExpandedItemIds.FieldFiberId, 300f, 3, 24, ForageResourceRarity.Common, ForageWorldBand.Starter, false, 8, 2, 0.95f, 1.10f,
                "explicit grass/reed/fiber/stalk current-scene vegetation mesh", "conservative General native donor provides inventory mechanics", "cordage, bindings, bows, shields, and workshop components"));
            r.Add(R(ForageResourceKind.ResinSprig, ForageResourcePool.OpenWood, "resin_sprig", "AutoResin_", "Resin Sprig", CraftingExpandedItemIds.ResinSprigId, 330f, 6, 28, ForageResourceRarity.Common, ForageWorldBand.Starter, false, 7, 2, 0.90f, 0.95f,
                "explicit shrub/twig/branch/sapling current-scene mesh small enough for a gather node", "conservative General native donor provides inventory mechanics", "resin grips, hardened bindings, weapons, and shields"));
            r.Add(R(ForageResourceKind.CaveMushroom, ForageResourcePool.CoveredFungi, "cave_mushroom", "AutoFungus_", "Cave Mushroom", CraftingExpandedItemIds.CaveMushroomId, 420f, 8, 32, ForageResourceRarity.Uncommon, ForageWorldBand.Starter, true, 6, 2, 0.85f, 0.80f,
                "explicit mushroom/toadstool/fungus/fungi/spore scene mesh", "safe ItemDB donor with explicit mushroom/fungus name evidence", "cave tonics, alchemical reagents, and dungeon-focused consumables"));
            r.Add(R(ForageResourceKind.MeadowReed, ForageResourcePool.OpenFibers, "meadow_reed", "AutoReed_", "Meadow Reed", CraftingExpandedItemIds.MeadowReedId, 330f, 9, 34, ForageResourceRarity.Common, ForageWorldBand.LowMid, false, 6, 2, 0.92f, 1.05f,
                "same explicit grass/reed/fiber/stalk visual family already admitted for Field Fiber", "conservative General native donor; donor icon retained", "travel cordage and early crafted tonics"));
            r.Add(R(ForageResourceKind.Amberbark, ForageResourcePool.OpenWood, "amberbark", "AutoAmberbark_", "Amberbark", CraftingExpandedItemIds.AmberbarkId, 390f, 12, 36, ForageResourceRarity.Uncommon, ForageWorldBand.LowMid, false, 5, 1, 0.92f, 0.90f,
                "same verified shrub/twig/branch/sapling family as Resin Sprig", "conservative General native donor; donor icon retained", "travel preparations and mid-tier wood/resin recipes"));
            r.Add(R(ForageResourceKind.WildBloom, ForageResourcePool.OpenFlowers, "wild_bloom", "AutoBloom_", "Wild Bloom", CraftingExpandedItemIds.WildBloomId, 360f, 14, 38, ForageResourceRarity.Uncommon, ForageWorldBand.LowMid, false, 4, 1, 0.95f, 0.90f,
                "explicit flower/blossom/bloom/petal current-scene mesh", "safe ItemDB donor with explicit flower/blossom/bloom/petal name evidence", "pigments, restorative preparations, and light utility elixirs"));
            r.Add(R(ForageResourceKind.Sunpetal, ForageResourcePool.OpenFlowers, "sunpetal", "AutoSunpetal_", "Sunpetal", CraftingExpandedItemIds.SunpetalId, 450f, 16, 42, ForageResourceRarity.Uncommon, ForageWorldBand.LowMid, false, 3, 1, 0.90f, 0.85f,
                "same explicit flower/blossom/bloom/petal visual family already proven for Wild Bloom", "conservative General native donor; donor icon retained", "mid-tier recovery preparations and Starleaf refinement"));
            r.Add(R(ForageResourceKind.Starleaf, ForageResourcePool.OpenHerbs, "starleaf", "AutoStarleaf_", "Starleaf", CraftingExpandedItemIds.StarleafId, 600f, 18, 46, ForageResourceRarity.Rare, ForageWorldBand.Mid, false, 2, 1, 0.82f, 0.80f,
                "safe open herb/plant mesh; rare selection shares proven herb visual family", "conservative General native donor provides inventory mechanics", "mid/high-tier infusions, bows, and refined tonics"));
            r.Add(R(ForageResourceKind.IronvineRoot, ForageResourcePool.OpenRoots, "ironvine_root", "AutoIronvine_", "Ironvine Root", CraftingExpandedItemIds.IronvineRootId, 540f, 22, 50, ForageResourceRarity.Uncommon, ForageWorldBand.Mid, false, 4, 1, 0.95f, 0.90f,
                "explicit root/rhizome/briar/bramble/vine/thorn current-scene visual family", "conservative General native donor; donor icon retained", "mid/high root temper and durable workshop components"));
            r.Add(R(ForageResourceKind.CaveMoss, ForageResourcePool.CoveredMoss, "cave_moss", "AutoMoss_", "Cave Moss", CraftingExpandedItemIds.CaveMossId, 540f, 24, 52, ForageResourceRarity.Rare, ForageWorldBand.Mid, true, 3, 1, 0.75f, 0.85f,
                "explicit moss/lichen current-scene mesh", "safe ItemDB donor with explicit moss/lichen name evidence", "binding agents and higher-tier dungeon remedies"));
            r.Add(R(ForageResourceKind.Ghostcap, ForageResourcePool.CoveredFungi, "ghostcap", "AutoGhostcap_", "Ghostcap", CraftingExpandedItemIds.GhostcapId, 720f, 30, 62, ForageResourceRarity.Rare, ForageWorldBand.MidHigh, true, 2, 1, 0.72f, 0.75f,
                "explicit covered fungus/mushroom current-scene mesh", "conservative General native donor provides inventory mechanics", "specialist cave extracts and rare dagger finishing"));
            ForageResourceDefinition blight = R(ForageResourceKind.Blightroot, ForageResourcePool.OpenRoots, "blightroot", "AutoBlightroot_", "Blightroot", CraftingExpandedItemIds.BlightrootId, 660f, 36, 68, ForageResourceRarity.Rare, ForageWorldBand.MidHigh, false, 2, 1, 1.00f, 0.95f,
                "explicit root/rhizome/briar/bramble/vine/thorn current-scene mesh in The Blight", "safe ItemDB donor with explicit root/rhizome/briar/bramble/vine/thorn name evidence", "late regional reagents and root temper");
            blight.RegionalRule = ForageRegionalRule.ExplicitScenes; blight.EligibleScenes = new string[] { "The Blight" }; r.Add(blight);
            r.Add(R(ForageResourceKind.Duskleaf, ForageResourcePool.OpenHerbs, "duskleaf", "AutoDuskleaf_", "Duskleaf", CraftingExpandedItemIds.DuskleafId, 780f, 42, 76, ForageResourceRarity.Rare, ForageWorldBand.High, false, 2, 1, 0.78f, 0.78f,
                "same proven open herb/plant visual family; high world threat is an additional mandatory gate", "conservative General native donor; donor icon retained", "late provisions and high-tier herbal work"));
            r.Add(R(ForageResourceKind.ElderResin, ForageResourcePool.OpenWood, "elder_resin", "AutoElderResin_", "Elder Resin", CraftingExpandedItemIds.ElderResinId, 840f, 46, 82, ForageResourceRarity.Rare, ForageWorldBand.High, false, 2, 1, 0.86f, 0.82f,
                "same proven shrub/twig/branch/sapling family; high world threat is mandatory", "conservative General native donor; donor icon retained", "late provisions and hardened high-tier compounds"));
            r.Add(R(ForageResourceKind.Voidcap, ForageResourcePool.CoveredFungi, "voidcap", "AutoVoidcap_", "Voidcap", CraftingExpandedItemIds.VoidcapId, 900f, 49, 90, ForageResourceRarity.Rare, ForageWorldBand.High, true, 1, 1, 0.68f, 0.70f,
                "same explicit covered fungus/mushroom family; high world threat is mandatory", "conservative General native donor; donor icon retained", "late specialist provisions where a proven native utility consumable donor exists"));
            return r;
        }

        private static ForageResourceDefinition R(ForageResourceKind kind, ForageResourcePool pool, string key, string prefix, string name, string reward,
            float respawn, int skill, int xp, ForageResourceRarity rarity, ForageWorldBand band, bool experimental, int density, int cap,
            float visualScale, float visualSpread, string visualEvidence, string donorEvidence, string purpose)
        {
            ForageResourceDefinition d = new ForageResourceDefinition();
            d.Kind = kind; d.Pool = pool; d.KnowledgeKey = key; d.NodeIdPrefix = prefix; d.DisplayName = name; d.RewardItemId = reward;
            d.RespawnSeconds = respawn; d.MinimumSkill = skill; d.GatherXp = xp; d.BaseYield = 1; d.Rarity = rarity; d.WorldBand = band;
            d.Experimental = experimental; d.EnabledByDefault = !experimental; d.RegionalRule = ForageRegionalRule.AnyScene; d.EligibleScenes = new string[0];
            d.DensityWeight = density; d.MaxAutoNodesPerScene = cap; d.VisualClumpCount = 3; d.VisualScaleMultiplier = visualScale; d.VisualSpreadMultiplier = visualSpread;
            d.VisualEvidenceRequirement = visualEvidence; d.ItemDonorEvidenceRequirement = donorEvidence; d.DiscoveryRule = ForageDiscoveryRule.FirstSuccessfulGather; d.FutureCraftingPurpose = purpose;
            return d;
        }

        public static ForageResourceDefinition ForEnvironment(ForageEnvironmentKind environment, bool coveredResourcesEnabled)
        {
            if (environment == ForageEnvironmentKind.Covered) return coveredResourcesEnabled ? FindByKnowledgeKey("cave_mushroom") : null;
            return FindByKnowledgeKey("wild_herb");
        }

        public static List<ForageResourceDefinition> ForEnvironmentAll(ForageEnvironmentKind environment, string scene, bool coveredResourcesEnabled)
        {
            List<ForageResourceDefinition> result = new List<ForageResourceDefinition>();
            for (int i = 0; i < Catalog.Count; i++)
            {
                ForageResourceDefinition resource = Catalog[i];
                if (!IsRuntimeEnabled(resource, coveredResourcesEnabled)) continue;
                if (!ForageEnvironmentPolicy.IsPoolCompatible(environment, resource.Pool)) continue;
                if (!ForageRegionalPolicy.IsEligible(resource, scene)) continue;
                result.Add(resource);
            }
            return result;
        }

        public static ForageResourceDefinition ForPool(ForageResourcePool pool, bool coveredResourcesEnabled)
        {
            for (int i = 0; i < Catalog.Count; i++) if (Catalog[i].Pool == pool && IsRuntimeEnabled(Catalog[i], coveredResourcesEnabled)) return Catalog[i];
            return null;
        }

        public static bool IsRuntimeEnabled(ForageResourceDefinition resource, bool coveredResourcesEnabled)
        {
            if (resource == null) return false;
            if (resource.Experimental) return coveredResourcesEnabled;
            return resource.EnabledByDefault;
        }

        public static ForageResourceDefinition FindByRewardItemId(string itemId)
        {
            for (int i = 0; i < Catalog.Count; i++) if (string.Equals(itemId, Catalog[i].RewardItemId, StringComparison.Ordinal)) return Catalog[i];
            return null;
        }

        public static ForageResourceDefinition FindByKnowledgeKey(string resourceKey)
        {
            string key = ForagingKnowledgeState.NormalizeKey(resourceKey);
            for (int i = 0; i < Catalog.Count; i++) if (string.Equals(key, Catalog[i].KnowledgeKey, StringComparison.Ordinal)) return Catalog[i];
            return null;
        }

        public static List<ForageResourceDefinition> All() { return new List<ForageResourceDefinition>(Catalog); }

        public static bool IsGatherableAtSkill(ForageResourceDefinition resource, int foragingLevel)
        {
            if (resource == null) return false;
            if (foragingLevel < 1) foragingLevel = 1;
            return foragingLevel >= resource.MinimumSkill;
        }

        public static bool ValidateDefinition(ForageResourceDefinition resource, out string reason)
        {
            reason = string.Empty;
            if (resource == null) { reason = "resource=null"; return false; }
            if (string.IsNullOrWhiteSpace(resource.KnowledgeKey) || string.IsNullOrWhiteSpace(resource.NodeIdPrefix) || string.IsNullOrWhiteSpace(resource.DisplayName)) { reason = "identity missing"; return false; }
            if (string.IsNullOrWhiteSpace(resource.RewardItemId) || !CraftingExpandedItemIds.IsInOwnedRange(resource.RewardItemId)) { reason = "reward item id outside owned range"; return false; }
            if (resource.WorldBand == ForageWorldBand.Unknown) { reason = "world band missing"; return false; }
            if (resource.MinimumSkill < 1 || resource.MinimumSkill > 50) { reason = "minimum skill outside 1-50"; return false; }
            if (resource.GatherXp <= 0 || resource.GatherXp > 500 || resource.BaseYield != 1) { reason = "gather economy invalid"; return false; }
            if (float.IsNaN(resource.RespawnSeconds) || float.IsInfinity(resource.RespawnSeconds) || resource.RespawnSeconds < 30f || resource.RespawnSeconds > 7200f) { reason = "respawn invalid"; return false; }
            if (resource.DensityWeight < 1 || resource.DensityWeight > 100) { reason = "density weight invalid"; return false; }
            if (resource.MaxAutoNodesPerScene < 1 || resource.MaxAutoNodesPerScene > ForagePlacementPolicy.DesiredClusterCount) { reason = "auto-node cap invalid"; return false; }
            if (resource.VisualClumpCount < 2 || resource.VisualClumpCount > ForagePresentationPolicy.PreferredClusterClumpCount) { reason = "visual clump count invalid"; return false; }
            if (resource.VisualScaleMultiplier < 0.5f || resource.VisualScaleMultiplier > 1.5f || resource.VisualSpreadMultiplier < 0.5f || resource.VisualSpreadMultiplier > 1.5f) { reason = "visual transform invalid"; return false; }
            if (resource.RegionalRule == ForageRegionalRule.ExplicitScenes && (resource.EligibleScenes == null || resource.EligibleScenes.Length == 0)) { reason = "explicit regional rule has no scenes"; return false; }
            if (string.IsNullOrWhiteSpace(resource.VisualEvidenceRequirement) || string.IsNullOrWhiteSpace(resource.ItemDonorEvidenceRequirement) || string.IsNullOrWhiteSpace(resource.FutureCraftingPurpose)) { reason = "evidence/purpose missing"; return false; }
            if (resource.DiscoveryRule != ForageDiscoveryRule.FirstSuccessfulGather) { reason = "unsupported discovery rule"; return false; }
            return true;
        }

        internal static string RunSelfTests()
        {
            List<ForageResourceDefinition> all = All();
            if (all.Count != 16) return "FAIL expected sixteen-resource gameplay catalog";
            HashSet<string> keys = new HashSet<string>(StringComparer.Ordinal); HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
            int[] decades = new int[5];
            for (int i = 0; i < all.Count; i++)
            {
                string reason; if (!ValidateDefinition(all[i], out reason)) return "FAIL catalog definition " + all[i].DisplayName + ": " + reason;
                if (!keys.Add(all[i].KnowledgeKey) || !ids.Add(all[i].RewardItemId)) return "FAIL duplicate resource identity";
                int decade = Math.Min(4, (all[i].MinimumSkill - 1) / 10); decades[decade]++;
            }
            for (int i = 0; i < decades.Length; i++) if (decades[i] == 0) return "FAIL progression decade coverage " + i;
            ForageResourceDefinition starleaf = FindByKnowledgeKey("starleaf");
            if (starleaf == null || starleaf.MinimumSkill != 18 || starleaf.WorldBand != ForageWorldBand.Mid) return "FAIL Starleaf world/skill separation";
            if (!WorldThreatPolicy.Allows(ForageWorldBand.High, starleaf.WorldBand) || WorldThreatPolicy.Allows(ForageWorldBand.Starter, starleaf.WorldBand)) return "FAIL Starleaf world-band eligibility";
            // Player skill is a separate gate: high-world existence can be true while harvesting is false.
            if (!WorldThreatPolicy.Allows(ForageWorldBand.High, starleaf.WorldBand) || IsGatherableAtSkill(starleaf, 1)) return "FAIL world visibility before player skill";
            ForageResourceDefinition blight = FindByKnowledgeKey("blightroot");
            if (blight == null || !ForageRegionalPolicy.IsEligible(blight, "The Blight") || ForageRegionalPolicy.IsEligible(blight, "Hidden Hills")) return "FAIL Blightroot regional boundary";
            if (FindByKnowledgeKey("duskleaf").MinimumSkill != 42 || FindByKnowledgeKey("elder_resin").MinimumSkill != 46 || FindByKnowledgeKey("voidcap").MinimumSkill != 49) return "FAIL high-tier resource coverage";
            if (ForEnvironmentAll(ForageEnvironmentKind.Covered, "Cave", false).Count != 0) return "FAIL covered resources leaked while gate off";
            if (ForEnvironmentAll(ForageEnvironmentKind.Covered, "Cave", true).Count != 4) return "FAIL covered pool count";
            if (ForEnvironmentAll(ForageEnvironmentKind.Open, "Hidden Hills", false).Count != 11) return "FAIL open ecology candidate count";
            return "PASS forage resource catalog";
        }
    }
}
