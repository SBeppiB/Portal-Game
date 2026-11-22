using System;
using System.Collections.Generic;
using Portals.Core;
using UnityEngine;
using UnityEngine.Profiling;

namespace Portals.Traversal
{
    /// <summary>Data published after a successful traversal (for VFX/audio/gameplay hooks).</summary>
    public struct PortalTraversalInfo
    {
        public PortalEndpoint Entrance;
        public PortalEndpoint Exit;
        public Vector3 EntryPosition;
        public Vector3 ExitPosition;
        public Vector3 EntryVelocity;
        public Vector3 ExitVelocity;
        public Quaternion RotationDelta;
    }

    /// <summary>
    /// Detects portal-plane crossings for one moving entity and transports it.
    ///
    /// Detection is a swept segment test (previous position -> current position), so
    /// fast movement cannot tunnel through the portal plane. Traversal runs after all
    /// Update-based controllers via DefaultExecutionOrder(500); tests and scripted
    /// movement can call TickDetection() directly for deterministic stepping.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(500)]
    public sealed class PortalTraverser : MonoBehaviour
    {
        [Header("Detection")]
        [SerializeField, Tooltip("Explicit portal list. Leave empty to use all registered portals.")]
        private PortalEndpoint[] m_Portals = Array.Empty<PortalEndpoint>();

        [SerializeField, Tooltip("When no explicit list is set, discover portals from both systems.")]
        private bool m_UseRegisteredPortals = true;

        [SerializeField, Min(0f), Tooltip("Broad-phase radius in metres. 0 = unlimited.")]
        private float m_MaxPortalDistance = 0f;

        [SerializeField, Min(0f), Tooltip("Optional safety cap in m/s. 0 = unlimited.")]
        private float m_MaxTraversalSpeed = 0f;

        [SerializeField, Tooltip("Ignore the exit portal until the entity is this far in front of it."), Min(0f)]
        private float m_ReentryGraceDistance = 0.4f;

        [SerializeField, Min(0f)] private float m_IgnoreTimeout = 0.5f;

        [Header("Behaviour")]
        [SerializeField] private bool m_SwitchUniverseOnInterversalTravel = true;
        [SerializeField] private bool m_NotifyMomentumReceivers = true;

        [Header("Object Validation")]
        [SerializeField, Tooltip("Reject objects whose colliders are wider/taller than the portal opening.")]
        private bool m_ValidateObjectSize = true;
        [SerializeField, Min(1f), Tooltip("Small tolerance applied to the aperture when checking object size.")]
        private float m_ApertureTolerance = 1.05f;
        [SerializeField, Tooltip("Include colliders on child objects when measuring size.")]
        private bool m_IncludeChildColliders = true;
        [SerializeField, Tooltip("Detect crossing from the collider body when the origin alone does not cross (e.g. a CharacterController pressed against a wall portal).")]
        private bool m_BodyIntersectionFallback = true;
        [SerializeField, Min(0f), Tooltip("How close the collider's front face must get to the portal plane to count as entering. Must cover the surface offset used by PortalPlacer.")]
        private float m_BodyContactTolerance = 0.6f;

        [Header("Floor Exit Assist (player only)")]
        [SerializeField, Tooltip("Give character-controller players a small assist when exiting a floor portal.")]
        private bool m_EnableFloorExitAssist = false;
        [SerializeField, Min(0f), Tooltip("Upward velocity in m/s added to existing momentum.")]
        private float m_ExitAssistUpwardBoost = 5f;
        [SerializeField, Min(0f), Tooltip("Horizontal velocity in m/s added along the deterministic exit direction.")]
        private float m_ExitAssistHorizontalBoost = 1.5f;
        [SerializeField, Tooltip("Degrees to rotate the horizontal impulse about world up, measured from the player's post-traversal facing.")]
        private float m_ExitAssistAngle = 0f;
        [SerializeField, Range(0f, 1f), Tooltip("Minimum dot(exit normal, up) for a portal to count as floor-mounted.")]
        private float m_FloorNormalThreshold = 0.8f;
        [SerializeField, Min(0f), Tooltip("Traversal grace period in seconds applied after an assisted floor exit.")]
        private float m_ExitAssistGracePeriod = 0.5f;

        public event Action<PortalTraversalInfo> Traversed;

