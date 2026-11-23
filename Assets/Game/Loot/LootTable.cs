using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game
{
    [CreateAssetMenu(menuName = "Game/Loot Table", fileName = "LootTable_")]
    public class LootTable : ScriptableObject
    {
        [Serializable]
        public class Entry
        {
            public ResourceDefinition resource;
            [Min(1)] public int minAmount = 1;
            [Min(1)] public int maxAmount = 1;
            [Min(0f)] public float weight = 1f;
        }

        [SerializeField] private List<Entry> m_Entries = new List<Entry>();

        public IReadOnlyList<Entry> Entries => m_Entries;

        /// <summary>Rolls one or more entries and returns granted stacks.</summary>
        public List<ResourceStack> Roll(System.Random rng, int rolls = 1)
        {
            var result = new List<ResourceStack>();
            if (m_Entries.Count == 0 || rng == null) return result;

            float totalWeight = 0f;
            foreach (var e in m_Entries) if (e != null) totalWeight += Mathf.Max(0f, e.weight);
            if (totalWeight <= 0f) return result;

            for (int r = 0; r < Mathf.Max(1, rolls); r++)
            {
                float pick = (float)(rng.NextDouble() * totalWeight);
                float acc = 0f;
                foreach (var e in m_Entries)
                {
                    if (e == null || e.resource == null) continue;
                    acc += Mathf.Max(0f, e.weight);
                    if (pick <= acc)
                    {
                        int amount = rng.Next(e.minAmount, e.maxAmount + 1);
                        result.Add(new ResourceStack(e.resource, amount));
                        break;
                    }
                }
            }
            return result;
        }
    }
}