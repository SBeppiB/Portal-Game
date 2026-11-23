using System;
using UnityEngine;
using UnityEngine.Events;

namespace Game.Omniverse
{
    /// <summary>
    /// Drifts the active universe position over time (e.g. L1-R3-P44 -> L1-R3-P45).
    /// Fires events; preserves save data by exposing the new coordinate.
    /// </summary>
    public class UniverseDriftManager : MonoBehaviour
    {
        [SerializeField] private float m_DriftIntervalSeconds = 60f;
        [SerializeField] private bool m_AutoDrift = false;

        public event Action<OmniverseCoordinate, OmniverseCoordinate> Drifted;

        [SerializeField] private UnityEvent m_OnDriftWarning;

        private float m_Timer;
        private OmniverseCoordinate m_Current = OmniverseCoordinate.Prime;

        public OmniverseCoordinate Current => m_Current;

        public void SetCurrent(OmniverseCoordinate coord) => m_Current = coord;

        public void DriftOnce()
        {
            var previous = m_Current;
            var adjacent = m_Current.GetAdjacentUniverses();
            // Drift forward (index 1 = next).
            if (adjacent.Count > 1) m_Current = adjacent[1];
            Drifted?.Invoke(previous, m_Current);
        }

        private void Update()
        {
            if (!m_AutoDrift) return;
            m_Timer += Time.deltaTime;
            if (m_Timer >= m_DriftIntervalSeconds)
            {
                m_Timer = 0f;
                m_OnDriftWarning?.Invoke();
                DriftOnce();
            }
        }
    }
}