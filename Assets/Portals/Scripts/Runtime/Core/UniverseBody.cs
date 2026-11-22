using UnityEngine;

namespace Portals.Core
{
    /// <summary>
    /// Marks a GameObject as belonging to a universe. For Rigidbodies it optionally
    /// applies that universe's physics profile (gravity/damping) without touching the
    /// global Physics settings, so different universes can coexist in one scene.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class UniverseBody : MonoBehaviour
    {
        [SerializeField] private UniverseId m_Universe;
        [SerializeField, Tooltip("Apply the universe physics profile to this Rigidbody.")]
        private bool m_ApplyPhysicsProfile = true;

        private Rigidbody m_Body;
        private bool m_PreviousUseGravity;
        private bool m_HasPreviousGravityState;
        private bool m_OverridingGravity;

        public UniverseId Universe => m_Universe;

        private void Awake()
        {
            m_Body = GetComponent<Rigidbody>();
        }

        private void OnEnable()
        {
            InterversalPortalSystem.RegisterBody(this);
            ApplyProfile();
        }

        private void OnDisable()
        {
            RestoreGravity();
            InterversalPortalSystem.UnregisterBody(this);
        }

        /// <summary>Used by portal traversal when an entity changes universe.</summary>
        public void SetUniverse(UniverseId universe)
        {
            if (m_Universe == universe) return;

            RestoreGravity();
            m_Universe = universe;
            InterversalPortalSystem.RegisterBody(this);
            ApplyProfile();
        }

        private UniversePhysicsProfile GetProfile()
        {
            InterversalPortalSystem system = InterversalPortalSystem.Instance;
            return system != null ? system.GetPhysicsProfile(m_Universe) : null;
        }

        private void ApplyProfile()
        {
            if (m_Body == null || !m_ApplyPhysicsProfile) return;

            UniversePhysicsProfile profile = GetProfile();
            if (profile == null) return;

            if (!m_HasPreviousGravityState)
            {
                m_PreviousUseGravity = m_Body.useGravity;
                m_HasPreviousGravityState = true;
            }

            m_Body.linearDamping = profile.linearDamping;
            m_Body.angularDamping = profile.angularDamping;

            if (profile.overrideGravity)
            {
                m_Body.useGravity = false;
                m_OverridingGravity = true;
            }
            else
            {
                m_Body.useGravity = m_PreviousUseGravity;
                m_OverridingGravity = false;
            }
        }

        private void RestoreGravity()
        {
            if (m_Body == null || !m_HasPreviousGravityState) return;
            if (m_OverridingGravity) m_Body.useGravity = m_PreviousUseGravity;
            m_HasPreviousGravityState = false;
            m_OverridingGravity = false;
        }

        private void FixedUpdate()
        {
            if (m_Body == null || m_Body.isKinematic || !m_OverridingGravity) return;

            UniversePhysicsProfile profile = GetProfile();
            if (profile == null) return;

            // Acceleration (not Force) keeps this mass-independent and matches
            // Physics.gravity semantics.
            if (profile.gravity.sqrMagnitude > 0f)
                m_Body.AddForce(profile.gravity, ForceMode.Acceleration);
        }
    }
}