        /// <summary>Raised with the world-space impulse when floor exit assist is applied.</summary>
        public event Action<Vector3> ExitAssistApplied;

        private Transform m_Transform;
        private Rigidbody m_Body;
        private CharacterController m_Controller;
        private IPortalMomentumReceiver[] m_MomentumReceivers = Array.Empty<IPortalMomentumReceiver>();
        private Collider[] m_Colliders = Array.Empty<Collider>();
        private readonly List<PortalEndpoint> m_Candidates = new List<PortalEndpoint>();

        private Vector3 m_PreviousPosition;
        private bool m_HasPrevious;
        private PortalEndpoint m_IgnoredPortal;
        private float m_IgnoreUntil;
        private bool m_AssistGraceActive;
        private int m_LastTraversalFrame = -1;

        public PortalEndpoint IgnoredPortal => m_IgnoredPortal;
        public bool HasTraversedThisFrame => m_LastTraversalFrame == Time.frameCount;
        public float IgnoreUntil => m_IgnoreUntil;

        public bool FloorExitAssistEnabled => m_EnableFloorExitAssist;
        public float ExitAssistUpwardBoost => m_ExitAssistUpwardBoost;
        public float ExitAssistHorizontalBoost => m_ExitAssistHorizontalBoost;
        public float ExitAssistAngle => m_ExitAssistAngle;
        public float FloorNormalThreshold => m_FloorNormalThreshold;
        public float ExitAssistGracePeriod => m_ExitAssistGracePeriod;

        /// <summary>Impulse used by the most recent traversal (zero when assist did not apply).</summary>
        public Vector3 LastExitAssistImpulse { get; private set; }

        /// <summary>True only when the most recent traversal used floor exit assist.</summary>
        public bool LastExitAssistApplied { get; private set; }

        /// <summary>Configures the optional floor exit assist at runtime.</summary>
        public void ConfigureFloorExitAssist(bool enabled, float upwardBoost, float horizontalBoost,
            float exitAngle, float floorNormalThreshold, float gracePeriod)
        {
            m_EnableFloorExitAssist = enabled;
            m_ExitAssistUpwardBoost = Mathf.Max(0f, upwardBoost);
            m_ExitAssistHorizontalBoost = Mathf.Max(0f, horizontalBoost);
            m_ExitAssistAngle = exitAngle;
            m_FloorNormalThreshold = Mathf.Clamp01(floorNormalThreshold);
            m_ExitAssistGracePeriod = Mathf.Max(0f, gracePeriod);
        }

        /// <summary>True when a surface normal points upward enough to be considered a floor.</summary>
        public static bool IsFloorNormal(Vector3 normal, float threshold)
        {
            if (normal.sqrMagnitude < 1e-8f) return false;
            return Vector3.Dot(normal.normalized, Vector3.up) >= Mathf.Clamp01(threshold);
        }

        /// <summary>
        /// Deterministic assist impulse: world-up boost plus a horizontal boost rotated by
        /// a fixed angle from the player's post-traversal facing (or the exit normal when
        /// the facing has no horizontal component). No randomness is involved.
        /// </summary>
        public static Vector3 ComputeExitAssistImpulse(Vector3 exitForward, Vector3 playerForward,
            float upwardBoost, float horizontalBoost, float exitAngle)
        {
            Vector3 horizontal = Vector3.ProjectOnPlane(playerForward, Vector3.up);
            if (horizontal.sqrMagnitude < 1e-8f) horizontal = Vector3.ProjectOnPlane(exitForward, Vector3.up);
            if (horizontal.sqrMagnitude > 1e-8f) horizontal.Normalize();

            horizontal = Quaternion.AngleAxis(exitAngle, Vector3.up) * horizontal;
            return Vector3.up * Mathf.Max(0f, upwardBoost) +
                   horizontal * Mathf.Max(0f, horizontalBoost);
        }

        /// <summary>Disable when an external system drives TickDetection explicitly.</summary>
        public bool AutoDetect { get; set; } = true;

        /// <summary>Overrides the explicit portal list (also used by tests).</summary>
        public void SetPortals(params PortalEndpoint[] portals)
        {
            EnsureInitialized();
            m_Portals = portals ?? Array.Empty<PortalEndpoint>();
        }

        private bool m_Initialized;

        private void Awake()
        {
            EnsureInitialized();
        }

