namespace Portals.Core
{
    /// <summary>
    /// Runtime record for the single active portal pair. Contains the two endpoints
    /// (A and B), their technology and the shared lifetime/persistence state.
    /// Pair ids and multi-pair selection were removed with multi-portal support.
    /// </summary>
    public sealed class PortalPair
    {
        public PortalPair(PortalKind kind, PortalEndpoint entrance, PortalEndpoint exit,
            float lifetime, bool persistent)
        {
            Kind = kind;
            Entrance = entrance;
            Exit = exit;
            RemainingLifetime = lifetime;
            Persistent = persistent;
        }

        public PortalKind Kind { get; }
        public PortalEndpoint Entrance { get; }
        public PortalEndpoint Exit { get; }

        /// <summary>Seconds left before both endpoints close. Paused while persistent.</summary>
        public float RemainingLifetime { get; set; }

        public bool Persistent { get; set; }

        /// <summary>True once both endpoints have been placed and are active.</summary>
        public bool IsComplete => Entrance != null && Exit != null &&
                                  Entrance.IsActive && Exit.IsActive;

        public bool Contains(PortalEndpoint endpoint) =>
            endpoint != null && (ReferenceEquals(endpoint, Entrance) || ReferenceEquals(endpoint, Exit));

        public override string ToString() => $"Portal Pair ({Kind})";
    }
}
