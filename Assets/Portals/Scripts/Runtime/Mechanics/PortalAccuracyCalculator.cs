using System;

namespace Portals.Runtime.Mechanics
{
    public static class PortalAccuracyCalculator
    {
        public static double CalculateCoordinateError(double epsL, double epsR, double epsA, double epsRho, double epsX)
        {
            if (double.IsNaN(epsL) || double.IsNaN(epsR) || double.IsNaN(epsA) || double.IsNaN(epsRho) || double.IsNaN(epsX)) throw new ArgumentOutOfRangeException();
            double sumsq = epsL * epsL + epsR * epsR + epsA * epsA + epsRho * epsRho + epsX * epsX;
            if (sumsq < 0) sumsq = 0;
            double result = Math.Sqrt(sumsq);
            if (double.IsNaN(result) || double.IsInfinity(result)) throw new InvalidOperationException("Coordinate error calculation produced invalid value.");
            return result;
        }

        public static double CalculateAccuracy(double coordinateError)
        {
            if (coordinateError < 0 || double.IsNaN(coordinateError) || double.IsInfinity(coordinateError)) throw new ArgumentOutOfRangeException(nameof(coordinateError));
            double result = 1.0 / (1.0 + coordinateError);
            if (double.IsNaN(result) || double.IsInfinity(result)) throw new InvalidOperationException("Accuracy calculation produced invalid value.");
            return result;
        }

        public static double ComputeCoordinateError(double epsL, double epsR, double epsA, double epsRho, double epsX) => CalculateCoordinateError(epsL, epsR, epsA, epsRho, epsX);
        public static double ComputeAccuracy(double coordinateError) => CalculateAccuracy(coordinateError);
    }
}
