using System;
using NUnit.Framework;
using UnityEngine;
using Portals.Runtime.Mechanics;

namespace Portals.Tests.EditMode
{
    public class OPMFoundationTests
    {
        private OPMConfiguration Cfg() => OPMConfiguration.CreateDefault();

        [Test]
        public void DisplacementConfig_Defaults_Valid()
        {
            var cfg = OPMDisplacementConfiguration.CreateDefault();
            Assert.IsTrue(cfg.IsValid());
            Assert.AreEqual(0.25, cfg.weightLayer, 1e-12);
            Assert.AreEqual(0.20, cfg.weightRing, 1e-12);
            Assert.AreEqual(0.20, cfg.weightAu, 1e-12);
            Assert.AreEqual(0.10, cfg.weightRho, 1e-12);
            Assert.AreEqual(0.10, cfg.weightTheta, 1e-12);
            Assert.AreEqual(0.15, cfg.weightSpatial, 1e-12);
            Assert.AreEqual(100.0, cfg.characteristicRadialScale, 1e-12);
            Assert.AreEqual(180.0, cfg.characteristicAngularScale, 1e-12);
            Assert.AreEqual(1000.0, cfg.characteristicSpatialScale, 1e-12);
        }

        [Test]
        public void DisplacementConfig_InvalidWeights_Rejected()
        {
            var cfg = OPMDisplacementConfiguration.CreateDefault();
            cfg.weightLayer = 0.5;
            Assert.IsFalse(cfg.IsValid());
            cfg = OPMDisplacementConfiguration.CreateDefault();
            cfg.weightLayer = double.NaN;
            Assert.IsFalse(cfg.IsValid());
            cfg = OPMDisplacementConfiguration.CreateDefault();
            cfg.weightLayer = double.PositiveInfinity;
            Assert.IsFalse(cfg.IsValid());
            cfg = OPMDisplacementConfiguration.CreateDefault();
            cfg.weightLayer = -0.1;
            Assert.IsFalse(cfg.IsValid());
        }

        [Test]
        public void DisplacementConfig_InvalidScales_Rejected()
        {
            var cfg = OPMDisplacementConfiguration.CreateDefault();
            cfg.characteristicRadialScale = 0;
            Assert.IsFalse(cfg.IsValid());
            cfg = OPMDisplacementConfiguration.CreateDefault();
            cfg.characteristicAngularScale = -1;
            Assert.IsFalse(cfg.IsValid());
            cfg = OPMDisplacementConfiguration.CreateDefault();
            cfg.characteristicSpatialScale = double.NaN;
            Assert.IsFalse(cfg.IsValid());
            cfg = OPMDisplacementConfiguration.CreateDefault();
            cfg.characteristicRadialScale = double.PositiveInfinity;
            Assert.IsFalse(cfg.IsValid());
        }

        [Test]
        public void OPMConfig_Defaults_Valid()
        {
            var cfg = Cfg();
            Assert.IsTrue(cfg.IsValid());
            Assert.AreEqual(0.25, cfg.weightLayer, 1e-12);
            Assert.AreEqual(100.0, cfg.characteristicRadialScale, 1e-12);
            Assert.AreEqual(1000.0, cfg.characteristicSpatialScale, 1e-12);
        }

        [Test]
        public void OPMConfig_NanInf_Weights_Rejected()
        {
            var cfg = Cfg();
            cfg.weightLayer = double.NaN;
            Assert.IsFalse(cfg.IsValid());
            cfg = Cfg();
            cfg.weightSpatial = double.PositiveInfinity;
            Assert.IsFalse(cfg.IsValid());
        }

        [Test]
        public void OPMConfig_NanInf_Scales_Rejected()
        {
            var cfg = Cfg();
            cfg.characteristicRadialScale = double.NaN;
            Assert.IsFalse(cfg.IsValid());
            cfg = Cfg();
            cfg.characteristicSpatialScale = double.PositiveInfinity;
            Assert.IsFalse(cfg.IsValid());
        }

        [Test]
        public void Displacement_Identical_Zero()
        {
            var cfg = Cfg();
            var c = new OmniversalCoordinates(1, 0, 0, 0, 0, Vector3.zero);
            var d = PortalDisplacementCalculator.Calculate(c, c, cfg);
            Assert.AreEqual(0.0, d.TotalDisplacement, 1e-12);
            Assert.AreEqual(0.0, d.LayerDisplacement, 1e-12);
            Assert.AreEqual(0.0, d.RingDisplacement, 1e-12);
            Assert.AreEqual(0.0, d.AuDisplacement, 1e-12);
            Assert.AreEqual(0.0, d.AngularDisplacement, 1e-12);
            Assert.AreEqual(0.0, d.RadialDisplacement, 1e-12);
            Assert.AreEqual(0.0, d.SpatialDisplacement, 1e-12);
        }

        [Test]
        public void Displacement_Different_NonNegative()
        {
            var cfg = Cfg();
            var a = new OmniversalCoordinates(1, 0, 0, 0, 0, Vector3.zero);
            var b = new OmniversalCoordinates(2, 1, 5, 10, 1, Vector3.one);
            var d = PortalDisplacementCalculator.Calculate(a, b, cfg);
            Assert.GreaterOrEqual(d.TotalDisplacement, 0.0);
            Assert.IsTrue(double.IsFinite(d.TotalDisplacement));
        }

        [Test]
        public void Displacement_AuWraparound()
        {
            var cfg = Cfg();
            var a = new OmniversalCoordinates(1, 0, 1, 0, 0, Vector3.zero);
            var b = new OmniversalCoordinates(1, 0, 179, 0, 0, Vector3.zero);
            var d = PortalDisplacementCalculator.Calculate(a, b, cfg);
            Assert.AreEqual(2.0, d.AuDisplacement, 1e-9);
        }

