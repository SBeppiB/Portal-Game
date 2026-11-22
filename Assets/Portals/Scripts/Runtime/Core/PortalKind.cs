namespace Portals.Core
{
    /// <summary>
    /// Identifies which portal technology an endpoint belongs to.
    /// The two systems are deliberately separate and are NOT required to behave identically.
    /// </summary>
    public enum PortalKind
    {
        /// <summary>Transports entities between two locations in the same universe.</summary>
        Locaversal = 0,

        /// <summary>Transports entities between locations in different universes.</summary>
        Interversal = 1
    }
}
