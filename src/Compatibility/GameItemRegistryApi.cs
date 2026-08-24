using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace ErenshorCraftingExpanded
{
    public sealed class CustomItemRegistrationOutcome
    {
        public string DefinitionId;
        public CustomItemRegistrationState State;
        public string ConflictingExistingItemName;
        public string FailureReason;
        public string BaseItemName;
        public string BaseItemId;
        public string BaseSelectionReason;
    }

    // The only place custom-item registration touches ItemDatabase internals via reflection -
    // matches cammaron/Arcanism's proven-in-production approach (Harmony Traverse there, plain
    // cached reflection here), revalidated against this build's actual field layout in
    // docs/NATIVE_ITEM_REGISTRY_FINDINGS.md. Reflection lookups are resolved once and cached,
    // never repeated in Update() (per the plan's architecture instruction).
    internal static class GameItemRegistryApi
    {
        private const BindingFlags AllInstance = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
        private const BindingFlags AllStatic = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
        private const string OwnershipNamePrefix = "ErenshorCraftingExpanded::";

        // Runtime lookup cache for item objects this plugin has verified/inserted. Ownership of an
        // existing live native entry is never inferred from this cache; the explicit Unity object
        // marker on that entry is authoritative so an ItemDatabase rebuild cannot turn a foreign
        // same-id item into an assumed-owned object.
        private static readonly HashSet<string> OwnedIds = new HashSet<string>();
        private static readonly Dictionary<string, object> ResolvedItemsById = new Dictionary<string, object>();
        private static readonly Dictionary<string, string> SemanticDonorById = new Dictionary<string, string>();

        internal static string LastBaseItemName = string.Empty;
        internal static string LastBaseItemId = string.Empty;
        internal static string LastBaseSelectionReason = string.Empty;

        private static FieldInfo _itemDictField;
        private static FieldInfo _itemDbField;
        private static FieldInfo _itemDbListField;
        private static bool _reflectionResolved;

        internal static void ResetSessionBindings()
        {
            // Do not remove anything from native ItemDatabase here. Existing marked entries may be
            // needed to resolve save inventory while the plugin is installed. Only discard stale
            // managed lookup bindings so the next registration pass must revalidate the live DB.
            OwnedIds.Clear();
            ResolvedItemsById.Clear();
            SemanticDonorById.Clear();
            LastBaseItemName = string.Empty;
            LastBaseItemId = string.Empty;
            LastBaseSelectionReason = string.Empty;
        }

        internal static bool TryRegisterAll(object itemDatabaseInstance, IEnumerable<CustomItemDefinition> definitions, List<CustomItemRegistrationOutcome> outcomes)
        {
            if (itemDatabaseInstance == null) return false;
            ResolveReflectionOnce(itemDatabaseInstance.GetType());
            if (!_reflectionResolved) return false;

            // A late Lunaris load can observe GameData.ItemDB after the native singleton field is
            // assigned but before ItemDatabase.Start() has populated its backing collection. Do
            // not consume the one-shot registration attempt against that half-initialized state;
            // the caller will retry after the database contains ordinary native items.
            IList liveItemDb = _itemDbField.GetValue(itemDatabaseInstance) as IList;
            if (liveItemDb == null || liveItemDb.Count == 0) return false;

            foreach (CustomItemDefinition definition in definitions)
            {
                CustomItemRegistrationOutcome outcome = new CustomItemRegistrationOutcome { DefinitionId = definition == null ? string.Empty : definition.Id };
                outcomes.Add(outcome);

                CustomItemDefinitionRejectReason validation = CustomItemRegistry.Validate(definition, null);
                bool definitionShapeValid = validation == CustomItemDefinitionRejectReason.None;
                object existingEntry = definitionShapeValid ? TryGetExisting(itemDatabaseInstance, definition.Id) : null;
                bool nativeEntryExists = existingEntry != null;
                // Treat the explicit runtime marker on the actual live Item as ownership
                // authority. A static id cache alone can outlive an ItemDatabase recreation and
                // must never turn a foreign same-id entry into an assumed-owned object.
                bool ownedByUs = nativeEntryExists && HasOwnershipMarker(existingEntry, definition.Id);

                object baseItem = null;
                string baseReason = string.Empty;
                if (definitionShapeValid && !nativeEntryExists)
                {
                    baseItem = FindSafeBaseItem(itemDatabaseInstance, definition.VisualKind, out baseReason);
                    if (baseItem == null && !string.IsNullOrEmpty(definition.IconAssetPath))
                    {
                        string genericReason;
                        baseItem = FindAnySafeMaterialDonor(itemDatabaseInstance, out genericReason);
                        if (baseItem != null) baseReason = "generated icon supplies identity; " + genericReason;
                    }
                }

                outcome.BaseItemName = baseItem != null ? ReadName(baseItem) : string.Empty;
                outcome.BaseItemId = baseItem != null ? ReadId(baseItem) : string.Empty;
                outcome.BaseSelectionReason = baseReason;

                if (definitionShapeValid && definition.Id == CraftingExpandedItemIds.WildHerbId)
                {
                    LastBaseItemName = baseItem != null ? ReadName(baseItem) : (ownedByUs ? ReadName(existingEntry) : "(none found)");
                    LastBaseItemId = baseItem != null ? ReadId(baseItem) : (ownedByUs ? ReadId(existingEntry) : string.Empty);
                    LastBaseSelectionReason = ownedByUs ? "existing owned Wild Herb registration reused" : baseReason;
                }

                bool canCreate = definitionShapeValid && baseItem != null;
                bool policyDefinitionReady = definitionShapeValid && (nativeEntryExists || canCreate);
                CustomItemRegistrationState state = CustomItemRegistrationPolicy.Evaluate(
                    policyDefinitionReady, nativeEntryExists, ownedByUs);
                outcome.State = state;

                if (state == CustomItemRegistrationState.Unavailable)
                {
                    outcome.FailureReason = !definitionShapeValid
                        ? ("Definition invalid: " + validation)
                        : "No safe native base item found for visual kind " + definition.VisualKind + " (" + baseReason + ").";
                    continue;
                }
                if (state == CustomItemRegistrationState.Collision)
                {
                    outcome.ConflictingExistingItemName = ReadName(existingEntry);
                    outcome.FailureReason = "Id already occupied by an existing item not owned by this mod.";
                    continue;
                }
                if (ownedByUs)
                {
                    ItemSemanticsDecision reuseSemantics = ItemSemanticsPolicy.ForRawResource(definition.Value);
                    if (!ApplyOrdinaryItemSemantics(existingEntry, reuseSemantics))
                    {
                        outcome.State = CustomItemRegistrationState.Unavailable;
                        outcome.FailureReason = "Existing owned item failed ordinary inventory-semantics validation.";
                        continue;
                    }
                    OwnedIds.Add(definition.Id);
                    ResolvedItemsById[definition.Id] = existingEntry;
                    SemanticDonorById[definition.Id] = "existing-owned (raw resource semantics reapplied)";
                    outcome.BaseItemName = ReadName(existingEntry);
                    outcome.BaseItemId = ReadId(existingEntry);
                    outcome.BaseSelectionReason = "existing owned registration reused; ordinary inventory semantics reapplied";
                    string reuseIconFailure;
                    ItemIconAssetLoader.TryApply(existingEntry, definition.Id, definition.IconAssetPath, out reuseIconFailure);
                    continue;
                }

                object clone = CloneAndConfigure(baseItem, definition);
                if (clone != null)
                {
                    string iconFailure;
                    ItemIconAssetLoader.TryApply(clone, definition.Id, definition.IconAssetPath, out iconFailure);
                }
                if (clone == null)
                {
                    outcome.State = CustomItemRegistrationState.Unavailable;
                    outcome.FailureReason = "Clone/configure failed.";
                    continue;
                }

                if (!InsertIntoDatabase(itemDatabaseInstance, clone))
                {
                    try { UnityEngine.Object unityClone = clone as UnityEngine.Object; if (unityClone != null) UnityEngine.Object.Destroy(unityClone); } catch { }
                    outcome.State = CustomItemRegistrationState.Unavailable;
                    outcome.FailureReason = "Native ItemDB/itemDict insertion failed.";
                    continue;
                }

                OwnedIds.Add(definition.Id);
                ResolvedItemsById[definition.Id] = clone;
                SemanticDonorById[definition.Id] = ReadName(baseItem) + "#" + ReadId(baseItem);
            }
            return true;
        }

        internal static object TryResolveCustomItem(string id)
        {
            object item;
            return ResolvedItemsById.TryGetValue(id, out item) ? item : null;
        }

        internal static bool IsCustomItemAvailable(string id)
        {
            return OwnedIds.Contains(id);
        }

        internal static string DescribeOwnedInventorySemanticsSummary()
        {
            int available = 0; int compliant = 0; int mismatch = 0;
            foreach (CustomItemDefinition d in CraftingExpandedItems.Registry.All)
                CountSemanticStatus(d.Id, ItemSemanticFamily.RawResource, d.Value, ref available, ref compliant, ref mismatch);
            int consumableAvailable = 0; int consumableCompliant = 0;
            for (int i = 0; i < ExpandedContentItems.All.Count; i++)
            {
                ExpandedItemDefinition d = ExpandedContentItems.All[i];
                if (d.Kind == ExpandedItemKind.Consumable)
                {
                    object live = TryResolveCustomItem(d.Id);
                    if (live != null && OwnedIds.Contains(d.Id))
                    {
                        consumableAvailable++;
                        if (NativeConsumablePolicy.MatchesFamily(ReadConsumableFacts(live), d.ConsumableFamily) && GetInt(live.GetType(), live, "ItemValue", -1) == d.Value) consumableCompliant++;
                    }
                    continue;
                }
                ItemSemanticFamily family = d.Kind == ExpandedItemKind.Equipment
                    ? ItemSemanticFamily.FinishedEquipment
                    : (string.Equals(d.Category, "component", StringComparison.Ordinal) ? ItemSemanticFamily.ProcessedComponent : ItemSemanticFamily.RawResource);
                CountSemanticStatus(d.Id, family, d.Value, ref available, ref compliant, ref mismatch);
            }
            return "ordinaryAvailable=" + available + "/32 compliant=" + compliant + " mismatch=" + mismatch +
                " consumables=" + consumableAvailable + "/5 nativeSemantics=" + consumableCompliant +
                " protectedRecipeTemplates=PlayerCannotSell+NoTradeNoDestroy+value0";
        }

        internal static List<string> BuildOwnedInventorySemanticsLines(int maxLines)
        {
            List<string> lines = new List<string>();
            if (maxLines < 1) return lines;
            foreach (CustomItemDefinition d in CraftingExpandedItems.Registry.All)
            {
                if (lines.Count >= maxLines) break;
                lines.Add(DescribeOwnedSemanticLine(d.Id, d.Name, ItemSemanticFamily.RawResource, d.Value));
            }
            for (int i = 0; i < ExpandedContentItems.All.Count && lines.Count < maxLines; i++)
            {
                ExpandedItemDefinition d = ExpandedContentItems.All[i];
                if (d.Kind == ExpandedItemKind.Consumable)
                {
                    object live = TryResolveCustomItem(d.Id);
                    lines.Add(d.Name + "#" + d.Id + " family=NativeConsumable(" + d.ConsumableFamily + ") status=" +
                        (live == null ? "UNAVAILABLE" : (NativeConsumablePolicy.MatchesFamily(ReadConsumableFacts(live), d.ConsumableFamily) ? "PASS" : "MISMATCH")));
                    continue;
                }
                ItemSemanticFamily family = d.Kind == ExpandedItemKind.Equipment
                    ? ItemSemanticFamily.FinishedEquipment
                    : (string.Equals(d.Category, "component", StringComparison.Ordinal) ? ItemSemanticFamily.ProcessedComponent : ItemSemanticFamily.RawResource);
                lines.Add(DescribeOwnedSemanticLine(d.Id, d.Name, family, d.Value));
            }
            return lines;
        }

        internal static List<string> BuildNativeInventorySemanticsReferenceLines()
        {
            List<string> result = new List<string>();
            try
            {
                object db = TryGetLiveItemDatabase();
                if (db == null) { result.Add("Native item examples: live ItemDB unavailable"); return result; }
                ResolveReflectionOnce(db.GetType());
                IList items = _reflectionResolved ? _itemDbField.GetValue(db) as IList : null;
                if (items == null) { result.Add("Native item examples: ItemDB collection unavailable"); return result; }

                object commodity = null; object ingredient = null; object consumable = null; object weapon = null; object armor = null; object protectedItem = null;
                foreach (object item in items)
                {
                    if (item == null || CraftingExpandedItemIds.IsInOwnedRange(ReadId(item))) continue;
                    Type t = item.GetType();
                    string slot = ReadEnumName(t, item, "RequiredSlot");
                    bool stackable = GetBool(t, item, "Stackable") == true;
                    bool protectedFlag = GetBool(t, item, "NoTradeNoDestroy") == true || GetBool(t, item, "PlayerCannotSell") == true ||
                        GetRef(t, item, "AssignQuestOnRead") != null || GetRef(t, item, "CompleteOnRead") != null;
                    int value = GetInt(t, item, "ItemValue", 0);
                    bool template = GetBool(t, item, "Template") == true;
                    bool disposable = GetBool(t, item, "Disposable") == true;
                    if (protectedItem == null && protectedFlag) protectedItem = item;
                    if (commodity == null && slot == "General" && stackable && !protectedFlag && !template && !disposable && value > 0) commodity = item;
                    if (consumable == null && slot == "General" && disposable && GetRef(t, item, "ItemEffectOnClick") != null) consumable = item;
                    if (weapon == null && (slot == "Primary" || slot == "PrimaryOrSecondary" || slot == "Secondary") && !stackable && !protectedFlag && value > 0) weapon = item;
                    if (armor == null && !string.IsNullOrEmpty(slot) && slot != "General" && slot != "Aura" && slot != "Charm" &&
                        slot != "Primary" && slot != "PrimaryOrSecondary" && slot != "Secondary" && !stackable && !protectedFlag && value > 0) armor = item;
                    if (ingredient == null && template)
                    {
                        IList required = GetRef(t, item, "TemplateIngredients") as IList;
                        if (required != null && required.Count > 0 && required[0] != null) ingredient = required[0];
                    }
                }
                AddNativeExample(result, "ordinary General/stackable commodity", commodity);
                AddNativeExample(result, "native recipe ingredient", ingredient);
                AddNativeExample(result, "stackable/use-item example", consumable);
                AddNativeExample(result, "normal weapon", weapon);
                AddNativeExample(result, "normal armor", armor);
                AddNativeExample(result, "protected/quest/no-destroy example", protectedItem);
            }
            catch (Exception ex) { result.Add("Native item examples: probe failed " + ex.GetType().Name); }
            return result;
        }

        private static void CountSemanticStatus(string id, ItemSemanticFamily family, int declaredValue, ref int available, ref int compliant, ref int mismatch)
        {
            object item = TryResolveCustomItem(id);
            if (item == null || !OwnedIds.Contains(id)) return;
            available++;
            if (IsLiveOrdinarySemanticMatch(item, family, declaredValue)) compliant++; else mismatch++;
        }

        private static string DescribeOwnedSemanticLine(string id, string name, ItemSemanticFamily family, int declaredValue)
        {
            object item = TryResolveCustomItem(id);
            if (item == null || !OwnedIds.Contains(id)) return name + "#" + id + " family=" + family + " status=UNAVAILABLE";
            Type t = item.GetType();
            int value = GetInt(t, item, "ItemValue", -1);
            bool playerCannotSell = GetBool(t, item, "PlayerCannotSell") == true;
            bool noTradeNoDestroy = GetBool(t, item, "NoTradeNoDestroy") == true;
            string donor; if (!SemanticDonorById.TryGetValue(id, out donor)) donor = "(unknown/reused)";
            bool compliant = IsLiveOrdinarySemanticMatch(item, family, declaredValue);
            return name + "#" + id + " family=" + family +
                " stackable=" + BoolText(GetBool(t, item, "Stackable")) +
                " sellable=" + (!playerCannotSell && !noTradeNoDestroy && value > 0 ? "yes" : "no") +
                " destroyable=" + (!noTradeNoDestroy ? "yes" : "no") +
                " tradeProtected=" + (noTradeNoDestroy ? "yes" : "no") +
                " simBlocked=" + BoolText(GetBool(t, item, "SimPlayersCantGet")) +
                " itemValue=" + value + " donor=" + donor +
                " questRead=" + ((GetRef(t, item, "AssignQuestOnRead") != null || GetRef(t, item, "CompleteOnRead") != null) ? "yes" : "no") +
                " status=" + (compliant ? "PASS" : "MISMATCH");
        }

        private static bool IsLiveOrdinarySemanticMatch(object item, ItemSemanticFamily family, int declaredValue)
        {
            if (item == null) return false; Type t = item.GetType();
            bool? stackable = GetBool(t, item, "Stackable"); bool? noSell = GetBool(t, item, "PlayerCannotSell");
            bool? noDestroy = GetBool(t, item, "NoTradeNoDestroy"); bool? disposable = GetBool(t, item, "Disposable");
            bool? mustEquip = GetBool(t, item, "MustBeEquippedToClick"); bool? unique = GetBool(t, item, "Unique");
            bool? relic = GetBool(t, item, "Relic"); bool? rare = GetBool(t, item, "RareItem");
            bool? simBlocked = GetBool(t, item, "SimPlayersCantGet");
            if (!stackable.HasValue || !noSell.HasValue || !noDestroy.HasValue || !disposable.HasValue || !mustEquip.HasValue || !unique.HasValue || !relic.HasValue || !rare.HasValue || !simBlocked.HasValue) return false;
            int value = GetInt(t, item, "ItemValue", -1);
            if (family == ItemSemanticFamily.FinishedEquipment)
                return !stackable.Value && !noSell.Value && !noDestroy.Value && !disposable.Value && !mustEquip.Value && !unique.Value && !relic.Value && !rare.Value && !simBlocked.Value && value > 0 &&
                    GetRef(t, item, "AssignQuestOnRead") == null && GetRef(t, item, "CompleteOnRead") == null;
            ItemSemanticsDecision expected = family == ItemSemanticFamily.ProcessedComponent
                ? ItemSemanticsPolicy.ForProcessedComponent(declaredValue) : ItemSemanticsPolicy.ForRawResource(declaredValue);
            return ItemSemanticsPolicy.IsOrdinaryInventorySemantics(expected, stackable.Value, noSell.Value, noDestroy.Value, disposable.Value, mustEquip.Value, unique.Value, relic.Value, rare.Value, simBlocked.Value, value) &&
                GetRef(t, item, "AssignQuestOnRead") == null && GetRef(t, item, "CompleteOnRead") == null;
        }

        private static string ReadEnumName(Type t, object instance, string fieldName)
        {
            object value = GetRef(t, instance, fieldName); return value == null ? string.Empty : value.ToString();
        }

        private static string BoolText(bool? value) { return value.HasValue ? (value.Value ? "yes" : "no") : "unknown"; }

        private static void AddNativeExample(List<string> result, string label, object item)
        {
            if (item == null) { result.Add("Native item example " + label + ": (none found)"); return; }
            Type t = item.GetType();
            result.Add("Native item example " + label + ": " + ReadName(item) + "#" + ReadId(item) +
                " slot=" + ReadEnumName(t, item, "RequiredSlot") + " stackable=" + BoolText(GetBool(t, item, "Stackable")) +
                " disposable=" + BoolText(GetBool(t, item, "Disposable")) + " playerCannotSell=" + BoolText(GetBool(t, item, "PlayerCannotSell")) +
                " noTradeNoDestroy=" + BoolText(GetBool(t, item, "NoTradeNoDestroy")) + " simBlocked=" + BoolText(GetBool(t, item, "SimPlayersCantGet")) +
                " itemValue=" + GetInt(t, item, "ItemValue", -1) +
                " questRead=" + ((GetRef(t, item, "AssignQuestOnRead") != null || GetRef(t, item, "CompleteOnRead") != null) ? "yes" : "no"));
        }

        // Historical/current project IL evidence establishes GameData.ItemDB as the live native
        // ItemDatabase back-reference set at the start of ItemDatabase.Start(). Reflection keeps
        // visibility assumptions out of the compile surface and enables safe late-plugin recovery
        // when Lunaris loads this mod after Start() already ran. Read-only.
        internal static object TryGetLiveItemDatabase()
        {
            try { return GetStaticField("GameData", "ItemDB"); }
            catch { return null; }
        }

        internal enum InventoryOnlyGrantResult
        {
            Success = 0,
            ItemUnavailable = 1,
            InventoryUnavailable = 2,
            NativeGrantUnavailable = 3,
            InventoryRejected = 4,
            Failed = 5
        }

        // Recovery grants deliberately never call ForceItemToInv. A replacement template must
        // respect native inventory capacity: if the normal AddItemToInv path rejects it, the
        // permanent recipe knowledge/entitlement remains and the player can retry after making
        // room. The existing generic resource-grant path below retains its historical behavior.
        internal static InventoryOnlyGrantResult GrantRegisteredItemToInventoryOnly(string id)
        {
            object item = TryResolveCustomItem(id);
            if (item == null || !OwnedIds.Contains(id)) return InventoryOnlyGrantResult.ItemUnavailable;
            try
            {
                object playerInv = GetStaticField("GameData", "PlayerInv");
                if (playerInv == null) return InventoryOnlyGrantResult.InventoryUnavailable;
                Type invType = playerInv.GetType();

                MethodInfo addWithQty = FindMethod(invType, "AddItemToInv", item.GetType(), typeof(int));
                if (addWithQty != null)
                {
                    object result = addWithQty.Invoke(playerInv, new object[] { item, 1 });
                    if (!(result is bool)) return InventoryOnlyGrantResult.NativeGrantUnavailable;
                    return (bool)result ? InventoryOnlyGrantResult.Success : InventoryOnlyGrantResult.InventoryRejected;
                }

                MethodInfo add = FindMethod(invType, "AddItemToInv", item.GetType());
                if (add == null) return InventoryOnlyGrantResult.NativeGrantUnavailable;
                object oneResult = add.Invoke(playerInv, new object[] { item });
                if (!(oneResult is bool)) return InventoryOnlyGrantResult.NativeGrantUnavailable;
                return (bool)oneResult ? InventoryOnlyGrantResult.Success : InventoryOnlyGrantResult.InventoryRejected;
            }
            catch { return InventoryOnlyGrantResult.Failed; }
        }

        // Foraging-specific strict grant. Exactly one native AddItemToInv overload is selected
        // before invocation and is invoked at most once. There is deliberately no ForceItemToInv
        // fallback and no second-overload retry after an invocation. If reflection/native code
        // throws after Invoke begins, mutation is ambiguous and the node must fail closed.
        internal static ForagingInventoryGrantResult GrantRegisteredItemForForaging(string id, int quantity, out bool nativeInvokeStarted)
        {
            nativeInvokeStarted = false;
            object item = TryResolveCustomItem(id);
            if (item == null || !OwnedIds.Contains(id) || quantity <= 0) return ForagingInventoryGrantResult.ItemUnavailable;

            object playerInv;
            try { playerInv = GetStaticField("GameData", "PlayerInv"); }
            catch { return ForagingInventoryGrantResult.NativeGrantUnavailable; }
            if (playerInv == null) return ForagingInventoryGrantResult.NativeGrantUnavailable;

            Type invType = playerInv.GetType();
            MethodInfo addWithQty = FindMethod(invType, "AddItemToInv", item.GetType(), typeof(int));
            if (addWithQty != null)
            {
                try
                {
                    nativeInvokeStarted = true;
                    object result = addWithQty.Invoke(playerInv, new object[] { item, quantity });
                    if (!(result is bool)) return ForagingInventoryGrantResult.UnknownAfterInvoke;
                    return (bool)result ? ForagingInventoryGrantResult.Success : ForagingInventoryGrantResult.InventoryRejected;
                }
                catch { return ForagingInventoryGrantResult.UnknownAfterInvoke; }
            }

            if (quantity != 1) return ForagingInventoryGrantResult.NativeGrantUnavailable;
            MethodInfo add = FindMethod(invType, "AddItemToInv", item.GetType());
            if (add == null) return ForagingInventoryGrantResult.NativeGrantUnavailable;
            try
            {
                nativeInvokeStarted = true;
                object result = add.Invoke(playerInv, new object[] { item });
                if (!(result is bool)) return ForagingInventoryGrantResult.UnknownAfterInvoke;
                return (bool)result ? ForagingInventoryGrantResult.Success : ForagingInventoryGrantResult.InventoryRejected;
            }
            catch { return ForagingInventoryGrantResult.UnknownAfterInvoke; }
        }

        // Applies only player-ownership presentation/safety fields to an already registered,
        // explicitly mod-owned recipe-template Item. Recipe mutation/ingredients/rewards remain
        // the native-recipe workstream's responsibility. Unique is intentionally not touched.
        internal static bool TryApplyRecipeTemplateSafety(string id, string recipeDisplayName)
        {
            if (!CraftingExpandedItemIds.IsInRecipeTemplateRange(id)) return false;
            object item = TryResolveCustomItem(id);
            if (item == null || !OwnedIds.Contains(id) || !HasOwnershipMarker(item, id)) return false;
            try
            {
                SetField(item, "ItemName", RecipeTemplateItemPolicy.FormatTemplateName(recipeDisplayName));
                SetField(item, "ItemValue", RecipeTemplateItemPolicy.SafeVendorValue);
                SetField(item, "PlayerCannotSell", RecipeTemplateItemPolicy.PlayerCannotSell);
                SetField(item, "NoTradeNoDestroy", RecipeTemplateItemPolicy.NoTradeNoDestroy);

                object value = GetRef(item.GetType(), item, "ItemValue");
                return ReadName(item) == RecipeTemplateItemPolicy.FormatTemplateName(recipeDisplayName) &&
                    GetBool(item.GetType(), item, "PlayerCannotSell") == true &&
                    GetBool(item.GetType(), item, "NoTradeNoDestroy") == true &&
                    value is int && (int)value == RecipeTemplateItemPolicy.SafeVendorValue;
            }
            catch { return false; }
        }

        internal static bool GrantRegisteredItem(string id, int quantity)
        {
            object item = TryResolveCustomItem(id);
            if (item == null || quantity <= 0) return false;
            try
            {
                object playerInv = GetStaticField("GameData", "PlayerInv");
                if (playerInv == null) return false;
                Type invType = playerInv.GetType();

                MethodInfo addWithQty = FindMethod(invType, "AddItemToInv", item.GetType(), typeof(int));
                bool added = false;
                if (addWithQty != null)
                {
                    object result = addWithQty.Invoke(playerInv, new object[] { item, quantity });
                    added = result is bool && (bool)result;
                }
                if (!added)
                {
                    // The one-item native overload cannot truthfully satisfy a multi-item grant.
                    // Fail closed rather than partially grant N=1 and then let a caller retry the
                    // node/command and accidentally duplicate. Current Wild Herb nodes request 1;
                    // future >1 rewards must prove the native quantity overload in the live build.
                    if (quantity != 1) return false;
                    MethodInfo add = FindMethod(invType, "AddItemToInv", item.GetType());
                    if (add != null)
                    {
                        object result = add.Invoke(playerInv, new object[] { item });
                        added = result is bool && (bool)result;
                    }
                }
                if (added) return true;

                if (quantity != 1) return false;
                MethodInfo force = FindMethod(invType, "ForceItemToInv", item.GetType());
                if (force == null) return false;
                force.Invoke(playerInv, new object[] { item });
                return true;
            }
            catch { return false; }
        }

        // Narrow internal primitives shared by the experimental recipe bridge. They reuse the
        // same verified ItemDB/itemDict/ItemDBList transaction as custom materials instead of
        // creating a second database mutation implementation.
        internal static object TryGetLiveItem(object itemDatabaseInstance, string id)
        {
            if (itemDatabaseInstance == null || string.IsNullOrEmpty(id)) return null;
            ResolveReflectionOnce(itemDatabaseInstance.GetType());
            return _reflectionResolved ? TryGetExisting(itemDatabaseInstance, id) : null;
        }

        internal static bool HasOwnedMarker(object item, string id) { return HasOwnershipMarker(item, id); }

        internal static void MarkOwned(object item, string id)
        {
            try
            {
                UnityEngine.Object unityItem = item as UnityEngine.Object;
                if (unityItem != null) unityItem.name = OwnershipNamePrefix + id;
            }
            catch { }
        }

        internal static bool TryInsertOwnedItem(object itemDatabaseInstance, string id, object item, out object liveItem)
        {
            liveItem = null;
            if (itemDatabaseInstance == null || item == null || string.IsNullOrEmpty(id)) return false;
            ResolveReflectionOnce(itemDatabaseInstance.GetType());
            if (!_reflectionResolved || !string.Equals(ReadId(item), id, StringComparison.Ordinal)) return false;

            object existing = TryGetExisting(itemDatabaseInstance, id);
            if (existing != null)
            {
                if (!HasOwnershipMarker(existing, id)) return false;
                OwnedIds.Add(id);
                ResolvedItemsById[id] = existing;
                liveItem = existing;
                return true;
            }

            MarkOwned(item, id);
            if (!HasOwnershipMarker(item, id) || !InsertIntoDatabase(itemDatabaseInstance, item)) return false;
            OwnedIds.Add(id);
            ResolvedItemsById[id] = item;
            liveItem = item;
            return true;
        }

        internal static string ReadLiveId(object item) { return item == null ? string.Empty : ReadId(item); }
        internal static string ReadLiveName(object item) { return item == null ? string.Empty : ReadName(item); }

        // Physical recipe templates need a truthful inventory-full signal. Unlike the historically
        // live-tested forage-material path above, this strict grant never calls ForceItemToInv.
        // Knowledge can therefore remain permanent while a failed physical delivery becomes
        // RestoreAvailable for an explicit later retry.
        internal static bool GrantRegisteredItemStrict(string id, int quantity)
        {
            object item = TryResolveCustomItem(id);
            if (item == null || quantity <= 0) return false;
            try
            {
                object playerInv = GetStaticField("GameData", "PlayerInv");
                if (playerInv == null) return false;
                Type invType = playerInv.GetType();
                MethodInfo addWithQty = FindMethod(invType, "AddItemToInv", item.GetType(), typeof(int));
                if (addWithQty != null)
                {
                    object result = addWithQty.Invoke(playerInv, new object[] { item, quantity });
                    if (result is bool && (bool)result) return true;
                }
                if (quantity != 1) return false;
                MethodInfo add = FindMethod(invType, "AddItemToInv", item.GetType());
                if (add == null) return false;
                object oneResult = add.Invoke(playerInv, new object[] { item });
                return oneResult is bool && (bool)oneResult;
            }
            catch { return false; }
        }

        internal static bool TryRegisterExpandedItem(object itemDatabaseInstance, ExpandedItemDefinition definition, out object liveItem, out string donorName, out string failure)
        {
            liveItem = null; donorName = string.Empty; failure = string.Empty;
            if (itemDatabaseInstance == null || definition == null || string.IsNullOrEmpty(definition.Id) || !CraftingExpandedItemIds.IsInOwnedRange(definition.Id) || CraftingExpandedItemIds.IsInRecipeTemplateRange(definition.Id))
            { failure = "expanded item inputs/range invalid"; return false; }
            ResolveReflectionOnce(itemDatabaseInstance.GetType());
            if (!_reflectionResolved) { failure = "ItemDatabase reflection unavailable"; return false; }
            object existing = TryGetExisting(itemDatabaseInstance, definition.Id);
            if (existing != null)
            {
                if (!HasOwnershipMarker(existing, definition.Id)) { failure = "item id collision"; donorName = ReadName(existing); return false; }
                object semanticDonor = null;
                string semanticDonorReason = string.Empty;
                if (definition.Kind == ExpandedItemKind.Equipment)
                {
                    semanticDonor = FindSafeEquipmentDonor(itemDatabaseInstance, definition, out semanticDonorReason);
                    if (semanticDonor == null)
                    {
                        failure = "existing owned equipment could not re-resolve native value donor: " + semanticDonorReason;
                        return false;
                    }
                }
                else if (definition.Kind == ExpandedItemKind.Consumable)
                {
                    semanticDonor = FindSafeConsumableDonor(itemDatabaseInstance, definition, out semanticDonorReason);
                    if (semanticDonor == null)
                    {
                        failure = "existing owned consumable could not re-resolve native use donor: " + semanticDonorReason;
                        return false;
                    }
                }
                if (definition.Kind == ExpandedItemKind.Consumable)
                {
                    if (!ApplyConsumableSemantics(existing, semanticDonor, definition))
                    {
                        failure = "existing owned consumable failed native-use semantics validation";
                        return false;
                    }
                }
                else
                {
                    ItemSemanticsDecision existingSemantics = ExpandedSemantics(definition, semanticDonor);
                    if (!ApplyOrdinaryItemSemantics(existing, existingSemantics))
                    {
                        failure = "existing owned item failed ordinary inventory-semantics validation";
                        return false;
                    }
                }
                OwnedIds.Add(definition.Id); ResolvedItemsById[definition.Id] = existing; liveItem = existing;
                string existingIconFailure; ItemIconAssetLoader.TryApply(existing, definition.Id, definition.IconAssetPath, out existingIconFailure);
                donorName = semanticDonor == null ? "existing owned registration" : ReadName(semanticDonor);
                SemanticDonorById[definition.Id] = semanticDonor == null
                    ? "existing-owned (material semantics reapplied)"
                    : ReadName(semanticDonor) + "#" + ReadId(semanticDonor);
                return true;
            }

            string selectionReason;
            object donor;
            if (definition.Kind == ExpandedItemKind.Equipment)
                donor = FindSafeEquipmentDonor(itemDatabaseInstance, definition, out selectionReason);
            else if (definition.Kind == ExpandedItemKind.Consumable)
                donor = FindSafeConsumableDonor(itemDatabaseInstance, definition, out selectionReason);
            else
                donor = FindAnySafeMaterialDonor(itemDatabaseInstance, out selectionReason);
            if (donor == null) { failure = selectionReason; return false; }
            donorName = ReadName(donor);
            object clone = CloneExpandedItem(donor, definition, out failure);
            if (clone == null) return false;
            string iconFailure; ItemIconAssetLoader.TryApply(clone, definition.Id, definition.IconAssetPath, out iconFailure);
            if (!TryInsertOwnedItem(itemDatabaseInstance, definition.Id, clone, out liveItem))
            {
                try { UnityEngine.Object unityClone = clone as UnityEngine.Object; if (unityClone != null) UnityEngine.Object.Destroy(unityClone); } catch { }
                failure = "expanded item insertion rejected or collided";
                return false;
            }
            if (liveItem != clone) try { UnityEngine.Object unityClone = clone as UnityEngine.Object; if (unityClone != null) UnityEngine.Object.Destroy(unityClone); } catch { }
            SemanticDonorById[definition.Id] = ReadName(donor) + "#" + ReadId(donor);
            return true;
        }

        private static object FindAnySafeMaterialDonor(object itemDatabaseInstance, out string selectionReason)
        {
            selectionReason = string.Empty;
            try
            {
                IList itemDb = _itemDbField.GetValue(itemDatabaseInstance) as IList;
                if (itemDb == null) { selectionReason = "live ItemDB unavailable"; return null; }
                object best = null; string bestName = string.Empty; string bestId = string.Empty;
                foreach (object item in itemDb)
                {
                    if (item == null || !IsSafeBaseCandidate(item)) continue;
                    string name = ReadName(item); string id = ReadId(item);
                    if (best == null || string.Compare(name, bestName, StringComparison.OrdinalIgnoreCase) < 0 ||
                        (string.Equals(name, bestName, StringComparison.OrdinalIgnoreCase) && string.Compare(id, bestId, StringComparison.Ordinal) < 0))
                    { best = item; bestName = name; bestId = id; }
                }
                if (best == null) { selectionReason = "no conservative General/stackable native material donor available"; return null; }
                selectionReason = "conservative General/stackable native donor";
                return best;
            }
            catch (Exception ex) { selectionReason = "material donor selection failed: " + ex.GetType().Name; return null; }
        }

        private sealed class ConsumableCandidate
        {
            internal object Item;
            internal NativeConsumableFacts Facts;
            internal string Name;
            internal string Id;
            internal double Magnitude;
        }

        private static object FindSafeConsumableDonor(object itemDatabaseInstance, ExpandedItemDefinition definition, out string selectionReason)
        {
            selectionReason = string.Empty;
            try
            {
                IList itemDb = _itemDbField.GetValue(itemDatabaseInstance) as IList;
                if (itemDb == null) { selectionReason = "live ItemDB unavailable"; return null; }
                List<ConsumableCandidate> candidates = new List<ConsumableCandidate>();
                int familyCount = 0; int progressionRejected = 0;
                foreach (object item in itemDb)
                {
                    if (item == null || CraftingExpandedItemIds.IsInOwnedRange(ReadId(item))) continue;
                    NativeConsumableFacts facts = ReadConsumableFacts(item);
                    if (!NativeConsumablePolicy.MatchesFamily(facts, definition.ConsumableFamily)) continue;
                    familyCount++;
                    if (!NativeConsumablePolicy.IsProgressionCompatible(facts, definition.TargetItemLevel)) { progressionRejected++; continue; }
                    ConsumableCandidate c = new ConsumableCandidate();
                    c.Item = item; c.Facts = facts; c.Name = ReadName(item); c.Id = ReadId(item);
                    c.Magnitude = NativeConsumablePolicy.PowerMagnitude(facts, definition.ConsumableFamily);
                    candidates.Add(c);
                }

                if (definition.ConsumableFamily == NativeConsumableFamily.ManaRecovery)
                {
                    bool hasFlat = false;
                    for (int i = 0; i < candidates.Count; i++) if (candidates[i].Facts.FlatMana > 0.0) { hasFlat = true; break; }
                    if (hasFlat)
                        for (int i = candidates.Count - 1; i >= 0; i--) if (NativeConsumablePolicy.UsesPercentageMana(candidates[i].Facts)) candidates.RemoveAt(i);
                }
                else if (definition.ConsumableFamily == NativeConsumableFamily.AnyRecovery)
                {
                    bool hasHealth = false;
                    for (int i = 0; i < candidates.Count; i++) if (NativeConsumablePolicy.IsHealthRecovery(candidates[i].Facts)) { hasHealth = true; break; }
                    if (hasHealth)
                        for (int i = candidates.Count - 1; i >= 0; i--) if (!NativeConsumablePolicy.IsHealthRecovery(candidates[i].Facts)) candidates.RemoveAt(i);
                }

                if (candidates.Count == 0)
                {
                    selectionReason = "no safe native consumable donor family=" + definition.ConsumableFamily +
                        " targetTierLevel=" + definition.TargetItemLevel + " (familyCandidates=" + familyCount +
                        " progressionRejected=" + progressionRejected + ")";
                    return null;
                }

                candidates.Sort(delegate(ConsumableCandidate a, ConsumableCandidate b)
                {
                    int byMagnitude = a.Magnitude.CompareTo(b.Magnitude);
                    if (byMagnitude != 0) return byMagnitude;
                    int aProgress = System.Math.Max(a.Facts.ItemLevel, a.Facts.EffectRequiredLevel);
                    int bProgress = System.Math.Max(b.Facts.ItemLevel, b.Facts.EffectRequiredLevel);
                    if (aProgress != bProgress) return aProgress.CompareTo(bProgress);
                    if (a.Facts.ItemValue != b.Facts.ItemValue) return a.Facts.ItemValue.CompareTo(b.Facts.ItemValue);
                    int byName = string.Compare(a.Name ?? string.Empty, b.Name ?? string.Empty, StringComparison.OrdinalIgnoreCase);
                    if (byName != 0) return byName;
                    return string.Compare(a.Id ?? string.Empty, b.Id ?? string.Empty, StringComparison.Ordinal);
                });

                // Equivalent-power selection: native item/effect level markers are hard ceilings when
                // present; among the remaining same-family donors, the crafted tier chooses a bounded
                // rank by proven effect magnitude rather than treating ItemLevel as the effect itself.
                int clamped = definition.TargetItemLevel;
                if (clamped < 1) clamped = 1; if (clamped > 35) clamped = 35;
                double fraction = (clamped - 1) / 34.0;
                int index = (int)System.Math.Floor(fraction * (candidates.Count - 1) + 0.000001);
                if (index < 0) index = 0; if (index >= candidates.Count) index = candidates.Count - 1;
                ConsumableCandidate best = candidates[index];
                selectionReason = "native consumable donor family=" + definition.ConsumableFamily +
                    " rank=" + (index + 1) + "/" + candidates.Count +
                    " magnitude=" + best.Magnitude.ToString("0.###") + " source=" + NativeConsumablePolicy.MagnitudeSource(best.Facts, definition.ConsumableFamily) +
                    " itemLevel=" + best.Facts.ItemLevel + " effectLevel=" + best.Facts.EffectRequiredLevel +
                    " effect={" + DescribeConsumableEffect(best.Item) + "}";
                return best.Item;
            }
            catch (Exception ex) { selectionReason = "consumable donor selection failed: " + ex.GetType().Name; return null; }
        }

        private static NativeConsumableFacts ReadConsumableFacts(object item)
        {
            NativeConsumableFacts facts = new NativeConsumableFacts();
            if (item == null) return facts;
            Type t = item.GetType();
            facts.GeneralSlot = IsGeneralSlot(t, item);
            facts.Stackable = GetBool(t, item, "Stackable") == true;
            facts.Disposable = GetBool(t, item, "Disposable") == true;
            facts.MustBeEquippedToClick = GetBool(t, item, "MustBeEquippedToClick") == true;
            facts.Unique = GetBool(t, item, "Unique") == true;
            facts.Template = GetBool(t, item, "Template") == true;
            facts.FuelSource = GetBool(t, item, "FuelSource") == true;
            facts.Relic = GetBool(t, item, "Relic") == true;
            facts.RareItem = GetBool(t, item, "RareItem") == true;
            facts.PlayerCannotSell = GetBool(t, item, "PlayerCannotSell") == true;
            facts.NoTradeNoDestroy = GetBool(t, item, "NoTradeNoDestroy") == true;
            facts.SimPlayersCantGet = GetBool(t, item, "SimPlayersCantGet") == true;
            facts.HasIcon = GetRef(t, item, "ItemIcon") != null;
            facts.ItemSpellCastTime = GetNumber(t, item, "SpellCastTime");
            object effect = GetRef(t, item, "ItemEffectOnClick");
            facts.HasClickEffect = effect != null;
            facts.HasTeachSpell = GetRef(t, item, "TeachSpell") != null;
            facts.HasTeachSkill = GetRef(t, item, "TeachSkill") != null;
            facts.HasQuestRead = GetRef(t, item, "AssignQuestOnRead") != null || GetRef(t, item, "CompleteOnRead") != null;
            facts.HasAura = GetRef(t, item, "Aura") != null;
            facts.HasWornEffect = GetRef(t, item, "WornEffect") != null;
            facts.HasWeaponProc = GetRef(t, item, "WeaponProcOnHit") != null;
            facts.ItemValue = GetInt(t, item, "ItemValue", 0);
            facts.ItemLevel = GetInt(t, item, "ItemLevel", 0);
            if (effect != null)
            {
                Type e = effect.GetType();
                facts.SpellTypeCode = GetEnumInt(e, effect, "Type", -1);
                facts.DamageTypeCode = GetEnumInt(e, effect, "MyDamageType", -1);
                facts.EffectRequiredLevel = GetInt(e, effect, "RequiredLevel", 0);
                facts.ManaCost = GetNumber(e, effect, "ManaCost");
                facts.Cooldown = GetNumber(e, effect, "Cooldown");
                facts.SpellRange = GetNumber(e, effect, "SpellRange");
                facts.DirectHp = GetNumber(e, effect, "HP");
                facts.FlatMana = GetNumber(e, effect, "Mana");
                facts.TargetHealing = GetNumber(e, effect, "TargetHealing");
                facts.CasterHealing = GetNumber(e, effect, "CasterHealing");
                facts.PercentManaRestoration = GetNumber(e, effect, "PercentManaRestoration");
                facts.LevelScaledManaRestoration = GetNumber(e, effect, "LevelScaledManaRestoration");
                facts.TargetDamage = GetNumber(e, effect, "TargetDamage");
                facts.BleedDamagePercent = GetNumber(e, effect, "BleedDamagePercent");
                facts.Lifetap = GetBool(e, effect, "Lifetap") == true;
                facts.BeneficialType = facts.SpellTypeCode == NativeConsumablePolicy.SpellTypeBeneficial;
                facts.HealType = facts.SpellTypeCode == NativeConsumablePolicy.SpellTypeHeal;
                facts.SelfOnly = GetBool(e, effect, "SelfOnly") == true;
                facts.ApplyToCaster = GetBool(e, effect, "ApplyToCaster") == true;
                facts.InflictOnSelf = GetBool(e, effect, "InflictOnSelf") == true;
                facts.GroupEffect = GetBool(e, effect, "GroupEffect") == true;
                facts.AreaEffect = facts.SpellTypeCode == NativeConsumablePolicy.SpellTypeArea || facts.SpellTypeCode == NativeConsumablePolicy.SpellTypePointBlankArea;
                facts.PetSummon = GetRef(e, effect, "PetToSummon") != null || facts.SpellTypeCode == 7;
                facts.CharmTarget = GetBool(e, effect, "CharmTarget") == true;
                facts.CrowdControl = GetBool(e, effect, "CrowdControlSpell") == true || GetBool(e, effect, "RootTarget") == true ||
                    GetBool(e, effect, "StunTarget") == true || GetBool(e, effect, "FearTarget") == true;
                facts.HasStatusEffect = GetRef(e, effect, "StatusEffectToApply") != null;
            }
            return facts;
        }

        private static bool ApplyConsumableSemantics(object item, object donor, ExpandedItemDefinition definition)
        {
            if (item == null || donor == null || definition == null || definition.Kind != ExpandedItemKind.Consumable) return false;
            NativeConsumableFacts donorFacts = ReadConsumableFacts(donor);
            if (!NativeConsumablePolicy.MatchesFamily(donorFacts, definition.ConsumableFamily) ||
                !NativeConsumablePolicy.IsProgressionCompatible(donorFacts, definition.TargetItemLevel)) return false;
            Type t = item.GetType(); Type donorType = donor.GetType();
            object donorEffect = GetRef(donorType, donor, "ItemEffectOnClick");
            object donorIcon = GetRef(donorType, donor, "ItemIcon");
            object donorCastTime = GetMemberValue(donorType, donor, "SpellCastTime");

            // Presentation/economy fields may differ. Native use semantics stay donor-exact.
            SetField(item, "ItemValue", definition.Value);
            SetField(item, "Stackable", donorFacts.Stackable); SetField(item, "Disposable", donorFacts.Disposable);
            SetField(item, "MustBeEquippedToClick", donorFacts.MustBeEquippedToClick);
            SetField(item, "PlayerCannotSell", donorFacts.PlayerCannotSell); SetField(item, "NoTradeNoDestroy", donorFacts.NoTradeNoDestroy);
            SetField(item, "Unique", donorFacts.Unique); SetField(item, "Relic", donorFacts.Relic); SetField(item, "RareItem", donorFacts.RareItem);
            SetField(item, "SimPlayersCantGet", donorFacts.SimPlayersCantGet); SetField(item, "Template", false); SetField(item, "FuelSource", false);
            SetField(item, "ItemEffectOnClick", donorEffect); SetField(item, "ItemIcon", donorIcon); SetField(item, "SpellCastTime", donorCastTime);
            SetField(item, "AssignQuestOnRead", null); SetField(item, "CompleteOnRead", null);
            SetField(item, "TeachSpell", null); SetField(item, "TeachSkill", null);
            SetField(item, "Aura", null); SetField(item, "WornEffect", null); SetField(item, "WeaponProcOnHit", null);
            // Do not rewrite RequiredSlot/Classes: the safe donor is already a General-slot commodity,
            // and preserving the clone avoids inventing eligibility semantics.
            SetField(item, "WeaponDmg", 0); SetField(item, "HP", 0); SetField(item, "AC", 0); SetField(item, "Mana", 0);
            SetField(item, "Str", 0); SetField(item, "End", 0); SetField(item, "Dex", 0); SetField(item, "Agi", 0);
            SetField(item, "Int", 0); SetField(item, "Wis", 0); SetField(item, "Cha", 0); SetField(item, "Res", 0);
            NativeConsumableFacts actual = ReadConsumableFacts(item);
            return NativeConsumablePolicy.MatchesFamily(actual, definition.ConsumableFamily) && actual.ItemValue == definition.Value &&
                object.ReferenceEquals(GetRef(t, item, "ItemEffectOnClick"), donorEffect) && object.ReferenceEquals(GetRef(t, item, "ItemIcon"), donorIcon) &&
                System.Math.Abs(actual.ItemSpellCastTime - donorFacts.ItemSpellCastTime) < 0.0001;
        }

        internal static List<string> BuildConsumableReferenceLines(int maxNative)
        {
            List<string> result = new List<string>();
            try
            {
                result.Add("native-use=ItemIcon.UseConsumable -> CastSpell.StartSpell(ItemEffectOnClick, targetStats, Item.SpellCastTime) -> SpellVessel.ResolveSpell; Disposable decrements Quantity exactly once after StartSpell call");
                for (int i = 0; i < ExpandedContentItems.All.Count; i++)
                {
                    ExpandedItemDefinition d = ExpandedContentItems.All[i];
                    if (d.Kind != ExpandedItemKind.Consumable) continue;
                    object live = TryResolveCustomItem(d.Id);
                    CustomItemRegistrationOutcome outcome = CraftingExpandedItems.Outcome(d.Id);
                    CustomRecipeDefinition recipe = FindRecipeForOutput(d.Id);
                    bool recipeActive = recipe != null && ExpandedContentRecipeRegistry.IsRegisteredCurrentSession(recipe.TemplateItemId);
                    string state = outcome == null ? CraftingExpandedItems.State(d.Id).ToString() : outcome.State.ToString();
                    string fail = outcome == null ? CraftingExpandedItems.FailureReason(d.Id) : outcome.FailureReason;
                    string family = live == null ? d.ConsumableFamily.ToString() : DescribeConsumableFamily(ReadConsumableFacts(live));
                    string effect = live == null ? "unavailable" : DescribeConsumableEffect(live);
                    result.Add("custom=" + d.Name + "#" + d.Id + " donor=" + (live == null ? "(none)" : DescribeSemanticDonor(d.Id)) +
                        " nativeFamily=" + family + " effect={" + effect + "} consume=" + (live == null ? "unverified" : "native Disposable quantity-1") +
                        " registration=" + state + " recipe=" + (recipe == null ? "none" : (recipeActive ? "active" : "inactive")) +
                        (string.IsNullOrEmpty(fail) ? string.Empty : " fail={" + fail + "}"));
                }
            }
            catch (Exception ex) { result.Add("consumable probe failed " + ex.GetType().Name); }
            return result;
        }

        internal static List<string> BuildNativeConsumableReferenceLines(int maxNative)
        {
            List<string> result = new List<string>();
            try
            {
                object db = TryGetLiveItemDatabase();
                if (db == null) { result.Add("native consumables: live ItemDB unavailable"); return result; }
                ResolveReflectionOnce(db.GetType());
                IList items = _reflectionResolved ? _itemDbField.GetValue(db) as IList : null;
                if (items == null) { result.Add("native consumables: ItemDB collection unavailable"); return result; }
                int total = 0, click = 0, disposable = 0, stackable = 0, safe = 0;
                List<string> safeRows = new List<string>(); List<string> rejectedRows = new List<string>();
                foreach (object item in items)
                {
                    if (item == null || CraftingExpandedItemIds.IsInOwnedRange(ReadId(item))) continue;
                    total++;
                    NativeConsumableFacts facts = ReadConsumableFacts(item);
                    if (facts.HasClickEffect) click++; if (facts.Disposable) disposable++; if (facts.Stackable) stackable++;
                    bool familySafe = NativeConsumablePolicy.MatchesFamily(facts, NativeConsumableFamily.HealthRecovery) ||
                        NativeConsumablePolicy.MatchesFamily(facts, NativeConsumableFamily.ManaRecovery) || NativeConsumablePolicy.MatchesFamily(facts, NativeConsumableFamily.Utility);
                    if (familySafe) safe++;
                    if (!HasConsumableDiagnosticSignal(item, facts)) continue;
                    string reject = familySafe ? string.Empty : NativeConsumablePolicy.RejectionReason(facts);
                    if (!familySafe && string.IsNullOrEmpty(reject)) reject = facts.HasClickEffect ? "unsupported effect family" : "not a native commodity candidate";
                    string row = "native=" + ReadName(item) + "#" + ReadId(item) + " signals=" + DescribeConsumableSignals(facts) +
                        " family=" + DescribeConsumableFamily(facts) + " proof={" + NativeConsumablePolicy.RecoveryFamilyProof(facts) + "} effect={" + DescribeConsumableEffect(item) + "} consume=" +
                        (facts.Disposable ? "UseConsumable quantity-1" : "not Disposable") + " safeDonor=" + (familySafe ? "yes" : "no") +
                        (string.IsNullOrEmpty(reject) ? string.Empty : " reject={" + reject + "}");
                    (familySafe ? safeRows : rejectedRows).Add(row);
                }
                int shown = 0;
                for (int i = 0; i < safeRows.Count && shown < maxNative; i++, shown++) result.Add(safeRows[i]);
                for (int i = 0; i < rejectedRows.Count && shown < maxNative; i++, shown++) result.Add(rejectedRows[i]);
                result.Insert(0, "native ItemDB survey total=" + total + " clickEffect=" + click + " disposable=" + disposable + " stackable=" + stackable + " safeDonors=" + safe + " rows=" + shown + "/" + maxNative + " (safe donors first)");
            }
            catch (Exception ex) { result.Add("native consumable survey failed " + ex.GetType().Name); }
            return result;
        }

        private static CustomRecipeDefinition FindRecipeForOutput(string outputId)
        {
            IList<CustomRecipeDefinition> all = ExpandedContentRecipes.All;
            for (int i = 0; i < all.Count; i++) if (string.Equals(all[i].OutputItemId, outputId, StringComparison.Ordinal)) return all[i];
            return null;
        }

        private static bool HasConsumableDiagnosticSignal(object item, NativeConsumableFacts facts)
        {
            if (facts.HasClickEffect || facts.Disposable || facts.MustBeEquippedToClick) return true;
            string name = ReadName(item) ?? string.Empty;
            return name.IndexOf("potion", StringComparison.OrdinalIgnoreCase) >= 0 || name.IndexOf("tonic", StringComparison.OrdinalIgnoreCase) >= 0 ||
                name.IndexOf("elixir", StringComparison.OrdinalIgnoreCase) >= 0 || name.IndexOf("draught", StringComparison.OrdinalIgnoreCase) >= 0 ||
                name.IndexOf("food", StringComparison.OrdinalIgnoreCase) >= 0 || name.IndexOf("drink", StringComparison.OrdinalIgnoreCase) >= 0 ||
                name.IndexOf("provision", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string DescribeConsumableSignals(NativeConsumableFacts facts)
        {
            string text = facts.Stackable ? "stackable" : "single";
            if (facts.Disposable) text += ",disposable"; if (facts.HasClickEffect) text += ",click";
            if (facts.MustBeEquippedToClick) text += ",equip-to-click"; if (facts.HasStatusEffect) text += ",status";
            return text;
        }

        private static string DescribeSemanticDonor(string id)
        {
            string value;
            return !string.IsNullOrEmpty(id) && SemanticDonorById.TryGetValue(id, out value) ? value : "(unknown)";
        }

        private static string DescribeConsumableFamily(NativeConsumableFacts facts)
        {
            if (NativeConsumablePolicy.MatchesFamily(facts, NativeConsumableFamily.HealthRecovery)) return "HealthRecovery";
            if (NativeConsumablePolicy.MatchesFamily(facts, NativeConsumableFamily.ManaRecovery)) return "ManaRecovery";
            if (NativeConsumablePolicy.MatchesFamily(facts, NativeConsumableFamily.Utility)) return "Utility";
            return facts != null && facts.HasClickEffect ? "OtherClickEffect" : "None";
        }

        private static string DescribeConsumableEffect(object item)
        {
            NativeConsumableFacts facts = ReadConsumableFacts(item);
            if (!facts.HasClickEffect) return "none";
            string text = "spellType=" + facts.SpellTypeCode + " damageBranch=" + facts.DamageTypeCode;
            if (facts.DirectHp > 0.0) text += " HP=" + facts.DirectHp.ToString("0.###") + " source=Spell.HP";
            if (facts.FlatMana > 0.0) text += " mana=" + facts.FlatMana.ToString("0.###") + " source=Spell.Mana";
            if (facts.PercentManaRestoration > 0.0) text += " mana%=" + facts.PercentManaRestoration.ToString("0.###") + " source=Spell.PercentManaRestoration";
            if (facts.TargetHealing > 0.0) text += " targetHealing=" + facts.TargetHealing.ToString("0.###");
            if (facts.CasterHealing > 0.0) text += " casterHealing=" + facts.CasterHealing.ToString("0.###");
            if (facts.LevelScaledManaRestoration > 0.0) text += " manaScaled=" + facts.LevelScaledManaRestoration.ToString("0.###");
            if (facts.HasStatusEffect) text += " statusEffect";
            if (facts.Cooldown > 0.0) text += " cooldown=" + facts.Cooldown.ToString("0.###");
            text += " castTime=" + facts.ItemSpellCastTime.ToString("0.###") + " itemLevel=" + facts.ItemLevel + " effectLevel=" + facts.EffectRequiredLevel;
            if (facts.SelfOnly || facts.ApplyToCaster || facts.InflictOnSelf) text += " self";
            return text;
        }

        private static object FindSafeEquipmentDonor(object itemDatabaseInstance, ExpandedItemDefinition definition, out string selectionReason)
        {
            selectionReason = string.Empty;
            try
            {
                IList itemDb = _itemDbField.GetValue(itemDatabaseInstance) as IList;
                if (itemDb == null) { selectionReason = "live ItemDB unavailable"; return null; }
                object best = null; int bestLevel = int.MinValue; string bestName = string.Empty; string bestId = string.Empty;
                object nearestAbove = null; int nearestAboveLevel = int.MaxValue; string nearestAboveName = string.Empty; string nearestAboveId = string.Empty;
                foreach (object item in itemDb)
                {
                    if (item == null || CraftingExpandedItemIds.IsInOwnedRange(ReadId(item))) continue;
                    Type t = item.GetType();
                    if (GetBool(t, item, "Stackable") != false || GetBool(t, item, "Unique") != false || GetBool(t, item, "Template") != false || GetBool(t, item, "FuelSource") != false ||
                        GetBool(t, item, "Relic") != false || GetBool(t, item, "RareItem") != false || GetBool(t, item, "Disposable") != false || GetBool(t, item, "MustBeEquippedToClick") != false ||
                        GetBool(t, item, "PlayerCannotSell") != false || GetBool(t, item, "NoTradeNoDestroy") != false || GetBool(t, item, "SimPlayersCantGet") != false || GetInt(t, item, "ItemValue", 0) <= 0 ||
                        GetRef(t, item, "ItemIcon") == null || GetRef(t, item, "TeachSpell") != null || GetRef(t, item, "TeachSkill") != null || GetRef(t, item, "ItemEffectOnClick") != null ||
                        GetRef(t, item, "AssignQuestOnRead") != null || GetRef(t, item, "CompleteOnRead") != null || GetRef(t, item, "Aura") != null || GetRef(t, item, "WornEffect") != null || GetRef(t, item, "WeaponProcOnHit") != null ||
                        !IsFieldDefault(t, item, "ItemSkillUse") || !IsFieldDefault(t, item, "Mining"))
                        continue;
                    object slot = GetRef(t, item, "RequiredSlot");
                    string slotName = slot == null ? string.Empty : slot.ToString();
                    bool shield = GetBool(t, item, "Shield") == true;
                    object weapon = GetRef(t, item, "ThisWeaponType");
                    string weaponName = weapon == null ? string.Empty : weapon.ToString();
                    if (!ExpandedEquipmentDonorPolicy.MatchesFamily(slotName, weaponName, shield,
                        definition.RequiredSlot, definition.WeaponType, definition.RequireShield)) continue;
                    FieldInfo levelField = t.GetField("ItemLevel", AllInstance);
                    object levelRaw = levelField == null ? null : levelField.GetValue(item);
                    if (!(levelRaw is int)) continue;
                    int level = (int)levelRaw;
                    if (level < 1) continue;
                    string name = ReadName(item); string id = ReadId(item);
                    if (level <= definition.TargetItemLevel)
                    {
                        bool better = ExpandedEquipmentDonorPolicy.IsPreferred(level, name, id, best != null, bestLevel, bestName, bestId);
                        if (better) { best = item; bestLevel = level; bestName = name; bestId = id; }
                    }
                    else
                    {
                        bool nearer = ExpandedEquipmentDonorPolicy.IsPreferredFloorCandidate(level, name, id,
                            nearestAbove != null, nearestAboveLevel, nearestAboveName, nearestAboveId);
                        if (nearer) { nearestAbove = item; nearestAboveLevel = level; nearestAboveName = name; nearestAboveId = id; }
                    }
                }
                if (best == null)
                {
                    selectionReason = "no conservative native equipment donor at/below item level " + definition.TargetItemLevel + " for slot=" + definition.RequiredSlot +
                        (string.IsNullOrEmpty(definition.WeaponType) ? string.Empty : " weapon=" + definition.WeaponType) + (definition.RequireShield ? " shield=true" : string.Empty);
                    if (nearestAbove != null)
                    {
                        selectionReason += "; nearest safe family donor above target=" + nearestAboveName + "#" + nearestAboveId +
                            " itemLevel=" + nearestAboveLevel + " (retier evidence only; not auto-selected)";
                    }
                    else
                    {
                        selectionReason += "; no safe donor in that native family was found in live ItemDB";
                    }
                    return null;
                }
                selectionReason = "native equipment donor preserved at item level " + bestLevel;
                return best;
            }
            catch (Exception ex) { selectionReason = "equipment donor selection failed: " + ex.GetType().Name; return null; }
        }

        private static object CloneExpandedItem(object donor, ExpandedItemDefinition definition, out string failure)
        {
            failure = string.Empty;
            try
            {
                UnityEngine.Object source = donor as UnityEngine.Object;
                if (source == null) { failure = "native donor is not a Unity item"; return null; }
                UnityEngine.Object clone = UnityEngine.Object.Instantiate(source);
                clone.name = OwnershipNamePrefix + definition.Id;
                SetField(clone, "Id", definition.Id);
                SetField(clone, "ItemName", definition.Name);
                SetField(clone, "Lore", definition.Lore ?? string.Empty);
                if (definition.Kind == ExpandedItemKind.Consumable)
                {
                    if (!ApplyConsumableSemantics(clone, donor, definition))
                    {
                        UnityEngine.Object.Destroy(clone); failure = "expanded consumable native-use semantics could not be applied"; return null;
                    }
                }
                else
                {
                    ItemSemanticsDecision semantics = ExpandedSemantics(definition, donor);
                    if (!ApplyOrdinaryItemSemantics(clone, semantics))
                    {
                        UnityEngine.Object.Destroy(clone); failure = "expanded item ordinary inventory semantics could not be applied"; return null;
                    }
                }
                SetField(clone, "Template", false);
                SetField(clone, "FuelSource", false);
                if (!ReplaceWithEmptyList(clone, "TemplateIngredients") || !ReplaceWithEmptyList(clone, "TemplateRewards"))
                { UnityEngine.Object.Destroy(clone); failure = "expanded item recipe lists could not be neutralized"; return null; }
                if (definition.Kind == ExpandedItemKind.Material)
                {
                    ClearClassRestrictions(clone);
                    SetField(clone, "WeaponDmg", 0); SetField(clone, "HP", 0); SetField(clone, "AC", 0); SetField(clone, "Mana", 0);
                    SetField(clone, "Str", 0); SetField(clone, "End", 0); SetField(clone, "Dex", 0); SetField(clone, "Agi", 0);
                    SetField(clone, "Int", 0); SetField(clone, "Wis", 0); SetField(clone, "Cha", 0); SetField(clone, "Res", 0);
                    SetField(clone, "WeaponProcOnHit", null); SetField(clone, "ItemEffectOnClick", null); SetField(clone, "TeachSpell", null); SetField(clone, "TeachSkill", null);
                    SetField(clone, "Aura", null); SetField(clone, "WornEffect", null); SetField(clone, "WandEffect", null); SetField(clone, "BowEffect", null);
                    SetField(clone, "IsWand", false); SetField(clone, "IsBow", false); SetField(clone, "Relic", false); SetField(clone, "RareItem", false); SetField(clone, "Unique", false);
                }
                if (!string.Equals(ReadId(clone), definition.Id, StringComparison.Ordinal) || !string.Equals(ReadName(clone), definition.Name, StringComparison.Ordinal) ||
                    GetBool(clone.GetType(), clone, "Template") != false || GetBool(clone.GetType(), clone, "FuelSource") != false)
                { UnityEngine.Object.Destroy(clone); failure = "expanded item failed post-clone identity validation"; return null; }
                return clone;
            }
            catch (Exception ex) { failure = "expanded item clone failed: " + ex.GetType().Name; return null; }
        }

        private static object FindSafeBaseItem(object itemDatabaseInstance, CustomItemVisualKind visualKind, out string selectionReason)
        {
            selectionReason = string.Empty;
            try
            {
                IList itemDb = _itemDbField.GetValue(itemDatabaseInstance) as IList;
                if (itemDb == null)
                {
                    selectionReason = "live ItemDB unavailable";
                    return null;
                }

                object best = null;
                int bestScore = int.MinValue;
                string bestName = string.Empty;
                string bestId = string.Empty;

                foreach (object item in itemDb)
                {
                    if (item == null || !IsSafeBaseCandidate(item)) continue;
                    string name = ReadName(item);
                    int score = OrganicItemBasePolicy.ScoreName(name, visualKind);
                    if (score == int.MinValue) continue;

                    string id = ReadId(item);
                    bool better = score > bestScore;
                    if (!better && score == bestScore)
                    {
                        int byName = string.Compare(name, bestName, StringComparison.OrdinalIgnoreCase);
                        if (byName < 0 || (byName == 0 && string.Compare(id, bestId, StringComparison.Ordinal) < 0))
                            better = true;
                    }
                    if (!better) continue;

                    best = item;
                    bestScore = score;
                    bestName = name;
                    bestId = id;
                }

                if (best == null)
                {
                    selectionReason = "no safe native Item template matched " +
                        OrganicItemBasePolicy.EvidenceDescription(visualKind) +
                        "; unrelated plant/food/geological fallback refused";
                    return null;
                }

                selectionReason = "native visual template selected from live ItemDB kind=" + visualKind + " score=" + bestScore;
                return best;
            }
            catch (Exception ex)
            {
                selectionReason = "base selection failed: " + ex.GetType().Name;
                return null;
            }
        }

        // Every field checked here is a confirmed real Item field (see
        // NATIVE_CRAFTING_FINDINGS.md's Item member dump) - no guessed field names.
        private static bool IsSafeBaseCandidate(object item)
        {
            Type t = item.GetType();
            return
                GetBool(t, item, "Stackable") == true &&
                IsGeneralSlot(t, item) &&
                GetBool(t, item, "Unique") == false &&
                GetBool(t, item, "Template") == false &&
                GetBool(t, item, "FuelSource") == false &&
                IsListFieldEmpty(t, item, "TemplateIngredients") &&
                IsListFieldEmpty(t, item, "TemplateRewards") &&
                GetRef(t, item, "ItemIcon") != null &&
                GetRef(t, item, "TeachSpell") == null &&
                GetRef(t, item, "TeachSkill") == null &&
                GetRef(t, item, "ItemEffectOnClick") == null &&
                GetRef(t, item, "AssignQuestOnRead") == null &&
                GetRef(t, item, "CompleteOnRead") == null &&
                GetBool(t, item, "Disposable") == false &&
                GetBool(t, item, "MustBeEquippedToClick") == false &&
                GetRef(t, item, "WornEffect") == null &&
                GetRef(t, item, "Aura") == null;
        }

        private static bool IsGeneralSlot(Type t, object item)
        {
            try
            {
                FieldInfo f = t.GetField("RequiredSlot", AllInstance);
                object value = f == null ? null : f.GetValue(item);
                return value != null && value.ToString() == "General";
            }
            catch { return false; }
        }

        private static bool? GetBool(Type t, object instance, string name)
        {
            try
            {
                FieldInfo f = t.GetField(name, AllInstance);
                object value = f == null ? null : f.GetValue(instance);
                return value is bool ? (bool)value : (bool?)null;
            }
            catch { return null; }
        }

        private static object GetRef(Type t, object instance, string name)
        {
            try
            {
                FieldInfo f = t.GetField(name, AllInstance);
                return f == null ? null : f.GetValue(instance);
            }
            catch { return null; }
        }

        private static object GetMemberValue(Type t, object instance, string name)
        {
            if (t == null || instance == null || string.IsNullOrEmpty(name)) return null;
            try
            {
                FieldInfo f = t.GetField(name, AllInstance);
                if (f != null) return f.GetValue(instance);
                PropertyInfo p = t.GetProperty(name, AllInstance);
                if (p != null && p.GetIndexParameters().Length == 0) return p.GetValue(instance, null);
            }
            catch { }
            return null;
        }

        private static bool IsFieldDefault(Type t, object instance, string name)
        {
            try
            {
                FieldInfo f = t.GetField(name, AllInstance);
                if (f == null) return true;
                object value = f.GetValue(instance);
                if (value == null) return true;
                if (value is bool) return !(bool)value;
                if (value is int) return (int)value == 0;
                if (value is long) return (long)value == 0L;
                if (value is float) return System.Math.Abs((float)value) < 0.0001f;
                if (value is double) return System.Math.Abs((double)value) < 0.0001;
                if (value is string) return string.IsNullOrEmpty((string)value);
                if (value.GetType().IsEnum) return System.Convert.ToInt64(value) == 0L;
                IList list = value as IList;
                if (list != null) return list.Count == 0;
                return false;
            }
            catch { return false; }
        }

        private static bool IsListFieldEmpty(Type t, object instance, string name)
        {
            try
            {
                FieldInfo f = t.GetField(name, AllInstance);
                if (f == null) return false;
                object value = f.GetValue(instance);
                if (value == null) return true;
                IList list = value as IList;
                return list != null && list.Count == 0;
            }
            catch { return false; }
        }

        private static void SetField(object instance, string name, object value)
        {
            try
            {
                FieldInfo f = instance.GetType().GetField(name, AllInstance);
                if (f != null) f.SetValue(instance, value);
            }
            catch { }
        }

        private static int GetInt(Type t, object instance, string name, int fallback)
        {
            try
            {
                FieldInfo f = t.GetField(name, AllInstance);
                object value = f == null ? null : f.GetValue(instance);
                return value is int ? (int)value : fallback;
            }
            catch { return fallback; }
        }

        private static double GetNumber(Type t, object instance, string name)
        {
            try
            {
                FieldInfo f = t.GetField(name, AllInstance);
                object value = f == null ? null : f.GetValue(instance);
                if (value == null) return 0.0;
                return Convert.ToDouble(value);
            }
            catch { return 0.0; }
        }

        private static int GetEnumInt(Type t, object instance, string name, int fallback)
        {
            try
            {
                object value = GetMemberValue(t, instance, name);
                return value == null ? fallback : Convert.ToInt32(value);
            }
            catch { return fallback; }
        }

        private static bool ApplyOrdinaryItemSemantics(object item, ItemSemanticsDecision semantics)
        {
            if (item == null || semantics == null) return false;
            Type t = item.GetType();
            SetField(item, "ItemValue", semantics.ItemValue);
            SetField(item, "Stackable", semantics.Stackable);
            SetField(item, "PlayerCannotSell", semantics.PlayerCannotSell);
            SetField(item, "NoTradeNoDestroy", semantics.NoTradeNoDestroy);
            SetField(item, "Disposable", semantics.Disposable);
            SetField(item, "MustBeEquippedToClick", semantics.MustBeEquippedToClick);
            SetField(item, "Unique", semantics.Unique);
            SetField(item, "Relic", semantics.Relic);
            SetField(item, "RareItem", semantics.RareItem);
            SetField(item, "SimPlayersCantGet", semantics.SimPlayersCantGet);
            // Custom commodities/equipment are not quest-read objects. These are real native Item
            // fields and are cleared explicitly so a visual/equipment donor cannot leak quest state.
            SetField(item, "AssignQuestOnRead", null);
            SetField(item, "CompleteOnRead", null);

            bool? stackable = GetBool(t, item, "Stackable");
            bool? playerCannotSell = GetBool(t, item, "PlayerCannotSell");
            bool? noTradeNoDestroy = GetBool(t, item, "NoTradeNoDestroy");
            bool? disposable = GetBool(t, item, "Disposable");
            bool? mustEquip = GetBool(t, item, "MustBeEquippedToClick");
            bool? unique = GetBool(t, item, "Unique");
            bool? relic = GetBool(t, item, "Relic");
            bool? rare = GetBool(t, item, "RareItem");
            bool? simPlayersCantGet = GetBool(t, item, "SimPlayersCantGet");
            if (!stackable.HasValue || !playerCannotSell.HasValue || !noTradeNoDestroy.HasValue ||
                !disposable.HasValue || !mustEquip.HasValue || !unique.HasValue || !relic.HasValue || !rare.HasValue || !simPlayersCantGet.HasValue) return false;
            return ItemSemanticsPolicy.IsOrdinaryInventorySemantics(semantics, stackable.Value,
                playerCannotSell.Value, noTradeNoDestroy.Value, disposable.Value, mustEquip.Value,
                unique.Value, relic.Value, rare.Value, simPlayersCantGet.Value, GetInt(t, item, "ItemValue", -1)) &&
                GetRef(t, item, "AssignQuestOnRead") == null && GetRef(t, item, "CompleteOnRead") == null;
        }

        private static ItemSemanticsDecision ExpandedSemantics(ExpandedItemDefinition definition, object donor)
        {
            if (definition == null) return null;
            if (definition.Kind == ExpandedItemKind.Equipment)
            {
                if (donor == null) return null;
                int donorValue = GetInt(donor.GetType(), donor, "ItemValue", 0);
                if (donorValue <= 0) return null;
                return ItemSemanticsPolicy.ForFinishedEquipment(donorValue);
            }
            return string.Equals(definition.Category, "component", StringComparison.Ordinal)
                ? ItemSemanticsPolicy.ForProcessedComponent(definition.Value)
                : ItemSemanticsPolicy.ForRawResource(definition.Value);
        }

        // UnityEngine.Object.Instantiate produces an independent ScriptableObject copy - the
        // source asset is never mutated. See findings doc §5-6 for exactly which fields are
        // overridden vs. intentionally inherited.
        private static object CloneAndConfigure(object baseItem, CustomItemDefinition definition)
        {
            try
            {
                UnityEngine.Object source = baseItem as UnityEngine.Object;
                if (source == null) return null;
                UnityEngine.Object clone = UnityEngine.Object.Instantiate(source);
                clone.name = OwnershipNamePrefix + definition.Id;

                SetField(clone, "Id", definition.Id);
                SetField(clone, "ItemName", definition.Name);
                SetField(clone, "Lore", definition.Lore ?? string.Empty);
                ClearClassRestrictions(clone); // no class restriction - built with Classes' real generic List<Class> type
                ItemSemanticsDecision semantics = ItemSemanticsPolicy.ForRawResource(definition.Value);
                if (!ApplyOrdinaryItemSemantics(clone, semantics))
                {
                    UnityEngine.Object.Destroy(clone);
                    return null;
                }

                // Defense in depth: zero out anything that could grant combat/utility value even
                // though the base-item predicate should already exclude items with these set.
                SetField(clone, "WeaponDmg", 0); SetField(clone, "HP", 0); SetField(clone, "AC", 0); SetField(clone, "Mana", 0);
                SetField(clone, "Str", 0); SetField(clone, "End", 0); SetField(clone, "Dex", 0); SetField(clone, "Agi", 0);
                SetField(clone, "Int", 0); SetField(clone, "Wis", 0); SetField(clone, "Cha", 0); SetField(clone, "Res", 0);
                SetField(clone, "WeaponProcOnHit", null); SetField(clone, "ItemEffectOnClick", null);
                SetField(clone, "TeachSpell", null); SetField(clone, "TeachSkill", null);
                SetField(clone, "Aura", null); SetField(clone, "WornEffect", null);
                SetField(clone, "WandEffect", null); SetField(clone, "BowEffect", null);
                SetField(clone, "IsWand", false); SetField(clone, "IsBow", false);
                SetField(clone, "Relic", false); SetField(clone, "RareItem", false);
                SetField(clone, "Unique", false); SetField(clone, "SimPlayersCantGet", false);
                SetField(clone, "Template", false); SetField(clone, "FuelSource", false);
                if (!ReplaceWithEmptyList(clone, "TemplateIngredients") ||
                    !ReplaceWithEmptyList(clone, "TemplateRewards") ||
                    ReadId(clone) != definition.Id || ReadName(clone) != definition.Name ||
                    GetBool(clone.GetType(), clone, "Template") != false ||
                    GetBool(clone.GetType(), clone, "FuelSource") != false ||
                    !IsListFieldEmpty(clone.GetType(), clone, "TemplateIngredients") ||
                    !IsListFieldEmpty(clone.GetType(), clone, "TemplateRewards"))
                {
                    try { UnityEngine.Object.Destroy(clone); } catch { }
                    return null;
                }

                return clone;
            }
            catch { return null; }
        }

        private static void ClearClassRestrictions(object clone)
        {
            try
            {
                FieldInfo f = clone.GetType().GetField("Classes", AllInstance);
                if (f == null) return;
                object listInstance = Activator.CreateInstance(f.FieldType);
                f.SetValue(clone, listInstance);
            }
            catch { }
        }

        private static bool ReplaceWithEmptyList(object clone, string fieldName)
        {
            try
            {
                FieldInfo field = clone.GetType().GetField(fieldName, AllInstance);
                if (field == null) return false;
                object empty = Activator.CreateInstance(field.FieldType);
                field.SetValue(clone, empty);
                IList list = field.GetValue(clone) as IList;
                return list != null && list.Count == 0;
            }
            catch { return false; }
        }

        private static bool InsertIntoDatabase(object itemDatabaseInstance, object item)
        {
            Array oldArray = null;
            object oldList = null;
            IDictionary dict = null;
            string id = null;
            bool itemDbChanged = false;
            bool dictChanged = false;
            bool listChanged = false;
            try
            {
                oldArray = (Array)_itemDbField.GetValue(itemDatabaseInstance);
                if (oldArray == null) return false;
                dict = (IDictionary)_itemDictField.GetValue(itemDatabaseInstance);
                if (dict == null) return false;

                FieldInfo idField = item.GetType().GetField("Id", AllInstance);
                id = idField == null ? null : idField.GetValue(item) as string;
                if (string.IsNullOrEmpty(id) || dict.Contains(id)) return false;

                Type elementType = oldArray.GetType().GetElementType();
                Array newArray = Array.CreateInstance(elementType, oldArray.Length + 1);
                Array.Copy(oldArray, newArray, oldArray.Length);
                newArray.SetValue(item, oldArray.Length);

                object newList = null;
                if (_itemDbListField != null)
                {
                    oldList = _itemDbListField.GetValue(itemDatabaseInstance);
                    Type listType = _itemDbListField.FieldType;
                    newList = Activator.CreateInstance(listType, (object)newArray);
                }

                // Commit only after every object required for the new state has been built. If
                // any setter/add below throws, the catch block restores the previous structures.
                _itemDbField.SetValue(itemDatabaseInstance, newArray);
                itemDbChanged = true;
                dict.Add(id, item);
                dictChanged = true;
                if (_itemDbListField != null)
                {
                    _itemDbListField.SetValue(itemDatabaseInstance, newList);
                    listChanged = true;
                }
                return true;
            }
            catch
            {
                try { if (listChanged && _itemDbListField != null) _itemDbListField.SetValue(itemDatabaseInstance, oldList); } catch { }
                try { if (dictChanged && dict != null && id != null) dict.Remove(id); } catch { }
                try { if (itemDbChanged && oldArray != null) _itemDbField.SetValue(itemDatabaseInstance, oldArray); } catch { }
                return false;
            }
        }

        private static bool HasOwnershipMarker(object item, string id)
        {
            try
            {
                UnityEngine.Object unityItem = item as UnityEngine.Object;
                return unityItem != null && string.Equals(unityItem.name, OwnershipNamePrefix + id, StringComparison.Ordinal);
            }
            catch { return false; }
        }

        private static object TryGetExisting(object itemDatabaseInstance, string id)
        {
            try
            {
                MethodInfo method = itemDatabaseInstance.GetType().GetMethod("GetItemByID", AllInstance);
                object result = method == null ? null : method.Invoke(itemDatabaseInstance, new object[] { id });
                if (result == null) return null;
                // GetItemByID falls back to Inventory.Empty rather than null on a miss (see
                // findings doc §1) - treat that sentinel the same as "not found".
                object emptyItem = GetEmptyItemSentinel();
                return ReferenceEquals(result, emptyItem) ? null : result;
            }
            catch { return null; }
        }

        private static object GetEmptyItemSentinel()
        {
            try
            {
                object playerInv = GetStaticField("GameData", "PlayerInv");
                return playerInv == null ? null : GetRef(playerInv.GetType(), playerInv, "Empty");
            }
            catch { return null; }
        }

        private static string ReadId(object item)
        {
            try
            {
                FieldInfo f = item.GetType().GetField("Id", AllInstance);
                return f == null ? string.Empty : (f.GetValue(item) as string) ?? string.Empty;
            }
            catch { return string.Empty; }
        }

        private static string ReadName(object item)
        {
            try
            {
                FieldInfo f = item.GetType().GetField("ItemName", AllInstance);
                return f == null ? "(unknown)" : (f.GetValue(item) as string) ?? "(unknown)";
            }
            catch { return "(unknown)"; }
        }

        private static void ResolveReflectionOnce(Type itemDatabaseType)
        {
            if (_reflectionResolved) return;
            try
            {
                _itemDictField = itemDatabaseType.GetField("itemDict", AllInstance);
                _itemDbField = itemDatabaseType.GetField("ItemDB", AllInstance);
                _itemDbListField = itemDatabaseType.GetField("ItemDBList", AllInstance);
                _reflectionResolved = _itemDictField != null && _itemDbField != null;
            }
            catch { _reflectionResolved = false; }
        }

        private static MethodInfo FindMethod(Type declaringType, string name, params Type[] argTypes)
        {
            foreach (MethodInfo candidate in declaringType.GetMethods(AllInstance))
            {
                if (candidate.Name != name) continue;
                ParameterInfo[] parameters = candidate.GetParameters();
                if (parameters.Length != argTypes.Length) continue;
                bool match = true;
                for (int i = 0; i < parameters.Length; i++)
                    if (!parameters[i].ParameterType.IsAssignableFrom(argTypes[i])) { match = false; break; }
                if (match) return candidate;
            }
            return null;
        }

        private static object GetStaticField(string typeName, string fieldName)
        {
            Type type = FindType(typeName);
            if (type == null) return null;
            FieldInfo field = type.GetField(fieldName, AllStatic);
            return field == null ? null : field.GetValue(null);
        }

        private static Type FindType(string name)
        {
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
                try { Type type = assembly.GetType(name, false); if (type != null) return type; } catch { }
            return null;
        }
    }
}
