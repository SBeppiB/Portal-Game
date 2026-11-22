using System;
using UnityEngine;

namespace Portals.Core
{
    /// <summary>
    /// Shared geometry, state and frame maths for both portal technologies.
    /// A portal is an ellipse lying in the local XY plane, facing local +Z.
    /// The visual representation (gun, ring, shader, particles) is intentionally
    /// NOT part of this component and can be authored separately.
    /// </summary>
    public abstract class PortalEndpoint : MonoBehaviour
    {
        [Header("Portal Geometry")]
        [SerializeField, Tooltip("Optional child transform defining the opening. Defaults to this transform.")]
        private Transform m_Opening;

        [SerializeField, Tooltip("Half width (X) and half height (Y) of the elliptical opening, in metres.")]
        private Vector2 m_OpeningHalfSize = new Vector2(1f, 1.5f);

        [SerializeField, Min(0f), Tooltip("Distance in front of the exit plane to place a traversing object.")]
        private float m_ExitOffset = 0.05f;

        [SerializeField] private bool m_IsActive = true;

        // Separate from m_IsActive so a placed portal can keep its logical/visible state
        // while its opening animation plays. Traversal alone consults this flag; closing
        // portals clear it immediately, before the shrink animation starts.
        private bool m_TraversalEnabled = true;

        private Vector3 m_LastPosition;
        private Quaternion m_LastRotation;
        private bool m_HasMotionSample;
        private float m_LastMotionTime;

        /// <summary>Which portal technology this endpoint belongs to.</summary>
        public abstract PortalKind Kind { get; }

        public Transform Opening => m_Opening != null ? m_Opening : transform;

        public Vector2 OpeningHalfSize
        {
            get => m_OpeningHalfSize;
            set => m_OpeningHalfSize = new Vector2(Mathf.Max(0f, value.x), Mathf.Max(0f, value.y));
        }

        public float ExitOffset
        {
            get => m_ExitOffset;
            set => m_ExitOffset = Mathf.Max(0f, value);
        }

        public bool IsActive => m_IsActive;

        /// <summary>
        /// True only while the portal is active AND its opening is fully usable.
        /// PortalTraverser uses this instead of IsActive so open/close animations can
        /// temporarily block transport without hiding the surface or changing placement.
        /// </summary>
        public bool IsTraversable => m_IsActive && m_TraversalEnabled;

        public Vector3 Position => Opening.position;
        public Quaternion Rotation => Opening.rotation;
        public Vector3 Forward => Opening.forward;
        public Vector3 Up => Opening.up;

        /// <summary>Velocity of the portal surface in world space (for moving endpoints).</summary>
        public Vector3 LinearVelocity { get; private set; }

        /// <summary>Angular velocity of the portal surface in radians/second.</summary>
        public Vector3 AngularVelocity { get; private set; }

        public event Action<PortalEndpoint> ActiveChanged;

        /// <summary>The destination endpoint, or null when unlinked/inactive.</summary>
        public abstract PortalEndpoint GetLinkedEndpoint();

        /// <summary>
        /// True when a partner endpoint has been assigned, regardless of whether it is
        /// currently active. Placement uses this so the first of a pair can be placed
        /// before its partner is active; traversal still uses GetLinkedEndpoint().
        /// </summary>
        public virtual bool HasConfiguredLink => GetLinkedEndpoint() != null;

        /// <summary>The assigned partner even when inactive (used by debug visualisation).</summary>
        public virtual PortalEndpoint ConfiguredPartner => GetLinkedEndpoint();

        protected virtual void Awake()
        {
            m_LastPosition = Position;
            m_LastRotation = Rotation;
            m_LastMotionTime = Time.time;
            m_HasMotionSample = true;
        }

        /// <summary>
        /// Tracks endpoint motion once per frame so traversal can account for
        /// portals that are moving or rotating.
        /// </summary>
        protected virtual void Update()
        {
            UpdateMotion();
        }

        public void UpdateMotion() => UpdateMotion(Time.time);

        /// <summary>Samples motion at an explicit timestamp (useful for deterministic tests).</summary>
        public void UpdateMotion(float now)
        {
            float dt = now - m_LastMotionTime;
            if (dt <= 1e-5f) return;

            Vector3 position = Position;
            Quaternion rotation = Rotation;

            if (m_HasMotionSample)
            {
                Vector3 displacement = position - m_LastPosition;
                // A very large jump means the portal itself was teleported; do not
                // turn that into an absurd velocity impulse for traversing objects.
                LinearVelocity = displacement.sqrMagnitude > 2500f
                    ? Vector3.zero
                    : displacement / dt;

                Quaternion delta = rotation * Quaternion.Inverse(m_LastRotation);
                delta.ToAngleAxis(out float angleDegrees, out Vector3 axis);
                if (angleDegrees > 180f) angleDegrees -= 360f;
                AngularVelocity = angleDegrees * Mathf.Deg2Rad / dt * axis.normalized;
                if (float.IsNaN(AngularVelocity.x) || float.IsNaN(AngularVelocity.y) || float.IsNaN(AngularVelocity.z))
                    AngularVelocity = Vector3.zero;
            }

            m_LastPosition = position;
            m_LastRotation = rotation;
            m_LastMotionTime = now;
            m_HasMotionSample = true;
        }

