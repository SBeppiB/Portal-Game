using System;

namespace Portals.Runtime.Mechanics
{
    public static class PortalEnergyCalculator
    {
        public static double CalculateBaseEnergy(OPMConfiguration config, double totalDisplacement)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            if (!config.IsValid()) throw new InvalidOperationException("Invalid configuration.");
            if (double.IsNaN(totalDisplacement) || double.IsInfinity(totalDisplacement) || totalDisplacement < 0) throw new ArgumentOutOfRangeException(nameof(totalDisplacement));
            double result = config.universalConversionConstant * Math.Pow(totalDisplacement, config.dimensionalScalingExponent);
            if (double.IsNaN(result) || double.IsInfinity(result)) throw new InvalidOperationException("Base energy calculation produced invalid value.");
            if (result < 0) result = 0;
            return result;
        }

        public static double CalculateStructuralResistance(OPMConfiguration config, double structuralDivergence)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            if (!config.IsValid()) throw new InvalidOperationException("Invalid configuration.");
            double vd = Math.Clamp(structuralDivergence, 0.0, 1.0);
            double result = 1.0 + config.structuralResistanceBeta * vd;
            if (!double.IsFinite(result)) throw new InvalidOperationException("Structural resistance calculation produced invalid value.");
            return result;
        }

        public static double CalculateDestinationInstabilityMultiplier(OPMConfiguration config, double instabilityU)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            if (!config.IsValid()) throw new InvalidOperationException("Invalid configuration.");
            if (instabilityU < 0) throw new ArgumentOutOfRangeException(nameof(instabilityU));
            double result = 1.0 + config.instabilityCoefficientGamma * instabilityU;
            if (!double.IsFinite(result)) throw new InvalidOperationException("Instability multiplier calculation produced invalid value.");
            return result;
        }

        public static double CalculateLayerRingResistance(OPMConfiguration config, double layerDisplacement, double ringVR)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            if (!config.IsValid()) throw new InvalidOperationException("Invalid configuration.");
            double result = 1.0 + config.layerResistanceLambda * layerDisplacement + config.ringResistanceMu * ringVR;
            if (!double.IsFinite(result)) throw new InvalidOperationException("Layer/ring resistance calculation produced invalid value.");
            return result;
        }

        public static double CalculateRiftInterferenceMultiplier(OPMConfiguration config, double riftInterferenceF, double structuralBoundaryFactorB)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            if (!config.IsValid()) throw new InvalidOperationException("Invalid configuration.");
            if (structuralBoundaryFactorB <= -1.0) throw new ArgumentOutOfRangeException(nameof(structuralBoundaryFactorB));
            double denom = 1.0 + structuralBoundaryFactorB;
            if (Math.Abs(denom) < double.Epsilon) throw new InvalidOperationException("Invalid structural boundary factor.");
            double result = 1.0 + config.riftInterferenceEta * riftInterferenceF / denom;
            if (!double.IsFinite(result)) throw new InvalidOperationException("Rift interference calculation produced invalid value.");
            return result;
        }

        public static double CalculateTotalEnergy(
            OPMConfiguration config,
            OmniversalCoordinates origin,
            OmniversalCoordinates destination,
            UniverseStructuralProfile profile,
            double riftInterferenceOverride = double.NaN)
        {
            if (!TryCalculateTotalEnergy(config, origin, destination, profile, riftInterferenceOverride, out double energy, out string error))
                throw new InvalidOperationException(error);
            return energy;
        }

        public static bool TryCalculateTotalEnergy(
            OPMConfiguration config,
            OmniversalCoordinates origin,
            OmniversalCoordinates destination,
            UniverseStructuralProfile profile,
            double riftInterferenceOverride,
            out double energy,
            out string error)
        {
            energy = 0;
            error = string.Empty;
            if (config == null) { error = "Configuration is null."; return false; }
            if (!config.IsValid()) { error = "Invalid configuration."; return false; }
            if (!origin.IsValid(config) || !destination.IsValid(config)) { error = "Invalid coordinates."; return false; }
            if (profile != null && !profile.IsValid()) { error = "Invalid destination profile."; return false; }

            try
            {
                PortalDisplacement disp = PortalDisplacementCalculator.Calculate(origin, destination, config);
                double vd = PortalStructuralAnalyzer.CalculateStructuralDivergence(origin, destination, config);

                double u = profile != null ? profile.DestinationInstability : 0.0;
                double f = double.IsNaN(riftInterferenceOverride)
                    ? (profile != null ? profile.RiftInterference : 0.0)
                    : riftInterferenceOverride;
                double b = profile != null ? profile.StructuralBoundaryFactor : 1.0;

                double baseE = CalculateBaseEnergy(config, disp.TotalDisplacement);
                double rs = CalculateStructuralResistance(config, vd);
                double ism = CalculateDestinationInstabilityMultiplier(config, u);
                double rl = CalculateLayerRingResistance(config, disp.LayerDisplacement, PortalStructuralAnalyzer.VR(disp.RingDisplacement, config));
                double ir = CalculateRiftInterferenceMultiplier(config, f, b);

                double total = baseE * rs * ism * rl * ir;
                if (double.IsNaN(total) || double.IsInfinity(total) || total < 0)
                {
                    error = "Total energy calculation produced invalid value.";
                    return false;
                }
                energy = total;
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        // Aliases
        public static double ComputeBaseEnergy(OPMConfiguration config, double totalDisplacement) => CalculateBaseEnergy(config, totalDisplacement);
        public static double ComputeTotalEnergy(OPMConfiguration config, OmniversalCoordinates origin, OmniversalCoordinates destination, UniverseStructuralProfile profile) =>
            CalculateTotalEnergy(config, origin, destination, profile);
    }
}
