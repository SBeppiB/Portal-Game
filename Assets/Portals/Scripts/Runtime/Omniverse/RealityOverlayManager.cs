using System.Collections.Generic;
using UnityEngine;

namespace Game.Omniverse
{
    /// <summary>
    /// Allows multiple universes to occupy the same space. Switches visibility of
    /// per-universe content roots. Does not touch physics or portals.
    /// </summary>
    public class RealityOverlayManager : MonoBehaviour
    {
        [SerializeField] private List<GameObject> m_UniverseRoots = new List<GameObject>();
        [SerializeField] private int m_ActiveIndex;

        public int ActiveIndex => m_ActiveIndex;
        public int Count => m_UniverseRoots.Count;

        public void SetActiveUniverse(int index)
        {
            if (m_UniverseRoots.Count == 0) return;
            m_ActiveIndex = Mathf.Clamp(index, 0, m_UniverseRoots.Count - 1);
            for (int i = 0; i < m_UniverseRoots.Count; i++)
            {
                if (m_UniverseRoots[i] != null) m_UniverseRoots[i].SetActive(i == m_ActiveIndex);
            }
        }

        public void RegisterRoot(GameObject root)
        {
            if (root != null && !m_UniverseRoots.Contains(root)) m_UniverseRoots.Add(root);
        }
    }
}