        /// <summary>Resolves component references lazily (also safe outside play mode).</summary>
        public void EnsureInitialized()
        {
            if (m_Initialized) return;
            m_Initialized = true;

            m_Transform = transform;
            m_Body = GetComponent<Rigidbody>();
            m_Controller = GetComponent<CharacterController>();

            List<IPortalMomentumReceiver> receivers = new List<IPortalMomentumReceiver>();
            foreach (MonoBehaviour behaviour in GetComponents<MonoBehaviour>())
            {
                if (behaviour is IPortalMomentumReceiver receiver) receivers.Add(receiver);
            }
            m_MomentumReceivers = receivers.ToArray();

            m_Colliders = m_IncludeChildColliders
                ? GetComponentsInChildren<Collider>(true)
                : GetComponents<Collider>();
        }

        private void LateUpdate()
        {
            if (AutoDetect) TickDetection();
        }

        /// <summary>
        /// Performs one detection step. Public so tests or custom movement loops can
        /// drive it explicitly. Returns true if a traversal happened.
        /// </summary>
        public bool TickDetection()
        {
            Profiler.BeginSample("PortalTraverser.TickDetection");
            EnsureInitialized();
            Vector3 current = m_Body != null ? m_Body.position : m_Transform.position;

            UpdateIgnoreState(current);

            bool traversed = false;
            if (m_HasPrevious && !HasTraversedThisFrame)
            {
                Vector3 delta = current - m_PreviousPosition;

                if (m_MaxTraversalSpeed <= 0f ||
                    delta.sqrMagnitude <= m_MaxTraversalSpeed * Time.deltaTime * m_MaxTraversalSpeed * Time.deltaTime)
                {
                    BuildCandidateList(current);
                    for (int i = 0; i < m_Candidates.Count; i++)
                    {
                        PortalEndpoint entrance = m_Candidates[i];
                        if (entrance == null || !entrance.IsTraversable || entrance == m_IgnoredPortal) continue;

                        PortalEndpoint exit = entrance.GetLinkedEndpoint();
                        if (exit == null || !exit.IsTraversable) continue;

                        Vector3 previousLocal = entrance.WorldToLocalPoint(m_PreviousPosition);
                        Vector3 currentLocal = entrance.WorldToLocalPoint(current);

                        bool crossingFound = PortalMath.TryFindPlaneCrossing(previousLocal, currentLocal,
                            out Vector3 hitLocal, out _, out bool enteredFromBack);

                        if (!crossingFound)
                        {
                            // Grounded characters keep their origin (feet) on the front
                            // side of a floor portal. Fall back to the capsule body.
                            if (!m_BodyIntersectionFallback ||
                                !TryBodyPlaneCrossing(entrance, delta, out enteredFromBack))
                                continue;
                        }
                        else if (!entrance.ContainsLocalPoint(hitLocal))
                        {
                            continue;
                        }

                        if (m_ValidateObjectSize &&
                            !ObjectFitsAperture(entrance, out string sizeReason))
                        {
                            Debug.LogWarning($"[PortalTraverser] '{gameObject.name}' cannot enter " +
                                             $"'{entrance.name}': {sizeReason}");
                            continue;
                        }

                        traversed = TryTraverse(entrance, exit, current, enteredFromBack);
                        if (traversed) break;
                    }
                }
            }

            // Store the (possibly updated) position so the next segment starts from
            // the exit side and cannot immediately re-detect the same crossing.
            m_PreviousPosition = m_Body != null ? m_Body.position : m_Transform.position;
            m_HasPrevious = true;
            Profiler.EndSample();
            return traversed;
        }

        private void UpdateIgnoreState(Vector3 current)
        {
            if (m_IgnoredPortal == null) return;

            if (Time.time >= m_IgnoreUntil)
            {
                m_IgnoredPortal = null;
                m_AssistGraceActive = false;
                return;
            }

            // An assisted floor exit uses the configured time-based grace (the player is
            // deliberately launched upward and must not loop back in). Normal traversals
            // keep the original spatial-clear behaviour unchanged.
            if (m_AssistGraceActive) return;

            bool movedClear = m_IgnoredPortal.WorldToLocalPoint(current).z > m_ReentryGraceDistance;
            if (movedClear) m_IgnoredPortal = null;
        }

