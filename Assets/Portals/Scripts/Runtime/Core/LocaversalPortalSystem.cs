using System.Collections.Generic;
using UnityEngine;

namespace Portals.Core
{
    /// <summary>
    /// Registry and integrity checker for locaversal (same-universe) portal pairs.
    /// Registration is static so endpoints work even if no manager object is present;
    /// the optional MonoBehaviour only performs periodic health checks.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LocaversalPortalSystem : MonoBehaviour
    {
        private static readonly HashSet<LocaversalPortal> s_Portals = new HashSet<LocaversalPortal>();

        public static LocaversalPortalSystem Instance { get; private set; }

        public static int RegisteredPortalCount => s_Portals.Count;

        public static IEnumerable<PortalEndpoint> AllPortals
        {
            get
            {
                foreach (LocaversalPortal portal in s_Portals)
                    if (portal != null) yield return portal;
            }
        }

        [SerializeField, Tooltip("Log a warning when a registered portal has no valid pair.")]
        private bool m_ValidatePairs = true;

        [SerializeField, Min(0.1f)] private float m_ValidationInterval = 2f;

        private float m_NextValidation;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public static void Register(LocaversalPortal portal)
        {
            if (portal != null) s_Portals.Add(portal);
        }

        public static void Unregister(LocaversalPortal portal)
        {
            if (portal != null) s_Portals.Remove(portal);
        }

        /// <summary>Finds a usable exit for a locaversal entrance, or null.</summary>
        public static PortalEndpoint TryGetExit(PortalEndpoint entrance)
        {
            return entrance is LocaversalPortal locaversal ? locaversal.GetLinkedEndpoint() : null;
        }

        public static bool IsRegistered(LocaversalPortal portal) => portal != null && s_Portals.Contains(portal);

        private void Update()
        {
            if (!m_ValidatePairs || Time.time < m_NextValidation) return;
            m_NextValidation = Time.time + m_ValidationInterval;

            foreach (LocaversalPortal portal in s_Portals)
            {
                if (portal == null || !portal.IsActive) continue;
                if (portal.GetLinkedEndpoint() == null)
                {
                    Debug.LogWarning($"[LocaversalPortalSystem] Portal '{portal.name}' has no active pair.", portal);
                }
            }
        }
    }
}
