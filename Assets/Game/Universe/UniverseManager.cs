using System.Collections.Generic;
using UnityEngine;
using Portals.Save;

namespace Game
{
    /// <summary>
    /// Manages alternate-universe content roots. Physics is never changed here;
    /// only content GameObjects are activated/deactivated and generated.
    ///
    /// Universe roots are cached instead of destroyed when switching universes,
    /// so revisiting a universe restores the same generated content instances.
    /// Cached roots are bounded to <see cref="m_MaxCachedRoots"/> to control memory.
    ///
    /// Only one universe's content is kept active at a time. Harvested nodes are
    /// remembered by stable spawn key so they stay harvested across switches and
    /// save/load cycles.
    /// </summary>
    public class UniverseManager : MonoBehaviour, IGameplaySaveState
    {
        [SerializeField] private UniverseContentGenerator m_Generator;
        [SerializeField] private HomeBaseManager m_HomeBase;
        [SerializeField] private Transform m_ContentParent;

        /// <summary>
        /// Maximum number of cached universe roots. Inactive roots are evicted
        /// (oldest first) when this limit is exceeded. The active root is never
        /// evicted. Set to 8 for the playable slice; revisiting a universe beyond
        /// this limit regenerates its content deterministically.
        /// </summary>
        private const int m_MaxCachedRoots = 8;

        private readonly Dictionary<string, GameObject> m_Roots = new Dictionary<string, GameObject>();
        private readonly HashSet<string> m_HarvestedNodes = new HashSet<string>();
        private GameObject m_ActiveRoot;
        private UniverseCoordinate m_Current = UniverseCoordinate.Prime;

        public UniverseCoordinate Current => m_Current;

        public void EnterUniverse(string universeId)
        {
            if (!UniverseCoordinate.TryParse(universeId, out var coord))
            {
                // Not a coordinate-style universe id; ignore (normal dimension).
                return;
            }

            if (coord == m_Current && m_ActiveRoot != null) return;

            string key = coord.ToString();

            // Reuse a cached root if one exists for this coordinate.
            if (m_Roots.TryGetValue(key, out GameObject existing) && existing != null)
            {
                DeactivateAllRootsExcept(key);
                existing.SetActive(true);
                ApplyHarvestedState(existing);

                m_Current = coord;
                m_ActiveRoot = existing;
                if (m_HomeBase != null) m_HomeBase.Discover(coord);
                return;
            }

            // No cached root: deactivate all current roots (keep them cached),
            // evict inactive roots if at capacity, then generate fresh content.
            DeactivateAllRoots();
            EvictInactiveRoots();

            GameObject target = new GameObject($"Universe_{coord}");
            if (m_ContentParent != null) target.transform.SetParent(m_ContentParent, false);
            m_Roots[key] = target;

            if (m_Generator != null) m_Generator.Generate(coord, target.transform);

            ApplyHarvestedState(target);

            m_Current = coord;
            m_ActiveRoot = target;
            m_ActiveRoot.SetActive(true);

            if (m_HomeBase != null) m_HomeBase.Discover(coord);
        }

        /// <summary>Deactivates every cached root without destroying it.</summary>
        private void DeactivateAllRoots()
        {
            foreach (var pair in m_Roots)
            {
                if (pair.Value == null) continue;
                pair.Value.SetActive(false);
            }
        }

        /// <summary>
        /// Deactivates every cached root except the one identified by <paramref name="keepKey"/>.
        /// </summary>
        private void DeactivateAllRootsExcept(string keepKey)
        {
            foreach (var pair in m_Roots)
            {
                if (pair.Key == keepKey || pair.Value == null) continue;
                pair.Value.SetActive(false);
            }
        }

        /// <summary>
        /// Evicts inactive cached roots (oldest first) when the cache exceeds
        /// <see cref="m_MaxCachedRoots"/>, making room for new roots. The active root
        /// is never evicted.
        /// </summary>
        private void EvictInactiveRoots()
        {
            while (m_Roots.Count >= m_MaxCachedRoots)
            {
                string evictKey = null;
                foreach (var pair in m_Roots)
                {
                    if (pair.Value == m_ActiveRoot || pair.Value == null) continue;
                    evictKey = pair.Key;
                    break;
                }

                if (evictKey == null) break; // Nothing safe to evict.

                if (m_Roots.TryGetValue(evictKey, out GameObject root) && root != null)
                {
                    Destroy(root);
                }
                m_Roots.Remove(evictKey);
            }
        }

        /// <summary>
        /// Explicitly destroys all cached roots and clears the cache. Used for
        /// scene cleanup and save/load transitions where a clean slate is needed.
        /// </summary>
        public void CleanupAllRoots()
        {
            foreach (var pair in m_Roots)
            {
                if (pair.Value == null) continue;
                foreach (ResourceNode node in pair.Value.GetComponentsInChildren<ResourceNode>(true))
                    node.Harvested -= OnNodeHarvested;
                Destroy(pair.Value);
            }
            m_Roots.Clear();
            m_ActiveRoot = null;
        }

        private void ApplyHarvestedState(GameObject root)
        {
            if (root == null) return;

            foreach (ResourceNode node in root.GetComponentsInChildren<ResourceNode>(true))
            {
                node.Harvested -= OnNodeHarvested;
                node.Harvested += OnNodeHarvested;
                node.RestoreHarvested(m_HarvestedNodes.Contains(node.SpawnKey));
            }
        }

        private void OnNodeHarvested(string key)
        {
            if (!string.IsNullOrEmpty(key)) m_HarvestedNodes.Add(key);
        }

        private void OnEnable()
        {
            SaveStateRegistry.Register(this);
        }

        private void OnDisable()
        {
            SaveStateRegistry.Unregister(this);
        }

        private void OnDestroy()
        {
            CleanupAllRoots();
        }

        public void CaptureInto(SaveGameData data)
        {
            if (data.progression == null) data.progression = new ProgressionStateData();
            data.progression.currentUniverse = m_Current.ToString();

            if (data.world == null) data.world = new WorldStateData();
            if (data.world.harvestedNodes == null) data.world.harvestedNodes = new List<string>();
            data.world.harvestedNodes.Clear();
            foreach (string key in m_HarvestedNodes)
                data.world.harvestedNodes.Add(key);
        }

        public bool ApplyFrom(SaveGameData data)
        {
            if (data == null || data.progression == null) return true;

            m_HarvestedNodes.Clear();
            if (data.world != null && data.world.harvestedNodes != null)
            {
                foreach (string key in data.world.harvestedNodes)
                    if (!string.IsNullOrEmpty(key)) m_HarvestedNodes.Add(key);
            }

            string saved = data.progression.currentUniverse;
            if (!string.IsNullOrEmpty(saved) && UniverseCoordinate.TryParse(saved, out UniverseCoordinate coord))
            {
                if (coord == m_Current && m_ActiveRoot != null)
                {
                    // Same scene: roots still exist, just re-apply harvested state.
                    ApplyHarvestedState(m_ActiveRoot);
                }
                else
                {
                    // EnterUniverse will reactivate the cached root or generate fresh content.
                    EnterUniverse(saved);
                }
            }
            else if (m_ActiveRoot == null)
            {
                // Old save without a universe: make sure the starting world has content.
                EnterUniverse(UniverseCoordinate.Prime.ToString());
            }

            return true;
        }
    }
}