        private void BuildCandidateList(Vector3 current)
        {
            m_Candidates.Clear();

            if (m_Portals != null && m_Portals.Length > 0)
            {
                for (int i = 0; i < m_Portals.Length; i++)
                {
                    if (m_Portals[i] != null) m_Candidates.Add(m_Portals[i]);
                }
                return;
            }

            if (!m_UseRegisteredPortals) return;

            float maxSqr = m_MaxPortalDistance > 0f ? m_MaxPortalDistance * m_MaxPortalDistance : float.MaxValue;
            AddClosePortals(LocaversalPortalSystem.AllPortals, current, maxSqr);
            AddClosePortals(InterversalPortalSystem.AllPortals, current, maxSqr);
        }

        private void AddClosePortals(IEnumerable<PortalEndpoint> portals, Vector3 current, float maxSqr)
        {
            foreach (PortalEndpoint portal in portals)
            {
                if (portal == null) continue;
                if ((portal.Position - current).sqrMagnitude <= maxSqr) m_Candidates.Add(portal);
            }
        }

        /// <summary>
        /// Detects a portal-plane crossing from the object's collision body rather than
        /// its origin. Used for grounded CharacterControllers standing on floor portals,
        /// where the origin stays above the plane while the capsule already straddles it.
        /// Requires the body to have approached from the front (or currently straddle the
        /// plane), to be moving toward/behind the plane, and to be centred inside the
        /// aperture, which prevents false positives from nearby geometry.
        /// </summary>
        private bool TryBodyPlaneCrossing(PortalEndpoint portal, Vector3 delta, out bool enteredFromBack)
        {
            enteredFromBack = false;
            if (m_Colliders == null || m_Colliders.Length == 0) return false;

            Vector3 min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
            Vector3 max = new Vector3(float.MinValue, float.MinValue, float.MinValue);
            Vector3 previousMin = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
            Vector3 previousMax = new Vector3(float.MinValue, float.MinValue, float.MinValue);
            bool measured = false;
            Vector3 previousOffset = -delta;

            if (m_Controller != null && m_Controller.enabled)
            {
                AccumulateBodyBounds(m_Controller, portal, Vector3.zero, ref min, ref max);
                AccumulateBodyBounds(m_Controller, portal, previousOffset, ref previousMin, ref previousMax);
                measured = true;
            }
            else
            {
                for (int i = 0; i < m_Colliders.Length; i++)
                {
                    Collider collider = m_Colliders[i];
                    if (collider == null || !collider.enabled) continue;
                    AccumulateBodyBounds(collider, portal, Vector3.zero, ref min, ref max);
                    AccumulateBodyBounds(collider, portal, previousOffset, ref previousMin, ref previousMax);
                    measured = true;
                }
            }

            if (!measured) return false;

            // A CharacterController (and any collider-backed object) is stopped by the
            // wall's collider before its body can pass behind the portal plane: the portal
            // sits m_SurfaceOffset (0.05 m) in front of that wall. Treat the body as
            // entering when its front face reaches the plane within a small tolerance,
            // provided it was on the front side and is not moving away.
            float reach = Mathf.Max(0f, m_BodyContactTolerance);
            bool fromFront = previousMax.z > 0f && max.z > 0f && min.z <= reach;
            bool fromBack = previousMax.z <= 0f && min.z < 0f && max.z >= -reach;
            bool straddling = min.z <= 0f && max.z >= 0f;
            if (!fromFront && !fromBack && !straddling) return false;

            float centreZ = (min.z + max.z) * 0.5f;
            float previousCentreZ = (previousMin.z + previousMax.z) * 0.5f;
            if (centreZ > previousCentreZ + 0.02f) return false; // moving away from the plane

            Vector2 centreXY = new Vector2((min.x + max.x) * 0.5f, (min.y + max.y) * 0.5f);
            if (!portal.ContainsLocalPoint(centreXY)) return false;

            enteredFromBack = previousMax.z <= 0f;
            return true;
        }

        private static void AccumulateBodyBounds(Collider collider, PortalEndpoint portal,
            Vector3 worldOffset, ref Vector3 min, ref Vector3 max)
        {
            Bounds bounds = collider.bounds;
            Vector3 centre = bounds.center + worldOffset;
            Vector3 extents = bounds.extents;

            for (int corner = 0; corner < 8; corner++)
            {
                Vector3 point = new Vector3(
                    (corner & 1) != 0 ? centre.x + extents.x : centre.x - extents.x,
                    (corner & 2) != 0 ? centre.y + extents.y : centre.y - extents.y,
                    (corner & 4) != 0 ? centre.z + extents.z : centre.z - extents.z);
                Vector3 local = portal.WorldToLocalPoint(point);
                min = Vector3.Min(min, local);
                max = Vector3.Max(max, local);
            }
        }

