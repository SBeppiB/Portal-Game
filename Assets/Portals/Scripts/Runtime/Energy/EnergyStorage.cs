using System;
using UnityEngine;

namespace Portals.Energy
{
    /// <summary>
    /// Generic, inspector-configurable energy pool for portal technology.
    /// Completely independent of the player's stamina system.
    ///
    /// Time is advanced through Tick(float) rather than reading Time.time directly,
    /// which keeps regeneration deterministic and unit-testable.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EnergyStorage : MonoBehaviour
    {
        [Header("Capacity")]
        [SerializeField, Min(1f)] private float m_Capacity = 100f;
        [SerializeField, Min(0f)] private float m_StartingEnergy = 100f;
        [SerializeField] private bool m_AllowOvercharge = false;

        [Header("Regeneration")]
        [SerializeField, Min(0f)] private float m_RegenerationPerSecond = 10f;
        [SerializeField, Min(0f)] private float m_RegenerationDelay = 1f;

        /// <summary>Current and maximum energy (useful for UI bars).</summary>
        public event Action<float, float> Changed;

        /// <summary>Fired once when energy reaches zero.</summary>
        public event Action Depleted;

        /// <summary>Fired when energy recovers from zero.</summary>
        public event Action Restored;

        private float m_Current;
        private float m_TimeSinceSpend;
        private bool m_WasDepleted;

        public float Capacity => m_Capacity;
        public float Current => m_Current;
        public float Ratio => m_Capacity <= 0f ? 0f : Mathf.Clamp01(m_Current / m_Capacity);
        public bool IsDepleted => m_Current <= Mathf.Epsilon;
        public bool IsFull => m_Current >= m_Capacity - Mathf.Epsilon;
        public float RegenerationPerSecond => m_RegenerationPerSecond;
        public float RegenerationDelay => m_RegenerationDelay;

        private void Awake()
        {
            Initialize();
        }

        /// <summary>Initialises the pool. Called by Awake; safe to call from tests.</summary>
        public void Initialize()
        {
            m_Current = Mathf.Clamp(m_StartingEnergy, 0f, m_Capacity);
            m_TimeSinceSpend = m_RegenerationDelay; // allow regeneration from the start
            m_WasDepleted = IsDepleted;
            Changed?.Invoke(m_Current, m_Capacity);
        }

        private void Update()
        {
            Tick(Time.deltaTime);
        }

        /// <summary>Runtime configuration hook (also useful for upgrades and tests).</summary>
        public void Configure(float capacity, float startingEnergy, float regenerationPerSecond,
            float regenerationDelay, bool allowOvercharge = false)
        {
            m_Capacity = Mathf.Max(1f, capacity);
            m_StartingEnergy = Mathf.Clamp(startingEnergy, 0f, m_Capacity);
            m_RegenerationPerSecond = Mathf.Max(0f, regenerationPerSecond);
            m_RegenerationDelay = Mathf.Max(0f, regenerationDelay);
            m_AllowOvercharge = allowOvercharge;
            Initialize();
        }

        /// <summary>Advances regeneration by <paramref name="deltaTime"/> seconds.</summary>
        public void Tick(float deltaTime)
        {
            if (deltaTime <= 0f) return;

            m_TimeSinceSpend += deltaTime;
            if (m_RegenerationPerSecond <= 0f || m_TimeSinceSpend < m_RegenerationDelay) return;

            float upperBound = m_AllowOvercharge ? float.MaxValue : m_Capacity;
            if (m_Current >= upperBound) return;

            SetCurrent(Mathf.Min(upperBound, m_Current + m_RegenerationPerSecond * deltaTime));
        }

        /// <summary>Atomically spends energy. Returns false and changes nothing if unaffordable.</summary>
        public bool TrySpend(float amount)
        {
            if (amount <= 0f) return false;
            if (m_Current + Mathf.Epsilon < amount) return false;

            SetCurrent(m_Current - amount);
            m_TimeSinceSpend = 0f;
            return true;
        }

        /// <summary>
        /// Spends as much of <paramref name="amount"/> as is available and returns the
        /// amount actually spent. Used for continuous upkeep such as portal maintenance,
        /// which should drain to zero rather than fail outright.
        /// </summary>
        public float SpendContinuous(float amount)
        {
            if (amount <= 0f || m_Current <= 0f) return 0f;

            float spent = Mathf.Min(m_Current, amount);
            SetCurrent(m_Current - spent);
            if (amount > 0f) m_TimeSinceSpend = 0f;
            return spent;
        }

        public void Recharge(float amount)
        {
            if (amount <= 0f) return;
            float upperBound = m_AllowOvercharge ? float.MaxValue : m_Capacity;
            SetCurrent(Mathf.Min(upperBound, m_Current + amount));
        }

        public void DrainAll()
        {
            if (m_Current <= 0f) return;
            SetCurrent(0f);
            m_TimeSinceSpend = 0f;
        }

        public void SetCapacity(float newCapacity, bool preserveRatio = true)
        {
            float ratio = Ratio;
            m_Capacity = Mathf.Max(1f, newCapacity);
            m_Current = preserveRatio
                ? Mathf.Clamp01(ratio) * m_Capacity
                : Mathf.Clamp(m_Current, 0f, m_Capacity);
            Changed?.Invoke(m_Current, m_Capacity);
        }

        private void SetCurrent(float value)
        {
            float next = Mathf.Max(0f, value);
            if (Mathf.Approximately(next, m_Current)) return;

            m_Current = next;

            if (IsDepleted && !m_WasDepleted)
            {
                m_WasDepleted = true;
                Depleted?.Invoke();
            }
            else if (!IsDepleted && m_WasDepleted)
            {
                m_WasDepleted = false;
                Restored?.Invoke();
            }

            Changed?.Invoke(m_Current, m_Capacity);
        }

        private void OnValidate()
        {
            m_Capacity = Mathf.Max(1f, m_Capacity);
            m_StartingEnergy = Mathf.Clamp(m_StartingEnergy, 0f, m_Capacity);
        }
    }
}
