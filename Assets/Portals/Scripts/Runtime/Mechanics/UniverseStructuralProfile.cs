using System;
using System.Collections.Generic;

namespace Portals.Runtime.Mechanics
{
    [Serializable]
    public class UniverseStructuralProfile
    {
        public double StructuralStability = 1.0;
        public double RiftInterference = 0.0;
        public double DestinationInstability = 0.0;
        public double StructuralBoundaryFactor = 1.0;
        public double PortalCompatibility = 1.0;
        public Dictionary<string, double> EnvironmentalModifiers = new Dictionary<string, double>();

        public UniverseStructuralProfile() { }

        public UniverseStructuralProfile(double stability, double riftInterference, double instability, double boundaryFactor, double compatibility)
        {
            StructuralStability = stability;
            RiftInterference = riftInterference;
            DestinationInstability = instability;
            StructuralBoundaryFactor = boundaryFactor;
            PortalCompatibility = compatibility;
        }

        public static UniverseStructuralProfile CreateDefault() => new UniverseStructuralProfile(1.0, 0.0, 0.0, 1.0, 1.0);

        public bool IsValid()
        {
            if (double.IsNaN(StructuralStability) || double.IsInfinity(StructuralStability)) return false;
            if (double.IsNaN(RiftInterference) || double.IsInfinity(RiftInterference)) return false;
            if (DestinationInstability < 0 || double.IsNaN(DestinationInstability) || double.IsInfinity(DestinationInstability)) return false;
            if (StructuralBoundaryFactor <= -1.0 || double.IsNaN(StructuralBoundaryFactor) || double.IsInfinity(StructuralBoundaryFactor)) return false;
            return true;
        }
    }
}
