using UnityEngine;

namespace Portals.Core
{
    /// <summary>
    /// Per-universe physics settings. Applied by InterversalPortalSystem to bodies
    /// registered in that universe via UniverseBody, rather than mutating the global
    /// Physics.gravity (which would affect every universe at once).
    /// </summary>
    [System.Serializable]
    public sealed class UniversePhysicsProfile
    {
        public Vector3 gravity = new Vector3(0f, -9.81f, 0f);
        [Range(0f, 1f)] public float linearDamping = 0f;
        [Range(0f, 1f)] public float angularDamping = 0f;
        public bool overrideGravity = true;

        public UniversePhysicsProfile Clone() => new UniversePhysicsProfile
        {
            gravity = gravity,
            linearDamping = linearDamping,
            angularDamping = angularDamping,
            overrideGravity = overrideGravity
        };

        public static UniversePhysicsProfile Default => new UniversePhysicsProfile();
    }
}
