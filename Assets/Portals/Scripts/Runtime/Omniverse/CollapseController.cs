using System;
using UnityEngine;
using UnityEngine.Events;

namespace Game.Omniverse
{
    public enum CollapseStage { Stable, Stress, Fracture, Collapse }

    /// <summary>Drives reality collapse stages with event hooks.</summary>
    public class CollapseController : MonoBehaviour
    {
        [SerializeField] private CollapseStage m_Stage = CollapseStage.Stable;
        [SerializeField] private float m_StressThreshold = 0.3f;
        [SerializeField] private float m_FractureThreshold = 0.6f;
        [SerializeField] private float m_CollapseThreshold = 0.9f;

        public CollapseStage Stage => m_Stage;

        public event Action<CollapseStage> StageChanged;

        [SerializeField] private UnityEvent m_OnStress;
        [SerializeField] private UnityEvent m_OnFracture;
        [SerializeField] private UnityEvent m_OnCollapse;

        public void SetInstability(float value01)
        {
            CollapseStage next;
            if (value01 >= m_CollapseThreshold) next = CollapseStage.Collapse;
            else if (value01 >= m_FractureThreshold) next = CollapseStage.Fracture;
            else if (value01 >= m_StressThreshold) next = CollapseStage.Stress;
            else next = CollapseStage.Stable;

            if (next == m_Stage) return;
            m_Stage = next;
            StageChanged?.Invoke(m_Stage);
            switch (m_Stage)
            {
                case CollapseStage.Stress: m_OnStress?.Invoke(); break;
                case CollapseStage.Fracture: m_OnFracture?.Invoke(); break;
                case CollapseStage.Collapse: m_OnCollapse?.Invoke(); break;
            }
        }
    }
}