using UnityEngine;
using Portals.Traversal;
using Portals.Core;

namespace Game
{
    /// <summary>
    /// Hooks portal traversal to universe content management without modifying
    /// the portal engine. Subscribes to PortalTraverser.Traversed.
    /// </summary>
    public class PortalUniverseBridge : MonoBehaviour
    {
        [SerializeField] private UniverseManager m_UniverseManager;

        private PortalTraverser m_Traverser;

        private void OnEnable()
        {
            Subscribe();
        }

        private void OnDisable()
        {
            Unsubscribe();
            m_Traverser = null;
        }

        /// <summary>
        /// GameBootstrap can add this bridge before PortalSceneSetup has added the
        /// player's PortalTraverser, so keep retrying until the traverser exists.
        /// </summary>
        private void Update()
        {
            if (m_Traverser != null) return;
            Subscribe();
        }

        private void Subscribe()
        {
            if (m_Traverser != null) return;
            m_Traverser = FindFirstObjectByType<PortalTraverser>();
            if (m_Traverser != null) m_Traverser.Traversed += OnTraversed;
        }

        private void Unsubscribe()
        {
            if (m_Traverser != null) m_Traverser.Traversed -= OnTraversed;
        }

        private void OnTraversed(PortalTraversalInfo info)
        {
            if (m_UniverseManager == null || info.Exit == null) return;
            if (info.Exit is not InterversalPortal interversal) return;

            m_UniverseManager.EnterUniverse(interversal.Universe.ToString());
        }
    }
}