using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ErenshorCraftingExpanded
{
    internal static class ExpandedContentRecipeRegistry
    {
        private static readonly HashSet<string> Active = new HashSet<string>(StringComparer.Ordinal);
        private static readonly HashSet<string> Identities = new HashSet<string>(StringComparer.Ordinal);
        private static object _lastDb;
        private static float _nextAttempt;
        private static string _lastFailure = string.Empty;
        internal static string LastFailure { get { return _lastFailure; } }
        internal static int ActiveCount { get { return Active.Count; } }
        internal static int IdentityCount { get { return Identities.Count; } }

        internal static void BeginSession()
        {
            Active.Clear(); Identities.Clear(); _lastDb = null; _nextAttempt = 0f; _lastFailure = string.Empty;
        }

        internal static bool IsRegisteredCurrentSession(string templateId) { return templateId != null && Active.Contains(templateId); }

        internal static void TryRegisterIdentities(object itemDatabaseInstance)
        {
            if (itemDatabaseInstance == null) return;
            _lastDb = itemDatabaseInstance;
            object donor = GameNativeRecipeRegistryApi.FindExpandedIdentityDonor(itemDatabaseInstance);
            if (donor == null) { _lastFailure = "no conservative native Template donor available for expanded identities"; return; }
            IList<CustomRecipeDefinition> recipes = ExpandedContentRecipes.All;
            for (int i = 0; i < recipes.Count; i++) EnsureIdentity(itemDatabaseInstance, donor, recipes[i]);
        }

        internal static void Tick(bool enabled)
        {
            object liveDb = GameItemRegistryApi.TryGetLiveItemDatabase();
            if (liveDb == null) return;
            if (_lastDb == null || !ReferenceEquals(_lastDb, liveDb))
            {
                Active.Clear();
                Identities.Clear();
                _lastDb = liveDb;
                _nextAttempt = 0f;
                TryRegisterIdentities(liveDb);
            }
            if (!enabled)
            {
                DeactivateAll();
                return;
            }
            if (Time.unscaledTime < _nextAttempt || Active.Count == ExpandedContentRecipes.All.Count) return;
            _nextAttempt = Time.unscaledTime + 1.0f;
            if (GameNativeRecipeRegistryApi.ReadLiveComponentSlotCapacity() <= 0) { _lastFailure = "waiting for a live forge component shape"; return; }
            if (Identities.Count < ExpandedContentRecipes.All.Count) TryRegisterIdentities(_lastDb);
            ActivateAvailable();
        }

        private static bool EnsureIdentity(object db, object donor, CustomRecipeDefinition recipe)
        {
            object existing = GameItemRegistryApi.TryGetLiveItem(db, recipe.TemplateItemId);
            if (existing != null)
            {
                if (!GameItemRegistryApi.HasOwnedMarker(existing, recipe.TemplateItemId)) { _lastFailure = "recipe template id collision: " + recipe.TemplateItemId; return false; }
                string neutralFailure; GameNativeRecipeRegistryApi.ConfigureOwnedExpandedRecipe(existing, recipe, new ArrayList(), null, false, out neutralFailure);
                object rebound; if (!GameItemRegistryApi.TryInsertOwnedItem(db, recipe.TemplateItemId, existing, out rebound)) { _lastFailure = "recipe identity rebind failed: " + recipe.TemplateItemId; return false; }
                GameItemRegistryApi.TryApplyRecipeTemplateSafety(recipe.TemplateItemId, recipe.DisplayName); Identities.Add(recipe.TemplateItemId); Active.Remove(recipe.TemplateItemId); return true;
            }
            string failure; object clone = GameNativeRecipeRegistryApi.CloneExpandedTemplateIdentity(donor, recipe, out failure);
            if (clone == null) { _lastFailure = failure; return false; }
            object live;
            if (!GameItemRegistryApi.TryInsertOwnedItem(db, recipe.TemplateItemId, clone, out live)) { DestroyClone(clone); _lastFailure = "expanded recipe identity insertion rejected: " + recipe.TemplateItemId; return false; }
            if (live != clone) DestroyClone(clone);
            GameItemRegistryApi.TryApplyRecipeTemplateSafety(recipe.TemplateItemId, recipe.DisplayName); Identities.Add(recipe.TemplateItemId); return true;
        }

        private static void ActivateAvailable()
        {
            IList<CustomRecipeDefinition> recipes = ExpandedContentRecipes.All;
            int slots = GameNativeRecipeRegistryApi.ReadLiveComponentSlotCapacity();
            for (int i = 0; i < recipes.Count; i++)
            {
                CustomRecipeDefinition recipe = recipes[i];
                if (!Identities.Contains(recipe.TemplateItemId)) continue;
                if (recipe.Ingredients.Count > slots) { _lastFailure = "live forge cannot fit distinct ingredients for " + recipe.DisplayName; continue; }
                object output = ResolveItem(recipe.OutputItemId);
                if (output == null) { _lastFailure = "output unavailable for " + recipe.DisplayName; continue; }
                ArrayList ingredients = new ArrayList(); bool missing = false;
                for (int j = 0; j < recipe.Ingredients.Count; j++)
                {
                    object ingredient = ResolveItem(recipe.Ingredients[j].ItemId);
                    if (ingredient == null) { missing = true; break; }
                    for (int q = 0; q < recipe.Ingredients[j].Quantity; q++) ingredients.Add(ingredient);
                }
                if (missing) { _lastFailure = "ingredient unavailable for " + recipe.DisplayName; continue; }
                object template = GameItemRegistryApi.TryGetLiveItem(_lastDb, recipe.TemplateItemId);
                string failure;
                if (!GameNativeRecipeRegistryApi.ConfigureOwnedExpandedRecipe(template, recipe, ingredients, output, true, out failure)) { _lastFailure = failure; continue; }
                if (!GameItemRegistryApi.TryApplyRecipeTemplateSafety(recipe.TemplateItemId, recipe.DisplayName)) { _lastFailure = "template safety failed for " + recipe.DisplayName; continue; }
                Active.Add(recipe.TemplateItemId);
                CustomRecipeRejectReason add = CraftingRecipeCatalog.Production.Get(recipe.RecipeKey) != null
                    ? CustomRecipeRejectReason.DuplicateRecipeKey
                    : CraftingRecipeCatalog.TryAddRuntimeProduction(recipe);
                if (add != CustomRecipeRejectReason.None && add != CustomRecipeRejectReason.DuplicateRecipeKey && add != CustomRecipeRejectReason.DuplicateTemplateItemId)
                    _lastFailure = "recipe catalog rejected " + recipe.DisplayName + ": " + add;
            }
        }

        private static object ResolveItem(string id)
        {
            object item = GameItemRegistryApi.TryResolveCustomItem(id);
            if (item != null) return item;
            return _lastDb == null ? null : GameItemRegistryApi.TryGetLiveItem(_lastDb, id);
        }

        private static void DeactivateAll()
        {
            if (_lastDb == null) { Active.Clear(); return; }
            List<string> ids = new List<string>(Active);
            for (int i = 0; i < ids.Count; i++)
            {
                CustomRecipeDefinition recipe = ExpandedContentRecipes.All == null ? null : FindRecipe(ids[i]);
                object item = GameItemRegistryApi.TryGetLiveItem(_lastDb, ids[i]);
                if (item != null && recipe != null)
                {
                    string failure; GameNativeRecipeRegistryApi.ConfigureOwnedExpandedRecipe(item, recipe, new ArrayList(), null, false, out failure);
                }
            }
            Active.Clear();
        }

        private static CustomRecipeDefinition FindRecipe(string templateId)
        {
            IList<CustomRecipeDefinition> all = ExpandedContentRecipes.All;
            for (int i = 0; i < all.Count; i++) if (string.Equals(all[i].TemplateItemId, templateId, StringComparison.Ordinal)) return all[i];
            return null;
        }

        private static void DestroyClone(object value)
        {
            try { UnityEngine.Object o = value as UnityEngine.Object; if (o != null) UnityEngine.Object.Destroy(o); } catch { }
        }
    }

    internal static class CraftingRecipeRuntimeRegistry
    {
        internal static bool IsRegisteredCurrentSession(string templateItemId)
        {
            return ExpandedContentRecipeRegistry.IsRegisteredCurrentSession(templateItemId) || ProductionNativeRecipeRegistry.IsRegisteredCurrentSession(templateItemId);
        }
    }
}
