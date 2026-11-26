using System;

namespace Portals.Runtime.Mechanics
{
    public static class PortalStabilityCalculator
    {
        public static double CalculateStability(double availableEnergy, double portalEnergy, double instabilityU)
        {
            if (availableEnergy < 0) throw new ArgumentOutOfRangeException(nameof(availableEnergy));
            if (portalEnergy < 0) throw new ArgumentOutOfRangeException(nameof(portalEnergy));
            if (instabilityU < 0) throw new ArgumentOutOfRangeException(nameof(instabilityU));
            double denom = portalEnergy * (1.0 + instabilityU);
            if (denom <= 0) 
            {
                // If no energy required, treat as infinitely stable when available >=0.
                return availableEnergy >= 0 ? double.PositiveInfinity : 0.0;
            }
            double result = availableEnergy / denom;
            if (double.IsNaN(result) || double.IsInfinity(result) && availableEnergy > 0) 
            {
                // keep infinity as valid "very stable"
            }
            return result;
        }

        public static double CalculateCompatibility(double structuralDivergence)
        {
            double vd = Math.Clamp(structuralDivergence, 0.0, 1.0);
            return 1.0 - vd;
        }

        public static double CalculateIntegrity(double stability, double compatibility)
        {
            double result = stability * compatibility;
            if (double.IsNaN(result) || double.IsInfinity(result)) 
            {
                if (double.IsPositiveInfinity(stability) && compatibility >= 0) return double.PositiveInfinity;
                return 0.0;
            }
            return result;
        }

        public static bool IsEligible(double stability, double accuracy, OPMConfiguration config)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            if (!config.IsValid()) throw new InvalidOperationException("Invalid configuration.");
            if (double.IsNaN(stability) || double.IsInfinity(stability) && stability < 0) return false;
            if (double.IsNaN(accuracy) || double.IsInfinity(accuracy)) return false;
            bool spOk = double.IsPositiveInfinity(stability) || stability >= config.minimumStability;
            bool apOk = accuracy >= config.minimumAccuracy;
            return spOk && apOk;
        }

        public static double ComputeStability(double availableEnergy, double portalEnergy, double instabilityU) => CalculateStability(availableEnergy, portalEnergy, instabilityU);
        public static double ComputeCompatibility(double structuralDivergence) => CalculateCompatibility(structuralDivergence);
        public static double ComputeIntegrity(double stability, double compatibility) => CalculateIntegrity(stability, compatibility);
    }
}
