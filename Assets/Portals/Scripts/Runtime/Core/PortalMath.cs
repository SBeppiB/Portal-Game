using UnityEngine;

namespace Portals.Core
{
    /// <summary>
    /// Pure maths for portal coordinate conversions. No scene state, no allocations.
    ///
    /// LOCAL FRAME CONVENTION
    /// ----------------------
    /// A portal is a flat disc whose local +Z axis is the OUTWARD normal (the side you
    /// approach from), local +Y is "up" along the opening and local +X is "right".
    /// Therefore an object travelling INTO a portal moves along local -Z, and must
    /// emerge travelling along the exit portal's local +Z.
    ///
    /// THE ENTRANCE -> EXIT FRAME TRANSFORMATION
    /// -----------------------------------------
    /// Let E be the entrance frame (rotation) and X the exit frame. The rotation that
    /// maps the entrance frame onto the exit frame while flipping the travel direction
    /// is a half turn about local up:
    ///
    ///     R = X * Ry(180) * E^-1
    ///
    /// (Ry(180) maps local -Z onto local +Z.) For a world point p and world direction v:
    ///
    ///     p' = X.position + R * (p - E.position)
    ///     v' = R * v
    ///
    /// Because portals are rigid frames with unit scale, R is a pure rotation. This is
    /// numerically well behaved (no shear, no scale drift) and lets linear velocity,
    /// angular velocity and all of a controller's internal momentum vectors share the
    /// exact same transform.
    /// </summary>
    public static class PortalMath
    {
        /// <summary>Half turn about local up: maps "into the entrance" to "out of the exit".</summary>
        public static readonly Quaternion PortalFlip = Quaternion.Euler(0f, 180f, 0f);

        private const float PlaneEpsilon = 1e-6f;

        /// <summary>R = exit.Rotation * Ry(180) * inverse(entrance.Rotation).</summary>
        public static Quaternion ComputeFrameRotation(Quaternion entranceRotation, Quaternion exitRotation)
        {
            return ComputeFrameRotation(entranceRotation, exitRotation, true);
        }

        /// <summary>
        /// Frame rotation between two portals. The half turn is required when entering
        /// from the front (local -Z travel becomes exit +Z). Entering from the BACK of a
        /// portal means local +Z travel, so the half turn must be omitted to still emerge
        /// along the exit's outward normal.
        /// </summary>
        public static Quaternion ComputeFrameRotation(
            Quaternion entranceRotation, Quaternion exitRotation, bool includeHalfTurn)
        {
            Quaternion turn = includeHalfTurn ? PortalFlip : Quaternion.identity;
            return exitRotation * turn * Quaternion.Inverse(entranceRotation);
        }

        /// <summary>Maps a world position from the entrance frame into the exit frame.</summary>
        public static Vector3 TransformPoint(
            Vector3 point,
            Vector3 entrancePosition, Quaternion entranceRotation,
            Vector3 exitPosition, Quaternion exitRotation)
        {
            Quaternion rotation = ComputeFrameRotation(entranceRotation, exitRotation);
            return exitPosition + rotation * (point - entrancePosition);
        }

        /// <summary>Inverse of <see cref="TransformPoint"/> (useful for round-trip checks).</summary>
        public static Vector3 InverseTransformPoint(
            Vector3 point,
            Vector3 entrancePosition, Quaternion entranceRotation,
            Vector3 exitPosition, Quaternion exitRotation)
        {
            Quaternion rotation = ComputeFrameRotation(exitRotation, entranceRotation);
            return entrancePosition + rotation * (point - exitPosition);
        }

        /// <summary>Rotates a world direction/velocity into the exit frame (magnitude preserved).</summary>
        public static Vector3 TransformDirection(
            Vector3 direction, Quaternion entranceRotation, Quaternion exitRotation)
        {
            return ComputeFrameRotation(entranceRotation, exitRotation) * direction;
        }

        /// <summary>
        /// Transforms linear velocity while accounting for moving portal endpoints.
        ///
        ///     v' = R * (v - vEntrance) + vExit
        ///
        /// Subtracting the entrance velocity expresses v in the entrance frame; adding
        /// the exit velocity expresses it in the exit frame. With static portals this
        /// reduces to v' = R * v, preserving speed exactly.
        /// </summary>
        public static Vector3 TransformVelocity(
            Vector3 velocity,
            Vector3 entranceLinearVelocity, Vector3 exitLinearVelocity,
            Quaternion entranceRotation, Quaternion exitRotation)
        {
            Quaternion rotation = ComputeFrameRotation(entranceRotation, exitRotation);
            return rotation * (velocity - entranceLinearVelocity) + exitLinearVelocity;
        }