        /// <summary>
        /// Measures the object's collision body in the portal's local frame and rejects
        /// objects wider or taller than the aperture.
        ///
        /// A CharacterController is authoritative for player movement: Unity drives the
        /// body with that capsule alone. Any additional colliders on the same hierarchy
        /// (e.g. the decorative Capsule child in SampleScene, offset +1.27 m) are not used
        /// for movement and would inflate the union AABB, falsely rejecting a player that
        /// physically fits. In that case only the controller is measured.
        ///
        /// All other objects (Rigidbody / plain transform) keep the full multi-collider
        /// union measurement, so generic traversal validation is unchanged.
        /// </summary>
        private bool ObjectFitsAperture(PortalEndpoint portal, out string reason)
        {
            reason = string.Empty;

            // Colliders may be added after this component initialises; re-query when none
            // were found so size validation still applies to the final object shape.
            if (m_Colliders == null || m_Colliders.Length == 0)
            {
                m_Colliders = m_IncludeChildColliders
                    ? GetComponentsInChildren<Collider>(true)
                    : GetComponents<Collider>();
            }
            if (m_Colliders.Length == 0) return true;

            Vector2 min = new Vector2(float.MaxValue, float.MaxValue);
            Vector2 max = new Vector2(float.MinValue, float.MinValue);
            bool measured = false;

            if (m_Controller != null && m_Controller.enabled)
            {
                AccumulateColliderExtents(m_Controller, portal, ref min, ref max);
                measured = true;
            }
            else
            {
                for (int i = 0; i < m_Colliders.Length; i++)
                {
                    Collider collider = m_Colliders[i];
                    if (collider == null || !collider.enabled) continue;

                    AccumulateColliderExtents(collider, portal, ref min, ref max);
                    measured = true;
                }
            }

            if (!measured) return true;

            float halfWidth = (max.x - min.x) * 0.5f;
            float halfHeight = (max.y - min.y) * 0.5f;
            Vector2 limit = portal.OpeningHalfSize * m_ApertureTolerance;

            if (halfWidth <= limit.x && halfHeight <= limit.y) return true;

            reason = $"half extents {halfWidth:0.00} x {halfHeight:0.00} m exceed " +
                     $"aperture {limit.x:0.00} x {limit.y:0.00} m";
            return false;
        }

        private static void AccumulateColliderExtents(
            Collider collider, PortalEndpoint portal, ref Vector2 min, ref Vector2 max)
        {
            // Box colliders can be measured exactly from their own local corners, which
            // avoids the conservative overestimate of a world-space AABB when rotated.
            if (collider is BoxCollider box)
            {
                Transform ct = box.transform;
                Vector3 centre = ct.TransformPoint(box.center);
                Vector3 half = Vector3.Scale(box.size * 0.5f, ct.lossyScale);
                for (int corner = 0; corner < 8; corner++)
                {
                    Vector3 offset = new Vector3(
                        (corner & 1) != 0 ? half.x : -half.x,
                        (corner & 2) != 0 ? half.y : -half.y,
                        (corner & 4) != 0 ? half.z : -half.z);
                    AddLocalPoint(centre + ct.rotation * offset, portal, ref min, ref max);
                }
                return;
            }

            Bounds bounds = collider.bounds;
            Vector3 c = bounds.center;
            Vector3 e = bounds.extents;
            for (int corner = 0; corner < 8; corner++)
            {
                Vector3 point = new Vector3(
                    (corner & 1) != 0 ? c.x + e.x : c.x - e.x,
                    (corner & 2) != 0 ? c.y + e.y : c.y - e.y,
                    (corner & 4) != 0 ? c.z + e.z : c.z - e.z);
                AddLocalPoint(point, portal, ref min, ref max);
            }
        }

        private static void AddLocalPoint(
            Vector3 worldPoint, PortalEndpoint portal, ref Vector2 min, ref Vector2 max)
        {
            Vector3 local = portal.WorldToLocalPoint(worldPoint);
            min.x = Mathf.Min(min.x, local.x);
            min.y = Mathf.Min(min.y, local.y);
            max.x = Mathf.Max(max.x, local.x);
            max.y = Mathf.Max(max.y, local.y);
        }

