using System;
using NUnit.Framework;
using UnityEngine;
using Portals.Runtime.Mechanics;

namespace Portals.Tests.EditMode
{
    public class OPMDisplacementTests
    {
        private OPMDisplacementConfiguration Cfg() => OPMDisplacementConfiguration.CreateDefault();

        [Test]
        public void DisplacementConfig_DefaultValid()
        {
            var cfg = Cfg();
            Assert.IsTrue(cfg.IsValid());
        }

        [Test]
        public void DisplacementConfig_InvalidWeightNaN()
        {
            var cfg = Cfg();
            cfg.weightLayer = double.NaN;
            Assert.IsFalse(cfg.IsValid());
        }

        [Test]
        public void DisplacementConfig_InvalidWeightInfinity()
        {
            var cfg = Cfg();
            cfg.weightRing = double.PositiveInfinity;
            Assert.IsFalse(cfg.IsValid());
        }

        [Test]
        public void DisplacementConfig_NegativeWeight()
        {
            var cfg = Cfg();
            cfg.weightAu = -0.1;
            // adjust sum back to 1
            cfg.weightSpatial = 0.25;
            Assert.IsFalse(cfg.IsValid());
        }

        [Test]
        public void DisplacementConfig_WeightsDoNotSumToOne()
        {
            var cfg = Cfg();
            cfg.weightLayer = 0.5;
            Assert.IsFalse(cfg.IsValid());
        }

        [Test]
        public void DisplacementConfig_InvalidScaleZero()
        {
            var cfg = Cfg();
            cfg.characteristicRadialScale = 0;
            Assert.IsFalse(cfg.IsValid());
        }

        [Test]
        public void DisplacementConfig_InvalidScaleNaN()
        {
            var cfg = Cfg();
            cfg.characteristicAngularScale = double.NaN;
            Assert.IsFalse(cfg.IsValid());
        }

        [Test]
        public void DisplacementConfig_TryCalculateRejectsInvalidConfig()
        {
            var cfg = Cfg();
            cfg.weightLayer = 0.5; // break sum
            var a = new OmniversalCoordinates(1, 0, 0, 0, 0, Vector3.zero);
            var b = new OmniversalCoordinates(2, 0, 0, 0, 0, Vector3.zero);
            bool ok = PortalDisplacementCalculator.TryCalculate(a, b, cfg, out _, out string err);
            Assert.IsFalse(ok);
            Assert.IsNotEmpty(err);
        }

        [Test]
        public void Displacement_Zero_IdenticalCoordinates()
        {
            var cfg = Cfg();
            var c = new OmniversalCoordinates(1, 0, 0, 0, 0, Vector3.zero);
            var d = PortalDisplacementCalculator.Calculate(c, c, cfg);
            Assert.AreEqual(0.0, d.TotalDisplacement, 1e-9);
            Assert.AreEqual(0.0, d.LayerDisplacement, 1e-9);
            Assert.AreEqual(0.0, d.RingDisplacement, 1e-9);
            Assert.AreEqual(0.0, d.AuDisplacement, 1e-9);
            Assert.AreEqual(0.0, d.AngularDisplacement, 1e-9);
            Assert.AreEqual(0.0, d.RadialDisplacement, 1e-9);
            Assert.AreEqual(0.0, d.SpatialDisplacement, 1e-9);
        }

