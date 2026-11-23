using UnityEngine;

namespace Game
{
    [CreateAssetMenu(menuName = "Game/Resource Definition", fileName = "Resource_")]
    public class ResourceDefinition : ScriptableObject
    {
        [SerializeField] private string m_Id;
        [SerializeField] private string m_DisplayName;
        [SerializeField, Range(0, 5)] private int m_Tier;
        [SerializeField] private Sprite m_Icon;
        [TextArea] [SerializeField] private string m_Description;

        public string Id => string.IsNullOrEmpty(m_Id) ? name : m_Id;
        public string DisplayName => string.IsNullOrEmpty(m_DisplayName) ? name : m_DisplayName;
        public int Tier => m_Tier;
        public Sprite Icon => m_Icon;
        public string Description => m_Description;
    }
}