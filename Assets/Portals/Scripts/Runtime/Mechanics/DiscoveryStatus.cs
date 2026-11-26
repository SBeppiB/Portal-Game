namespace Portals.Runtime.Mechanics
{
    /// <summary>
    /// Discovery state of a universe relative to the scanner/registry.
    /// </summary>
    public enum DiscoveryStatus
    {
        Unknown = 0,
        Pending = 1,
        Discovered = 2,
        Registered = 3,
        Failed = 4,
        Cached = 5
    }

    /// <summary>
    /// Alias-style enum kept for callers that use the alternative name.
    /// </summary>
    public enum DiscoveryState
    {
        Unknown = 0,
        Pending = 1,
        Discovered = 2,
        Registered = 3,
        Failed = 4,
        Cached = 5
    }
}
