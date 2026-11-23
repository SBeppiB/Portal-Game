using System;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// Deterministic content generator for a universe coordinate.
    /// Same coordinate always yields the same seed, modifier and spawn decisions.
    /// Does not touch physics, portals or rendering.
    /// </summary>
    public class UniverseContentGenerator : MonoBehaviour
    {
        [Header("Spawn Prefabs (optional)")]
        [SerializeField] private GameObject[] m_ResourceNodePrefabs;
        [SerializeField] private GameObject[] m_EnemyPrefabs;

        [Header("Base Counts")]
        [SerializeField, Min(0)] private int m_BaseResourceNodes = 12;
        [SerializeField, Min(0)] private int m_BaseEnemies = 2;

        [Header("Spawn Area")]
        [SerializeField, Min(1f)] private float m_SpawnRadius = 12f;

        public UniverseModifier LastModifier { get; private set; } = UniverseModifier.None;

        public static int GetSeed(UniverseCoordinate coord) =>
            coord.GetHashCode() ^ (coord.Layer * 7919) ^ (coord.Ring * 104729) ^ (coord.Position * 131);

        public static System.Random CreateRng(UniverseCoordinate coord) =>
            new System.Random(GetSeed(coord));

        public UniverseModifier RollModifier(UniverseCoordinate coord)
        {
            var rng = CreateRng(coord);
            int roll = rng.Next(0, 100);
            if (coord.Ring <= 2) return roll < 20 ? UniverseModifier.ResourceRich : UniverseModifier.None;
            if (roll < 15) return UniverseModifier.ResourceRich;
            if (roll < 30) return UniverseModifier.EnemyDense;
            if (roll < 45) return UniverseModifier.Abandoned;
            if (roll < 60) return UniverseModifier.Infested;
            if (roll < 75) return UniverseModifier.Military;
            return UniverseModifier.None;
        }

        /// <summary>
        /// Populates <paramref name="root"/> with deterministic content for <paramref name="coord"/>.
        /// Safe to call with empty prefab arrays (logs only).
        /// </summary>
        public void Generate(UniverseCoordinate coord, Transform root)
        {
            if (root == null) return;

            var rng = CreateRng(coord);
            UniverseModifier modifier = RollModifier(coord);
            LastModifier = modifier;

            int resourceCount = m_BaseResourceNodes + coord.Ring;
            int enemyCount = m_BaseEnemies + Mathf.FloorToInt(coord.Ring * 0.8f);

            // Modifier gameplay effects.
            switch (modifier)
            {
                case UniverseModifier.ResourceRich: resourceCount = Mathf.RoundToInt(resourceCount * 1.5f); break;
                case UniverseModifier.EnemyDense: enemyCount = Mathf.RoundToInt(enemyCount * 1.5f); break;
                case UniverseModifier.Infested: enemyCount = Mathf.RoundToInt(enemyCount * 1.5f); break;
                case UniverseModifier.Abandoned: enemyCount = Mathf.Max(0, enemyCount - 2); resourceCount += 1; break;
                case UniverseModifier.Military: enemyCount = Mathf.RoundToInt(enemyCount * 1.2f); break;
            }

            SpawnGroup(m_ResourceNodePrefabs, root, resourceCount, rng, coord.Ring, true, coord + ":r");
            SpawnGroup(m_EnemyPrefabs, root, enemyCount, rng, coord.Ring, false, null);

            Debug.Log($"[UniverseContentGenerator] {coord}: modifier={modifier}, " +
                      $"resources={resourceCount}, enemies={enemyCount}.");
        }

        private void SpawnGroup(GameObject[] prefabs, Transform root, int count, System.Random rng, int ring,
            bool isResource, string keyPrefix)
        {
            if (prefabs == null || prefabs.Length == 0) return;

            for (int i = 0; i < count; i++)
            {
                GameObject prefab = prefabs[rng.Next(prefabs.Length)];
                if (prefab == null) continue;

                float angle = (float)(rng.NextDouble() * Math.PI * 2.0);
                float radius = (float)(rng.NextDouble() * m_SpawnRadius);
                Vector3 pos = root.position + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);

                GameObject instance = Instantiate(prefab, pos, Quaternion.Euler(0f, (float)(rng.NextDouble() * 360.0), 0f), root);

                if (isResource)
                {
                    var node = instance.GetComponent<ResourceNode>();
                    if (node != null)
                    {
                        node.ScaleByRing(ring);
                        if (!string.IsNullOrEmpty(keyPrefix)) node.BindSpawnKey(keyPrefix + i);
                    }
                }
            }
        }
    }
}