        public void SetActive(bool active)
        {
            if (m_IsActive == active) return;
            m_IsActive = active;
            // A state change resets the animation gate. PlayOpen deliberately re-disables
            // traversal after placement; PlayClose deactivates to block it immediately.
            m_TraversalEnabled = active;
            ActiveChanged?.Invoke(this);
        }

        /// <summary>
        /// Temporarily blocks or restores traversal for an active endpoint. Used by the
        /// opening animation (blocked until fully open) and closing animation (blocked).
        /// Does not affect placement, visuals, timers or pair completeness.
        /// </summary>
        public void SetTraversalEnabled(bool enabled)
        {
            m_TraversalEnabled = enabled;
        }

        public Vector3 WorldToLocalPoint(Vector3 worldPoint) => Opening.InverseTransformPoint(worldPoint);

        public Vector3 LocalToWorldPoint(Vector3 localPoint) => Opening.TransformPoint(localPoint);

        public Vector3 WorldToLocalDirection(Vector3 worldDirection) =>
            Quaternion.Inverse(Rotation) * worldDirection;

        /// <summary>True when a portal-local point lies inside the elliptical opening.</summary>
        public bool ContainsLocalPoint(Vector3 localPoint) =>
            PortalMath.IsInsideOpening(new Vector2(localPoint.x, localPoint.y), m_OpeningHalfSize);

        /// <summary>
        /// Maps a world pose from this portal (entrance) to the given exit portal.
        /// Virtual so interversal mapping modes can deliberately differ.
        /// </summary>
        public virtual void ComputeExitPose(
            PortalEndpoint exit,
            Vector3 worldPosition, Quaternion worldRotation,
            out Vector3 exitPosition, out Quaternion exitRotation)
        {
            ComputeExitPose(exit, worldPosition, worldRotation, false,
                out exitPosition, out exitRotation);
        }

        /// <summary>
        /// Pose mapping with explicit entry side. See <see cref="PortalMath.ComputeFrameRotation(Quaternion, Quaternion, bool)"/>.
        /// </summary>
        public virtual void ComputeExitPose(
            PortalEndpoint exit,
            Vector3 worldPosition, Quaternion worldRotation,
            bool enteredFromBack,
            out Vector3 exitPosition, out Quaternion exitRotation)
        {
            if (exit == null)
            {
                exitPosition = worldPosition;
                exitRotation = worldRotation;
                return;
            }

            Quaternion frameRotation = PortalMath.ComputeFrameRotation(
                Rotation, exit.Rotation, !enteredFromBack);

            // Point: p' = X + R * (p - E). Use the entrance plane centre as the origin.
            Vector3 relative = worldPosition - Position;
            exitPosition = exit.Position + frameRotation * relative;

            // Rotation: q' = R * q. Same frame rotation as velocity/direction.
            exitRotation = frameRotation * worldRotation;

            // Nudge out along the exit's outward normal so the object is not left
            // straddling the surface (which would immediately re-trigger traversal).
            exitPosition += exit.Forward * exit.ExitOffset;
        }

        /// <summary>
        /// Maps linear and angular velocity from this portal (entrance) to the exit,
        /// including motion of the portal endpoints themselves.
        /// </summary>
        public virtual void ComputeExitMotion(
            PortalEndpoint exit,
            Vector3 linearVelocity, Vector3 angularVelocity,
            out Vector3 exitLinearVelocity, out Vector3 exitAngularVelocity)
        {
            ComputeExitMotion(exit, linearVelocity, angularVelocity, false,
                out exitLinearVelocity, out exitAngularVelocity);
        }

        /// <summary>Motion mapping with explicit entry side.</summary>
        public virtual void ComputeExitMotion(
            PortalEndpoint exit,
            Vector3 linearVelocity, Vector3 angularVelocity,
            bool enteredFromBack,
            out Vector3 exitLinearVelocity, out Vector3 exitAngularVelocity)
        {
            if (exit == null)
            {
                exitLinearVelocity = linearVelocity;
                exitAngularVelocity = angularVelocity;
                return;
            }

            Quaternion frameRotation = PortalMath.ComputeFrameRotation(
                Rotation, exit.Rotation, !enteredFromBack);

            exitLinearVelocity =
                frameRotation * (linearVelocity - LinearVelocity) + exit.LinearVelocity;

            exitAngularVelocity =
                frameRotation * (angularVelocity - AngularVelocity) + exit.AngularVelocity;
        }

        /// <summary>Full homogeneous entrance-to-exit matrix (for advanced consumers).</summary>
        public Matrix4x4 BuildTraversalMatrixTo(PortalEndpoint exit)
        {
            if (exit == null) return Matrix4x4.identity;
            return PortalMath.BuildTraversalMatrix(Position, Rotation, exit.Position, exit.Rotation);
        }

        protected virtual void OnDrawGizmosSelected()
        {
            Gizmos.color = IsActive ? Color.cyan : Color.gray;

            // Draw the elliptical opening by sampling its rim.
            const int segments = 32;
            Vector3 previous = Vector3.zero;
            for (int i = 0; i <= segments; i++)
            {
                float angle = i / (float)segments * Mathf.PI * 2f;
                Vector3 local = new Vector3(
                    Mathf.Cos(angle) * m_OpeningHalfSize.x,
                    Mathf.Sin(angle) * m_OpeningHalfSize.y,
                    0f);
                Vector3 point = LocalToWorldPoint(local);
                if (i > 0) Gizmos.DrawLine(previous, point);
                previous = point;
            }

            Gizmos.DrawRay(Position, Forward * 0.75f);
        }
    }
}
