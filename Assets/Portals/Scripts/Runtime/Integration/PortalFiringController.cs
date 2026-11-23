using System;
using Portals.Core;
using Portals.Game;
using UnityEngine;

namespace Portals.Integration
{
    /// <summary>
    /// Portal gun input and selection state. Fires from the camera aim ray via PortalPlacer,
    /// so no weapon model or muzzle is required. A gun model may be assigned purely to be
    /// shown/hidden when the gun is equipped.
    ///
    /// Controls (T is intentionally untouched because PlayerMovement uses it for high jump):
    ///   G        equip / unequip the portal gun
    ///   M        toggle travel mode (LOCAVERSAL / INTERVERSAL)
    ///   1-9      select dimension / universe
    ///   LMB      fire the selected portal (alternates A/B after each successful shot)
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PortalFiringController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Camera m_AimCamera;
        [SerializeField] private PortalPlacer m_Placer;
        [SerializeField] private LocaversalPortal m_PortalA;
        [SerializeField] private LocaversalPortal m_PortalB;
        [SerializeField] private InterversalPortal m_InterversalA;
        [SerializeField] private InterversalPortal m_InterversalB;
        [SerializeField] private InterversalPortalSystem m_UniverseSystem;
        [SerializeField] private PortalLifecycleManager m_Lifecycle;
        [SerializeField, Tooltip("Optional gun model. Shown only while equipped. Defaults to a scene object named 'PORTAL GUN'.")]
        private GameObject m_GunModel;

        [Header("Input")]
        [SerializeField] private KeyCode m_EquipKey = KeyCode.T;
        [SerializeField] private KeyCode m_ModeToggleKey = KeyCode.M;
        [SerializeField] private KeyCode m_PersistenceKey = KeyCode.N;
        [SerializeField] private KeyCode m_CloseAllKey = KeyCode.B;
        [SerializeField] private bool m_UseMouseButtons = true;
        [SerializeField, Tooltip("AI-controlled guns ignore player input and are driven by AIFire.")]
        private bool m_AIControlled = false;

        [Header("Debug")]
        [SerializeField, Tooltip("Draw a short-lived ray showing where the shot was aimed.")]
        private bool m_ShowAimRay = true;
        [SerializeField] private Color m_AimRayColor = Color.magenta;
        [SerializeField, Min(0.01f)] private float m_AimRayDuration = 0.15f;

        /// <summary>Raised with a human-readable reason whenever placement is rejected.</summary>
        public event Action<string> PlacementFailed;

        /// <summary>Raised whenever equip/mode/dimension/portal selection changes (GUI hook).</summary>
        public event Action StateChanged;

        public LocaversalPortal PortalA => m_PortalA;
        public LocaversalPortal PortalB => m_PortalB;
        public InterversalPortal InterversalA => m_InterversalA;
        public InterversalPortal InterversalB => m_InterversalB;

        public bool IsEquipped { get; private set; }
        public PortalKind TravelMode { get; private set; } = PortalKind.Locaversal;
        public bool IsSelectedPortalA => m_SelectPortalA;

        public string TravelModeLabel =>
            TravelMode == PortalKind.Interversal ? "INTERVERSAL" : "LOCAVERSAL";

        public string SelectedPortalLabel => m_SelectPortalA ? "A" : "B";

        public int SelectedDimensionIndex => m_UniverseSystem != null ? m_UniverseSystem.SelectedDimensionIndex : 0;

        public string SelectedDimensionName =>
            m_UniverseSystem != null ? m_UniverseSystem.GetSelectedDimensionName() : "None";

        public PortalLifecycleManager Lifecycle => m_Lifecycle;

        /// <summary>
        /// Non-null for AI gunners: gives this gun its own independent portal pair.
        /// Null keeps the player's single shared pair.
        /// </summary>
        public object PairOwner { get; private set; }

        public void SetPairOwner(object owner) => PairOwner = owner;

        public string LifetimeLabel
        {
            get
            {
                if (m_Lifecycle == null) return "--";
                if (m_Lifecycle.PersistenceEnabled) return "PERSISTENT";
                PortalPair pair = m_Lifecycle.ActivePair;
                return pair == null ? "--" : $"{Mathf.Max(0f, pair.RemainingLifetime):0.0}s";
            }
        }

