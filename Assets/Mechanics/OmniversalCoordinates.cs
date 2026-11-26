using System;
using UnityEngine;

namespace Mechanics
{
    /// <summary>
    /// Immutable destination coordinate for the Omniverse:
    /// P = (L, R, A, theta, rho, x, y, z)
    /// </summary>
    [Serializable]
    public sealed class OmniversalCoordinates : IEquatable<OmniversalCoordinates>
    {
        private readonly int m_Layer;
        private readonly int m_Ring;
        private readonly int m_AuPosition;
        private readonly bool m_IsPrimeAu;
        private readonly double m_Theta;
        private readonly double m_Rho;
        private readonly Vector3 m_LocalPosition;

        public const int PositionsPerRing = 180;
        public const int LayerMin = 1;
        public const int LayerMax = 50;
        public const int RingMin = 0;
        public const int RingMax = 9;
        public const int AuMin = 0;
        public const int AuMax = PositionsPerRing - 1;
        public const double ThetaMin = 0.0;
        public const double ThetaMax = 360.0;
        public const double RhoMin = 0.0;

        public int Layer => m_Layer;
        public int Ring => m_Ring;
        public int AuPosition => m_AuPosition;
        public int A => m_AuPosition;
        public int Au => m_AuPosition;
        public bool IsPrimeAu => m_IsPrimeAu;
        public double AngularPosition => m_Theta;
        public double Theta => m_Theta;
        public double RadialPosition => m_Rho;
        public double Rho => m_Rho;
        public Vector3 LocalPosition => m_LocalPosition;
        public Vector3 LocalSpatialCoordinates => m_LocalPosition;
        public Vector3 LocalCoordinates => m_LocalPosition;
        public double X => m_LocalPosition.x;
        public double Y => m_LocalPosition.y;
        public double Z => m_LocalPosition.z;

        public OmniversalCoordinates(
            int layer,
            int ring,
            int auPosition,
            double angularPosition,
            double radialPosition,
            Vector3 localPosition,
            bool isPrimeAu = false)
        {
            m_Layer = layer;
            m_Ring = ring;
            m_AuPosition = auPosition;
            m_IsPrimeAu = isPrimeAu;
            m_Theta = angularPosition;
            m_Rho = radialPosition;
            m_LocalPosition = localPosition;
        }

        public OmniversalCoordinates(
            int layer,
            int ring,
            int auPosition,
            double angularPosition,
            double radialPosition,
            double x,
            double y,
            double z,
            bool isPrimeAu = false)
            : this(layer, ring, auPosition, angularPosition, radialPosition, new Vector3((float)x, (float)y, (float)z), isPrimeAu)
        {
        }

        public bool IsValid() => Validate();

        // Accepts an optional configuration object (e.g. OPMConfiguration) without
        // coupling this type to the portal systems. Validation uses the documented
        // Omniversal coordinate ranges.
        public bool IsValid(object configuration) => Validate();

        private bool Validate()
        {
            if (m_Layer < LayerMin || m_Layer > LayerMax) return false;
            if (m_Ring < RingMin || m_Ring > RingMax) return false;

            if (!m_IsPrimeAu)
            {
                if (m_AuPosition < AuMin || m_AuPosition > AuMax) return false;
            }

            if (double.IsNaN(m_Theta) || double.IsInfinity(m_Theta)) return false;
            if (m_Theta < ThetaMin || m_Theta >= ThetaMax) return false;

            if (double.IsNaN(m_Rho) || double.IsInfinity(m_Rho)) return false;
            if (m_Rho < RhoMin) return false;

            if (float.IsNaN(m_LocalPosition.x) || float.IsNaN(m_LocalPosition.y) || float.IsNaN(m_LocalPosition.z)) return false;
            if (float.IsInfinity(m_LocalPosition.x) || float.IsInfinity(m_LocalPosition.y) || float.IsInfinity(m_LocalPosition.z)) return false;

            return true;
        }

        public static int CircularAuDistance(int a, int b, int positionsPerRing = PositionsPerRing)
        {
            if (positionsPerRing <= 0) return 0;
            int na = ((a % positionsPerRing) + positionsPerRing) % positionsPerRing;
            int nb = ((b % positionsPerRing) + positionsPerRing) % positionsPerRing;
            int diff = Math.Abs(na - nb);
            return Math.Min(diff, positionsPerRing - diff);
        }

        public static int GetCircularAuDistance(int a, int b, int positionsPerRing = PositionsPerRing) =>
            CircularAuDistance(a, b, positionsPerRing);

        public int GetCircularAuDistance(OmniversalCoordinates other) =>
            other == null ? 0 : CircularAuDistance(m_AuPosition, other.m_AuPosition, PositionsPerRing);

        public int CircularAuDistance(OmniversalCoordinates other) =>
            GetCircularAuDistance(other);

        public int GetCircularDistance(OmniversalCoordinates other) =>
            GetCircularAuDistance(other);

        public int CircularDistance(OmniversalCoordinates other) =>
            GetCircularAuDistance(other);

        public override string ToString()
        {
            if (m_IsPrimeAu)
                return $"L{m_Layer:00}-R{m_Ring:00}-PRIME";
            return $"L{m_Layer:00}-R{m_Ring:00}-P{m_AuPosition:000}";
        }

        public bool Equals(OmniversalCoordinates other)
        {
            if (other is null) return false;
            if (ReferenceEquals(this, other)) return true;
            return m_Layer == other.m_Layer &&
                   m_Ring == other.m_Ring &&
                   m_AuPosition == other.m_AuPosition &&
                   m_IsPrimeAu == other.m_IsPrimeAu &&
                   Math.Abs(m_Theta - other.m_Theta) < 1e-9 &&
                   Math.Abs(m_Rho - other.m_Rho) < 1e-9 &&
                   m_LocalPosition == other.m_LocalPosition;
        }

        public override bool Equals(object obj) => obj is OmniversalCoordinates c && Equals(c);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = (hash * 31) + m_Layer;
                hash = (hash * 31) + m_Ring;
                hash = (hash * 31) + m_AuPosition;
                hash = (hash * 31) + (m_IsPrimeAu ? 1 : 0);
                hash = (hash * 31) + m_Theta.GetHashCode();
                hash = (hash * 31) + m_Rho.GetHashCode();
                hash = (hash * 31) + m_LocalPosition.GetHashCode();
                return hash;
            }
        }

        public static bool operator ==(OmniversalCoordinates a, OmniversalCoordinates b)
        {
            if (a is null) return b is null;
            return a.Equals(b);
        }

        public static bool operator !=(OmniversalCoordinates a, OmniversalCoordinates b) => !(a == b);
    }
}
