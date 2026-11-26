using System;

namespace Portals.Runtime.Mechanics
{
    public static class PortalStructuralAnalyzer
    {
        public static double CalculateStructuralDivergence(OmniversalCoordinates origin, OmniversalCoordinates destination, OPMConfiguration config)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            if (!config.IsValid()) throw new InvalidOperationException("Invalid configuration.");
            if (!origin.IsValid(config) || !destination.IsValid(config)) throw new InvalidOperationException("Invalid coordinates.");

            double dl = Math.Abs(destination.Layer - origin.Layer);
            int da = PortalDisplacementCalculator.CircularAuDistance(origin.AuPosition, destination.AuPosition, config.auPositionsPerRing);
            double dr = Math.Abs(destination.Ring - origin.Ring);

            double layerDenom = Math.Max(1.0, config.totalLayers - 1);
            double ringDenom = Math.Max(1.0, config.ringsPerLayer - 1);
            double auDenom = Math.Max(1.0, config.auPositionsPerRing / 2.0);

            double vl = dl / layerDenom;
            double vr = dr / ringDenom;
            double va = da / auDenom;

            double vd = config.structuralWeightRing * vr + config.structuralWeightAu * va + config.structuralWeightLayer * vl;
            if (double.IsNaN(vd) || double.IsInfinity(vd)) throw new InvalidOperationException("Structural divergence calculation produced invalid value.");
            vd = Math.Min(1.0, vd);
            if (vd < 0) vd = 0;
            return vd;
        }

        public static double ComputeStructuralDivergence(OmniversalCoordinates origin, OmniversalCoordinates destination, OPMConfiguration config) =>
            CalculateStructuralDivergence(origin, destination, config);

        public static bool TryCalculateStructuralDivergence(OmniversalCoordinates origin, OmniversalCoordinates destination, OPMConfiguration config, out double result, out string error)
        {
            result = 0;
            error = string.Empty;
            try
            {
                result = CalculateStructuralDivergence(origin, destination, config);
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        public static PortalStructuralDivergence Calculate(OmniversalCoordinates origin, OmniversalCoordinates destination, OPMConfiguration config)
        {
            double vd = CalculateStructuralDivergence(origin, destination, config);
            double dl = Math.Abs(destination.Layer - origin.Layer);
            double dr = Math.Abs(destination.Ring - origin.Ring);
            int da = PortalDisplacementCalculator.CircularAuDistance(origin.AuPosition, destination.AuPosition, config.auPositionsPerRing);
            double layerDenom = Math.Max(1.0, config.totalLayers - 1);
            double ringDenom = Math.Max(1.0, config.ringsPerLayer - 1);
            double auDenom = Math.Max(1.0, config.auPositionsPerRing / 2.0);
            double vl = dl / layerDenom;
            double vr = dr / ringDenom;
            double va = da / auDenom;
            return new PortalStructuralDivergence(vl, vr, va, vd, 1.0 - vd);
        }

        public static bool TryCalculate(OmniversalCoordinates origin, OmniversalCoordinates destination, OPMConfiguration config, out PortalStructuralDivergence result, out string error)
        {
            result = default;
            error = string.Empty;
            try
            {
                result = Calculate(origin, destination, config);
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        public static double VR(double ringDisplacement, OPMConfiguration config)
        {
            double denom = Math.Max(1.0, config.ringsPerLayer - 1);
            return ringDisplacement / denom;
        }

        public static double VA(int auDisplacement, OPMConfiguration config)
        {
            double denom = Math.Max(1.0, config.auPositionsPerRing / 2.0);
            return auDisplacement / denom;
        }

        public static double VL(double layerDisplacement, OPMConfiguration config)
        {
            double denom = Math.Max(1.0, config.totalLayers - 1);
            return layerDisplacement / denom;
        }
    }

    public static class PortalStructuralDivergenceExtensions
    {
        // no-op
    }
}
