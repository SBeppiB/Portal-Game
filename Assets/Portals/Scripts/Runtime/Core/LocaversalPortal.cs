using UnityEngine;

namespace Portals.Core
{
    /// <summary>
    /// One endpoint of a same-universe portal pair. Placement, traversal and energy
    /// are handled by PortalPlacer, PortalTraverser and PortalEnergySystem respectively.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LocaversalPortal : PortalEndpoint
    {
        [SerializeField, Tooltip("The other endpoint of this portal pair.")]
        private LocaversalPortal m_PairedPortal;

        public override PortalKind Kind => PortalKind.Locaversal;

        public LocaversalPortal PairedPortal => m_PairedPortal;

        public override PortalEndpoint GetLinkedEndpoint() =>
            m_PairedPortal != null && m_PairedPortal.IsActive ? m_PairedPortal : null;

        public override bool HasConfiguredLink => m_PairedPortal != null;
        public override PortalEndpoint ConfiguredPartner => m_PairedPortal;

        /// <summary>Creates a two-way pair. Silently ignores self-pairing.</summary>
        public void SetPair(LocaversalPortal other)
        {
            if (other == null || other == this) return;
            m_PairedPortal = other;
            other.m_PairedPortal = this;
        }

        public void ClearPair()
        {
            if (m_PairedPortal != null && m_PairedPortal.m_PairedPortal == this)
                m_PairedPortal.m_PairedPortal = null;
            m_PairedPortal = null;
        }

        private void OnEnable() => LocaversalPortalSystem.Register(this);

        private void OnDisable()
        {
            LocaversalPortalSystem.Unregister(this);
            ClearPair();
        }
    }
}
