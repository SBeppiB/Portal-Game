using UnityEngine;

namespace Game.Omniverse
{
    /// <summary>Marker for Rift universes: no fixed gravity, unstable portals, fragmented geometry.</summary>
    public class RiftUniverse : MonoBehaviour
    {
        [SerializeField] private bool m_NoFixedGravity = true;
        [SerializeField] private bool m_UnstablePortals = true;

        public bool NoFixedGravity => m_NoFixedGravity;
        public bool UnstablePortals => m_UnstablePortals;
    }
}