        /// <summary>Half-depth of the object's driving collider along the exit normal.</summary>
        private float GetHalfDepthAlong(Vector3 direction)
        {
            if (m_Controller != null && m_Controller.enabled)
                return ProjectedExtent(m_Controller.bounds.extents, direction);

            float max = 0f;
            if (m_Colliders != null)
            {
                for (int i = 0; i < m_Colliders.Length; i++)
                {
                    Collider collider = m_Colliders[i];
                    if (collider == null || !collider.enabled) continue;
                    max = Mathf.Max(max, ProjectedExtent(collider.bounds.extents, direction));
                }
            }
            return max;
        }

        private static float ProjectedExtent(Vector3 extents, Vector3 direction) =>
            Mathf.Abs(extents.x * direction.x) +
            Mathf.Abs(extents.y * direction.y) +
            Mathf.Abs(extents.z * direction.z);

        /// <summary>
        /// Nudges the player upward along a floor exit's normal until its capsule no
        /// longer overlaps solid geometry. Fully deterministic (fixed step, fixed limit)
        /// and only used when floor exit assist is active.
        /// </summary>
        private static void ResolveExitClearance(ref Vector3 exitPosition, CharacterController controller)
        {
            if (controller == null) return;

            float radius = Mathf.Max(0.01f, controller.radius);
            float halfSegment = Mathf.Max(0f, controller.height * 0.5f - radius);
            Vector3 centre = controller.center;
            const float step = 0.1f;
            const int maxSteps = 8;

            for (int i = 0; i < maxSteps; i++)
            {
                Vector3 bottom = exitPosition + centre - Vector3.up * halfSegment;
                Vector3 top = exitPosition + centre + Vector3.up * halfSegment;
                if (!Physics.CheckCapsule(bottom, top, radius * 0.95f,
                        ~0, QueryTriggerInteraction.Ignore))
                    return;

                exitPosition += Vector3.up * step;
            }
        }

