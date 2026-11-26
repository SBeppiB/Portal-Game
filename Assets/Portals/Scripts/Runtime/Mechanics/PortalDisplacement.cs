using System;

namespace Portals.Runtime.Mechanics
{
    /// <summary>
    /// Immutable displacement result for OPM.
    /// </summary>
    [Serializable]
    public struct PortalDisplacement : IEquatable<PortalDisplacement>
    {
        public double LayerDisplacement { get; }
        public double RingDisplacement { get; }
        public double AuDisplacement { get; }
        public double AngularDisplacement { get; }
        public double RadialDisplacement { get; }
        public double SpatialDisplacement { get; }
        public double TotalDisplacement { get; }

        public PortalDisplacement(
            double layerDisplacement,
            double ringDisplacement,
            double auDisplacement,
            double angularDisplacement,
            double radialDisplacement,
            double spatialDisplacement,
            double totalDisplacement)
        {
            LayerDisplacement = layerDisplacement;
            RingDisplacement = ringDisplacement;
            AuDisplacement = auDisplacement;
            AngularDisplacement = angularDisplacement;
            RadialDisplacement = radialDisplacement;
            SpatialDisplacement = spatialDisplacement;
            TotalDisplacement = totalDisplacement;
        }

        public bool Equals(PortalDisplacement other)
        {
            return LayerDisplacement == other.LayerDisplacement &&
                   RingDisplacement == other.RingDisplacement &&
                   AuDisplacement == other.AuDisplacement &&
                   AngularDisplacement == other.AngularDisplacement &&
                   RadialDisplacement == other.RadialDisplacement &&
                   SpatialDisplacement == other.SpatialDisplacement &&
                   TotalDisplacement == other.TotalDisplacement;
        }

        public override bool Equals(object obj) => obj is PortalDisplacement other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 31 + LayerDisplacement.GetHashCode();
                hash = hash * 31 + RingDisplacement.GetHashCode();
                hash = hash * 31 + AuDisplacement.GetHashCode();
                hash = hash * 31 + AngularDisplacement.GetHashCode();
                hash = hash * 31 + RadialDisplacement.GetHashCode();
                hash = hash * 31 + SpatialDisplacement.GetHashCode();
                hash = hash * 31 + TotalDisplacement.GetHashCode();
                return hash;
            }
        }

        public static bool operator ==(PortalDisplacement a, PortalDisplacement b) => a.Equals(b);
        public static bool operator !=(PortalDisplacement a, PortalDisplacement b) => !a.Equals(b);
    }
}
