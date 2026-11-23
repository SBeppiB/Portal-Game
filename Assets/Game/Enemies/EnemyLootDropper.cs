using System.Collections.Generic;
using UnityEngine;

namespace Game
{
    /// <summary>Drops loot from a LootTable into an inventory (or logs if none assigned).</summary>
    public class EnemyLootDropper : MonoBehaviour
    {
        [SerializeField] private LootTable m_LootTable;
        [SerializeField] private ResourceInventory m_TargetInventory;
        [SerializeField, Min(1)] private int m_Rolls = 1;

        public void DropLoot(int ring)
        {
            if (m_LootTable == null) return;

            var rng = new System.Random(ring * 997 + gameObject.GetInstanceID());
            List<ResourceStack> drops = m_LootTable.Roll(rng, m_Rolls);

            if (m_TargetInventory == null)
            {
                foreach (var d in drops)
                    Debug.Log($"[EnemyLootDropper] {name} dropped {d.amount}x {d.resource.DisplayName}.");
                return;
            }

            foreach (var d in drops) m_TargetInventory.Add(d.resource, d.amount);
        }
    }
}