        [Test]
        public void Displacement_Nonnegative()
        {
            var cfg = Cfg();
            var a = new OmniversalCoordinates(1, 0, 0, 0, 0, Vector3.zero);
            var b = new OmniversalCoordinates(2, 1, 5, 10, 1, Vector3.one);
            var d = PortalDisplacementCalculator.Calculate(a, b, cfg);
            Assert.GreaterOrEqual(d.TotalDisplacement, 0.0);
            Assert.GreaterOrEqual(d.LayerDisplacement, 0.0);
            Assert.GreaterOrEqual(d.SpatialDisplacement, 0.0);
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
        public void Displacement_MaxNormalizedDifferences()
        {
            var cfg = Cfg();
            var a = new OmniversalCoordinates(1, 0, 0, 0, 0, Vector3.zero);
            var b = new OmniversalCoordinates(50, 9, 90, 180, 100, new Vector3(1000, 0, 0));
            var d = PortalDisplacementCalculator.Calculate(a, b, cfg);
            Assert.AreEqual(1.0, d.TotalDisplacement, 1e-6);
        }

        [Test]
        public void Displacement_EqualityAndImmutability()
        {
            var cfg = Cfg();
            var a = new OmniversalCoordinates(1, 0, 0, 0, 0, Vector3.zero);
            var b = new OmniversalCoordinates(2, 0, 0, 0, 0, Vector3.zero);
            var d1 = PortalDisplacementCalculator.Calculate(a, b, cfg);
            var d2 = PortalDisplacementCalculator.Calculate(a, b, cfg);
            Assert.IsTrue(d1.Equals(d2));
            Assert.IsTrue(d1 == d2);
            // immutability: init-only properties cannot be reassigned; verify value stable
            Assert.AreEqual(d1.TotalDisplacement, d2.TotalDisplacement);
        }

        [Test]
        public void StructuralDivergence_IdenticalZero()
        {
            var cfg = OPMConfiguration.CreateDefault();
            var c = new OmniversalCoordinates(1, 0, 0, 0, 0, Vector3.zero);
            var div = PortalStructuralAnalyzer.Calculate(c, c, cfg);
            Assert.AreEqual(0.0, div.TotalDivergence, 1e-9);
            Assert.AreEqual(1.0, div.Compatibility, 1e-9);
        }

        [Test]
        public void StructuralDivergence_Boundaries()
        {
            var cfg = OPMConfiguration.CreateDefault();
            var a = new OmniversalCoordinates(1, 0, 0, 0, 0, Vector3.zero);
            var b = new OmniversalCoordinates(50, 9, 179, 0, 0, Vector3.zero);
            var div = PortalStructuralAnalyzer.Calculate(a, b, cfg);
            Assert.GreaterOrEqual(div.TotalDivergence, 0.0);
            Assert.LessOrEqual(div.TotalDivergence, 1.0);
            Assert.AreEqual(1.0 - div.TotalDivergence, div.Compatibility, 1e-9);
        }

        [Test]
        public void StructuralDivergence_Compatibility()
        {
            var cfg = OPMConfiguration.CreateDefault();
            var a = new OmniversalCoordinates(1, 0, 0, 0, 0, Vector3.zero);
            var b = new OmniversalCoordinates(2, 1, 5, 0, 0, Vector3.zero);
            var div = PortalStructuralAnalyzer.Calculate(a, b, cfg);
            Assert.AreEqual(1.0 - div.TotalDivergence, div.Compatibility, 1e-9);
            Assert.LessOrEqual(div.Compatibility, 1.0);
            Assert.GreaterOrEqual(div.Compatibility, 0.0);
        }

        [Test]
        public void StructuralDivergence_InvalidConfigThrows()
        {
            var cfg = OPMConfiguration.CreateDefault();
            cfg.structuralWeightLayer = 0.5; // break sum
            var a = new OmniversalCoordinates(1, 0, 0, 0, 0, Vector3.zero);
            var b = new OmniversalCoordinates(2, 0, 0, 0, 0, Vector3.zero);
            Assert.Throws<InvalidOperationException>(() =>
                PortalStructuralAnalyzer.Calculate(a, b, cfg));
        }

        [Test]
        public void StructuralDivergence_TryCalculateInvalidReturnsFalse()
        {
            var cfg = OPMConfiguration.CreateDefault();
            cfg.structuralWeightLayer = 0.5;
            var a = new OmniversalCoordinates(1, 0, 0, 0, 0, Vector3.zero);
            var b = new OmniversalCoordinates(2, 0, 0, 0, 0, Vector3.zero);
            bool ok = PortalStructuralAnalyzer.TryCalculate(a, b, cfg, out _, out string err);
            Assert.IsFalse(ok);
            Assert.IsNotEmpty(err);
        }

        [Test]
        public void StructuralDivergence_Equality()
        {
            var cfg = OPMConfiguration.CreateDefault();
            var a = new OmniversalCoordinates(1, 0, 0, 0, 0, Vector3.zero);
            var b = new OmniversalCoordinates(2, 0, 0, 0, 0, Vector3.zero);
            var d1 = PortalStructuralAnalyzer.Calculate(a, b, cfg);
            var d2 = PortalStructuralAnalyzer.Calculate(a, b, cfg);
            Assert.IsTrue(d1.Equals(d2));
            Assert.IsTrue(d1 == d2);
        }
    }
}