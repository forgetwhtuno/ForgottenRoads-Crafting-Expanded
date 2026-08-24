using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace ErenshorCraftingExpanded
{
    // Current-scene hostile-level survey. The eligibility surface is proven by this exact project
    // snapshot/current Assembly-CSharp and the same-snapshot Contracts/PvP compatibility code:
    // NPC.ThisSim/SimPlayer/NeverAggro/MiningNode/TreasureChest/SummonedByPlayer, Character
    // Alive/Master/Invulnerable/isVendor/BossXp/MyFaction/MyStats.Level, structural named-actor
    // wiring (dialog/quest/raid/rare-spawn origin), and the current temporary-PvP clone marker.
    // Nothing here guesses a zone level or hostility from the scene/display name.
    internal static class GameWorldThreatApi
    {
        private static bool _identityFieldsResolved;
        private static FieldInfo _npcDialogField;
        private static FieldInfo _npcQuestsField;
        private static FieldInfo _npcQuestToAssignField;
        private static FieldInfo _npcRaidManagerField;
        private static FieldInfo _npcSpawnPointField;

        internal static List<int> ReadOrdinaryHostileLevels(out int scanned, out int rejected)
        {
            List<int> levels = new List<int>();
            scanned = 0; rejected = 0;
            NPC[] npcs;
            try { npcs = UnityEngine.Object.FindObjectsOfType<NPC>(); }
            catch { return levels; }
            if (npcs == null) return levels;

            for (int i = 0; i < npcs.Length; i++)
            {
                NPC npc = npcs[i];
                if (npc == null) continue;
                scanned++;
                Character actor = null;
                try
                {
                    actor = npc.GetComponent<Character>();
                    if (actor == null) actor = npc.GetComponentInParent<Character>();
                }
                catch { }
                if (!IsOrdinaryHostile(actor, npc)) { rejected++; continue; }
                int level = 0;
                try { if (actor.MyStats != null) level = actor.MyStats.Level; } catch { level = 0; }
                if (level <= 0 || level > 200) { rejected++; continue; }
                levels.Add(level);
            }
            return levels;
        }

        internal static bool IsOrdinaryHostile(Character actor, NPC npc)
        {
            if (actor == null || npc == null) return false;
            try
            {
                if (!actor.Alive || actor.Master != null || actor.Invulnerable || actor.isVendor || actor.BossXp > 0f) return false;
                if (npc.SimPlayer || npc.ThisSim != null || npc.NeverAggro || npc.MiningNode || npc.TreasureChest || npc.SummonedByPlayer) return false;
                if (actor.MyFaction == Character.Faction.Player || actor.MyFaction == Character.Faction.PC ||
                    actor.MyFaction == Character.Faction.Villager || actor.MyFaction == Character.Faction.DEBUG) return false;
                if (LooksLikeTemporaryPvpProxy(npc)) return false;
                if (IsStructurallyNamedActor(npc, actor)) return false;
                return true;
            }
            catch { return false; }
        }

        private static void ResolveIdentityFields()
        {
            if (_identityFieldsResolved) return;
            _identityFieldsResolved = true;
            try
            {
                _npcDialogField = AccessTools.Field(typeof(NPC), "MyDialog");
                _npcQuestsField = AccessTools.Field(typeof(NPC), "MyQuests");
                _npcQuestToAssignField = AccessTools.Field(typeof(NPC), "questToAssign");
                _npcRaidManagerField = AccessTools.Field(typeof(NPC), "RM");
                _npcSpawnPointField = AccessTools.Field(typeof(NPC), "MySpawnPoint");
            }
            catch { }
        }

        private static bool IsStructurallyNamedActor(NPC npc, Character actor)
        {
            ResolveIdentityFields();
            // If one private identity surface disappears after a game update, that one signal stops
            // contributing. Other positive exclusions still apply and the robust distribution still
            // prevents a single high outlier from promoting a scene.
            bool hasDialog = HasUnityReference(_npcDialogField, npc);
            bool assignsQuest = HasUnityReference(_npcQuestsField, npc) || ReadBool(_npcQuestToAssignField, npc);
            bool completesQuestOnDeath = actor.QuestCompleteOnDeath != null;
            bool achievementActor = !string.IsNullOrEmpty(npc.SetAchievementOnDefeat) || !string.IsNullOrEmpty(npc.SetAchievementOnSpawn);
            bool raidManaged = HasUnityReference(_npcRaidManagerField, npc);
            bool rareSpawn = IsRareSpawnVariant(npc);
            return hasDialog || assignsQuest || completesQuestOnDeath || achievementActor || raidManaged || rareSpawn;
        }

        private static bool IsRareSpawnVariant(NPC npc)
        {
            try
            {
                ResolveIdentityFields();
                if (_npcSpawnPointField == null || npc == null || npc.gameObject == null) return false;
                SpawnPoint spawn = _npcSpawnPointField.GetValue(npc) as SpawnPoint;
                if (spawn == null || spawn.RareSpawns == null || spawn.RareSpawns.Count == 0) return false;
                string actorName = StripCloneSuffix(npc.gameObject.name);
                if (actorName.Length == 0) return false;
                for (int i = 0; i < spawn.RareSpawns.Count; i++)
                {
                    GameObject prefab = spawn.RareSpawns[i];
                    if (prefab == null) continue;
                    if (string.Equals(StripCloneSuffix(prefab.name), actorName, StringComparison.OrdinalIgnoreCase)) return true;
                }
                return false;
            }
            catch { return false; }
        }

        private static bool LooksLikeTemporaryPvpProxy(NPC npc)
        {
            try
            {
                string name = npc == null || npc.gameObject == null ? string.Empty : (npc.gameObject.name ?? string.Empty);
                return name.StartsWith("PvP_TemporaryClone", StringComparison.OrdinalIgnoreCase);
            }
            catch { return false; }
        }

        private static bool HasUnityReference(FieldInfo field, NPC npc)
        {
            if (field == null || npc == null) return false;
            try { return field.GetValue(npc) as UnityEngine.Object != null; }
            catch { return false; }
        }

        private static bool ReadBool(FieldInfo field, NPC npc)
        {
            if (field == null || npc == null) return false;
            try
            {
                object raw = field.GetValue(npc);
                return raw is bool && (bool)raw;
            }
            catch { return false; }
        }

        private static string StripCloneSuffix(string value)
        {
            string text = (value ?? string.Empty).Trim();
            while (text.EndsWith("(Clone)", StringComparison.OrdinalIgnoreCase))
                text = text.Substring(0, text.Length - "(Clone)".Length).TrimEnd();
            return text;
        }
    }

    internal static class WorldThreatRuntime
    {
        private const float StabilizeSeconds = 2.25f;
        private const float SparseFallbackSeconds = 6f;
        private static string _scene = string.Empty;
        private static float _startedAt;
        private static WorldThreatSnapshot _snapshot;
        private static int _lastScanned;
        private static int _lastRejected;

        internal static WorldThreatSnapshot Current { get { return _snapshot; } }
        internal static bool Ready { get { return _snapshot != null && _snapshot.Frozen; } }
        internal static int LastScanned { get { return _lastScanned; } }
        internal static int LastRejected { get { return _lastRejected; } }

        internal static void BeginScene(string scene, float now)
        {
            string normalized = scene ?? string.Empty;
            if (string.Equals(_scene, normalized, StringComparison.OrdinalIgnoreCase) && _snapshot != null) return;
            _scene = normalized;
            _startedAt = now;
            _snapshot = null;
            _lastScanned = 0;
            _lastRejected = 0;
        }

        internal static void Tick(string scene, float now)
        {
            string normalized = scene ?? string.Empty;
            if (!string.Equals(_scene, normalized, StringComparison.OrdinalIgnoreCase)) BeginScene(normalized, now);
            if (_snapshot != null || string.IsNullOrEmpty(normalized)) return;
            if (now - _startedAt < StabilizeSeconds) return;

            int scanned; int rejected;
            List<int> levels = GameWorldThreatApi.ReadOrdinaryHostileLevels(out scanned, out rejected);
            _lastScanned = scanned; _lastRejected = rejected;
            WorldThreatSnapshot candidate = WorldThreatPolicy.Compute(normalized, levels);
            if (candidate.Band != ForageWorldBand.Unknown)
            {
                _snapshot = candidate;
                return;
            }

            if (now - _startedAt >= SparseFallbackSeconds)
            {
                // This exact snapshot has not proven a native scene/zone difficulty metadata member.
                // Failing closed is safer than assigning a low tier from scene name or player level.
                _snapshot = WorldThreatPolicy.ConservativeFallback(normalized, candidate.HostileSampleCount,
                    "no-proven-native-zone-level-metadata");
            }
        }

        internal static void Reset()
        {
            _scene = string.Empty;
            _startedAt = 0f;
            _snapshot = null;
            _lastScanned = 0;
            _lastRejected = 0;
        }
    }
}
