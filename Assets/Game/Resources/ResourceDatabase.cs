using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Game
{
    [CreateAssetMenu(menuName = "Game/Resource Database", fileName = "ResourceDatabase")]
    public class ResourceDatabase : ScriptableObject
    {
        [SerializeField] private List<ResourceDefinition> m_Resources = new List<ResourceDefinition>();

        public IReadOnlyList<ResourceDefinition> All => m_Resources;

        public ResourceDefinition GetById(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            return m_Resources.FirstOrDefault(r => r != null && r.Id == id);
        }
    }
}