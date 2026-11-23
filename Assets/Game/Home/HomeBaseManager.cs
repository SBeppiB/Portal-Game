using System.Collections.Generic;
using UnityEngine;
using Portals.Save;

namespace Game
{
    /// <summary>Home base (Prime universe): storage, progression, tuner upgrades. Hand-authored only.</summary>
    public class HomeBaseManager : MonoBehaviour, IGameplaySaveState
    {
        [SerializeField] private ResourceInventory m_Storage;
        [SerializeField] private PortalTuner m_Tuner;

        [Header("Discovered Coordinates")]
        [SerializeField] private List<string> m_Discovered = new List<string>();

        public ResourceInventory Storage => m_Storage;
        public PortalTuner Tuner => m_Tuner;
        public IReadOnlyList<string> Discovered => m_Discovered;

        /// <summary>
        /// Cheap signature of everything the home UI displays. Changes only when
        /// storage contents, discovered coordinates or tuner progression change,
        /// so the UI can rebuild its labels only when needed.
        /// </summary>
        public int StateVersion
        {
            get
            {
                int version = 17;
                if (m_Storage != null) version = version * 31 + m_Storage.StateVersion;
                version = version * 31 + m_Discovered.Count;
                if (m_Tuner != null)
                {
                    version = version * 31 + m_Tuner.MaxRing;
                    version = version * 31 + m_Tuner.MaxLayer;
                }
                return version;
            }
        }

        public void Discover(UniverseCoordinate coord)
        {
            string s = coord.ToString();
            if (!m_Discovered.Contains(s)) m_Discovered.Add(s);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (other == null) return;
            foreach (var inv in FindObjectsByType<ResourceInventory>(FindObjectsSortMode.None))
            {
                if (inv == null || inv == m_Storage) continue;
                Deposit(inv);
            }
        }

        public void Deposit(ResourceInventory from)
        {
            if (from == null || m_Storage == null) return;
            foreach (var stack in from.Stacks)
            {
                if (stack == null || stack.resource == null) continue;
                m_Storage.Add(stack.resource, stack.amount);
            }
            from.Clear();
        }

        private void OnEnable() => SaveStateRegistry.Register(this);
        private void OnDisable() => SaveStateRegistry.Unregister(this);

        public void CaptureInto(SaveGameData data)
        {
            if (data.progression == null) data.progression = new ProgressionStateData();
            data.progression.discoveredCoordinates = new List<string>(m_Discovered);
        }

        public bool ApplyFrom(SaveGameData data)
        {
            if (data == null || data.progression == null) return true;
            m_Discovered = data.progression.discoveredCoordinates != null
                ? new List<string>(data.progression.discoveredCoordinates)
                : new List<string>();
            return true;
        }
    }
}