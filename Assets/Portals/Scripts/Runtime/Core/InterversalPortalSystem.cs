using System;
using System.Collections.Generic;
using UnityEngine;

namespace Portals.Core
{
    /// <summary>
    /// Owns the universe registry for interversal travel. Universes have independent
    /// state and physics profiles; entities can be moved between them on traversal.
    ///
    /// This system is deliberately NOT a wrapper around LocaversalPortalSystem:
    /// interversal links carry universe identity, per-universe physics and an
    /// alternative mapping mode instead of assuming both systems work identically.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class InterversalPortalSystem : MonoBehaviour
    {
        [System.Serializable]
        public sealed class UniverseDefinition
        {
            public UniverseId id;
            public UniversePhysicsProfile physics = new UniversePhysicsProfile();
        }

        /// <summary>A selectable dimension exposed to the portal gun GUI (keys 1-9).</summary>
        [System.Serializable]
        public sealed class DimensionDefinition
        {
            public UniverseId id;
            public string displayName;

            public DimensionDefinition(UniverseId id, string displayName)
            {
                this.id = id;
                this.displayName = displayName;
            }
        }

        private static readonly HashSet<InterversalPortal> s_Portals = new HashSet<InterversalPortal>();
        private static readonly Dictionary<UniverseBody, UniverseId> s_BodyUniverses =
            new Dictionary<UniverseBody, UniverseId>();
        private static readonly Dictionary<GameObject, UniverseId> s_EntityUniverses =
            new Dictionary<GameObject, UniverseId>();

        public static InterversalPortalSystem Instance { get; private set; }

        [SerializeField, Tooltip("Universes that exist when the game starts.")]
        private List<UniverseDefinition> m_InitialUniverses = new List<UniverseDefinition>();

        [SerializeField, Tooltip("Dimensions selectable with number keys 1-9.")]
        private List<DimensionDefinition> m_Dimensions = new List<DimensionDefinition>();

        [SerializeField, Tooltip("Universe currently selected by the portal gun.")]
        private UniverseId m_SelectedUniverse;

        /// <summary>Raised immediately whenever the selected universe changes.</summary>
        public event Action<UniverseId> SelectedUniverseChanged;

        public UniverseId SelectedUniverse => m_SelectedUniverse;
        public IReadOnlyList<DimensionDefinition> Dimensions => m_Dimensions;
        public int SelectedDimensionIndex { get; private set; } = -1;

        private readonly Dictionary<UniverseId, UniverseState> m_States =
            new Dictionary<UniverseId, UniverseState>();

        private bool m_Initialized;

        private void Awake()
        {
            Initialize();
        }

        /// <summary>Initialises the singleton and initial universes. Safe to call from tests.</summary>
        public void Initialize()
        {
            if (m_Initialized) return;
            m_Initialized = true;
            if (Instance == null) Instance = this;
            EnsureDimensionDefaults();
            EnsureInitialized();

            if (!m_SelectedUniverse.IsValid && m_Dimensions.Count > 0)
                m_SelectedUniverse = m_Dimensions[0].id;
            if (m_SelectedUniverse.IsValid) GetOrCreateUniverse(m_SelectedUniverse);

            for (int i = 0; i < m_Dimensions.Count; i++)
            {
                if (m_Dimensions[i].id == m_SelectedUniverse)
                {
                    SelectedDimensionIndex = i;
                    break;
                }
            }
        }

        private void EnsureDimensionDefaults()
        {
            if (m_Dimensions == null) m_Dimensions = new List<DimensionDefinition>();
            if (m_Dimensions.Count > 0) return;

            for (int i = 1; i <= 9; i++)
            {
                UniverseId id = new UniverseId($"Universe {i}");
                m_Dimensions.Add(new DimensionDefinition(id, $"Dimension {i}"));
            }
        }

        /// <summary>Selects a dimension by its 1-based index (number keys 1-9).</summary>
        public bool SelectDimension(int zeroBasedIndex)
        {
            if (m_Dimensions == null || zeroBasedIndex < 0 || zeroBasedIndex >= m_Dimensions.Count)
                return false;

            DimensionDefinition dimension = m_Dimensions[zeroBasedIndex];
            if (!SetSelectedUniverse(dimension.id)) return false;

            SelectedDimensionIndex = zeroBasedIndex;
            return true;
        }

        /// <summary>Selects a universe by id and notifies listeners. Creates its state if needed.</summary>
        public bool SetSelectedUniverse(UniverseId universe)
        {
            if (!universe.IsValid) return false;
            if (GetOrCreateUniverse(universe) == null) return false;

            if (m_SelectedUniverse == universe) return true;

            m_SelectedUniverse = universe;
            SelectedUniverseChanged?.Invoke(universe);
            return true;
        }

        public string GetSelectedDimensionName()
        {
            if (m_Dimensions == null) return m_SelectedUniverse.ToString();
            for (int i = 0; i < m_Dimensions.Count; i++)
            {
                if (m_Dimensions[i].id == m_SelectedUniverse)
                    return m_Dimensions[i].displayName;
            }
            return m_SelectedUniverse.ToString();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void EnsureInitialized()
        {
            if (m_InitialUniverses == null) return;

            foreach (UniverseDefinition definition in m_InitialUniverses)
            {
                if (definition == null || !definition.id.IsValid) continue;
                if (!m_States.ContainsKey(definition.id))
                    m_States.Add(definition.id, new UniverseState(definition.id, definition.physics));
            }
        }

        /// <summary>Gets (or lazily creates) the state for a universe id.</summary>
        public UniverseState GetOrCreateUniverse(UniverseId id)
        {
            if (!id.IsValid)
            {
                Debug.LogWarning("[InterversalPortalSystem] Cannot create a universe with an empty id.");
                return null;
            }

            EnsureInitialized();
            if (!m_States.TryGetValue(id, out UniverseState state))
            {
                state = new UniverseState(id, UniversePhysicsProfile.Default);
                m_States.Add(id, state);
            }
            return state;
        }

        public bool TryGetUniverse(UniverseId id, out UniverseState state)
        {
            EnsureInitialized();
            return m_States.TryGetValue(id, out state);
        }

        public IEnumerable<UniverseState> Universes
        {
            get
            {
                EnsureInitialized();
                return m_States.Values;
            }
        }

        public UniversePhysicsProfile GetPhysicsProfile(UniverseId id)
        {
            return TryGetUniverse(id, out UniverseState state) ? state.Physics : null;
        }

        public static PortalEndpoint TryGetExit(PortalEndpoint entrance)
        {
            return entrance is InterversalPortal interversal ? interversal.GetLinkedEndpoint() : null;
        }

        public static void RegisterPortal(InterversalPortal portal)
        {
            if (portal == null) return;
            s_Portals.Add(portal);
            if (portal.Universe.IsValid && Instance != null) Instance.GetOrCreateUniverse(portal.Universe);
        }

        public static void UnregisterPortal(InterversalPortal portal)
        {
            if (portal != null) s_Portals.Remove(portal);
        }

        public static IEnumerable<InterversalPortal> GetPortalsIn(UniverseId universe)
        {
            foreach (InterversalPortal portal in s_Portals)
                if (portal != null && portal.Universe == universe)
                    yield return portal;
        }

        public static int RegisteredPortalCount => s_Portals.Count;

        public static IEnumerable<PortalEndpoint> AllPortals
        {
            get
            {
                foreach (InterversalPortal portal in s_Portals)
                    if (portal != null) yield return portal;
            }
        }

        public static void RegisterBody(UniverseBody body)
        {
            if (body == null) return;
            s_BodyUniverses[body] = body.Universe;

            if (Instance == null) return;

            foreach (UniverseState state in Instance.m_States.Values) state.Bodies.Remove(body);
            if (!body.Universe.IsValid) return;
            UniverseState target = Instance.GetOrCreateUniverse(body.Universe);
            if (target != null) target.Bodies.Add(body);
        }

        public static void UnregisterBody(UniverseBody body)
        {
            if (body == null) return;
            s_BodyUniverses.Remove(body);

            if (Instance == null) return;
            foreach (UniverseState state in Instance.m_States.Values) state.Bodies.Remove(body);
        }

        /// <summary>
        /// Moves an entity (and its UniverseBody, if present) into another universe.
        /// Physics profiles are re-applied by UniverseBody.SetUniverse.
        /// </summary>
        public void SwitchEntityUniverse(GameObject entity, UniverseId targetUniverse)
        {
            if (entity == null || !targetUniverse.IsValid) return;

            if (s_EntityUniverses.TryGetValue(entity, out UniverseId previous) && previous != targetUniverse)
            {
                if (TryGetUniverse(previous, out UniverseState oldState)) oldState.Entities.Remove(entity);
            }
            else
            {
                previous = UniverseId.None;
            }

            s_EntityUniverses[entity] = targetUniverse;

            UniverseState newState = GetOrCreateUniverse(targetUniverse);
            if (newState != null) newState.Entities.Add(entity);

            UniverseBody body = entity.GetComponent<UniverseBody>();
            if (body != null) body.SetUniverse(targetUniverse);

            Debug.Log($"[InterversalPortalSystem] '{entity.name}' moved from {previous} to {targetUniverse}.");
        }

        public UniverseId GetEntityUniverse(GameObject entity) =>
            entity != null && s_EntityUniverses.TryGetValue(entity, out UniverseId id) ? id : UniverseId.None;

        public IEnumerable<GameObject> GetEntitiesIn(UniverseId universe)
        {
            EnsureInitialized();
            return m_States.TryGetValue(universe, out UniverseState state)
                ? state.Entities
                : (IEnumerable<GameObject>)System.Array.Empty<GameObject>();
        }
    }
}
