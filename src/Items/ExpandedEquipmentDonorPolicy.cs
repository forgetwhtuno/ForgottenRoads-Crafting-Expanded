using System;

namespace ErenshorCraftingExpanded
{
    // Pure donor-profile/ranking policy. Reflection remains in GameItemRegistryApi.
    public static class ExpandedEquipmentDonorPolicy
    {
        public static bool MatchesFamily(string candidateSlot, string candidateWeaponType, bool candidateShield,
            string requiredSlot, string requiredWeaponType, bool requireShield)
        {
            if (string.IsNullOrEmpty(candidateSlot) || string.IsNullOrEmpty(requiredSlot)) return false;
            bool slotMatches = string.Equals(candidateSlot, requiredSlot, StringComparison.Ordinal) ||
                (string.Equals(requiredSlot, "PrimaryOrSecondary", StringComparison.Ordinal) &&
                 (string.Equals(candidateSlot, "Primary", StringComparison.Ordinal) || string.Equals(candidateSlot, "Secondary", StringComparison.Ordinal)));
            if (!slotMatches || candidateShield != requireShield) return false;
            return string.IsNullOrEmpty(requiredWeaponType) || string.Equals(candidateWeaponType ?? string.Empty, requiredWeaponType, StringComparison.Ordinal);
        }

        public static bool MatchesProfile(string candidateSlot, string candidateWeaponType, bool candidateShield, int candidateItemLevel,
            string requiredSlot, string requiredWeaponType, bool requireShield, int targetItemLevel)
        {
            if (!MatchesFamily(candidateSlot, candidateWeaponType, candidateShield, requiredSlot, requiredWeaponType, requireShield)) return false;
            if (candidateItemLevel < 1 || targetItemLevel < 1 || candidateItemLevel > targetItemLevel) return false;
            return true;
        }

        public static bool IsPreferred(int candidateLevel, string candidateName, string candidateId, bool haveSelected,
            int selectedLevel, string selectedName, string selectedId)
        {
            if (!haveSelected) return true;
            if (candidateLevel != selectedLevel) return candidateLevel > selectedLevel;
            int byName = string.Compare(candidateName ?? string.Empty, selectedName ?? string.Empty, StringComparison.OrdinalIgnoreCase);
            if (byName != 0) return byName < 0;
            return string.Compare(candidateId ?? string.Empty, selectedId ?? string.Empty, StringComparison.Ordinal) < 0;
        }

        public static bool IsPreferredFloorCandidate(int candidateLevel, string candidateName, string candidateId, bool haveSelected,
            int selectedLevel, string selectedName, string selectedId)
        {
            if (!haveSelected) return true;
            if (candidateLevel != selectedLevel) return candidateLevel < selectedLevel;
            int byName = string.Compare(candidateName ?? string.Empty, selectedName ?? string.Empty, StringComparison.OrdinalIgnoreCase);
            if (byName != 0) return byName < 0;
            return string.Compare(candidateId ?? string.Empty, selectedId ?? string.Empty, StringComparison.Ordinal) < 0;
        }

        internal static string RunSelfTests()
        {
            if (!MatchesFamily("Primary", "OneHandMelee", false, "PrimaryOrSecondary", "OneHandMelee", false)) return "FAIL current one-hand donor family";
            if (!MatchesProfile("Primary", "OneHandMelee", false, 6, "PrimaryOrSecondary", "OneHandMelee", false, 6)) return "FAIL current one-hand donor profile";
            if (MatchesProfile("Primary", "OneHandMelee", false, 6, "Secondary", "OneHandMelee", false, 6)) return "FAIL slot mismatch accepted";
            if (MatchesProfile("Secondary", string.Empty, true, 8, "Primary", "TwoHandMelee", false, 8)) return "FAIL shield should not satisfy Barkguard Maul profile";
            if (!MatchesProfile("Primary", "TwoHandMelee", false, 8, "Primary", "TwoHandMelee", false, 8)) return "FAIL Barkguard Maul donor profile";
            if (MatchesProfile("Primary", "OneHandDagger", false, 13, "PrimaryOrSecondary", "OneHandDagger", false, 12)) return "FAIL donor above target level accepted";
            if (!IsPreferred(8, "A", "2", true, 7, "Z", "1")) return "FAIL highest level donor preference";
            if (!IsPreferred(8, "A", "9", true, 8, "B", "1")) return "FAIL deterministic name tiebreak";
            if (!IsPreferred(8, "Same", "1", true, 8, "Same", "2")) return "FAIL deterministic id tiebreak";
            if (!IsPreferredFloorCandidate(20, "A", "1", true, 24, "B", "2")) return "FAIL nearest higher donor floor preference";
            if (IsPreferredFloorCandidate(24, "A", "1", true, 20, "B", "2")) return "FAIL higher donor displaced nearer floor";
            return "PASS expanded equipment donor policy";
        }
    }
}
