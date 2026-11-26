using System;
using System.Collections.Generic;
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

        /// <summary>Maps a portal's UniverseId to a UniverseCoordinate for content generation.</summary>
        [System.Serializable]
        public sealed class UniverseMapping
        {
            public UniverseId universeId;
            public UniverseCoordinate coordinate;
        }

        [SerializeField, Tooltip("Maps each interversal portal's UniverseId to its content coordinate.")]
        private List<UniverseMapping> m_UniverseMappings = new();

        private PortalTraverser m_Traverser;
        private readonly HashSet<PortalTraverser> m_SubscribedTraversers = new();
        private Dictionary<UniverseId, UniverseCoordinate> m_IdToCoord;

        private void Awake()
        {
            BuildMapping();
        }

        private void OnEnable()
        {
            Subscribe();
            BuildMapping();
        }

        private void OnDisable()
        {
            Unsubscribe();
            m_Traverser = null;
            m_SubscribedTraversers.Clear();
        }

        /// <summary>
        /// GameBootstrap can add this bridge before PortalSceneSetup has added the
        /// player's PortalTraverser, so keep retrying until the traverser exists.
        /// </summary>
        private void Update()
        {
            // Keep checking for newly created traversers (e.g. test-created players
            // that appear after the bridge's initial subscription). The HashSet guard
            // in Subscribe() prevents duplicate subscriptions.
            Subscribe();
        }

        private void Subscribe()
        {
            var traversers = FindObjectsByType<PortalTraverser>(FindObjectsSortMode.None);
            bool anySubscribed = false;
            foreach (var traverser in traversers)
            {
                if (traverser != null && m_SubscribedTraversers.Add(traverser))
                {
                    traverser.Traversed += OnTraversed;
                    anySubscribed = true;
                }
            }
            if (anySubscribed && traversers.Length > 0)
                m_Traverser = traversers[0];
        }

        private void Unsubscribe()
        {
            foreach (var traverser in m_SubscribedTraversers)
            {
                if (traverser != null)
                    traverser.Traversed -= OnTraversed;
            }
            m_SubscribedTraversers.Clear();
            m_Traverser = null;
        }

        /// <summary>Programmatically add a UniverseId → UniverseCoordinate mapping.</summary>
        public void AddMapping(UniverseId universeId, UniverseCoordinate coordinate)
        {
            if (!universeId.IsValid) return;
            if (m_IdToCoord == null) m_IdToCoord = new Dictionary<UniverseId, UniverseCoordinate>();
            m_IdToCoord[universeId] = coordinate;
        }

        private void BuildMapping()
        {
            if (m_IdToCoord == null) m_IdToCoord = new Dictionary<UniverseId, UniverseCoordinate>();
            // Merge rather than replace so programmatically added mappings (via AddMapping)
            // survive inspector-driven rebuilds.
            foreach (var mapping in m_UniverseMappings)
            {
                if (mapping.universeId.IsValid && !m_IdToCoord.ContainsKey(mapping.universeId))
                    m_IdToCoord[mapping.universeId] = mapping.coordinate;
            }
        }

        private void OnTraversed(PortalTraversalInfo info)
        {
            if (m_UniverseManager == null || info.Exit == null) return;
            if (info.Exit is not InterversalPortal interversal) return;

            UniverseId destinationId = interversal.Universe;
            if (!destinationId.IsValid) return;

            // Map the portal's UniverseId to a content coordinate before entering.
            if (m_IdToCoord == null || !m_IdToCoord.TryGetValue(destinationId, out UniverseCoordinate coord)) return;

            m_UniverseManager.EnterUniverse(coord.ToString());
        }
    }
}