        private bool TryTraverse(
            PortalEndpoint entrance, PortalEndpoint exit, Vector3 entryPosition, bool enteredFromBack)
        {
            Vector3 position = m_Body != null ? m_Body.position : m_Transform.position;
            Quaternion rotation = m_Body != null ? m_Body.rotation : m_Transform.rotation;
            Vector3 linearVelocity = m_Body != null ? m_Body.linearVelocity : Vector3.zero;
            Vector3 angularVelocity = m_Body != null ? m_Body.angularVelocity : Vector3.zero;

            // Anchor the exit pose at the ENTRANCE PLANE, not at the object's current
            // origin. The origin is still in front of the entrance when the collider
            // touches the plane; feeding that offset through the 180-degree flip places
            // the object BEHIND the exit wall. Use only the lateral (x/y) offset from
            // the aperture, then push out along the exit normal by the object's depth.
            Vector3 entranceLocal = entrance.WorldToLocalPoint(position);
            Vector3 planePoint = entrance.LocalToWorldPoint(
                new Vector3(entranceLocal.x, entranceLocal.y, 0f));

            // Derive exit pose and momentum from the entrance/exit frames. Both
            // systems may override these methods (interversal mapping modes differ).
            entrance.ComputeExitPose(exit, planePoint, rotation, enteredFromBack,
                out Vector3 exitPosition, out Quaternion exitRotation);
            entrance.ComputeExitMotion(exit, linearVelocity, angularVelocity, enteredFromBack,
                out Vector3 exitLinearVelocity, out Vector3 exitAngularVelocity);

            // Clear the exit surface so the teleported body never spawns inside the wall.
            exitPosition += exit.Forward * (GetHalfDepthAlong(exit.Forward) + 0.02f);

            // Player-like objects store yaw on the transform and pitch on a child camera.
            // Applying the portal frame's full rotation to the transform would tilt/roll
            // the camera (and invert it for floor/ceiling exits). Convert to yaw only.
            Quaternion frameRotation = exitRotation * Quaternion.Inverse(rotation);
            bool constrainUpright = m_Controller != null;
            Quaternion finalRotation = exitRotation;

            if (constrainUpright)
            {
                Vector3 forward = frameRotation * m_Transform.forward;
                forward.y = 0f;
                if (forward.sqrMagnitude < 1e-4f)
                {
                    forward = exit.Forward;
                    forward.y = 0f;
                }
                if (forward.sqrMagnitude < 1e-4f) forward = Vector3.forward;
                finalRotation = Quaternion.LookRotation(forward.normalized, Vector3.up);
            }

            // Player-only assist: character-controller players, never Rigidbody objects.
            // Determined from the EXIT portal's outward normal (a floor points up).
            bool applyFloorAssist = m_EnableFloorExitAssist && m_Controller != null && m_Body == null &&
                                    IsFloorNormal(exit.Forward, m_FloorNormalThreshold);

            if (applyFloorAssist) ResolveExitClearance(ref exitPosition, m_Controller);

            if (m_Body != null)
            {
                m_Body.position = exitPosition;
                m_Body.rotation = finalRotation;
                m_Body.linearVelocity = exitLinearVelocity;
                m_Body.angularVelocity = exitAngularVelocity;
                m_Body.WakeUp();
            }
            else if (m_Controller != null)
            {
                // CharacterController cannot be moved by setting the transform while
                // enabled without stale overlap state; disable/enable around the jump.
                m_Controller.enabled = false;
                m_Transform.SetPositionAndRotation(exitPosition, finalRotation);
                m_Controller.enabled = true;
            }
            else
            {
                m_Transform.SetPositionAndRotation(exitPosition, finalRotation);
            }

            // Controllers keep their own cached direction vectors. Apply the same
            // (upright-constrained where needed) rotation delta.
            Quaternion rotationDelta = finalRotation * Quaternion.Inverse(rotation);
            if (m_NotifyMomentumReceivers)
            {
                for (int i = 0; i < m_MomentumReceivers.Length; i++)
                    m_MomentumReceivers[i]?.ApplyPortalRotation(rotationDelta);
            }

            LastExitAssistApplied = false;
            LastExitAssistImpulse = Vector3.zero;
            if (applyFloorAssist)
            {
                // The facing has already been transformed into the exit frame, so this
                // direction is deterministic for a given entry pose and portal pair.
                Vector3 impulse = ComputeExitAssistImpulse(exit.Forward, m_Transform.forward,
                    m_ExitAssistUpwardBoost, m_ExitAssistHorizontalBoost, m_ExitAssistAngle);
                LastExitAssistImpulse = impulse;
                LastExitAssistApplied = true;

                for (int i = 0; i < m_MomentumReceivers.Length; i++)
                {
                    if (m_MomentumReceivers[i] is IPortalExitAssistReceiver assistReceiver)
                        assistReceiver.ApplyExitImpulse(impulse);
                }

                ExitAssistApplied?.Invoke(impulse);
            }

            if (m_SwitchUniverseOnInterversalTravel && entrance.Kind == PortalKind.Interversal)
            {
                UniverseId destination = exit is InterversalPortal interversalExit
                    ? interversalExit.Universe
                    : UniverseId.None;

                if (destination.IsValid && InterversalPortalSystem.Instance != null)
                    InterversalPortalSystem.Instance.SwitchEntityUniverse(gameObject, destination);
            }

            m_IgnoredPortal = exit;
            // The existing grace system prevents the post-assist arc from immediately
            // re-entering the destination portal. An assisted exit extends it deterministically.
            m_IgnoreUntil = Time.time + (applyFloorAssist
                ? Mathf.Max(m_IgnoreTimeout, m_ExitAssistGracePeriod)
                : m_IgnoreTimeout);
            m_AssistGraceActive = applyFloorAssist;
            m_LastTraversalFrame = Time.frameCount;

            Traversed?.Invoke(new PortalTraversalInfo
            {
                Entrance = entrance,
                Exit = exit,
                EntryPosition = entryPosition,
                ExitPosition = exitPosition,
                EntryVelocity = linearVelocity,
                ExitVelocity = exitLinearVelocity,
                RotationDelta = rotationDelta
            });

            return true;
        }

        /// <summary>Call after any scripted teleport to prevent a false crossing on the next tick.</summary>
        public void ResetTracking()
        {
            EnsureInitialized();
            m_PreviousPosition = m_Body != null ? m_Body.position : m_Transform.position;
            m_HasPrevious = true;
            m_IgnoredPortal = null;
            m_AssistGraceActive = false;
        }
    }
}
