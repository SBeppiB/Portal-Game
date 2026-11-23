using System;
using System.Collections.Generic;
using Portals.Integration;
using Portals.Traversal;
using UnityEngine;

namespace Portals.Core
{
    /// <summary>
    /// Owns the single active portal pair: its lifetime, persistence, replacement and
    /// shutdown. Firing a new pair closes the current one first; both endpoints shrink
    /// together, become non-traversable immediately, leave blue goo on the floor beneath
    /// them and are returned to a per-technology pool when their animation finishes.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PortalLifecycleManager : MonoBehaviour
    {
        public static PortalLifecycleManager Instance { get; private set; }

        [Header("Lifetime")]
        [SerializeField, Min(1f)] private float m_DefaultLifetime = 30f;
        [SerializeField] private bool m_StartPersistent = false;

        private readonly Dictionary<PortalKind, List<PortalEndpoint>> m_EndpointPool =
            new Dictionary<PortalKind, List<PortalEndpoint>>();
        private PortalPlacer m_Placer;
        private InterversalPortalSystem m_UniverseSystem;
        private PortalPair m_ActivePair;
        // Optional per-owner pairs (e.g. enemies with their own guns). null key = player/default pair.
        private readonly Dictionary<object, PortalPair> m_ScopedPairs =
            new Dictionary<object, PortalPair>();
        // Reused every Tick so the per-frame update performs no heap allocation.
        private readonly List<PortalPair> m_TickBuffer = new List<PortalPair>(4);
        private bool m_Initialized;

        public event Action StateChanged;

        public float DefaultLifetime => m_DefaultLifetime;
        public bool PersistenceEnabled { get; private set; }

        /// <summary>The one currently managed pair (A/B), or null when none is active.</summary>
        public PortalPair ActivePair => m_ActivePair;

        public bool HasActivePair => m_ActivePair != null;

        private PortalPair GetPair(object owner) =>
            owner == null ? m_ActivePair :
            (m_ScopedPairs.TryGetValue(owner, out PortalPair pair) ? pair : null);

        private void SetPair(object owner, PortalPair pair)
        {
            if (owner == null) m_ActivePair = pair;
            else if (pair == null) m_ScopedPairs.Remove(owner);
            else m_ScopedPairs[owner] = pair;
        }

        private IEnumerable<PortalPair> AllPairs()
        {
            if (m_ActivePair != null) yield return m_ActivePair;
            foreach (PortalPair pair in m_ScopedPairs.Values)
                if (pair != null) yield return pair;
        }

        /// <summary>True when this owner has its own active pair.</summary>
        public bool HasActivePairForOwner(object owner) => GetPair(owner) != null;

        /// <summary>Returns the pair owned by this object (or the default pair for null).</summary>
        public PortalPair GetPairForOwner(object owner) => GetPair(owner);

        /// <summary>Closes only the pair owned by this object (used by AI gunners).</summary>
        public void ClosePairForOwner(object owner)
        {
            PortalPair pair = GetPair(owner);
            if (pair != null) ClosePairInternal(owner, pair, "manual close");
        }

        private void Awake()
        {
            Initialize();
        }

        public void Initialize()
        {
            if (m_Initialized) return;
            m_Initialized = true;
            if (Instance == null) Instance = this;
            PersistenceEnabled = m_StartPersistent;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;

            // Scene cleanup: release the pair immediately. Coroutines cannot be relied on
            // here and freshly destroyed scenes cannot show goo anyway.
            foreach (PortalPair pair in AllPairs())
            {
                ReleaseEndpoint(pair.Entrance);
                ReleaseEndpoint(pair.Exit);
            }
            m_ActivePair = null;
            m_ScopedPairs.Clear();
        }

        public void Configure(PortalPlacer placer, InterversalPortalSystem universeSystem)
        {
            m_Placer = placer;
            m_UniverseSystem = universeSystem;
        }

        private void Update()
        {
            Tick(Time.deltaTime);
        }

        /// <summary>Advances every active pair's timer by deltaTime seconds.</summary>
        public void Tick(float deltaTime)
        {
            if (deltaTime <= 0f) return;

            // Fast path: nothing to tick (no player pair and no scoped pairs).
            if (m_ActivePair == null && m_ScopedPairs.Count == 0) return;

            // Snapshot into a reusable buffer (expiry may mutate the dictionaries).
            m_TickBuffer.Clear();
            if (m_ActivePair != null) m_TickBuffer.Add(m_ActivePair);
            foreach (PortalPair pair in m_ScopedPairs.Values)
                if (pair != null) m_TickBuffer.Add(pair);

            for (int i = 0; i < m_TickBuffer.Count; i++)
            {
                PortalPair pair = m_TickBuffer[i];
                if (pair.Persistent || !pair.IsComplete) continue;

                pair.RemainingLifetime -= deltaTime;
                if (pair.RemainingLifetime > 0f) continue;

                object owner = FindOwner(pair);
                ClosePairInternal(owner, pair, "lifetime expired");
            }
        }

        private object FindOwner(PortalPair pair)
        {
            if (ReferenceEquals(pair, m_ActivePair)) return null;
            foreach (KeyValuePair<object, PortalPair> entry in m_ScopedPairs)
                if (ReferenceEquals(entry.Value, pair)) return entry.Key;
            return null;
        }

        /// <summary>Registers an already-created pair (default/player scope).</summary>
        public PortalPair RegisterPair(PortalKind kind, PortalEndpoint entrance, PortalEndpoint exit) =>
            RegisterPair(kind, entrance, exit, null);

        /// <summary>Registers an already-created pair for a specific owner (e.g. an enemy).</summary>
        public PortalPair RegisterPair(PortalKind kind, PortalEndpoint entrance, PortalEndpoint exit, object owner)
        {
            Initialize();
            PortalPair existing = GetPair(owner);
            if (existing != null) ClosePairInternal(owner, existing, "replaced by registration");

            PortalPair pair = new PortalPair(kind, entrance, exit, m_DefaultLifetime, PersistenceEnabled);
            SetPair(owner, pair);
            StateChanged?.Invoke();
            return pair;
        }

        /// <summary>
        /// Returns the active pair, creating a fresh one when none exists or when the
        /// active pair is already complete (or belongs to another travel mode). Whatever
        /// was active before is closed first, so only one pair ever transports objects.
        /// </summary>
        public PortalPair GetOrCreatePair(PortalKind kind) => GetOrCreatePair(kind, null);

        /// <summary>
        /// Returns the owner's pair, creating a fresh one when needed. A null owner uses the
        /// single default/player pair; non-null owners get an independent A/B pair.
        /// </summary>
        public PortalPair GetOrCreatePair(PortalKind kind, object owner)
        {
            Initialize();

            PortalPair active = GetPair(owner);
            bool reuse;
            if (owner == null)
                reuse = active != null && active.Kind == kind && !active.IsComplete;
            else
                reuse = active != null && active.Kind == kind && active.IsComplete;
            if (reuse) return active;

            if (active != null) ClosePairInternal(owner, active, "replaced by a new pair");

            PortalEndpoint entrance = CreateEndpoint(kind, true);
            PortalEndpoint exit = CreateEndpoint(kind, false);
            LinkEndpoints(kind, entrance, exit);

            PortalPair pair = new PortalPair(kind, entrance, exit, m_DefaultLifetime, PersistenceEnabled);
            SetPair(owner, pair);
            StateChanged?.Invoke();
            return pair;
        }

        public PortalEndpoint GetEndpointForFire(PortalPair pair, bool selectA) =>
            pair == null ? null : (selectA ? pair.Entrance : pair.Exit);

        /// <summary>Called after an endpoint is successfully placed.</summary>
        public void NotifyEndpointPlaced(PortalEndpoint endpoint)
        {
            if (FindActivePair(endpoint) != null) StateChanged?.Invoke();
        }

        public void TogglePersistence()
        {
            Initialize();
            PersistenceEnabled = !PersistenceEnabled;

            foreach (PortalPair pair in AllPairs()) pair.Persistent = PersistenceEnabled;

            Debug.Log($"[PortalLifecycle] Portals are now {(PersistenceEnabled ? "PERSISTENT" : "temporary")}.");
            StateChanged?.Invoke();
        }

        /// <summary>Closes every active pair (scene-level shutdown).</summary>
        public void CloseAll()
        {
            foreach (PortalPair pair in AllPairs())
                ClosePairInternal(FindOwner(pair), pair, "manual close");

            ResetTraversers();
            Debug.Log("[PortalLifecycle] All portal pairs closed.");
            StateChanged?.Invoke();
        }

        public void ClosePair(PortalPair pair)
        {
            if (pair == null) return;
            ClosePairInternal(FindOwner(pair), pair, "manual close");
        }

        private void ClosePairInternal(object owner, PortalPair pair, string reason)
        {
            if (pair == null) return;
            if (ReferenceEquals(GetPair(owner), pair)) SetPair(owner, null);

            Debug.Log($"[PortalLifecycle] Closing {pair} ({reason}).");
            CloseEndpointPair(pair.Entrance, pair.Exit);
            ResetTraversers();
            StateChanged?.Invoke();
        }

        private void CloseEndpointPair(params PortalEndpoint[] endpoints)
        {
            for (int i = 0; i < endpoints.Length; i++) CloseEndpoint(endpoints[i]);
        }

        private void CloseEndpoint(PortalEndpoint endpoint)
        {
            if (endpoint == null) return;

            PortalVisual visual = endpoint.GetComponent<PortalVisual>();
            if (visual != null && endpoint.IsActive)
            {
                // Capture the position before the endpoint is pooled/moved. Both endpoints
                // of a pair start their animations in the same frame.
                Vector3 formerPosition = endpoint.Position;
                visual.PlayClose(() =>
                {
                    PortalGooResidue.SpawnUnder(formerPosition);
                    ReleaseEndpoint(endpoint);
                    AddToPool(endpoint.Kind, endpoint);
                });
                return;
            }

            // Nothing was ever open (or there is no visual to animate): close immediately.
            ReleaseEndpoint(endpoint);
            AddToPool(endpoint.Kind, endpoint);
        }

        private void ReleaseEndpoint(PortalEndpoint endpoint)
        {
            if (endpoint == null) return;

            if (m_Placer != null)
            {
                m_Placer.Close(endpoint);
                return;
            }

            PortalSurfaceAnchor anchor = endpoint.GetComponent<PortalSurfaceAnchor>();
            if (anchor != null) anchor.Detach();
            endpoint.SetActive(false);
        }

        private void AddToPool(PortalKind kind, PortalEndpoint endpoint)
        {
            if (endpoint == null) return;
            if (!m_EndpointPool.TryGetValue(kind, out List<PortalEndpoint> pool))
            {
                pool = new List<PortalEndpoint>();
                m_EndpointPool.Add(kind, pool);
            }
            if (!pool.Contains(endpoint)) pool.Add(endpoint);
        }

        private PortalEndpoint CreateEndpoint(PortalKind kind, bool isEntrance)
        {
            if (m_EndpointPool.TryGetValue(kind, out List<PortalEndpoint> pool) && pool.Count > 0)
            {
                // Endpoints are pooled entrance-first, so index 0 preserves the A/B role.
                PortalEndpoint reused = pool[0];
                pool.RemoveAt(0);
                PrepareReused(reused, kind);
                return reused;
            }

            GameObject owner = new GameObject($"{kind} Portal {(isEntrance ? "A" : "B")}");
            owner.transform.position = Vector3.zero;

            PortalEndpoint endpoint;
            if (kind == PortalKind.Interversal)
            {
                InterversalPortal interversal = owner.AddComponent<InterversalPortal>();
                UniverseId universe = m_UniverseSystem != null
                    ? m_UniverseSystem.SelectedUniverse
                    : UniverseId.None;
                interversal.Configure(universe, null, InterversalMappingMode.ViaPortalFrame, Vector3.zero);
                endpoint = interversal;
            }
            else
            {
                endpoint = owner.AddComponent<LocaversalPortal>();
            }

            PortalDebugVisualizer visualizer = owner.AddComponent<PortalDebugVisualizer>();
            visualizer.Configure(endpoint,
                isEntrance ? new Color(0.2f, 0.8f, 1f) : new Color(1f, 0.55f, 0.15f));
            visualizer.SetRuntimeLinesVisible(false);

            PortalVisualColours.Apply(owner.AddComponent<PortalVisual>(), kind, endpoint.OpeningHalfSize);

            endpoint.SetActive(false);
            return endpoint;
        }

        private static void PrepareReused(PortalEndpoint endpoint, PortalKind kind)
        {
            PortalSurfaceAnchor oldAnchor = endpoint.GetComponent<PortalSurfaceAnchor>();
            if (oldAnchor != null) oldAnchor.Detach();
            endpoint.SetActive(false);

            // Pooled endpoints must get the right colour and a full-size surface for their
            // new pair. Configure also cancels any stale animation on the visual.
            PortalVisual visual = endpoint.GetComponent<PortalVisual>();
            if (visual == null) visual = endpoint.gameObject.AddComponent<PortalVisual>();
            PortalVisualColours.Apply(visual, kind, endpoint.OpeningHalfSize);
        }

        private void LinkEndpoints(PortalKind kind, PortalEndpoint entrance, PortalEndpoint exit)
        {
            if (entrance is LocaversalPortal locaversal && exit is LocaversalPortal locaversalExit)
            {
                locaversal.SetPair(locaversalExit);
                return;
            }

            if (entrance is InterversalPortal interversal && exit is InterversalPortal interversalExit)
            {
                UniverseId universe = m_UniverseSystem != null
                    ? m_UniverseSystem.SelectedUniverse
                    : UniverseId.None;
                interversal.Configure(universe, interversalExit, InterversalMappingMode.ViaPortalFrame, Vector3.zero);
                interversalExit.Configure(universe, interversal, InterversalMappingMode.ViaPortalFrame, Vector3.zero);
            }
        }

        private PortalPair FindActivePair(PortalEndpoint endpoint)
        {
            if (endpoint == null) return null;
            foreach (PortalPair pair in AllPairs())
                if (pair.Contains(endpoint)) return pair;
            return null;
        }

        private static void ResetTraversers()
        {
            PortalTraverser[] traversers = FindObjectsByType<PortalTraverser>(FindObjectsSortMode.None);
            for (int i = 0; i < traversers.Length; i++) traversers[i].ResetTracking();
        }
    }
}