        public string PairLabel
        {
            get
            {
                if (m_Lifecycle == null) return "Portal pair: --";
                return m_Lifecycle.HasActivePair ? "Portal pair: active" : "Portal pair: --";
            }
        }

        private LineRenderer m_AimLine;
        private float m_AimLineHideTime;
        private bool m_SelectPortalA = true;
        private bool m_SubscribedToUniverseEvents;

        /// <summary>Locaversal-only setup (kept for existing scene setup and tests).</summary>
        public void Configure(Camera aimCamera, PortalPlacer placer,
            LocaversalPortal portalA, LocaversalPortal portalB)
        {
            m_AimCamera = aimCamera;
            m_Placer = placer;
            m_PortalA = portalA;
            m_PortalB = portalB;
            if (m_Placer != null) m_Placer.SetIgnoreRoot(transform);
        }

        /// <summary>Adds interversal endpoints, universe registry and the optional gun model.</summary>
        public void ConfigureInterversal(InterversalPortal portalA, InterversalPortal portalB,
            InterversalPortalSystem universeSystem, GameObject gunModel)
        {
            m_InterversalA = portalA;
            m_InterversalB = portalB;
            m_UniverseSystem = universeSystem;
            if (gunModel != null) m_GunModel = gunModel;

            SubscribeToUniverseSystem();
            ApplySelectedUniverseToPortals();
        }

        /// <summary>Links the firing controller to the lifecycle manager.</summary>
        public void ConfigureLifecycle(PortalLifecycleManager lifecycle)
        {
            m_Lifecycle = lifecycle;
            if (m_Lifecycle != null)
            {
                m_Lifecycle.StateChanged += OnLifecycleStateChanged;
                if (m_Placer != null) m_Lifecycle.Configure(m_Placer, m_UniverseSystem);
            }
        }

        private void OnLifecycleStateChanged()
        {
            StateChanged?.Invoke();
        }

        private void SubscribeToUniverseSystem()
        {
            if (m_UniverseSystem == null || m_SubscribedToUniverseEvents) return;
            m_UniverseSystem.SelectedUniverseChanged += OnSelectedUniverseChanged;
            m_SubscribedToUniverseEvents = true;
        }

        private void Awake()
        {
            // The canonical equip key is T; override stale serialized values from
            // scenes authored before this control was restored.
            m_EquipKey = KeyCode.T;
            if (m_AimCamera == null) m_AimCamera = Camera.main;
            if (m_Placer == null) m_Placer = GetComponent<PortalPlacer>();
            if (m_UniverseSystem == null) m_UniverseSystem = FindFirstObjectByType<InterversalPortalSystem>();
            if (m_Lifecycle == null) m_Lifecycle = FindFirstObjectByType<PortalLifecycleManager>();
            if (m_GunModel == null)
            {
                GameObject found = GameObject.Find("PORTAL GUN");
                if (found != null) m_GunModel = found;
            }

            if (m_Placer != null) m_Placer.SetIgnoreRoot(transform);
            if (m_ShowAimRay) CreateAimLine();

            ApplyGunVisibility();
            SubscribeToUniverseSystem();
            if (m_Lifecycle != null)
            {
                m_Lifecycle.Configure(m_Placer, m_UniverseSystem);
                m_Lifecycle.StateChanged += OnLifecycleStateChanged;
            }
        }

        private void OnDestroy()
        {
            if (m_UniverseSystem != null && m_SubscribedToUniverseEvents)
                m_UniverseSystem.SelectedUniverseChanged -= OnSelectedUniverseChanged;
            if (m_Lifecycle != null)
                m_Lifecycle.StateChanged -= OnLifecycleStateChanged;

            if (m_AimLine == null) return;
            if (m_AimLine.sharedMaterial != null) Destroy(m_AimLine.sharedMaterial);
            Destroy(m_AimLine.gameObject);
        }

