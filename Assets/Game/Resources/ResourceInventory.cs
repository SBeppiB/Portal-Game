using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Portals.Save;

namespace Game
{
    /// <summary>
    /// Simple resource inventory. Persists via the existing save-state registry.
    /// </summary>
    public class ResourceInventory : MonoBehaviour, IGameplaySaveState
    {
        [SerializeField] private ResourceDatabase m_Database;
        [SerializeField] private List<ResourceStack> m_Stacks = new List<ResourceStack>();

        private int m_Version;

        public IReadOnlyList<ResourceStack> Stacks => m_Stacks;

        /// <summary>
        /// Monotonic counter bumped whenever the inventory contents change. UI can
        /// compare this instead of rebuilding display strings every frame.
        /// </summary>
        public int StateVersion => m_Version;

        private void Bump() => m_Version++;

        /// <summary>
        /// Save key prefix scoped to this component's GameObject so multiple
        /// inventories (player carry + home storage) never overwrite each other.
        /// </summary>
        private string SavePrefix => "res:" + gameObject.name + ":";

        private void OnEnable() => SaveStateRegistry.Register(this);
        private void OnDisable() => SaveStateRegistry.Unregister(this);

        public int GetAmount(ResourceDefinition resource) =>
            GetAmount(resource != null ? resource.Id : string.Empty);

        public int GetAmount(string resourceId)
        {
            var stack = m_Stacks.FirstOrDefault(s => s != null && s.Id == resourceId);
            return stack != null ? stack.amount : 0;
        }

        public void Add(ResourceDefinition resource, int amount)
        {
            if (resource == null || amount <= 0) return;
            var stack = m_Stacks.FirstOrDefault(s => s != null && s.Id == resource.Id);
            if (stack == null)
            {
                stack = new ResourceStack(resource, 0);
                m_Stacks.Add(stack);
            }
            stack.amount += amount;
            Bump();
        }

        public bool Remove(ResourceDefinition resource, int amount)
        {
            if (resource == null || amount <= 0) return false;
            var stack = m_Stacks.FirstOrDefault(s => s != null && s.Id == resource.Id);
            if (stack == null || stack.amount < amount) return false;
            stack.amount -= amount;
            if (stack.amount <= 0) m_Stacks.Remove(stack);
            Bump();
            return true;
        }

        public bool Contains(ResourceDefinition resource, int amount) =>
            GetAmount(resource) >= amount;

        public void Clear()
        {
            if (m_Stacks.Count == 0) return;
            m_Stacks.Clear();
            Bump();
        }

        public void CaptureInto(SaveGameData data)
        {
            // Merge into the existing inventory list using a per-owner prefix so
            // different inventories keep separate entries.
            if (data.inventory == null) data.inventory = new List<InventoryItemData>();
            string prefix = SavePrefix;
            foreach (var stack in m_Stacks)
            {
                if (stack == null || stack.resource == null) continue;
                data.inventory.Add(new InventoryItemData
                {
                    itemId = prefix + stack.resource.Id,
                    quantity = stack.amount
                });
            }
        }

        public bool ApplyFrom(SaveGameData data)
        {
            if (data == null || data.inventory == null) return true;

            m_Stacks.Clear();
            string prefix = SavePrefix;
            foreach (var item in data.inventory)
            {
                if (item == null || string.IsNullOrEmpty(item.itemId)) continue;
                if (!item.itemId.StartsWith(prefix, System.StringComparison.Ordinal)) continue;

                string id = item.itemId.Substring(prefix.Length);
                if (string.IsNullOrEmpty(id)) continue;

                ResourceDefinition def = m_Database != null ? m_Database.GetById(id) : null;
                if (def == null) continue;

                m_Stacks.Add(new ResourceStack(def, item.quantity));
            }
            Bump();
            return true;
        }
    }
}