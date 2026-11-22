using UnityEngine;

namespace Portals.Core
{
    /// <summary>
    /// Keeps a portal rigidly attached to the collider it was placed on, including
    /// moving and rotating surfaces. Uses local coordinates relative to the surface
    /// and writes the opening's WORLD pose each LateUpdate, so non-uniform surface
    /// scale cannot skew the portal's orientation (which parenting would do).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PortalSurfaceAnchor : MonoBehaviour
    {
        private PortalEndpoint m_Portal;
        private Transform m_Surface;
        private Vector3 m_LocalPosition;
        private Quaternion m_LocalRotation;
        private bool m_HasAnchor;

        public bool IsAnchored => m_HasAnchor && m_Surface != null;
        public Transform Surface => m_Surface;

        private void Awake()
        {
            m_Portal = GetComponent<PortalEndpoint>();
        }

        /// <summary>Stores the placement pose in the surface's local frame.</summary>
        public void Attach(Transform surface, Vector3 worldPosition, Quaternion worldRotation)
        {
            if (m_Portal == null) m_Portal = GetComponent<PortalEndpoint>();
            m_Surface = surface;
            m_HasAnchor = surface != null;

            if (!m_HasAnchor) return;

            m_LocalPosition = surface.InverseTransformPoint(worldPosition);
            m_LocalRotation = Quaternion.Inverse(surface.rotation) * worldRotation;
        }

        public void Detach()
        {
            m_HasAnchor = false;
            m_Surface = null;
        }

        private void LateUpdate()
        {
            UpdateAnchor();
        }

        /// <summary>Refreshes the world pose from the surface's current transform.</summary>
        public void UpdateAnchor()
        {
            if (!IsAnchored || m_Portal == null) return;

            Vector3 worldPosition = m_Surface.TransformPoint(m_LocalPosition);
            Quaternion worldRotation = m_Surface.rotation * m_LocalRotation;
            m_Portal.Opening.SetPositionAndRotation(worldPosition, worldRotation);
        }
    }
}
