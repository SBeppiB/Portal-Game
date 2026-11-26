using System;

namespace Portals.Runtime.Mechanics
{
    public static class PortalDisplacementCalculator
    {
        public static int CircularAuDistance(int a1, int a2, int positionsPerRing)
        {
            if (positionsPerRing <= 0) throw new ArgumentOutOfRangeException(nameof(positionsPerRing));
            int diff = Math.Abs(a1 - a2) % positionsPerRing;
            if (diff < 0) diff += positionsPerRing;
            int wrapped = positionsPerRing - diff;
            return Math.Min(diff, wrapped);
        }

        public static int CircularAuDistance(int a1, int a2)
        {
            return CircularAuDistance(a1, a2, 180);
        }

        public static double AngularDistance(double theta1, double theta2)
        {
            double d = Math.Abs(theta1 - theta2);
            d = d % 360.0;
            if (d < 0) d += 360.0;
            return Math.Min(d, 360.0 - d);
        }

        // OPMConfiguration path (existing behaviour)
        public static bool TryCalculate(OmniversalCoordinates origin, OmniversalCoordinates destination, OPMConfiguration config, out PortalDisplacement result, out string error)
        {
            result = default;
            error = string.Empty;
            if (config == null) { error = "Configuration is null."; return false; }
            if (!config.IsValid()) { error = "Invalid configuration."; return false; }
            if (!origin.IsValid(config)) { error = "Invalid origin coordinates."; return false; }
            if (!destination.IsValid(config)) { error = "Invalid destination coordinates."; return false; }

            try
            {
                double dl = Math.Abs(destination.Layer - origin.Layer);
                double dr = Math.Abs(destination.Ring - origin.Ring);
                int da = CircularAuDistance(origin.AuPosition, destination.AuPosition, config.auPositionsPerRing);
                double dtheta = AngularDistance(origin.Theta, destination.Theta);
                double drho = Math.Abs(destination.Rho - origin.Rho);
                double ds = Math.Sqrt(
                    Math.Pow(destination.X - origin.X, 2) +
                    Math.Pow(destination.Y - origin.Y, 2) +
                    Math.Pow(destination.Z - origin.Z, 2));

                double layerDenom = Math.Max(1.0, config.totalLayers - 1);
                double ringDenom = Math.Max(1.0, config.ringsPerLayer - 1);
                double auDenom = Math.Max(1.0, config.auPositionsPerRing / 2.0);

                double nL = dl / layerDenom;
                double nR = dr / ringDenom;
                double nA = da / auDenom;
                double nRho = drho / config.characteristicRadialScale;
                double nTheta = dtheta / config.characteristicAngularScale;
                double nX = ds / config.characteristicSpatialScale;

                double q =
                    config.weightLayer * nL * nL +
                    config.weightRing * nR * nR +
                    config.weightAu * nA * nA +
                    config.weightRho * nRho * nRho +
                    config.weightTheta * nTheta * nTheta +
                    config.weightSpatial * nX * nX;

                if (double.IsNaN(q) || double.IsInfinity(q) || q < 0) { error = "Displacement calculation produced invalid value."; return false; }
                double dt = Math.Sqrt(q);
                if (double.IsNaN(dt) || double.IsInfinity(dt)) { error = "Displacement calculation produced invalid value."; return false; }

                result = new PortalDisplacement(dl, dr, da, dtheta, drho, ds, dt);
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        // OPMDisplacementConfiguration path (mission constants)
        public static bool TryCalculate(OmniversalCoordinates origin, OmniversalCoordinates destination, OPMDisplacementConfiguration config, out PortalDisplacement result, out string error)
        {
            result = default;
            error = string.Empty;
            if (config == null) { error = "Configuration is null."; return false; }
            if (!config.IsValid()) { error = "Invalid configuration."; return false; }
            if (!origin.IsValid() || !destination.IsValid()) { error = "Invalid coordinates."; return false; }

            try
            {
                double dl = Math.Abs(destination.Layer - origin.Layer);
                double dr = Math.Abs(destination.Ring - origin.Ring);
                int da = CircularAuDistance(origin.AuPosition, destination.AuPosition, 180);
                double dtheta = AngularDistance(origin.Theta, destination.Theta);
                double drho = Math.Abs(destination.Rho - origin.Rho);
                double ds = Math.Sqrt(
                    Math.Pow(destination.X - origin.X, 2) +
                    Math.Pow(destination.Y - origin.Y, 2) +
                    Math.Pow(destination.Z - origin.Z, 2));

                const double layerDenom = 49.0;
                const double ringDenom = 9.0;
                const double auDenom = 90.0;

                double nL = dl / layerDenom;
                double nR = dr / ringDenom;
                double nA = da / auDenom;
                double nRho = drho / config.characteristicRadialScale;
                double nTheta = dtheta / config.characteristicAngularScale;
                double nX = ds / config.characteristicSpatialScale;

                double q =
                    config.weightLayer * nL * nL +
                    config.weightRing * nR * nR +
                    config.weightAu * nA * nA +
                    config.weightRho * nRho * nRho +
                    config.weightTheta * nTheta * nTheta +
                    config.weightSpatial * nX * nX;

                if (double.IsNaN(q) || double.IsInfinity(q) || q < 0) { error = "Displacement calculation produced invalid value."; return false; }
                double dt = Math.Sqrt(q);
                if (double.IsNaN(dt) || double.IsInfinity(dt)) { error = "Displacement calculation produced invalid value."; return false; }

                result = new PortalDisplacement(dl, dr, da, dtheta, drho, ds, dt);
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        public static PortalDisplacement Calculate(OmniversalCoordinates origin, OmniversalCoordinates destination, OPMConfiguration config)
        {
            if (!TryCalculate(origin, destination, config, out PortalDisplacement result, out string error))
                throw new InvalidOperationException(error);
            return result;
        }

        public static PortalDisplacement Calculate(OmniversalCoordinates origin, OmniversalCoordinates destination, OPMDisplacementConfiguration config)
        {
            if (!TryCalculate(origin, destination, config, out PortalDisplacement result, out string error))
                throw new InvalidOperationException(error);
            return result;
        }

        // Aliases
        public static PortalDisplacement Compute(OmniversalCoordinates origin, OmniversalCoordinates destination, OPMConfiguration config) =>
            Calculate(origin, destination, config);

        public static PortalDisplacement Compute(OmniversalCoordinates origin, OmniversalCoordinates destination, OPMDisplacementConfiguration config) =>
            Calculate(origin, destination, config);

        public static bool TryCompute(OmniversalCoordinates origin, OmniversalCoordinates destination, OPMConfiguration config, out PortalDisplacement result, out string error) =>
            TryCalculate(origin, destination, config, out result, out error);

        public static bool TryCompute(OmniversalCoordinates origin, OmniversalCoordinates destination, OPMDisplacementConfiguration config, out PortalDisplacement result, out string error) =>
            TryCalculate(origin, destination, config, out result, out error);
    }
}
