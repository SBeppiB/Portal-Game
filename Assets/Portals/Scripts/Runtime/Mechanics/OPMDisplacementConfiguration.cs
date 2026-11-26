using System;
using UnityEngine;

namespace Portals.Runtime.Mechanics
{
    /// <summary>
    /// Configuration for omniversal displacement weights and normalisation scales.
    /// </summary>
    [CreateAssetMenu(menuName = "Portals/OPM Displacement Configuration", fileName = "OPMDisplacementConfiguration")]
    public class OPMDisplacementConfiguration : ScriptableObject
    {
        [Header("Dimensional Displacement Weights (sum to 1)")]
        public double weightLayer = 0.25;
        public double weightRing = 0.20;
        public double weightAu = 0.20;
        public double weightRho = 0.10;
        public double weightTheta = 0.10;
        public double weightSpatial = 0.15;

        [Header("Characteristic Normalisation Scales")]
        public double characteristicRadialScale = 100.0;
        public double characteristicAngularScale = 180.0;
        public double characteristicSpatialScale = 1000.0;

        public OPMDisplacementConfiguration() { }

        public static OPMDisplacementConfiguration CreateDefault()
        {
            return CreateInstance<OPMDisplacementConfiguration>();
        }

        public static OPMDisplacementConfiguration FromOPMConfiguration(OPMConfiguration config)
        {
            if (config == null) return CreateDefault();
            var result = CreateDefault();
            result.weightLayer = config.weightLayer;
            result.weightRing = config.weightRing;
            result.weightAu = config.weightAu;
            result.weightRho = config.weightRho;
            result.weightTheta = config.weightTheta;
            result.weightSpatial = config.weightSpatial;
            result.characteristicRadialScale = config.characteristicRadialScale;
            result.characteristicAngularScale = config.characteristicAngularScale;
            result.characteristicSpatialScale = config.characteristicSpatialScale;
            return result;
        }

        public bool IsValid()
        {
            if (!double.IsFinite(weightLayer) || !double.IsFinite(weightRing) || !double.IsFinite(weightAu) ||
                !double.IsFinite(weightRho) || !double.IsFinite(weightTheta) || !double.IsFinite(weightSpatial))
                return false;
            if (weightLayer < 0 || weightRing < 0 || weightAu < 0 || weightRho < 0 || weightTheta < 0 || weightSpatial < 0)
                return false;
            double wsum = weightLayer + weightRing + weightAu + weightRho + weightTheta + weightSpatial;
            if (!double.IsFinite(wsum) || Math.Abs(wsum - 1.0) > 1e-6) return false;

            if (!double.IsFinite(characteristicRadialScale) || !double.IsFinite(characteristicAngularScale) || !double.IsFinite(characteristicSpatialScale))
                return false;
            if (characteristicRadialScale <= 0 || characteristicAngularScale <= 0 || characteristicSpatialScale <= 0)
                return false;
            return true;
        }
    }
}
