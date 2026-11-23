using UnityEngine;

namespace Game
{
    /// <summary>
    /// Harvestable resource node. Can be interacted with manually via Harvest().
    /// Reuses the existing InteractableObject component for scanning/pickup metadata.
    /// </summary>
    public class ResourceNode : MonoBehaviour
    {
        [SerializeField] private ResourceDefinition m_Resource;
        [SerializeField, Min(1)] private int m_MinAmount = 1;
        [SerializeField, Min(1)] private int m_MaxAmount = 3;
        [SerializeField] private bool m_Respawn = false;
        [SerializeField, Min(0.1f)] private float m_RespawnTime = 30f;

        private bool m_Harvested;
        private float m_RespawnAt;
        private string m_SpawnKey;

        /// <summary>Stable key assigned by <see cref="UniverseContentGenerator"/> (null for hand-placed nodes).</summary>
        public string SpawnKey => m_SpawnKey;

        /// <summary>Raised after a node is harvested, with its stable spawn key.</summary>
        public event System.Action<string> Harvested;

        public ResourceDefinition Resource => m_Resource;
        public bool IsHarvested => m_Harvested;

        /// <summary>Assigns the deterministic save key used to persist harvested state.</summary>
        public void BindSpawnKey(string key) => m_SpawnKey = key;

        public void ScaleByRing(int ring)
        {
            m_MinAmount = Mathf.Max(1, m_MinAmount + ring / 2);
            m_MaxAmount = Mathf.Max(m_MinAmount, m_MaxAmount + ring);
        }

        public int Harvest(ResourceInventory inventory)
        {
            if (m_Harvested || m_Resource == null) return 0;

            int amount = Random.Range(m_MinAmount, m_MaxAmount + 1);
            if (inventory != null) inventory.Add(m_Resource, amount);

            m_Harvested = true;
            gameObject.SetActive(false);

            if (m_Respawn) m_RespawnAt = Time.time + m_RespawnTime;
            if (!string.IsNullOrEmpty(m_SpawnKey)) Harvested?.Invoke(m_SpawnKey);
            return amount;
        }

        /// <summary>
        /// Restores the harvested state from a save. Harvested nodes are deactivated
        /// again (with a fresh respawn timer if they respawn); unharvested nodes are
        /// reactivated so a load can also undo harvests made after the save.
        /// </summary>
        public void RestoreHarvested(bool harvested)
        {
            if (harvested)
            {
                if (m_Harvested) return;
                m_Harvested = true;
                if (m_Respawn) m_RespawnAt = Time.time + m_RespawnTime;
                gameObject.SetActive(false);
            }
            else
            {
                if (!m_Harvested) return;
                m_Harvested = false;
                m_RespawnAt = 0f;
                gameObject.SetActive(true);
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (m_Harvested || m_Resource == null) return;
            ResourceInventory inventory = other != null
                ? other.GetComponentInParent<ResourceInventory>()
                : null;
            if (inventory == null) inventory = FindFirstObjectByType<ResourceInventory>();
            Harvest(inventory);
        }

        private void Update()
        {
            if (!m_Respawn || !m_Harvested) return;
            if (Time.time >= m_RespawnAt)
            {
                m_Harvested = false;
                gameObject.SetActive(true);
            }
        }
    }
}