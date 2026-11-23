using UnityEngine;

namespace Game.Omniverse
{
    public enum DivergenceRing
    {
        Prime = 0,
        Cosmetic = 1,
        NpcObject = 2,
        Historical = 3,
        Technology = 4,
        Biological = 5,
        Physics = 6,
        Temporal = 7,
        RealityInstability = 8,
        Conceptual = 9,
        Catastrophic = 10
    }

    [CreateAssetMenu(menuName = "Game/Omniverse/Divergence Profile", fileName = "DivergenceProfile_")]
    public class DivergenceProfile : ScriptableObject
    {
        [SerializeField] private int m_Ring;
        [SerializeField] private string m_Description;
        [SerializeField] private bool m_HasCosmeticChanges;
        [SerializeField] private bool m_HasNpcChanges;
        [SerializeField] private bool m_HasHistoricalChanges;
        [SerializeField] private bool m_HasTechnologyChanges;
        [SerializeField] private bool m_HasBiologicalHazards;
        [SerializeField] private bool m_HasPhysicsChanges;
        [SerializeField] private bool m_HasTimeDistortion;
        [SerializeField] private bool m_HasGeometryInstability;
        [SerializeField] private bool m_HasConceptualAnomalies;
        [SerializeField] private bool m_HasRealityCorruption;

        public int Ring => m_Ring;
        public string Description => m_Description;

        public static DivergenceProfile CreateDefault(int ring)
        {
            // Factory for code/tests; real data should be authored as assets.
            return null;
        }

        public bool HasCosmeticChanges => m_HasCosmeticChanges;
        public bool HasNpcChanges => m_HasNpcChanges;
        public bool HasHistoricalChanges => m_HasHistoricalChanges;
        public bool HasTechnologyChanges => m_HasTechnologyChanges;
        public bool HasBiologicalHazards => m_HasBiologicalHazards;
        public bool HasPhysicsChanges => m_HasPhysicsChanges;
        public bool HasTimeDistortion => m_HasTimeDistortion;
        public bool HasGeometryInstability => m_HasGeometryInstability;
        public bool HasConceptualAnomalies => m_HasConceptualAnomalies;
        public bool HasRealityCorruption => m_HasRealityCorruption;
    }
}