using System;

namespace Portals.Runtime.Mechanics
{
    [Serializable]
    public class PortalDiagnosticData
    {
        public OmniversalCoordinates OriginCoordinates;
        public OmniversalCoordinates DestinationCoordinates;
        public double SpatialDisplacement;
        public double DimensionalDisplacement;
        public double StructuralDivergence;
        public double DestinationInstability;
        public double BaseEnergy;
        public double StructuralResistance;
        public double LayerResistance;
        public double RiftMultiplier;
        public double TotalEnergyRequirement;
        public double AvailableEnergy;
        public double Stability;
        public double Accuracy;
        public double Integrity;
        public PortalFormationFailureReason FormationResult;
    }
}