        private void Update()
        {
            if (m_AIControlled) return;

            if (GameplayInput.GetKeyDown(m_EquipKey)) ToggleEquipped();
            if (GameplayInput.GetKeyDown(m_ModeToggleKey)) ToggleTravelMode();
            if (GameplayInput.GetKeyDown(m_PersistenceKey)) TogglePersistence();
            if (GameplayInput.GetKeyDown(m_CloseAllKey)) CloseAllPortals();

            for (int i = 0; i < 9; i++)
            {
                KeyCode digit = (KeyCode)((int)KeyCode.Alpha1 + i);
                KeyCode keypad = (KeyCode)((int)KeyCode.Keypad1 + i);
                if (GameplayInput.GetKeyDown(digit) || GameplayInput.GetKeyDown(keypad)) SelectDimension(i);
            }

            if (IsEquipped && m_UseMouseButtons && GameplayInput.GetMouseButtonDown(0))
                Fire();

            if (m_AimLine != null && m_AimLine.enabled && Time.time >= m_AimLineHideTime)
                m_AimLine.enabled = false;
        }

        public void ToggleEquipped()
        {
            IsEquipped = !IsEquipped;
            ApplyGunVisibility();
            Debug.Log($"[PortalFiring] Portal gun {(IsEquipped ? "equipped" : "unequipped")}.");
            StateChanged?.Invoke();
        }

        public void ToggleTravelMode()
        {
            TravelMode = TravelMode == PortalKind.Locaversal
                ? PortalKind.Interversal
                : PortalKind.Locaversal;
            Debug.Log($"[PortalFiring] Travel mode: {TravelModeLabel}.");
            StateChanged?.Invoke();
        }

        public void TogglePersistence()
        {
            if (m_Lifecycle == null) return;
            m_Lifecycle.TogglePersistence();
            StateChanged?.Invoke();
        }

        public void CloseAllPortals()
        {
            if (m_Lifecycle == null) return;
            if (PairOwner != null) m_Lifecycle.ClosePairForOwner(PairOwner);
            else m_Lifecycle.CloseAll();
            m_SelectPortalA = true;
            StateChanged?.Invoke();
        }

        /// <summary>Selects a dimension by zero-based index and updates the portal system.</summary>
        public void SelectDimension(int zeroBasedIndex)
        {
            if (m_UniverseSystem == null || !m_UniverseSystem.SelectDimension(zeroBasedIndex))
            {
                Debug.LogWarning($"[PortalFiring] Cannot select dimension {zeroBasedIndex + 1}.");
                return;
            }

            ApplySelectedUniverseToPortals();
            Debug.Log($"[PortalFiring] Dimension {SelectedDimensionIndex + 1}: {SelectedDimensionName}.");
            StateChanged?.Invoke();
        }

        private void OnSelectedUniverseChanged(UniverseId universe)
        {
            ApplySelectedUniverseToPortals();
            StateChanged?.Invoke();
        }

        private void ApplySelectedUniverseToPortals()
        {
            if (m_UniverseSystem == null) return;
            UniverseId selected = m_UniverseSystem.SelectedUniverse;
            if (!selected.IsValid) return;

            if (m_InterversalA != null)
                m_InterversalA.Configure(selected, m_InterversalB,
                    m_InterversalA.MappingMode, m_InterversalA.WorldOffset);

            if (m_InterversalB != null)
                m_InterversalB.Configure(selected, m_InterversalA,
                    m_InterversalB.MappingMode, m_InterversalB.WorldOffset);
        }

        private void ApplyGunVisibility()
        {
            if (m_GunModel != null) m_GunModel.SetActive(IsEquipped);
        }

        /// <summary>Fires the currently selected portal and advances A/B on success.</summary>
        public void Fire()
        {
            if (!IsEquipped) return;

            PortalEndpoint endpoint = null;
            if (m_Lifecycle != null)
            {
                PortalPair pair = m_Lifecycle.GetOrCreatePair(TravelMode, PairOwner);
                endpoint = m_Lifecycle.GetEndpointForFire(pair, m_SelectPortalA);
            }
            else
            {
                // Fallback for scenes without a lifecycle manager (legacy behaviour).
                endpoint = TravelMode == PortalKind.Locaversal
                    ? (PortalEndpoint)(m_SelectPortalA ? m_PortalA : m_PortalB)
                    : (PortalEndpoint)(m_SelectPortalA ? m_InterversalA : m_InterversalB);
            }

            if (endpoint == null)
            {
                Debug.LogWarning($"[PortalFiring] No {TravelMode} portal endpoint available.");
                return;
            }

            bool placed = FireEndpoint(endpoint);

            if (placed)
            {
                m_Lifecycle?.NotifyEndpointPlaced(endpoint);
                m_SelectPortalA = !m_SelectPortalA;
                StateChanged?.Invoke();
            }
        }

