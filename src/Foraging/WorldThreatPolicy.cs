using System;
using System.Collections.Generic;

namespace ErenshorCraftingExpanded
{
    public enum ForageWorldBand
    {
        Unknown = 0,
        Starter = 1,
        LowMid = 2,
        Mid = 3,
        MidHigh = 4,
        High = 5
    }

    public sealed class WorldThreatSnapshot
    {
        public string Scene = string.Empty;
        public int HostileSampleCount;
        public int MinimumLevel;
        public int MedianLevel;
        public int UpperOrdinaryLevel;
        public int MaximumLevel;
        public ForageWorldBand Band = ForageWorldBand.Unknown;
        public string Source = string.Empty;
        public string Note = string.Empty;
        public bool Frozen;

        public string Summary()
        {
            return "scene=" + (string.IsNullOrEmpty(Scene) ? "(unknown)" : Scene) +
                " sample=" + HostileSampleCount.ToString() +
                " min=" + MinimumLevel.ToString() +
                " median=" + MedianLevel.ToString() +
                " upper=" + UpperOrdinaryLevel.ToString() +
                " max=" + MaximumLevel.ToString() +
                " band=" + WorldThreatPolicy.DisplayName(Band) +
                " source=" + (string.IsNullOrEmpty(Source) ? "unknown" : Source) +
                (string.IsNullOrEmpty(Note) ? string.Empty : " note={" + Note + "}");
        }
    }

    // Pure fixed-world threat policy. Player level and Foraging level are intentionally absent
    // from every input. The scene is classified from ordinary hostile population evidence only.
    // Median + 75th percentile describe the repeated population while the raw maximum is retained
    // only for diagnostics; a single anomalous named/high-level actor therefore cannot promote a
    // starter scene into an endgame gathering band.
    public static class WorldThreatPolicy
    {
        public const int MinimumRobustSample = 5;

        public static WorldThreatSnapshot Compute(string scene, IList<int> hostileLevels)
        {
            WorldThreatSnapshot result = new WorldThreatSnapshot();
            result.Scene = scene ?? string.Empty;
            result.Source = "ordinary-hostiles";
            result.Frozen = true;

            List<int> levels = new List<int>();
            if (hostileLevels != null)
            {
                for (int i = 0; i < hostileLevels.Count; i++)
                    if (hostileLevels[i] > 0 && hostileLevels[i] <= 200) levels.Add(hostileLevels[i]);
            }
            levels.Sort();
            result.HostileSampleCount = levels.Count;
            if (levels.Count == 0)
            {
                result.Band = ForageWorldBand.Unknown;
                result.Source = "insufficient-hostiles";
                result.Note = "no ordinary hostile levels were available";
                return result;
            }

            result.MinimumLevel = levels[0];
            result.MaximumLevel = levels[levels.Count - 1];
            result.MedianLevel = PercentileNearestRank(levels, 0.50f);
            result.UpperOrdinaryLevel = PercentileNearestRank(levels, 0.75f);

            if (levels.Count < MinimumRobustSample)
            {
                result.Band = ForageWorldBand.Unknown;
                result.Source = "insufficient-hostiles";
                result.Note = "robust classification requires at least " + MinimumRobustSample.ToString() + " ordinary hostile samples";
                return result;
            }

            result.Band = Classify(result.MedianLevel, result.UpperOrdinaryLevel);
            return result;
        }

        public static WorldThreatSnapshot ConservativeFallback(string scene, int observedCount, string source)
        {
            WorldThreatSnapshot result = new WorldThreatSnapshot();
            result.Scene = scene ?? string.Empty;
            result.HostileSampleCount = Math.Max(0, observedCount);
            result.Band = ForageWorldBand.Unknown;
            result.Source = string.IsNullOrEmpty(source) ? "unclassified-fallback" : source;
            result.Note = "no verified native zone-level metadata surface is available; sparse/unknown scenes fail closed for auto-placement";
            result.Frozen = true;
            return result;
        }

        public static ForageWorldBand Classify(int medianLevel, int upperOrdinaryLevel)
        {
            if (medianLevel <= 0 || upperOrdinaryLevel <= 0) return ForageWorldBand.Unknown;
            if (medianLevel <= 6 && upperOrdinaryLevel <= 9) return ForageWorldBand.Starter;
            if (medianLevel <= 11 && upperOrdinaryLevel <= 16) return ForageWorldBand.LowMid;
            if (medianLevel <= 18 && upperOrdinaryLevel <= 24) return ForageWorldBand.Mid;
            if (medianLevel <= 26 && upperOrdinaryLevel <= 32) return ForageWorldBand.MidHigh;
            return ForageWorldBand.High;
        }

