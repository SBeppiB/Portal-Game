using System;
using System.Collections.Generic;
using Portals.Core;
using UnityEngine;

namespace Portals.Energy
{
    /// <summary>
    /// Facade between the energy pool and portal actions. Weapons/guns should call
    /// TryAuthorizeCreation before placing a portal and NotifyPortalCreated afterwards;
    /// maintenance is drained automatically for every registered portal.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PortalEnergySystem : MonoBehaviour
    {
        [SerializeField] private EnergyStorage m_Storage;
        [SerializeField] private PortalEnergyCosts m_Costs = new PortalEnergyCosts();
        [SerializeField, Tooltip("Continuously drain energy for each active portal.")]
        private bool m_ChargeMaintenance = true;

        private readonly HashSet<PortalEndpoint> m_MaintainedPortals = new HashSet<PortalEndpoint>();

        public event Action<PortalEndpoint, string> CreationDenied;
        public event Action EnergyDepleted;
        public event Action EnergyRestored;

        public EnergyStorage Storage => m_Storage;
        public PortalEnergyCosts Costs => m_Costs;
        public int MaintainedPortalCount => m_MaintainedPortals.Count;

        private bool m_Initialized;

        private void Awake()
        {
            Initialize();
        }

        /// <summary>Allows setup code to assign the energy pool explicitly.</summary>
        public void SetStorage(EnergyStorage storage)
        {
            m_Storage = storage;
        }

        /// <summary>Resolves dependencies. Called by Awake; safe to call from tests.</summary>
        public void Initialize()
        {
            if (m_Initialized) return;
            m_Initialized = true;

            if (m_Storage == null) m_Storage = GetComponent<EnergyStorage>();
            if (m_Storage == null)
            {
                m_Storage = gameObject.AddComponent<EnergyStorage>();
                Debug.LogWarning("[PortalEnergySystem] No EnergyStorage assigned; created one automatically.", this);
            }
            m_Storage.Initialize();
        }

        private void OnEnable()
        {
            if (m_Storage == null) return;
            m_Storage.Depleted += OnStorageDepleted;
            m_Storage.Restored += OnStorageRestored;
        }

        private void OnDisable()
        {
            if (m_Storage == null) return;
            m_Storage.Depleted -= OnStorageDepleted;
            m_Storage.Restored -= OnStorageRestored;
        }

        private void OnStorageDepleted() => EnergyDepleted?.Invoke();

        private void OnStorageRestored() => EnergyRestored?.Invoke();

        private void Update()
        {
            Tick(Time.deltaTime);
        }

        /// <summary>Advances upkeep by <paramref name="deltaTime"/> seconds.</summary>
        public void Tick(float deltaTime)
        {
            if (!m_ChargeMaintenance || m_Storage == null || m_MaintainedPortals.Count == 0) return;
            m_Storage.SpendContinuous(m_Costs.maintainPerPortalPerSecond * m_MaintainedPortals.Count * deltaTime);
        }

        public bool CanAfford(PortalKind kind)
        {
            return m_Storage != null && m_Storage.Current + Mathf.Epsilon >= m_Costs.GetCreationCost(kind);
        }

        /// <summary>Pure query: no energy is spent here.</summary>
        public bool TryAuthorizeCreation(PortalKind kind, out string failureReason)
        {
            failureReason = string.Empty;
            if (m_Storage == null)
            {
                failureReason = "No energy storage configured.";
                return false;
            }

            float cost = m_Costs.GetCreationCost(kind);
            if (m_Storage.Current + Mathf.Epsilon < cost)
            {
                failureReason = $"Not enough energy (need {cost:0.0}, have {m_Storage.Current:0.0}).";
                return false;
            }

            return true;
        }

        /// <summary>
        /// Spends the creation cost and registers the portal for maintenance.
        /// Call only after the portal has actually been placed successfully.
        /// </summary>
        public bool NotifyPortalCreated(PortalEndpoint portal)
        {
            if (portal == null) return false;

            if (!TryAuthorizeCreation(portal.Kind, out string reason))
            {
                CreationDenied?.Invoke(portal, reason);
                return false;
            }

            m_Storage.TrySpend(m_Costs.GetCreationCost(portal.Kind));
            m_MaintainedPortals.Add(portal);
            return true;
        }

        public void NotifyPortalDestroyed(PortalEndpoint portal)
        {
            if (portal != null) m_MaintainedPortals.Remove(portal);
        }

        public bool IsMaintaining(PortalEndpoint portal) =>
            portal != null && m_MaintainedPortals.Contains(portal);

        public bool TrySpendUniverseSwitch(out string failureReason)
        {
            failureReason = string.Empty;
            float cost = m_Costs.universeSwitch;
            if (cost <= 0f) return true;

            if (m_Storage == null || !m_Storage.TrySpend(cost))
            {
                failureReason = $"Not enough energy for a universe switch (need {cost:0.0}).";
                return false;
            }
            return true;
        }
    }
}
