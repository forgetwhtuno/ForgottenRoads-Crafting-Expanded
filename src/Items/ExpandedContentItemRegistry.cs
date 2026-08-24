using System;
using System.Collections.Generic;

namespace ErenshorCraftingExpanded
{
    public sealed class ExpandedItemRegistrationStatus
    {
        public string ItemId = string.Empty;
        public bool Available;
        public string DonorName = string.Empty;
        public string FailureReason = string.Empty;
    }

    internal static class ExpandedContentItemRegistry
    {
        private static readonly Dictionary<string, ExpandedItemRegistrationStatus> StatusById = new Dictionary<string, ExpandedItemRegistrationStatus>(StringComparer.Ordinal);
        private static bool _attempted;

        internal static int AvailableCount
        {
            get
            {
                int count = 0;
                foreach (KeyValuePair<string, ExpandedItemRegistrationStatus> pair in StatusById) if (pair.Value != null && pair.Value.Available) count++;
                return count;
            }
        }

        internal static int FailureCount
        {
            get
            {
                int count = 0;
                foreach (KeyValuePair<string, ExpandedItemRegistrationStatus> pair in StatusById)
                    if (pair.Value != null && !pair.Value.Available) count++;
                return count;
            }
        }

        internal static string DescribeFailures(int max)
        {
            if (max <= 0) max = 1;
            List<string> failures = new List<string>();
            foreach (KeyValuePair<string, ExpandedItemRegistrationStatus> pair in StatusById)
            {
                ExpandedItemRegistrationStatus status = pair.Value;
                if (status == null || status.Available) continue;
                failures.Add(pair.Key + "=" + (string.IsNullOrEmpty(status.FailureReason) ? "unavailable" : status.FailureReason));
                if (failures.Count >= max) break;
            }
            return failures.Count == 0 ? string.Empty : string.Join(" | ", failures.ToArray());
        }

        internal static void BeginSession() { _attempted = false; StatusById.Clear(); }

        internal static ExpandedItemRegistrationStatus Status(string id)
        {
            ExpandedItemRegistrationStatus status;
            return id != null && StatusById.TryGetValue(id, out status) ? status : null;
        }

        internal static bool IsAvailable(string id)
        {
            ExpandedItemRegistrationStatus status = Status(id);
            return status != null && status.Available && GameItemRegistryApi.IsCustomItemAvailable(id);
        }

        internal static bool TryRegister(object itemDatabaseInstance)
        {
            if (itemDatabaseInstance == null) return false;
            StatusById.Clear();
            IList<ExpandedItemDefinition> definitions = ExpandedContentItems.All;
            for (int i = 0; i < definitions.Count; i++)
            {
                ExpandedItemDefinition definition = definitions[i];
                ExpandedItemRegistrationStatus status = new ExpandedItemRegistrationStatus();
                status.ItemId = definition.Id;
                object live; string donor; string failure;
                status.Available = GameItemRegistryApi.TryRegisterExpandedItem(itemDatabaseInstance, definition, out live, out donor, out failure);
                status.DonorName = donor; status.FailureReason = failure;
                StatusById[definition.Id] = status;
            }
            _attempted = true;
            return true;
        }

        internal static bool EnsureRegisteredFromLiveDatabase()
        {
            if (_attempted) return true;
            object db = GameItemRegistryApi.TryGetLiveItemDatabase();
            return db != null && TryRegister(db);
        }
    }
}
