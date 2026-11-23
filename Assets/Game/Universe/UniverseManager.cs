using System.Collections.Generic;
using UnityEngine;
using Portals.Save;

namespace Game
{
    /// <summary>
    /// Manages alternate-universe content roots. Physics is never changed here;
    /// only content GameObjects are activated/deactivated and generated.
    ///
    /// Only one universe's content is kept alive at a time: switching destroys the
    /// other roots and regenerates deterministically when revisited. Harvested nodes
    /// are remembered by stable spawn key so they stay harvested across switches and
    /// save/load cycles.
    /// </summary>
    public class UniverseManager : MonoBehaviour, IGameplaySaveState
    {
        [SerializeField] private UniverseContentGenerator m_Generator;
        [SerializeField] private HomeBaseManager m_HomeBase;
        [SerializeField] private Transform m_ContentParent;

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
            GameObject target = m_Roots.TryGetValue(key, out GameObject existing) ? existing : null;

            if (target == null)
            {
                DestroyAllRoots();

                target = new GameObject($"Universe_{coord}");
                if (m_ContentParent != null) target.transform.SetParent(m_ContentParent, false);
                m_Roots[key] = target;

                if (m_Generator != null) m_Generator.Generate(coord, target.transform);
            }
            else
            {
                // Keep the cached root, discard every other universe's root.
                DestroyOtherRoots(key);
            }

            ApplyHarvestedState(target);

            m_Current = coord;
            m_ActiveRoot = target;
            m_ActiveRoot.SetActive(true);

            if (m_HomeBase != null) m_HomeBase.Discover(coord);
        }

        private void DestroyOtherRoots(string keepKey)
        {
            var stale = new List<string>();
            foreach (var pair in m_Roots)
            {
                if (pair.Key == keepKey || pair.Value == null) continue;
                pair.Value.SetActive(false);
                if (Application.isPlaying) Destroy(pair.Value);
                else DestroyImmediate(pair.Value);
                stale.Add(pair.Key);
            }
            for (int i = 0; i < stale.Count; i++) m_Roots.Remove(stale[i]);
        }

        private void DestroyAllRoots()
        {
            foreach (var pair in m_Roots)
            {
                if (pair.Value == null) continue;
                pair.Value.SetActive(false);
                if (Application.isPlaying) Destroy(pair.Value);
                else DestroyImmediate(pair.Value);
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
            foreach (var pair in m_Roots)
            {
                if (pair.Value == null) continue;
                foreach (ResourceNode node in pair.Value.GetComponentsInChildren<ResourceNode>(true))
                    node.Harvested -= OnNodeHarvested;
            }
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
                    DestroyAllRoots();
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