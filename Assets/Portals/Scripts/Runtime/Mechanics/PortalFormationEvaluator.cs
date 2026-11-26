using System;

namespace Portals.Runtime.Mechanics
{
    public static class PortalFormationEvaluator
    {
        public static PortalFormationResult Evaluate(
            PortalTravelMode mode,
            OmniversalCoordinates origin,
            OmniversalCoordinates destination,
            OPMConfiguration config,
            OmniversalScanResult scan,
            double availableEnergy,
            double coordinateErrorOverride = -1.0)
        {
            PortalFormationResult result = new PortalFormationResult();
            if (config == null || !config.IsValid())
            {
                result.Success = false;
                result.FailureReason = PortalFormationFailureReason.InvalidConfiguration;
                result.Message = "Invalid OPM configuration.";
                return result;
            }
            if (!origin.IsValid(config) || !destination.IsValid(config))
            {
                result.Success = false;
                result.FailureReason = PortalFormationFailureReason.InvalidCoordinates;
                result.Message = "Invalid coordinates.";
                return result;
            }
            if (scan == null || !scan.Success)
            {
                result.Success = false;
                result.FailureReason = PortalFormationFailureReason.DestinationUnavailable;
                result.Message = scan != null ? scan.FailureReason : "No scan result.";
                return result;
            }
            if (mode == PortalTravelMode.Altiport && scan.Signature == null)
            {
                result.Success = false;
                result.FailureReason = PortalFormationFailureReason.UniverseAccessDenied;
                result.Message = "Universe access denied.";
                return result;
            }

            UniverseStructuralProfile profile = scan.Signature != null ? scan.Signature.Profile : UniverseStructuralProfile.CreateDefault();

            double coordinateError = coordinateErrorOverride >= 0 ? coordinateErrorOverride : scan.CoordinateError;
            if (coordinateError < 0) coordinateError = 0;

            double requiredEnergy;
            string err;
            if (!PortalEnergyCalculator.TryCalculateTotalEnergy(config, origin, destination, profile, scan.RiftInterference, out requiredEnergy, out err))
            {
                result.Success = false;
                result.FailureReason = PortalFormationFailureReason.InvalidConfiguration;
                result.Message = err;
                return result;
            }

            double vd = scan.StructuralDivergence;
            try { vd = PortalStructuralAnalyzer.CalculateStructuralDivergence(origin, destination, config); } catch { /* keep scan value */ }

            double sp = PortalStabilityCalculator.CalculateStability(availableEnergy, requiredEnergy, profile.DestinationInstability);
            double ap = PortalAccuracyCalculator.CalculateAccuracy(coordinateError);
            double cp = PortalStabilityCalculator.CalculateCompatibility(vd);
            double ip = PortalStabilityCalculator.CalculateIntegrity(sp, cp);

            result.RequiredEnergy = requiredEnergy;

            // Build diagnostics
            PortalDisplacement disp;
            try { disp = PortalDisplacementCalculator.Calculate(origin, destination, config); }
            catch { disp = default; }

            result.Diagnostics = new PortalDiagnosticData
            {
                OriginCoordinates = origin,
                DestinationCoordinates = destination,
                SpatialDisplacement = disp.SpatialDisplacement,
                DimensionalDisplacement = disp.TotalDisplacement,
                StructuralDivergence = vd,
                DestinationInstability = profile.DestinationInstability,
                BaseEnergy = PortalEnergyCalculator.CalculateBaseEnergy(config, disp.TotalDisplacement),
                StructuralResistance = PortalEnergyCalculator.CalculateStructuralResistance(config, vd),
                LayerResistance = PortalEnergyCalculator.CalculateLayerRingResistance(config, disp.LayerDisplacement, PortalStructuralAnalyzer.VR(disp.RingDisplacement, config)),
                RiftMultiplier = PortalEnergyCalculator.CalculateRiftInterferenceMultiplier(config, profile.RiftInterference, profile.StructuralBoundaryFactor),
                TotalEnergyRequirement = requiredEnergy,
                AvailableEnergy = availableEnergy,
                Stability = sp,
                Accuracy = ap,
                Integrity = ip
            };

            // Failure checks
            if (availableEnergy + 1e-9 < requiredEnergy)
            {
                result.Success = false;
                result.FailureReason = PortalFormationFailureReason.InsufficientEnergy;
                result.Message = "Insufficient energy.";
                result.Diagnostics.FormationResult = result.FailureReason;
                return result;
            }
            if (double.IsNaN(sp) || double.IsInfinity(sp) && sp < 0)
            {
                result.Success = false;
                result.FailureReason = PortalFormationFailureReason.UnstableDestination;
                result.Message = "Unstable destination.";
                result.Diagnostics.FormationResult = result.FailureReason;
                return result;
            }
            if (!PortalStabilityCalculator.IsEligible(sp, ap, config))
            {
                if (sp < config.minimumStability && !double.IsPositiveInfinity(sp))
                {
                    result.Success = false;
                    result.FailureReason = PortalFormationFailureReason.UnstableDestination;
                    result.Message = "Stability below threshold.";
                }
                else
                {
                    result.Success = false;
                    result.FailureReason = PortalFormationFailureReason.InsufficientAccuracy;
                    result.Message = "Accuracy below threshold.";
                }
                result.Diagnostics.FormationResult = result.FailureReason;
                return result;
            }

            // Structural mismatch heuristic
            if (profile.PortalCompatibility < 0.0)
            {
                result.Success = false;
                result.FailureReason = PortalFormationFailureReason.StructuralMismatch;
                result.Message = "Structural mismatch.";
                result.Diagnostics.FormationResult = result.FailureReason;
                return result;
            }

            result.Success = true;
            result.FailureReason = PortalFormationFailureReason.None;
            result.Message = "Formation successful.";
            result.Diagnostics.FormationResult = PortalFormationFailureReason.None;
            return result;
        }
    }

    public static class PortalFormationService
    {
        public static PortalFormationResult Evaluate(
            PortalTravelMode mode,
            OmniversalCoordinates origin,
            OmniversalCoordinates destination,
            OPMConfiguration config,
            OmniversalScanResult scan,
            double availableEnergy,
            double coordinateErrorOverride = -1.0)
        {
            return PortalFormationEvaluator.Evaluate(mode, origin, destination, config, scan, availableEnergy, coordinateErrorOverride);
        }
    }
}