        /// <summary>Enables or disables AI control (AI guns never read player input).</summary>
        public void SetAIControlled(bool aiControlled)
        {
            m_AIControlled = aiControlled;
        }

        public bool AIControlled => m_AIControlled;

        /// <summary>
        /// Fires the next endpoint along an explicit ray. Used by AI agents; returns true
        /// when the portal was placed. Alternates A/B exactly like a player shot.
        /// </summary>
        public bool AIFire(Ray ray)
        {
            if (m_Placer == null) return false;

            PortalEndpoint endpoint = null;
            if (m_Lifecycle != null)
            {
                PortalPair pair = m_Lifecycle.GetOrCreatePair(TravelMode, PairOwner);
                endpoint = m_Lifecycle.GetEndpointForFire(pair, m_SelectPortalA);
            }
            else
            {
                endpoint = TravelMode == PortalKind.Locaversal
                    ? (PortalEndpoint)(m_SelectPortalA ? m_PortalA : m_PortalB)
                    : (PortalEndpoint)(m_SelectPortalA ? m_InterversalA : m_InterversalB);
            }
            if (endpoint == null) return false;

            bool placed = m_Placer.TryPlaceFromShot(endpoint, ray,
                out PortalPlacementFailure failure, out string detail);

            if (placed)
            {
                m_Lifecycle?.NotifyEndpointPlaced(endpoint);
                m_SelectPortalA = !m_SelectPortalA;
                StateChanged?.Invoke();
            }
            else
            {
                Debug.LogWarning($"[PortalFiring] AI could not place '{endpoint.name}' ({failure}): {detail}");
            }

            if (m_ShowAimRay)
            {
                m_AimLineHideTime = Time.time + m_AimRayDuration;
            }
            return placed;
        }

        /// <summary>Low-level locaversal fire, kept public for tools and existing tests.</summary>
        public void Fire(LocaversalPortal portal)
        {
            FireEndpoint(portal);
        }

        /// <summary>Low-level interversal fire.</summary>
        public void Fire(InterversalPortal portal)
        {
            FireEndpoint(portal);
        }

        private bool FireEndpoint(PortalEndpoint portal)
        {
            if (portal == null || m_AimCamera == null || m_Placer == null) return false;

            Ray ray = new Ray(m_AimCamera.transform.position, m_AimCamera.transform.forward);
            bool placed = m_Placer.TryPlaceFromShot(portal, ray,
                out PortalPlacementFailure failure, out string detail);

            if (placed)
            {
                Debug.Log($"[PortalFiring] Placed '{portal.name}': {detail}");
            }
            else
            {
                string reason = $"Cannot place '{portal.name}' ({failure}): {detail}";
                Debug.LogWarning($"[PortalFiring] {reason}");
                PlacementFailed?.Invoke(reason);
            }

            if (m_ShowAimRay) ShowAimRay(ray, placed);
            return placed;
        }

        private void ShowAimRay(Ray ray, bool placed)
        {
            if (m_AimLine == null) return;

            Vector3 end = ray.origin + ray.direction * m_Placer.MaxPlacementDistance;
            if (m_Placer.TryFindPlacementHit(ray, out RaycastHit hit)) end = hit.point;

            m_AimLine.SetPosition(0, ray.origin);
            m_AimLine.SetPosition(1, end);
            m_AimLine.startColor = m_AimRayColor;
            m_AimLine.endColor = placed ? Color.green : Color.red;
            m_AimLine.enabled = true;
            m_AimLineHideTime = Time.time + m_AimRayDuration;
        }

        private void CreateAimLine()
        {
            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null) return;

            GameObject child = new GameObject("Portal Aim Ray");
            child.transform.SetParent(transform, false);
            m_AimLine = child.AddComponent<LineRenderer>();
            m_AimLine.useWorldSpace = true;
            m_AimLine.positionCount = 2;
            m_AimLine.startWidth = 0.02f;
            m_AimLine.endWidth = 0.02f;
            m_AimLine.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            m_AimLine.receiveShadows = false;
            m_AimLine.material = new Material(shader);
            m_AimLine.enabled = false;
        }
    }
}
