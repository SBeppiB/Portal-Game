using System;
using System.Globalization;

namespace Portals.Runtime.Mechanics
{
    /// <summary>
    /// Observed identity of a universe. The signature is a deterministic string derived
    /// from the universe coordinates and is stable across sessions without relying on
    /// runtime hash codes for persistent identity.
    /// </summary>
    [Serializable]
    public class UniverseSignature : IEquatable<UniverseSignature>
    {
        public string UniverseId = string.Empty;
        public OmniversalCoordinates Coordinates;
        public UniverseStructuralProfile Profile = UniverseStructuralProfile.CreateDefault();
        public bool IsValid = true;

        /// <summary>
        /// Deterministic signature string derived from <see cref="Coordinates"/>.
        /// </summary>
        public string Signature => BuildSignature(Coordinates);

        /// <summary>Alias for <see cref="Signature"/>.</summary>
        public string SignatureId => Signature;

        /// <summary>Alias for <see cref="Signature"/>.</summary>
        public string Id => Signature;

        public UniverseSignature() { }

        public UniverseSignature(string universeId, OmniversalCoordinates coordinates)
        {
            UniverseId = universeId ?? string.Empty;
            Coordinates = coordinates;
        }

        public UniverseSignature(string universeId, OmniversalCoordinates coordinates, UniverseStructuralProfile profile, bool isValid = true)
            : this(universeId, coordinates)
        {
            Profile = profile ?? UniverseStructuralProfile.CreateDefault();
            IsValid = isValid;
        }

        public static UniverseSignature Generate(OmniversalCoordinates coordinates)
        {
            return Generate(null, coordinates, null, true);
        }

        public static UniverseSignature Generate(string universeId, OmniversalCoordinates coordinates)
        {
            return Generate(universeId, coordinates, null, true);
        }

        public static UniverseSignature Generate(OmniversalCoordinates coordinates, UniverseStructuralProfile profile)
        {
            return Generate(null, coordinates, profile, true);
        }

        public static UniverseSignature Generate(string universeId, OmniversalCoordinates coordinates, UniverseStructuralProfile profile, bool isValid = true)
        {
            string id = string.IsNullOrEmpty(universeId) ? BuildSignature(coordinates) : universeId;
            return new UniverseSignature(id, coordinates, profile ?? UniverseStructuralProfile.CreateDefault(), isValid);
        }

        public static UniverseSignature Create(OmniversalCoordinates coordinates) => Generate(coordinates);

        public static UniverseSignature Create(string universeId, OmniversalCoordinates coordinates) => Generate(universeId, coordinates);

        public static UniverseSignature Create(OmniversalCoordinates coordinates, UniverseStructuralProfile profile) => Generate(coordinates, profile);

        public static UniverseSignature Create(string universeId, OmniversalCoordinates coordinates, UniverseStructuralProfile profile, bool isValid = true)
            => Generate(universeId, coordinates, profile, isValid);

        public static UniverseSignature FromCoordinates(OmniversalCoordinates coordinates) => Generate(coordinates);

        public static UniverseSignature FromCoordinates(string universeId, OmniversalCoordinates coordinates) => Generate(universeId, coordinates);

        public static UniverseSignature FromCoordinates(OmniversalCoordinates coordinates, UniverseStructuralProfile profile) => Generate(coordinates, profile);
        public static UniverseSignature Generate(OmniversalCoordinates coordinates, UniverseStructuralProfile profile, bool isValid)
            => Generate(null, coordinates, profile, isValid);

        public static UniverseSignature Generate(string universeId, OmniversalCoordinates coordinates, bool isValid)
            => Generate(universeId, coordinates, null, isValid);

        public static UniverseSignature Generate(Portals.Core.UniverseId universeId, OmniversalCoordinates coordinates)
            => Generate(universeId.IsValid ? universeId.Value : null, coordinates, null, true);

        public static UniverseSignature Generate(Portals.Core.UniverseId universeId, OmniversalCoordinates coordinates, UniverseStructuralProfile profile, bool isValid = true)
            => Generate(universeId.IsValid ? universeId.Value : null, coordinates, profile, isValid);

        public static UniverseSignature Create(OmniversalCoordinates coordinates, UniverseStructuralProfile profile, bool isValid)
            => Generate(coordinates, profile, isValid);

        public static UniverseSignature Create(string universeId, OmniversalCoordinates coordinates, bool isValid)
            => Generate(universeId, coordinates, isValid);

        public static UniverseSignature Create(Portals.Core.UniverseId universeId, OmniversalCoordinates coordinates)
            => Generate(universeId, coordinates);

        public static UniverseSignature FromCoordinates(OmniversalCoordinates coordinates, UniverseStructuralProfile profile, bool isValid)
            => Generate(coordinates, profile, isValid);

        public static UniverseSignature FromCoordinates(string universeId, OmniversalCoordinates coordinates, bool isValid)
            => Generate(universeId, coordinates, isValid);

        public static UniverseSignature FromCoordinates(Portals.Core.UniverseId universeId, OmniversalCoordinates coordinates)
            => Generate(universeId, coordinates);

        private static string BuildSignature(OmniversalCoordinates coordinates)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "L{0:00}-R{1:00}-A{2:000}{3}-T{4:G17}-Q{5:G17}-X{6:G9}-Y{7:G9}-Z{8:G9}",
                coordinates.Layer,
                coordinates.Ring,
                coordinates.AuPosition,
                coordinates.IsPrimeAu ? ":P" : string.Empty,
                coordinates.Theta,
                coordinates.Rho,
                coordinates.X,
                coordinates.Y,
                coordinates.Z);
        }

        public bool Equals(UniverseSignature other)
        {
            if (ReferenceEquals(this, other)) return true;
            if (other is null) return false;
            return string.Equals(Signature, other.Signature, StringComparison.Ordinal);
        }

        public override bool Equals(object obj) => obj is UniverseSignature other && Equals(other);

        public override int GetHashCode() => Signature.GetHashCode(StringComparison.Ordinal);

        public static bool operator ==(UniverseSignature a, UniverseSignature b)
        {
            if (ReferenceEquals(a, b)) return true;
            if (a is null || b is null) return false;
            return a.Equals(b);
        }

        public static bool operator !=(UniverseSignature a, UniverseSignature b) => !(a == b);
    }
}
