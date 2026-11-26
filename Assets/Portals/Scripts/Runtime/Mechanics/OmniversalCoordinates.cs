using System;
using UnityEngine;

namespace Portals.Runtime.Mechanics
{
    /// <summary>
    /// Destination within the Great Omniverse:
    /// P = (L, R, A, theta, rho, x, y, z)
    /// </summary>
    [Serializable]
    public struct OmniversalCoordinates : IEquatable<OmniversalCoordinates>
    {
        [SerializeField] private int m_Layer;
        [SerializeField] private int m_Ring;
        [SerializeField] private int m_AuPosition;
        [SerializeField] private bool m_IsPrimeAu;
        [SerializeField] private double m_Theta;
        [SerializeField] private double m_Rho;
        [SerializeField] private Vector3 m_LocalPosition;

        public int Layer => m_Layer;
        public int Ring => m_Ring;
        public int AuPosition => m_AuPosition;
        public int A => m_AuPosition;
        public bool IsPrimeAu => m_IsPrimeAu;
        public double Theta => m_Theta;
        public double Rho => m_Rho;
        public Vector3 LocalPosition => m_LocalPosition;
        public double X => m_LocalPosition.x;
        public double Y => m_LocalPosition.y;
        public double Z => m_LocalPosition.z;
        public double L => m_Layer;
        public double R => m_Ring;
        // Alias for angular/radial names.
        public double AngularPosition => m_Theta;
        public double RadialPosition => m_Rho;

        public OmniversalCoordinates(int layer, int ring, int auPosition, double theta, double rho, Vector3 localPosition, bool isPrimeAu = false)
        {
            m_Layer = layer;
            m_Ring = ring;
            m_AuPosition = auPosition;
            m_IsPrimeAu = isPrimeAu;
            m_Theta = theta;
            m_Rho = rho;
            m_LocalPosition = localPosition;
        }

        public OmniversalCoordinates(int layer, int ring, int auPosition, double theta, double rho, double x, double y, double z, bool isPrimeAu = false)
            : this(layer, ring, auPosition, theta, rho, new Vector3((float)x, (float)y, (float)z), isPrimeAu)
        {
        }

        public bool IsValid(OPMConfiguration config)
        {
            if (config == null) return false;
            if (m_Layer < config.layerMin || m_Layer > config.LayerMax) return false;
            if (m_Ring < config.ringMin || m_Ring > config.RingMax) return false;
            if (!m_IsPrimeAu)
            {
                if (m_AuPosition < config.auMin || m_AuPosition > config.AuMax) return false;
            }
            if (double.IsNaN(m_Theta) || double.IsInfinity(m_Theta)) return false;
            if (m_Theta < config.thetaMin || m_Theta >= config.thetaMax) return false;
            if (double.IsNaN(m_Rho) || double.IsInfinity(m_Rho)) return false;
            if (m_Rho < config.rhoMin) return false;
            if (float.IsNaN(m_LocalPosition.x) || float.IsNaN(m_LocalPosition.y) || float.IsNaN(m_LocalPosition.z)) return false;
            if (float.IsInfinity(m_LocalPosition.x) || float.IsInfinity(m_LocalPosition.y) || float.IsInfinity(m_LocalPosition.z)) return false;
            return true;
        }

        public bool IsValid()
        {
            return IsValid(OPMConfiguration.CreateDefault());
        }

        public override string ToString()
        {
            if (m_IsPrimeAu)
                return $"L{m_Layer:00}-R{m_Ring:00}-PRIME";
            return $"L{m_Layer:00}-R{m_Ring:00}-P{m_AuPosition:000}";
        }

        public bool Equals(OmniversalCoordinates other)
        {
            return m_Layer == other.m_Layer &&
                   m_Ring == other.m_Ring &&
                   m_AuPosition == other.m_AuPosition &&
                   m_IsPrimeAu == other.m_IsPrimeAu &&
                   Math.Abs(m_Theta - other.m_Theta) < 1e-9 &&
                   Math.Abs(m_Rho - other.m_Rho) < 1e-9 &&
                   m_LocalPosition == other.m_LocalPosition;
        }

        public override bool Equals(object obj) => obj is OmniversalCoordinates c && Equals(c);
        public override int GetHashCode() => (m_Layer * 397) ^ (m_Ring * 31) ^ m_AuPosition ^ (m_IsPrimeAu ? 1 : 0);
        public static bool operator ==(OmniversalCoordinates a, OmniversalCoordinates b) => a.Equals(b);
        public static bool operator !=(OmniversalCoordinates a, OmniversalCoordinates b) => !a.Equals(b);
    }
}
