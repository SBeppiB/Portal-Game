using UnityEngine;

namespace Portals.Core
{
    /// <summary>
    /// How an interversal link maps an object from one universe to another.
    /// These modes are intentionally different from the locaversal frame transform:
    /// "corresponding location" links need not share an orientation with the exit ring.
    /// </summary>
    public enum InterversalMappingMode
    {
        /// <summary>Use the portal frames (same maths as locaversal) across universes.</summary>
        ViaPortalFrame = 0,

        /// <summary>
        /// Translate by a fixed world offset, preserving rotation and velocity.
        /// Useful for "same coordinates, different universe" links.
        /// </summary>
        PreserveWorldOffset = 1
    }

    /// <summary>One endpoint of a portal link between two universes.</summary>
    [DisallowMultipleComponent]
    public sealed class InterversalPortal : PortalEndpoint
    {
        [SerializeField, Tooltip("Universe this endpoint exists in.")]
        private UniverseId m_Universe;

        [SerializeField, Tooltip("Endpoint in the destination universe.")]
        private InterversalPortal m_LinkedPortal;

        [SerializeField] private InterversalMappingMode m_MappingMode = InterversalMappingMode.ViaPortalFrame;

        [SerializeField, Tooltip("World-space translation used by the PreserveWorldOffset mapping mode.")]
        private Vector3 m_WorldOffset;

        public override PortalKind Kind => PortalKind.Interversal;

        public UniverseId Universe => m_Universe;
        public InterversalMappingMode MappingMode => m_MappingMode;
        public Vector3 WorldOffset => m_WorldOffset;

        public override PortalEndpoint GetLinkedEndpoint() =>
            m_LinkedPortal != null && m_LinkedPortal.IsActive ? m_LinkedPortal : null;

        public override bool HasConfiguredLink => m_LinkedPortal != null;
        public override PortalEndpoint ConfiguredPartner => m_LinkedPortal;

        public void Configure(UniverseId universe, InterversalPortal linkedPortal,
            InterversalMappingMode mappingMode, Vector3 worldOffset)
        {
            m_Universe = universe;
            m_LinkedPortal = linkedPortal;
            m_MappingMode = mappingMode;
            m_WorldOffset = worldOffset;
        }

        /// <summary>
        /// Interversal PreserveWorldOffset links deliberately ignore the exit ring's
        /// rotation: the object keeps its orientation and is simply moved to the
        /// corresponding point in the other universe.
        /// </summary>
        public override void ComputeExitPose(
            PortalEndpoint exit,
            Vector3 worldPosition, Quaternion worldRotation,
            bool enteredFromBack,
            out Vector3 exitPosition, out Quaternion exitRotation)
        {
            if (m_MappingMode == InterversalMappingMode.PreserveWorldOffset)
            {
                exitPosition = worldPosition + m_WorldOffset;
                exitRotation = worldRotation;
                return;
            }

            base.ComputeExitPose(exit, worldPosition, worldRotation, enteredFromBack,
                out exitPosition, out exitRotation);
        }

        /// <summary>PreserveWorldOffset also leaves momentum untouched.</summary>
        public override void ComputeExitMotion(
            PortalEndpoint exit,
            Vector3 linearVelocity, Vector3 angularVelocity,
            bool enteredFromBack,
            out Vector3 exitLinearVelocity, out Vector3 exitAngularVelocity)
        {
            if (m_MappingMode == InterversalMappingMode.PreserveWorldOffset)
            {
                exitLinearVelocity = linearVelocity;
                exitAngularVelocity = angularVelocity;
                return;
            }

            base.ComputeExitMotion(exit, linearVelocity, angularVelocity, enteredFromBack,
                out exitLinearVelocity, out exitAngularVelocity);
        }

        private void OnEnable() => InterversalPortalSystem.RegisterPortal(this);

        private void OnDisable() => InterversalPortalSystem.UnregisterPortal(this);
    }
}
