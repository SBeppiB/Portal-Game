using System;
using UnityEngine;

namespace Portals.Runtime.Mechanics
{
    /// <summary>
    /// ScriptableObject configuration for Omniversal Portal Mechanics (OPM).
    /// </summary>
    [CreateAssetMenu(menuName = "Portals/OPM Configuration", fileName = "OPMConfiguration")]
    public class OPMConfiguration : ScriptableObject
    {
        [Header("Coordinate Ranges")]
        [Min(1)] public int totalLayers = 50;
        [Min(1)] public int ringsPerLayer = 10;
        [Min(1)] public int auPositionsPerRing = 180;

        [Min(0)] public int layerMin = 1;
        [Min(0)] public int ringMin = 0;
        [Min(0)] public int auMin = 0;

        [Header("Coordinate Validity")]
        public double thetaMin = 0.0;
        public double thetaMax = 360.0;
        public double rhoMin = 0.0;

        [Header("Characteristic Normalisation Scales")]
        public double characteristicRadialScale = 100.0;
        public double characteristicAngularScale = 180.0;
        public double characteristicSpatialScale = 1000.0;

        [Header("Dimensional Displacement Weights (sum to 1)")]
        public double weightLayer = 0.25;
        public double weightRing = 0.20;
        public double weightAu = 0.20;
        public double weightRho = 0.10;
        public double weightTheta = 0.10;
        public double weightSpatial = 0.15;

        [Header("Structural Divergence Weights (sum to 1)")]
        public double structuralWeightRing = 1.0 / 3.0;
        public double structuralWeightAu = 1.0 / 3.0;
        public double structuralWeightLayer = 1.0 / 3.0;

        [Header("Energy Constants")]
        public double universalConversionConstant = 1.0;
        public double dimensionalScalingExponent = 1.0;
        public double structuralResistanceBeta = 0.5;
        public double instabilityCoefficientGamma = 0.5;
        public double layerResistanceLambda = 0.1;
        public double ringResistanceMu = 0.1;
        public double riftInterferenceEta = 0.5;

        [Header("Thresholds")]
        public double minimumStability = 0.5;
        public double minimumAccuracy = 0.5;

        [Header("Scanner")]
        [Min(0.001f)] public float scannerFrequency = 10f;
        [Min(0f)] public float scannerCacheDuration = 5f;

        public int LayerMax => layerMin + totalLayers - 1;
        public int RingMax => ringMin + ringsPerLayer - 1;
        public int AuMax => auMin + auPositionsPerRing - 1;

        public static OPMConfiguration CreateDefault()
        {
            return CreateInstance<OPMConfiguration>();
        }

        public bool IsValid()
        {
            if (totalLayers <= 0 || ringsPerLayer <= 0 || auPositionsPerRing <= 0) return false;
            if (characteristicRadialScale <= 0 || characteristicAngularScale <= 0 || characteristicSpatialScale <= 0) return false;
            if (!double.IsFinite(characteristicRadialScale) || !double.IsFinite(characteristicAngularScale) || !double.IsFinite(characteristicSpatialScale)) return false;
            if (universalConversionConstant <= 0 || dimensionalScalingExponent <= 0) return false;
            if (!double.IsFinite(universalConversionConstant) || !double.IsFinite(dimensionalScalingExponent)) return false;
            if (structuralResistanceBeta < 0 || instabilityCoefficientGamma < 0 || layerResistanceLambda < 0 || ringResistanceMu < 0 || riftInterferenceEta < 0) return false;
            if (!double.IsFinite(structuralResistanceBeta) || !double.IsFinite(instabilityCoefficientGamma) || !double.IsFinite(layerResistanceLambda) || !double.IsFinite(ringResistanceMu) || !double.IsFinite(riftInterferenceEta)) return false;
            if (scannerFrequency <= 0 || scannerCacheDuration < 0) return false;
            if (!float.IsFinite(scannerFrequency) || !float.IsFinite(scannerCacheDuration)) return false;

            if (!double.IsFinite(weightLayer) || !double.IsFinite(weightRing) || !double.IsFinite(weightAu) || !double.IsFinite(weightRho) || !double.IsFinite(weightTheta) || !double.IsFinite(weightSpatial)) return false;
            if (weightLayer < 0 || weightRing < 0 || weightAu < 0 || weightRho < 0 || weightTheta < 0 || weightSpatial < 0) return false;
            double wsum = weightLayer + weightRing + weightAu + weightRho + weightTheta + weightSpatial;
            if (!double.IsFinite(wsum) || Math.Abs(wsum - 1.0) > 1e-6) return false;

            if (!double.IsFinite(structuralWeightRing) || !double.IsFinite(structuralWeightAu) || !double.IsFinite(structuralWeightLayer)) return false;
            if (structuralWeightRing < 0 || structuralWeightAu < 0 || structuralWeightLayer < 0) return false;
            double ssum = structuralWeightRing + structuralWeightAu + structuralWeightLayer;
            if (!double.IsFinite(ssum) || Math.Abs(ssum - 1.0) > 1e-6) return false;

            if (thetaMax <= thetaMin) return false;
            if (!double.IsFinite(thetaMin) || !double.IsFinite(thetaMax)) return false;
            if (rhoMin < 0 || !double.IsFinite(rhoMin)) return false;
            if (layerMin < 0 || ringMin < 0 || auMin < 0) return false;
            return true;
        }

        private void OnValidate()
        {
            totalLayers = Mathf.Max(1, totalLayers);
            ringsPerLayer = Mathf.Max(1, ringsPerLayer);
            auPositionsPerRing = Mathf.Max(1, auPositionsPerRing);
            characteristicRadialScale = Math.Max(double.Epsilon, characteristicRadialScale);
            characteristicAngularScale = Math.Max(double.Epsilon, characteristicAngularScale);
            characteristicSpatialScale = Math.Max(double.Epsilon, characteristicSpatialScale);
            universalConversionConstant = Math.Max(double.Epsilon, universalConversionConstant);
            dimensionalScalingExponent = Math.Max(double.Epsilon, dimensionalScalingExponent);
            scannerFrequency = Mathf.Max(0.001f, scannerFrequency);
            scannerCacheDuration = Mathf.Max(0f, scannerCacheDuration);
        }
    }

    // Alias suggested by the brief.
    public class PortalMechanicsConfiguration : OPMConfiguration { }
}
