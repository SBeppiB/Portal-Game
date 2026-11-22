using UnityEngine;

namespace Portals.Interaction
{
    /// <summary>
    /// Lightweight metadata/compatibility flags for objects that can be scanned, picked
    /// up, dragged or stored. Keeps future identification mechanics decoupled from the
    /// interaction and inventory systems.
    /// </summary>
    [DisallowMultipleComponent]
    public class InteractableObject : MonoBehaviour
    {
        public enum MassCategory { Light, Medium, Heavy }

        public enum InteractionType { None, Pickup, Switch, Battery }

        [SerializeField] private string m_DisplayName = "Object";
        [SerializeField] private string m_Description = "";
        [SerializeField, Tooltip("Mass category used for future identification/puzzles.")]
        private MassCategory m_MassCategory = MassCategory.Medium;
        [SerializeField] private string m_UniverseOfOrigin = "";
        [SerializeField] private bool m_PickupCompatible = true;
        [SerializeField] private bool m_InventoryCompatible = true;
        [SerializeField] private InteractionType m_InteractionType = InteractionType.Pickup;
        [SerializeField, Min(0f), Tooltip("Maximum size (longest collider extent) allowed in inventory.")]
        private float m_MaxInventorySize = 1.5f;

        public string DisplayName => m_DisplayName;
        public string Description => m_Description;
        public MassCategory Mass => m_MassCategory;
        public string UniverseOfOrigin => m_UniverseOfOrigin;
        public bool PickupCompatible => m_PickupCompatible;
        public bool InventoryCompatible => m_InventoryCompatible;
        public InteractionType Type => m_InteractionType;
        public float MaxInventorySize => m_MaxInventorySize;

        /// <summary>Longest world-space extent of this object's colliders.</summary>
        public float SizeExtent
        {
            get
            {
                Collider[] colliders = GetComponentsInChildren<Collider>(true);
                float max = 0f;
                for (int i = 0; i < colliders.Length; i++)
                {
                    if (colliders[i] == null) continue;
                    Vector3 e = colliders[i].bounds.extents;
                    max = Mathf.Max(max, Mathf.Max(e.x, Mathf.Max(e.y, e.z)) * 2f);
                }
                return max;
            }
        }

        public bool FitsInInventory =>
            m_InventoryCompatible && SizeExtent <= m_MaxInventorySize + 0.001f;
    }
}