        public static bool Allows(ForageWorldBand sceneBand, ForageWorldBand resourceBand)
        {
            // Unknown evidence must not promote tiered progression resources, but the authored
            // Starter catalog is baseline forage rather than a world-tier reward. This preserves
            // fail-closed progression while allowing ordinary herbs/fibers in sparse safe scenes.
            if (resourceBand == ForageWorldBand.Unknown) return false;
            if (sceneBand == ForageWorldBand.Unknown) return resourceBand == ForageWorldBand.Starter;
            return (int)sceneBand >= (int)resourceBand;
        }

        public static string DisplayName(ForageWorldBand band)
        {
            if (band == ForageWorldBand.Starter) return "Starter";
            if (band == ForageWorldBand.LowMid) return "Low-mid";
            if (band == ForageWorldBand.Mid) return "Mid";
            if (band == ForageWorldBand.MidHigh) return "Mid-high";
            if (band == ForageWorldBand.High) return "High";
            return "Unknown";
        }

        private static int PercentileNearestRank(List<int> sorted, float percentile)
        {
            if (sorted == null || sorted.Count == 0) return 0;
            if (percentile <= 0f) return sorted[0];
            if (percentile >= 1f) return sorted[sorted.Count - 1];
            int rank = (int)Math.Ceiling(percentile * sorted.Count);
            int index = Math.Max(0, Math.Min(sorted.Count - 1, rank - 1));
            return sorted[index];
        }

        internal static string RunSelfTests()
        {
            WorldThreatSnapshot starter = Compute("Starter", new int[] { 3, 4, 4, 5, 6, 50 });
            if (starter.Band != ForageWorldBand.Starter || starter.MaximumLevel != 50 || starter.UpperOrdinaryLevel > 6)
                return "FAIL world tier outlier resistance";

            WorldThreatSnapshot lowMid = Compute("LowMid", new int[] { 8, 9, 10, 10, 11, 14, 40 });
            if (lowMid.Band != ForageWorldBand.LowMid) return "FAIL low-mid classification";
            WorldThreatSnapshot mid = Compute("Mid", new int[] { 15, 16, 17, 18, 20, 22, 25 });
            if (mid.Band != ForageWorldBand.Mid) return "FAIL mid classification";
            WorldThreatSnapshot high = Compute("High", new int[] { 29, 30, 31, 32, 33, 34, 35 });
            if (high.Band != ForageWorldBand.High) return "FAIL high classification";

            WorldThreatSnapshot sparse = Compute("Sparse", new int[] { 20, 21 });
            if (sparse.Band != ForageWorldBand.Unknown || sparse.Source != "insufficient-hostiles")
                return "FAIL sparse evidence must not self-promote";
            WorldThreatSnapshot fallback = ConservativeFallback("Sparse", sparse.HostileSampleCount, "no-proven-zone-metadata");
            if (fallback.Band != ForageWorldBand.Unknown) return "FAIL sparse fallback must stay unknown";
            if (!Allows(fallback.Band, ForageWorldBand.Starter)) return "FAIL unknown world must retain explicit Starter baseline";
            if (Allows(fallback.Band, ForageWorldBand.LowMid)) return "FAIL unknown world promoted Low-mid resource";
            if (Allows(fallback.Band, ForageWorldBand.Mid)) return "FAIL unknown world promoted Mid resource";

            // Faerie's Brake regression fixture: if its live ordinary population resolves to this
            // low distribution, a Mid resource such as Starleaf is systemically excluded. The test
            // intentionally contains no scene-name special case.
            WorldThreatSnapshot brake = Compute("Faerie's Brake", new int[] { 4, 5, 5, 6, 6, 7, 7 });
            if (brake.Band != ForageWorldBand.Starter) return "FAIL Brake low-threat fixture";
            if (Allows(brake.Band, ForageWorldBand.Mid)) return "FAIL Brake admitted Mid resource";
            if (!Allows(high.Band, ForageWorldBand.Mid)) return "FAIL high world should expose Mid resource before player skill";

            // Player level is not an input to Compute/Classify/Allows. Re-running the same world
            // levels represents any player level and must be identical.
            WorldThreatSnapshot fixedA = Compute("Fixed", new int[] { 10, 11, 11, 12, 13, 14 });
            WorldThreatSnapshot fixedB = Compute("Fixed", new int[] { 10, 11, 11, 12, 13, 14 });
            if (fixedA.Band != fixedB.Band || fixedA.MedianLevel != fixedB.MedianLevel || fixedA.UpperOrdinaryLevel != fixedB.UpperOrdinaryLevel)
                return "FAIL fixed-world classification changed without world evidence change";

            return "PASS world threat policy";
        }
    }
}
