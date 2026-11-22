using Portals.Energy;
using Portals.Integration;
using UnityEngine;

namespace Portals.Core
{
    public enum PortalPlacementFailure
    {
        None,
        NoEndpoint,
        NoEnergy,
        NoSurfaceHit,
        InvalidSurfaceAngle, // retained for compatibility; no angle restriction is applied
        OverlapsOtherPortal,
        MissingPair,
        InvalidSurface,
        ExcludedObject,
        OpeningDoesNotFit,
        MidAirObstructed
    }

    /// <summary>
    /// Places an existing portal endpoint onto any valid collider surface using the
    /// raycast hit point and surface normal. No surface type, tag or angle restriction is
    /// applied: floors, walls, ceilings, slopes, rotated objects and MeshColliders all work.
    ///
    /// Validation is geometry-based rather than category-based: the opening must be backed by
    /// surface on all sampled rim points, with consistent normals and plane distance. This
    /// prevents portals hanging over edges or spanning incompatible geometry.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PortalPlacer : MonoBehaviour
    {
        [Header("Raycast")]
        [SerializeField] private LayerMask m_PlaceableLayers = ~0;
        [SerializeField, Min(0.1f)] private float m_MaxPlacementDistance = 40f;
        [SerializeField, Min(0f)] private float m_SurfaceOffset = 0.05f;
        [SerializeField, Min(0f)] private float m_PortalOverlapRadius = 1.25f;

        [Header("Opening Fit Validation")]
        [SerializeField] private bool m_ValidateOpeningFit = true;
        [SerializeField, Min(0.05f)] private float m_FitProbeDepth = 0.35f;
        [SerializeField, Range(0.5f, 0.999f)] private float m_FitNormalDot = 0.85f;
        [SerializeField, Min(0f)] private float m_FitPlaneTolerance = 0.12f;
        [SerializeField, Range(4, 24)] private int m_FitSampleCount = 12;

        [Header("Mid-Air Placement")]
        [SerializeField, Tooltip("When a shot reaches its range without hitting a surface, create a free-floating portal.")]
        private bool m_AllowMidAirPlacement = true;
        [SerializeField, Min(1f), Tooltip("Maximum travel distance of a portal shot in world units.")]
        private float m_MidAirTravelDistance = 20f;

        [Header("Rules")]
        [SerializeField] private bool m_RequireLinkedPortal = true;
        [SerializeField] private PortalEnergySystem m_EnergySystem;
        [SerializeField, Tooltip("Colliders on these transforms (and children) cannot host portals.")]
        private Transform[] m_AdditionalExclusions = System.Array.Empty<Transform>();

        [SerializeField, Tooltip("Colliders on this transform (and its children) are ignored, e.g. the shooter.")]
        private Transform m_IgnoreRoot;

        [SerializeField, Tooltip("Reference used to decide the portal's 'up' on vertical normals. Defaults to this transform.")]
        private Transform m_OrientationReference;

        private readonly RaycastHit[] m_HitBuffer = new RaycastHit[32];
        private readonly RaycastHit[] m_FitBuffer = new RaycastHit[16];
        private readonly Collider[] m_MidAirBuffer = new Collider[16];

        private void Awake()
        {
            if (m_EnergySystem == null) m_EnergySystem = GetComponent<PortalEnergySystem>();
            if (m_EnergySystem == null) m_EnergySystem = FindFirstObjectByType<PortalEnergySystem>();
        }

        public float MaxPlacementDistance => m_MaxPlacementDistance;
        public LayerMask PlaceableLayers => m_PlaceableLayers;

        public void SetEnergySystem(PortalEnergySystem energySystem)
        {
            m_EnergySystem = energySystem;
        }

        /// <summary>Ignores the shooter's own colliders so a first-person camera can aim freely.</summary>
        public void SetIgnoreRoot(Transform root)
        {
            m_IgnoreRoot = root;
        }

        /// <summary>Sets additional transforms whose colliders must never host portals.</summary>
        public void SetAdditionalExclusions(params Transform[] excluded)
        {
            m_AdditionalExclusions = excluded ?? System.Array.Empty<Transform>();
        }

        /// <summary>
        /// Finds the closest non-excluded surface hit. Uses a reusable buffer to avoid
        /// per-shot allocations. Triangles, primitives and MeshColliders are all supported.
        /// </summary>
        public bool TryFindPlacementHit(Ray ray, out RaycastHit hit)
        {
            return TryFindPlacementHit(ray, m_MaxPlacementDistance, out hit);
        }

        /// <summary>Raycast variant with an explicit maximum travel distance.</summary>
        public bool TryFindPlacementHit(Ray ray, float maxDistance, out RaycastHit hit)
        {
            int count = Physics.RaycastNonAlloc(ray, m_HitBuffer, maxDistance,
                m_PlaceableLayers, QueryTriggerInteraction.Ignore);

            bool found = false;
            float bestDistance = float.MaxValue;
            hit = default;

            for (int i = 0; i < count; i++)
            {
                RaycastHit candidate = m_HitBuffer[i];
                if (IsExcluded(candidate.collider.transform)) continue;

                if (candidate.distance < bestDistance)
                {
                    bestDistance = candidate.distance;
                    hit = candidate;
                    found = true;
                }
            }

            return found;
        }

        private bool IsExcluded(Transform candidate)
        {
            if (candidate == null) return true;
            if (m_IgnoreRoot != null && (candidate == m_IgnoreRoot || candidate.IsChildOf(m_IgnoreRoot)))
                return true;

            if (m_AdditionalExclusions != null)
            {
                for (int i = 0; i < m_AdditionalExclusions.Length; i++)
                {
                    Transform excluded = m_AdditionalExclusions[i];
                    if (excluded != null && (candidate == excluded || candidate.IsChildOf(excluded)))
                        return true;
                }
            }

            return false;
        }

        /// <summary>Backwards-compatible overload without the human-readable detail.</summary>
        public bool TryPlace(PortalEndpoint endpoint, Ray ray, out PortalPlacementFailure failure)
        {
            return TryPlace(endpoint, ray, out failure, out _);
        }

        /// <summary>
        /// Attempts to place <paramref name="endpoint"/> on the first valid collider hit
        /// within the full placement range. Never creates a mid-air portal.
        /// </summary>
        public bool TryPlace(PortalEndpoint endpoint, Ray ray,
            out PortalPlacementFailure failure, out string detail)
        {
            return TryPlaceInternal(endpoint, ray, m_MaxPlacementDistance, false,
                out failure, out detail);
        }

        /// <summary>
        /// Fires a virtual portal shot along <paramref name="shotRay"/>. A surface hit is
        /// validated exactly like manual placement; if nothing is hit within the shot
        /// range, a free-floating portal is created at the endpoint instead.
        /// </summary>
        public bool TryPlaceFromShot(PortalEndpoint endpoint, Ray shotRay,
            out PortalPlacementFailure failure, out string detail)
        {
            return TryPlaceInternal(endpoint, shotRay,
                m_MidAirTravelDistance, m_AllowMidAirPlacement, out failure, out detail);
        }

        private bool TryPlaceInternal(PortalEndpoint endpoint, Ray ray, float maxDistance,
            bool allowMidAir, out PortalPlacementFailure failure, out string detail)
        {
            failure = PortalPlacementFailure.None;
            detail = string.Empty;

            if (endpoint == null)
            {
                failure = PortalPlacementFailure.NoEndpoint;
                detail = "No portal endpoint was supplied.";
                return false;
            }

            if (m_RequireLinkedPortal && !endpoint.HasConfiguredLink)
            {
                failure = PortalPlacementFailure.MissingPair;
                detail = $"'{endpoint.name}' has no assigned partner; pair the two portals first.";
                return false;
            }

            // The partner may be inactive before it has been fired; that is fine for
            // placement but traversal still requires both endpoints to be active.
            PortalEndpoint linked = endpoint.GetLinkedEndpoint();

            if (m_EnergySystem != null && !m_EnergySystem.CanAfford(endpoint.Kind))
            {
                failure = PortalPlacementFailure.NoEnergy;
                detail = $"Not enough portal energy (need {m_EnergySystem.Costs.GetCreationCost(endpoint.Kind):0.0}).";
                return false;
            }

            if (!TryFindPlacementHit(ray, maxDistance, out RaycastHit hit))
            {
                if (!allowMidAir)
                {
                    failure = PortalPlacementFailure.NoSurfaceHit;
                    detail = $"Aim ray hit no placeable collider within {maxDistance:0.0} m " +
                             $"(layers: {m_PlaceableLayers.value}, excluded: {DescribeExclusions()}).";
                    return false;
                }

                return PlaceMidAir(endpoint, linked, ray, maxDistance, out failure, out detail);
            }

            Collider hitCollider = hit.collider;
            Vector3 normal = hit.normal;

            if (hitCollider == null || !IsFinite(normal) || normal.sqrMagnitude < 0.5f ||
                !IsFinite(hit.point))
            {
                failure = PortalPlacementFailure.InvalidSurface;
                detail = $"Hit collider '{NameOf(hitCollider)}' has an invalid surface normal or hit point.";
                return false;
            }

            normal.Normalize();

            Vector3 position = hit.point + normal * m_SurfaceOffset;
            Quaternion rotation = BuildPlacementRotation(normal);

            if (linked != null && Vector3.Distance(position, linked.Position) < m_PortalOverlapRadius)
            {
                failure = PortalPlacementFailure.OverlapsOtherPortal;
                detail = $"Too close to partner '{linked.name}' " +
                         $"({Vector3.Distance(position, linked.Position):0.00} m < {m_PortalOverlapRadius:0.00} m).";
                return false;
            }

            if (m_ValidateOpeningFit)
            {
                if (!DoesOpeningFit(position, normal, rotation, endpoint.OpeningHalfSize, out string fitDetail))
                {
                    failure = PortalPlacementFailure.OpeningDoesNotFit;
                    detail = $"Opening does not fit on '{NameOf(hitCollider)}' at {hit.point}: {fitDetail}";
                    return false;
                }
            }

            // Anchor in the surface's local frame so portals follow moving/rotating
            // objects without inheriting non-uniform scale. PortalEndpoint already tracks
            // the resulting world motion for traversal velocity/angular velocity.
            PortalSurfaceAnchor anchor = endpoint.GetComponent<PortalSurfaceAnchor>();
            if (anchor == null) anchor = endpoint.gameObject.AddComponent<PortalSurfaceAnchor>();
            anchor.Attach(hitCollider.transform, position, rotation);

            endpoint.Opening.SetPositionAndRotation(position, rotation);
            endpoint.SetActive(true);
            BeginOpening(endpoint);

            m_EnergySystem?.NotifyPortalCreated(endpoint);

            detail = $"Placed on '{NameOf(hitCollider)}' at {position} " +
                     $"with outward normal {normal} (surface type: {hitCollider.GetType().Name}).";
            return true;
        }

        private bool PlaceMidAir(PortalEndpoint endpoint, PortalEndpoint linked, Ray ray,
            float travelDistance, out PortalPlacementFailure failure, out string detail)
        {
            failure = PortalPlacementFailure.None;
            detail = string.Empty;

            Vector3 direction = ray.direction;
            if (!IsFinite(direction) || direction.sqrMagnitude < 1e-6f)
            {
                failure = PortalPlacementFailure.InvalidSurface;
                detail = "Portal shot has an invalid direction.";
                return false;
            }
            direction.Normalize();

            Vector3 position = ray.origin + direction * travelDistance;
            Quaternion rotation = BuildMidAirRotation(direction);

            if (linked != null && Vector3.Distance(position, linked.Position) < m_PortalOverlapRadius)
            {
                failure = PortalPlacementFailure.OverlapsOtherPortal;
                detail = $"Mid-air endpoint is too close to partner '{linked.name}' " +
                         $"({Vector3.Distance(position, linked.Position):0.00} m < {m_PortalOverlapRadius:0.00} m).";
                return false;
            }

            if (!IsMidAirClear(endpoint, position, out string obstruction))
            {
                failure = PortalPlacementFailure.MidAirObstructed;
                detail = $"Mid-air endpoint at {position} is obstructed: {obstruction}";
                return false;
            }

            // A free-floating portal must not stay bound to a previous surface.
            PortalSurfaceAnchor anchor = endpoint.GetComponent<PortalSurfaceAnchor>();
            if (anchor != null) anchor.Detach();

            endpoint.Opening.SetPositionAndRotation(position, rotation);
            endpoint.SetActive(true);
            BeginOpening(endpoint);
            m_EnergySystem?.NotifyPortalCreated(endpoint);

            detail = $"Mid-air portal created at {position} after {travelDistance:0.0} m " +
                     $"(faces back along the shot direction {rotation * Vector3.forward}).";
            return true;
        }

        /// <summary>
        /// A mid-air portal faces back toward the shooter, so an object travelling along
        /// the shot ray enters through its front (local -Z) and exits normally.
        /// </summary>
        private Quaternion BuildMidAirRotation(Vector3 shotDirection)
        {
            Vector3 outward = -shotDirection;
            if (!IsFinite(outward) || outward.sqrMagnitude < 1e-6f) outward = transform.forward;
            outward.Normalize();

            Vector3 upHint = Mathf.Abs(Vector3.Dot(outward, Vector3.up)) > 0.95f
                ? (m_OrientationReference != null ? m_OrientationReference.forward : transform.forward)
                : Vector3.up;

            if (!IsFinite(upHint) || upHint.sqrMagnitude < 1e-6f)
                upHint = Vector3.Cross(outward, Vector3.right);
            if (upHint.sqrMagnitude < 1e-6f)
                upHint = Vector3.Cross(outward, Vector3.forward);

            return Quaternion.LookRotation(outward, upHint);
        }

        private bool IsMidAirClear(PortalEndpoint endpoint, Vector3 position, out string reason)
        {
            reason = string.Empty;
            Vector2 half = endpoint.OpeningHalfSize;
            float radius = Mathf.Max(0.5f, Mathf.Max(half.x, half.y) * 0.6f);

            int count = Physics.OverlapSphereNonAlloc(position, radius, m_MidAirBuffer,
                m_PlaceableLayers, QueryTriggerInteraction.Ignore);

            for (int i = 0; i < count; i++)
            {
                Collider collider = m_MidAirBuffer[i];
                if (collider == null || IsExcluded(collider.transform)) continue;
                reason = $"overlaps collider '{NameOf(collider)}'";
                return false;
            }

            return true;
        }

        /// <summary>
        /// Aligns the portal's local +Z with the surface normal. World up is used as the
        /// up hint on walls/slopes; on floors and ceilings the player's facing is used so
        /// the opening stays level with the view. Degenerate cases fall back gracefully.
        /// </summary>
        private Quaternion BuildPlacementRotation(Vector3 normal)
        {
            Vector3 reference = m_OrientationReference != null
                ? m_OrientationReference.forward
                : transform.forward;

            Vector3 upHint;
            if (Mathf.Abs(Vector3.Dot(normal, Vector3.up)) > 0.95f)
                upHint = Vector3.ProjectOnPlane(reference, normal);
            else
                upHint = Vector3.up;

            if (upHint.sqrMagnitude < 1e-6f)
                upHint = Vector3.ProjectOnPlane(Vector3.forward, normal);
            if (upHint.sqrMagnitude < 1e-6f)
                upHint = Vector3.Cross(normal, Vector3.right);
            if (upHint.sqrMagnitude < 1e-6f)
                upHint = Vector3.Cross(normal, Vector3.forward);

            return Quaternion.LookRotation(normal, upHint);
        }

        /// <summary>
        /// Confirms the elliptical opening is actually backed by a compatible surface.
        /// Probes the centre and rim from just in front of the portal, casting back along
        /// the normal. A miss, a mismatched normal, or excessive plane deviation means the
        /// opening would hang in space or cut through an edge, so placement is rejected.
        /// </summary>
        private bool DoesOpeningFit(Vector3 centre, Vector3 normal, Quaternion rotation,
            Vector2 halfSize, out string reason)
        {
            reason = string.Empty;

            if (!ProbeSurface(centre, normal, out string centreReason))
            {
                reason = $"centre unsupported ({centreReason})";
                return false;
            }

            int samples = Mathf.Max(4, m_FitSampleCount);
            for (int i = 0; i < samples; i++)
            {
                float angle = i / (float)samples * Mathf.PI * 2f;
                Vector3 local = new Vector3(Mathf.Cos(angle) * halfSize.x, Mathf.Sin(angle) * halfSize.y, 0f);
                Vector3 rimPoint = centre + rotation * local;

                if (!ProbeSurface(rimPoint, normal, out string rimReason))
                {
                    reason = $"rim sample {i + 1}/{samples} unsupported ({rimReason})";
                    return false;
                }
            }

            return true;
        }

        private bool ProbeSurface(Vector3 point, Vector3 normal, out string reason)
        {
            reason = string.Empty;
            Vector3 origin = point + normal * m_FitProbeDepth;
            int count = Physics.RaycastNonAlloc(origin, -normal, m_FitBuffer,
                m_FitProbeDepth * 2f, m_PlaceableLayers, QueryTriggerInteraction.Ignore);

            for (int i = 0; i < count; i++)
            {
                RaycastHit candidate = m_FitBuffer[i];
                if (IsExcluded(candidate.collider.transform)) continue;

                float alignment = Vector3.Dot(candidate.normal, normal);
                if (alignment < m_FitNormalDot)
                {
                    reason = $"surface angle mismatch (dot {alignment:0.00} < {m_FitNormalDot:0.00})";
                    continue;
                }

                float planeDeviation = Mathf.Abs(Vector3.Dot(candidate.point - point, normal));
                if (planeDeviation > m_FitPlaneTolerance)
                {
                    reason = $"surface deviates {planeDeviation:0.00} m (limit {m_FitPlaneTolerance:0.00} m)";
                    continue;
                }

                return true;
            }

            if (string.IsNullOrEmpty(reason)) reason = "no collider behind the opening";
            return false;
        }

        private string DescribeExclusions()
        {
            int count = 0;
            if (m_IgnoreRoot != null) count++;
            if (m_AdditionalExclusions != null) count += m_AdditionalExclusions.Length;
            return count.ToString();
        }

        private static bool IsFinite(Vector3 value) =>
            !float.IsNaN(value.x) && !float.IsNaN(value.y) && !float.IsNaN(value.z) &&
            !float.IsInfinity(value.x) && !float.IsInfinity(value.y) && !float.IsInfinity(value.z);

        private static string NameOf(Collider collider) =>
            collider != null ? collider.name : "<null>";

        /// <summary>
        /// Starts the visual opening animation when the endpoint has a PortalVisual. The
        /// visual keeps traversal blocked until fully open. Endpoints without a visual
        /// (e.g. pure-maths test fixtures) open instantly, preserving portal placement and
        /// traversal behaviour.
        /// </summary>
        private static void BeginOpening(PortalEndpoint endpoint)
        {
            if (endpoint == null) return;
            PortalVisual visual = endpoint.GetComponent<PortalVisual>();
            if (visual != null) visual.PlayOpen();
        }

        /// <summary>Deactivates an endpoint, detaches it from any moving surface, and releases upkeep.</summary>
        public void Close(PortalEndpoint endpoint)
        {
            if (endpoint == null) return;
            PortalSurfaceAnchor anchor = endpoint.GetComponent<PortalSurfaceAnchor>();
            if (anchor != null) anchor.Detach();
            endpoint.SetActive(false);
            m_EnergySystem?.NotifyPortalDestroyed(endpoint);
        }
    }
}
