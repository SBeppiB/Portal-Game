using System;
using UnityEngine;
using Portals.Save;

namespace Game
{
    /// <summary>Tracks which rings/layers the player may access. Persists via save registry.</summary>
    public class PortalTuner : MonoBehaviour, IGameplaySaveState
    {
        [SerializeField, Min(1)] private int m_MaxRing = 2;
        [SerializeField, Min(0)] private int m_MaxLayer = 0;

        public int MaxRing => m_MaxRing;
        public int MaxLayer => m_MaxLayer;

        public bool CanAccess(int ring, int layer) => ring <= m_MaxRing && layer <= m_MaxLayer;

        public static int MaxRingForTier(int tier)
        {
            switch (tier)
            {
                case 1: return 2;
                case 2: return 4;
                case 3: return 6;
                case 4: return 8;
                default: return 10;
            }
        }

        public void UnlockRingTier(int tier) => m_MaxRing = Mathf.Max(m_MaxRing, MaxRingForTier(tier));
        public void UnlockLayer(int layer) => m_MaxLayer = Mathf.Max(m_MaxLayer, layer);

        private void OnEnable() => SaveStateRegistry.Register(this);
        private void OnDisable() => SaveStateRegistry.Unregister(this);

        public void CaptureInto(SaveGameData data)
        {
            if (data.progression == null) data.progression = new ProgressionStateData();
            data.progression.portalTunerMaxRing = m_MaxRing;
            data.progression.portalTunerMaxLayer = m_MaxLayer;
        }

        public bool ApplyFrom(SaveGameData data)
        {
            if (data == null || data.progression == null) return true;
            m_MaxRing = data.progression.portalTunerMaxRing;
            m_MaxLayer = data.progression.portalTunerMaxLayer;
            return true;
        }
    }
}