        /// <summary>
        /// Transforms angular velocity for moving portals using the same frame change.
        /// Angular velocity is an axial vector; for proper rotations (det = +1) it
        /// transforms as w' = R * w. The portal spin terms are combined additively, which
        /// is exact for aligned spin axes and a good approximation for general motion.
        /// </summary>
        public static Vector3 TransformAngularVelocity(
            Vector3 angularVelocity,
            Vector3 entranceAngularVelocity, Vector3 exitAngularVelocity,
            Quaternion entranceRotation, Quaternion exitRotation)
        {
            Quaternion rotation = ComputeFrameRotation(entranceRotation, exitRotation);
            return rotation * (angularVelocity - entranceAngularVelocity) + exitAngularVelocity;
        }

        /// <summary>
        /// Continuous crossing test against the portal plane (local z = 0).
        /// Returns true only when travelling from the front (z &gt; 0) to the back
        /// (z &lt;= 0). The intersection is found by linear interpolation, which is
        /// exact for straight-line motion over the frame and prevents tunnelling at
        /// high speed. Curved trajectories are handled by the small per-frame step.
        /// </summary>
        public static bool TryFindPlaneCrossing(
            Vector3 previousLocal, Vector3 currentLocal,
            out Vector3 hitLocal, out float crossingT)
        {
            hitLocal = default;
            crossingT = 0f;

            // Legacy overload keeps its original front-only behaviour; the 5-argument
            // overload is used by the traverser to support entry from either side.
            if (previousLocal.z <= 0f || currentLocal.z > 0f) return false;

            float denominator = previousLocal.z - currentLocal.z;
            if (Mathf.Abs(denominator) < PlaneEpsilon) return false;

            crossingT = Mathf.Clamp01(previousLocal.z / denominator);
            hitLocal = Vector3.Lerp(previousLocal, currentLocal, crossingT);
            return true;
        }

        /// <summary>
        /// Swept crossing test that accepts either direction: front (z &gt; 0 to z &lt;= 0)
        /// or back (z &lt; 0 to z &gt;= 0). <paramref name="enteredFromBack"/> reports which
        /// side was crossed so the correct frame rotation can be chosen.
        /// </summary>
        public static bool TryFindPlaneCrossing(
            Vector3 previousLocal, Vector3 currentLocal,
            out Vector3 hitLocal, out float crossingT, out bool enteredFromBack)
        {
            hitLocal = default;
            crossingT = 0f;
            enteredFromBack = false;

            bool frontToBack = previousLocal.z > 0f && currentLocal.z <= 0f;
            bool backToFront = previousLocal.z < 0f && currentLocal.z >= 0f;
            if (!frontToBack && !backToFront) return false;

            float denominator = previousLocal.z - currentLocal.z;
            if (Mathf.Abs(denominator) < PlaneEpsilon)
                return false;

            enteredFromBack = backToFront;
            crossingT = Mathf.Clamp01(previousLocal.z / denominator);
            hitLocal = Vector3.Lerp(previousLocal, currentLocal, crossingT);
            return true;
        }

        /// <summary>Elliptical opening test in portal-local XY space (unit circle scaled by half extents).</summary>
        public static bool IsInsideOpening(Vector2 localXY, Vector2 halfExtents)
        {
            if (halfExtents.x <= 0f || halfExtents.y <= 0f) return false;

            float nx = localXY.x / halfExtents.x;
            float ny = localXY.y / halfExtents.y;
            return nx * nx + ny * ny <= 1f;
        }

        /// <summary>
        /// Builds the full 4x4 homogeneous transform entrance-to-exit.
        /// M = T(exit) * Ry(180) * inverse(T(entrance)).
        /// Provided for advanced use (e.g. transforming attached cameras/effects);
        /// the point/direction helpers above are cheaper and numerically equivalent
        /// for unit-scale portals.
        /// </summary>
        public static Matrix4x4 BuildTraversalMatrix(
            Vector3 entrancePosition, Quaternion entranceRotation,
            Vector3 exitPosition, Quaternion exitRotation)
        {
            Matrix4x4 entrance = Matrix4x4.TRS(entrancePosition, entranceRotation, Vector3.one);
            Matrix4x4 exit = Matrix4x4.TRS(exitPosition, exitRotation, Vector3.one);
            Matrix4x4 flip = Matrix4x4.TRS(Vector3.zero, PortalFlip, Vector3.one);
            return exit * flip * entrance.inverse;
        }
    }
}
