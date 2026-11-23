using UnityEngine;

namespace Game
{
    [CreateAssetMenu(menuName = "Game/Enemy Definition", fileName = "Enemy_")]
    public class EnemyDefinition : ScriptableObject
    {
        [SerializeField] private GameObject m_Prefab;
        [SerializeField, Min(1f)] private float m_BaseHealth = 50f;
        [SerializeField, Min(0f)] private float m_BaseDamage = 5f;
        [SerializeField, Range(1, 10)] private int m_MinimumRing = 1;
        [SerializeField] private LootTable m_LootTable;

        public GameObject Prefab => m_Prefab;
        public float BaseHealth => m_BaseHealth;
        public float BaseDamage => m_BaseDamage;
        public int MinimumRing => m_MinimumRing;
        public LootTable LootTable => m_LootTable;

        public float HealthMultiplier(int ring) => 1f + ring * 0.08f;
        public float DamageMultiplier(int ring) => 1f + ring * 0.08f;

        public float GetHealth(int ring) => m_BaseHealth * HealthMultiplier(ring);
        public float GetDamage(int ring) => m_BaseDamage * DamageMultiplier(ring);
    }
}