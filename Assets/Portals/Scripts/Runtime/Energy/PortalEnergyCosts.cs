using System;
using Portals.Core;
using UnityEngine;

namespace Portals.Energy
{
    /// <summary>All configurable costs for portal technology, exposed inline in the Inspector.</summary>
    [Serializable]
    public sealed class PortalEnergyCosts
    {
        [Min(0f)] public float createLocaversal = 20f;
        [Min(0f)] public float createInterversal = 40f;
        [Min(0f)] public float maintainPerPortalPerSecond = 2f;
        [Min(0f)] public float reactivatePortal = 5f;
        [Min(0f)] public float universeSwitch = 0f;

        public float GetCreationCost(PortalKind kind)
        {
            switch (kind)
            {
                case PortalKind.Locaversal: return createLocaversal;
                case PortalKind.Interversal: return createInterversal;
                default: return 0f;
            }
        }

        public PortalEnergyCosts Clone() => new PortalEnergyCosts
        {
            createLocaversal = createLocaversal,
            createInterversal = createInterversal,
            maintainPerPortalPerSecond = maintainPerPortalPerSecond,
            reactivatePortal = reactivatePortal,
            universeSwitch = universeSwitch
        };
    }
}
