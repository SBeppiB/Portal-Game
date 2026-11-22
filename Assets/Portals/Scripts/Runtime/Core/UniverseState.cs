using System.Collections.Generic;
using UnityEngine;

namespace Portals.Core
{
    /// <summary>
    /// Runtime state owned by one universe. Kept separate from other universes so
    /// future universe-specific mechanics have somewhere well-defined to live.
    /// </summary>
    public sealed class UniverseState
    {
        public UniverseState(UniverseId id, UniversePhysicsProfile physics)
        {
            Id = id;
            Physics = physics != null ? physics.Clone() : UniversePhysicsProfile.Default;
        }

        public UniverseId Id { get; }

        public UniversePhysicsProfile Physics { get; set; }

        public HashSet<UniverseBody> Bodies { get; } = new HashSet<UniverseBody>();

        public HashSet<GameObject> Entities { get; } = new HashSet<GameObject>();

        public int BodyCount => Bodies.Count;
        public int EntityCount => Entities.Count;
    }
}
