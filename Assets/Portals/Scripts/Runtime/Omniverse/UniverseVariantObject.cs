using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Omniverse
{
    /// <summary>
    /// An object that has different variants per universe, sharing a stable identity.
    /// Example: Prime has no cube, Ring 1 has weighted cube, Ring 6 has anti-grav cube.
    /// </summary>
    public class UniverseVariantObject : MonoBehaviour
    {
        [Serializable]
        public class Variant
        {
            public string universeId; // coordinate string or "*" for all
            public GameObject prefab;
        }

        [SerializeField] private string m_SharedId;
        [SerializeField] private List<Variant> m_Variants = new List<Variant>();

        public string SharedId => m_SharedId;

        public GameObject GetVariantPrefab(string universeId)
        {
            foreach (var v in m_Variants)
            {
                if (v == null) continue;
                if (v.universeId == "*" || v.universeId == universeId) return v.prefab;
            }
            return null;
        }
    }
}