        [Test]
        public void Displacement_AngularWraparound()
        {
            Assert.AreEqual(2.0, PortalDisplacementCalculator.AngularDistance(1.0, 359.0), 1e-9);
        }

        [Test]
        public void Displacement_MaxNormalized()
        {
            var cfg = Cfg();
            var a = new OmniversalCoordinates(1, 0, 0, 0, 0, Vector3.zero);
            var b = new OmniversalCoordinates(50, 9, 179, 0, 0, Vector3.zero);
            var d = PortalDisplacementCalculator.Calculate(a, b, cfg);
            Assert.LessOrEqual(d.TotalDisplacement, 1.0 + 1e-9);
        }

        [Test]
        public void Displacement_Immutable_Equality()
        {
            var a = new PortalDisplacement(1, 2, 3, 4, 5, 6, 7);
            var b = new PortalDisplacement(1, 2, 3, 4, 5, 6, 7);
            Assert.IsTrue(a == b);
            Assert.IsTrue(a.Equals(b));
            Assert.AreEqual(a.GetHashCode(), b.GetHashCode());
        }

        [Test]
        public void Displacement_InvalidConfig_Throws()
        {
            var cfg = Cfg();
            cfg.weightLayer = 0.5;
            var c = new OmniversalCoordinates(1, 0, 0, 0, 0, Vector3.zero);
            Assert.Throws<InvalidOperationException>(() =>
                PortalDisplacementCalculator.Calculate(c, c, cfg));
        }

        [Test]
        public void Displacement_TryCalculate_InvalidConfig_False()
        {
            var cfg = Cfg();
            cfg.weightLayer = 0.5;
            var c = new OmniversalCoordinates(1, 0, 0, 0, 0, Vector3.zero);
            bool ok = PortalDisplacementCalculator.TryCalculate(c, c, cfg, out var r, out var err);
            Assert.IsFalse(ok);
            Assert.IsNotEmpty(err);
        }

        [Test]
        public void StructuralDivergence_Boundaries()
        {
            var cfg = Cfg();
            var a = new OmniversalCoordinates(1, 0, 0, 0, 0, Vector3.zero);
            var b = new OmniversalCoordinates(50, 9, 179, 0, 0, Vector3.zero);
            double vd = PortalStructuralAnalyzer.CalculateStructuralDivergence(a, b, cfg);
            Assert.GreaterOrEqual(vd, 0.0);
            Assert.LessOrEqual(vd, 1.0);
            // identical -> 0
            double vd0 = PortalStructuralAnalyzer.CalculateStructuralDivergence(a, a, cfg);
            Assert.AreEqual(0.0, vd0, 1e-12);
        }

        [Test]
        public void StructuralDivergence_Compatibility()
        {
            var cfg = Cfg();
            var a = new OmniversalCoordinates(1, 0, 0, 0, 0, Vector3.zero);
            var b = new OmniversalCoordinates(2, 1, 5, 0, 0, Vector3.zero);
            double vd = PortalStructuralAnalyzer.CalculateStructuralDivergence(a, b, cfg);
            double cp = PortalStabilityCalculator.CalculateCompatibility(vd);
            Assert.AreEqual(1.0 - vd, cp, 1e-12);
        }

        [Test]
        public void StructuralDivergence_Result_Immutable()
        {
            var cfg = Cfg();
            var a = new OmniversalCoordinates(1, 0, 0, 0, 0, Vector3.zero);
            var b = new OmniversalCoordinates(2, 1, 5, 0, 0, Vector3.zero);
            var r = PortalStructuralAnalyzer.Calculate(a, b, cfg);
            Assert.GreaterOrEqual(r.TotalDivergence, 0.0);
            Assert.LessOrEqual(r.TotalDivergence, 1.0);
            Assert.AreEqual(1.0 - r.TotalDivergence, r.Compatibility, 1e-12);
        }

        [Test]
        public void StructuralDivergence_InvalidConfig_Throws()
        {
            var cfg = Cfg();
            cfg.structuralWeightLayer = 0.5; // break sum
            var a = new OmniversalCoordinates(1, 0, 0, 0, 0, Vector3.zero);
            Assert.Throws<InvalidOperationException>(() =>
                PortalStructuralAnalyzer.CalculateStructuralDivergence(a, a, cfg));
        }

        [Test]
        public void DisplacementConfig_FromOPMConfig()
        {
            var cfg = Cfg();
            var dcfg = OPMDisplacementConfiguration.FromOPMConfiguration(cfg);
            Assert.IsTrue(dcfg.IsValid());
            Assert.AreEqual(cfg.weightLayer, dcfg.weightLayer, 1e-12);
        }

        [Test]
        public void Displacement_WithDisplacementConfig()
        {
            var dcfg = OPMDisplacementConfiguration.CreateDefault();
            var a = new OmniversalCoordinates(1, 0, 0, 0, 0, Vector3.zero);
            var b = new OmniversalCoordinates(2, 0, 0, 0, 0, Vector3.zero);
            var d = PortalDisplacementCalculator.Calculate(a, b, dcfg);
            Assert.Greater(d.TotalDisplacement, 0.0);
        }

        [Test]
        public void StructuralDivergence_TryCalculate()
        {
            var cfg = Cfg();
            var a = new OmniversalCoordinates(1, 0, 0, 0, 0, Vector3.zero);
            bool ok = PortalStructuralAnalyzer.TryCalculateStructuralDivergence(a, a, cfg, out double vd, out string err);
            Assert.IsTrue(ok);
            Assert.AreEqual(0.0, vd, 1e-12);
            Assert.IsEmpty(err);
        }
    }
}
