using Portals.Core;
using UnityEngine;

namespace Portals.Integration
{
    /// <summary>
    /// Manages the <see cref="PortalWindowRenderer"/> lifecycle for a single portal endpoint.
    /// Ensures a renderer is created and configured when the endpoint is active, disabled
    /// when the endpoint closes, and reconfigured when a pooled endpoint is reused for a new
    /// pair. Prevents stale RenderTexture or camera references across portal reuse.
    /// </summary>
    [RequireComponent(typeof(PortalEndpoint))]
    [RequireComponent(typeof(PortalVisual))]
    [DisallowMultipleComponent]
    public sealed class PortalWindowIntegration : MonoBehaviour
    {
        private PortalEndpoint m_Endpoint;
        private PortalWindowRenderer m_Renderer;
        private bool m_Subscribed;

        public PortalWindowRenderer Renderer => m_Renderer;

        private void Awake()
        {
            m_Endpoint = GetComponent<PortalEndpoint>();
            Subscribe();
            if (m_Endpoint.IsActive)
                EnsureRenderer();
        }

        private void OnDestroy()
        {
            Unsubscribe();
            if (m_Renderer != null)
                Destroy(m_Renderer);
        }

        private void Subscribe()
        {
            if (m_Subscribed || m_Endpoint == null) return;
            m_Endpoint.ActiveChanged += OnEndpointActiveChanged;
            m_Subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!m_Subscribed || m_Endpoint == null) return;
            m_Endpoint.ActiveChanged -= OnEndpointActiveChanged;
            m_Subscribed = false;
        }

        private void OnEndpointActiveChanged(PortalEndpoint endpoint)
        {
            if (endpoint == null) return;
            if (endpoint.IsActive)
                EnsureRenderer();
            else
                CleanupRenderer();
        }

        /// <summary>
        /// Reconfigures the renderer for a new linked destination. Called when a pooled
        /// endpoint is reused for a different portal pair, ensuring no stale destination
        /// references remain.
        /// </summary>
        public void Reconfigure()
        {
            CleanupRenderer();
            if (m_Endpoint.IsActive)
                EnsureRenderer();
        }

        private void EnsureRenderer()
        {
            if (m_Endpoint == null) return;

            var linked = m_Endpoint.ConfiguredPartner;
            if (linked == null) return;

            m_Renderer = GetComponent<PortalWindowRenderer>();
            if (m_Renderer == null)
                m_Renderer = gameObject.AddComponent<PortalWindowRenderer>();

            m_Renderer.Configure(m_Endpoint, linked);
            m_Renderer.enabled = true;
        }

        private void CleanupRenderer()
        {
            if (m_Renderer == null) return;

            m_Renderer.enabled = false;

            // Reset _HasPortalView so the swirl fallback shows when not rendering.
            var visual = GetComponent<PortalVisual>();
            if (visual != null && visual.SurfaceObject != null)
            {
                var renderer = visual.SurfaceObject.GetComponent<MeshRenderer>();
                if (renderer != null && renderer.sharedMaterial != null)
                    renderer.sharedMaterial.SetFloat("_HasPortalView", 0f);
            }
        }
    }
}
