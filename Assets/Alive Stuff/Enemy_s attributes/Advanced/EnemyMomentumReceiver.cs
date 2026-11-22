using Portals.Traversal;
using UnityEngine;

namespace Portals.Enemy
{
    /// <summary>
    /// Routes portal traversal momentum into EnemyMovement so the AI keeps its dash and
    /// pursuit velocity through a portal, exactly like the player keeps its momentum.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EnemyMomentumReceiver : MonoBehaviour, IPortalMomentumReceiver
    {
        private EnemyMovement m_Movement;

        private void Awake()
        {
            m_Movement = GetComponent<EnemyMovement>();
        }

        public void ApplyPortalRotation(Quaternion rotation)
        {
            if (m_Movement == null) m_Movement = GetComponent<EnemyMovement>();
            if (m_Movement != null) m_Movement.RotateMomentum(rotation);
        }
    }
}
