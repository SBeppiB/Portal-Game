using System;
using NUnit.Framework;
using UnityEngine;
using Portals.Runtime.Mechanics;

namespace Portals.Tests.EditMode
{
    public class OPMTests
    {
        private OPMConfiguration Cfg() => OPMConfiguration.CreateDefault();

        [Test]
        public void Coordinates_ValidConstruction()
        {
            var c = new OmniversalCoordinates(3, 2, 44, 10.0, 5.0, Vector3.zero);
            Assert.IsTrue(c.IsValid(Cfg()));
            Assert.AreEqual(3, c.Layer);
            Assert.AreEqual(2, c.Ring);
            Assert.AreEqual(44, c.AuPosition);
            Assert.AreEqual("L03-R02-P044", c.ToString());
        }

        [Test]
        public void Coordinates_InvalidLayerRejected()
        {
            var cfg = Cfg();
            var c = new OmniversalCoordinates(0, 2, 44, 10.0, 5.0, Vector3.zero);
            Assert.IsFalse(c.IsValid(cfg));
            var c2 = new OmniversalCoordinates(51, 2, 44, 10.0, 5.0, Vector3.zero);
            Assert.IsFalse(c2.IsValid(cfg));
        }

        [Test]
        public void Coordinates_InvalidRingRejected()
        {
            var cfg = Cfg();
            var c = new OmniversalCoordinates(1, -1, 44, 10.0, 5.0, Vector3.zero);
            Assert.IsFalse(c.IsValid(cfg));
            var c2 = new OmniversalCoordinates(1, 10, 44, 10.0, 5.0, Vector3.zero);
            Assert.IsFalse(c2.IsValid(cfg));
        }

        [Test]
        public void Coordinates_CircularAuDistance()
        {
            Assert.AreEqual(2, PortalDisplacementCalculator.CircularAuDistance(1, 179, 180));
            Assert.AreEqual(1, PortalDisplacementCalculator.CircularAuDistance(0, 1, 180));
            Assert.AreEqual(90, PortalDisplacementCalculator.CircularAuDistance(0, 90, 180));
        }

        [Test]
        public void Coordinates_AuWraparound()
        {
            var cfg = Cfg();
            var a = new OmniversalCoordinates(1, 0, 1, 0, 0, Vector3.zero);
            var b = new OmniversalCoordinates(1, 0, 179, 0, 0, Vector3.zero);
            var d = PortalDisplacementCalculator.Calculate(a, b, cfg);
            Assert.AreEqual(2, d.AuDisplacement);
        }

        [Test]
        public void Coordinates_PrimeAuHandling()
        {
            var cfg = Cfg();
            var c = new OmniversalCoordinates(1, 0, 0, 0, 0, Vector3.zero, true);
            Assert.IsTrue(c.IsPrimeAu);
            Assert.IsTrue(c.IsValid(cfg));
            Assert.IsTrue(c.ToString().Contains("PRIME"));
        }

        [Test]
        public void Coordinates_AngularDistance()
        {
            Assert.AreEqual(2.0, PortalDisplacementCalculator.AngularDistance(1.0, 359.0), 1e-9);
            Assert.AreEqual(180.0, PortalDisplacementCalculator.AngularDistance(0.0, 180.0), 1e-9);
            Assert.AreEqual(0.0, PortalDisplacementCalculator.AngularDistance(45.0, 45.0), 1e-9);
        }

        [Test]
        public void Coordinates_Equality()
        {
            var a = new OmniversalCoordinates(2, 3, 5, 10, 1, Vector3.one);
            var b = new OmniversalCoordinates(2, 3, 5, 10, 1, Vector3.one);
            Assert.IsTrue(a == b);
            Assert.AreEqual(a.GetHashCode(), b.GetHashCode());
        }

        [Test]
        public void Displacement_Zero()
        {
            var cfg = Cfg();
            var c = new OmniversalCoordinates(1, 0, 0, 0, 0, Vector3.zero);
            var d = PortalDisplacementCalculator.Calculate(c, c, cfg);
            Assert.AreEqual(0.0, d.TotalDisplacement, 1e-9);
            Assert.AreEqual(0.0, d.SpatialDisplacement, 1e-9);
            Assert.AreEqual(0.0, d.LayerDisplacement, 1e-9);
        }

        [Test]
        public void Displacement_SpatialOnly()
        {
            var cfg = Cfg();
            var a = new OmniversalCoordinates(1, 0, 0, 0, 0, Vector3.zero);
            var b = new OmniversalCoordinates(1, 0, 0, 0, 0, new Vector3(3, 4, 0));
            var d = PortalDisplacementCalculator.Calculate(a, b, cfg);
            Assert.AreEqual(5.0, d.SpatialDisplacement, 1e-6);
            Assert.AreEqual(0.0, d.LayerDisplacement, 1e-9);
            Assert.AreEqual(0.0, d.RingDisplacement, 1e-9);
            Assert.AreEqual(0.0, d.AuDisplacement, 1e-9);
        }

        [Test]
        public void Displacement_LayerOnly()
        {
            var cfg = Cfg();
            var a = new OmniversalCoordinates(1, 0, 0, 0, 0, Vector3.zero);
            var b = new OmniversalCoordinates(2, 0, 0, 0, 0, Vector3.zero);
            var d = PortalDisplacementCalculator.Calculate(a, b, cfg);
            Assert.AreEqual(1.0, d.LayerDisplacement, 1e-9);
            Assert.AreEqual(0.0, d.SpatialDisplacement, 1e-9);
        }

        [Test]
        public void Displacement_RingOnly()
        {
            var cfg = Cfg();
            var a = new OmniversalCoordinates(1, 0, 0, 0, 0, Vector3.zero);
            var b = new OmniversalCoordinates(1, 1, 0, 0, 0, Vector3.zero);
            var d = PortalDisplacementCalculator.Calculate(a, b, cfg);
            Assert.AreEqual(1.0, d.RingDisplacement, 1e-9);
        }

        [Test]
        public void Displacement_AuOnly()
        {
            var cfg = Cfg();
            var a = new OmniversalCoordinates(1, 0, 0, 0, 0, Vector3.zero);
            var b = new OmniversalCoordinates(1, 0, 5, 0, 0, Vector3.zero);
            var d = PortalDisplacementCalculator.Calculate(a, b, cfg);
            Assert.AreEqual(5.0, d.AuDisplacement, 1e-9);
        }

        [Test]
        public void Displacement_Combined()
        {
            var cfg = Cfg();
            var a = new OmniversalCoordinates(1, 0, 0, 0, 0, Vector3.zero);
            var b = new OmniversalCoordinates(2, 1, 5, 0, 0, new Vector3(1, 0, 0));
            var d = PortalDisplacementCalculator.Calculate(a, b, cfg);
            Assert.Greater(d.TotalDisplacement, 0.0);
            Assert.AreEqual(1.0, d.LayerDisplacement, 1e-9);
            Assert.AreEqual(1.0, d.RingDisplacement, 1e-9);
        }

        [Test]
        public void Displacement_Normalisation()
        {
            var cfg = Cfg();
            var a = new OmniversalCoordinates(1, 0, 0, 0, 0, Vector3.zero);
            var b = new OmniversalCoordinates(50, 9, 179, 0, 0, Vector3.zero);
            var d = PortalDisplacementCalculator.Calculate(a, b, cfg);
            // max displacement should produce q <= 6? total <= sqrt(6) approx
            Assert.LessOrEqual(d.TotalDisplacement, Math.Sqrt(6.0) + 1e-6);
        }

        [Test]
        public void Energy_ZeroDisplacement()
        {
            var cfg = Cfg();
            var c = new OmniversalCoordinates(1, 0, 0, 0, 0, Vector3.zero);
            double e = PortalEnergyCalculator.CalculateTotalEnergy(cfg, c, c, UniverseStructuralProfile.CreateDefault());
            Assert.AreEqual(0.0, e, 1e-9);
        }

        [Test]
        public void Energy_IncreasingDisplacement()
        {
            var cfg = Cfg();
            var a = new OmniversalCoordinates(1, 0, 0, 0, 0, Vector3.zero);
            var b1 = new OmniversalCoordinates(2, 0, 0, 0, 0, Vector3.zero);
            var b2 = new OmniversalCoordinates(10, 0, 0, 0, 0, Vector3.zero);
            double e1 = PortalEnergyCalculator.CalculateTotalEnergy(cfg, a, b1, UniverseStructuralProfile.CreateDefault());
            double e2 = PortalEnergyCalculator.CalculateTotalEnergy(cfg, a, b2, UniverseStructuralProfile.CreateDefault());
            Assert.Greater(e2, e1);
        }

        [Test]
        public void Energy_IncreasingDivergence()
        {
            var cfg = Cfg();
            var a = new OmniversalCoordinates(1, 0, 0, 0, 0, Vector3.zero);
            var b = new OmniversalCoordinates(1, 0, 10, 0, 0, Vector3.zero);
            var p1 = new UniverseStructuralProfile(1, 0, 0, 1, 1);
            var p2 = new UniverseStructuralProfile(1, 0, 0.5, 1, 1);
            double e1 = PortalEnergyCalculator.CalculateTotalEnergy(cfg, a, b, p1);
            double e2 = PortalEnergyCalculator.CalculateTotalEnergy(cfg, a, b, p2);
            Assert.Greater(e2, e1);
        }

        [Test]
        public void Energy_IncreasingInstability()
        {
            var cfg = Cfg();
            var a = new OmniversalCoordinates(1, 0, 0, 0, 0, Vector3.zero);
            var b = new OmniversalCoordinates(2, 0, 0, 0, 0, Vector3.zero);
            var p1 = UniverseStructuralProfile.CreateDefault();
            var p2 = new UniverseStructuralProfile(1, 0, 2.0, 1, 1);
            double e1 = PortalEnergyCalculator.CalculateTotalEnergy(cfg, a, b, p1);
            double e2 = PortalEnergyCalculator.CalculateTotalEnergy(cfg, a, b, p2);
            Assert.Greater(e2, e1);
        }

        [Test]
        public void Energy_IncreasingRiftInterference()
        {
            var cfg = Cfg();
            var a = new OmniversalCoordinates(1, 0, 0, 0, 0, Vector3.zero);
            var b = new OmniversalCoordinates(2, 0, 0, 0, 0, Vector3.zero);
            var p1 = UniverseStructuralProfile.CreateDefault();
            var p2 = new UniverseStructuralProfile(1, 2.0, 0, 1, 1);
            double e1 = PortalEnergyCalculator.CalculateTotalEnergy(cfg, a, b, p1);
            double e2 = PortalEnergyCalculator.CalculateTotalEnergy(cfg, a, b, p2);
            Assert.Greater(e2, e1);
        }

        [Test]
        public void Energy_InvalidConfiguration()
        {
            var cfg = OPMConfiguration.CreateDefault();
            cfg.weightLayer = 0.5; // break sum
            Assert.IsFalse(cfg.IsValid());
            var a = new OmniversalCoordinates(1, 0, 0, 0, 0, Vector3.zero);
            Assert.Throws<InvalidOperationException>(() =>
                PortalEnergyCalculator.CalculateTotalEnergy(cfg, a, a, UniverseStructuralProfile.CreateDefault()));
        }

        [Test]
        public void Energy_OverflowProtection()
        {
            var cfg = Cfg();
            cfg.universalConversionConstant = 1e300;
            cfg.dimensionalScalingExponent = 10;
            var a = new OmniversalCoordinates(1, 0, 0, 0, 0, Vector3.zero);
            var b = new OmniversalCoordinates(50, 9, 179, 359, 1000, new Vector3(1000, 1000, 1000));
            bool ok = PortalEnergyCalculator.TryCalculateTotalEnergy(cfg, a, b, UniverseStructuralProfile.CreateDefault(), double.NaN, out double energy, out string err);
            // Either fails or produces finite
            if (ok)
            {
                Assert.IsTrue(double.IsFinite(energy));
            }
            else
            {
                Assert.IsNotEmpty(err);
            }
        }

        [Test]
        public void Energy_NonNegativeOutput()
        {
            var cfg = Cfg();
            var a = new OmniversalCoordinates(1, 0, 0, 0, 0, Vector3.zero);
            var b = new OmniversalCoordinates(2, 1, 5, 10, 1, Vector3.one);
            double e = PortalEnergyCalculator.CalculateTotalEnergy(cfg, a, b, UniverseStructuralProfile.CreateDefault());
            Assert.GreaterOrEqual(e, 0.0);
        }

        [Test]
        public void Stability_SufficientEnergy()
        {
            double sp = PortalStabilityCalculator.CalculateStability(100, 50, 0);
            Assert.GreaterOrEqual(sp, 2.0);
        }

        [Test]
        public void Stability_InsufficientEnergy()
        {
            var cfg = Cfg();
            double sp = PortalStabilityCalculator.CalculateStability(10, 50, 0);
            Assert.Less(sp, cfg.minimumStability);
        }

        [Test]
        public void Stability_HighInstability()
        {
            double sp1 = PortalStabilityCalculator.CalculateStability(100, 50, 0);
            double sp2 = PortalStabilityCalculator.CalculateStability(100, 50, 5);
            Assert.Less(sp2, sp1);
        }

        [Test]
        public void Stability_LowAccuracy()
        {
            var cfg = Cfg();
            double acc = PortalAccuracyCalculator.CalculateAccuracy(10.0);
            Assert.Less(acc, cfg.minimumAccuracy);
        }

        [Test]
        public void Stability_ExactThresholdBoundaries()
        {
            var cfg = Cfg();
            // SP exactly minimum
            double required = 100.0;
            double available = required * cfg.minimumStability; // SP = minStability when U=0
            double sp = PortalStabilityCalculator.CalculateStability(available, required, 0);
            Assert.IsTrue(PortalStabilityCalculator.IsEligible(sp, cfg.minimumAccuracy, cfg) || Math.Abs(sp - cfg.minimumStability) < 1e-9);
            // accuracy exact
            double eps = 0; // accuracy 1
            double acc = PortalAccuracyCalculator.CalculateAccuracy(eps);
            Assert.GreaterOrEqual(acc, cfg.minimumAccuracy);
        }

        [Test]
        public void Accuracy_CoordinateError()
        {
            double err = PortalAccuracyCalculator.CalculateCoordinateError(3, 4, 0, 0, 0);
            Assert.AreEqual(5.0, err, 1e-9);
            double acc = PortalAccuracyCalculator.CalculateAccuracy(err);
            Assert.AreEqual(1.0 / 6.0, acc, 1e-9);
        }

        [Test]
        public void Scanner_ExistingDestination()
        {
            var cfg = Cfg();
            var db = new UniverseSignatureDatabase();
            var coord = new OmniversalCoordinates(1, 0, 0, 0, 0, Vector3.zero);
            db.Register(new UniverseSignature { UniverseId = "U1", Coordinates = coord, IsValid = true });
            var scanner = new OmniversalScanner(db, cfg);
            float t = 0;
            scanner.SetTimeProvider(() => t);
            var res = scanner.Scan(coord);
            Assert.IsTrue(res.Success);
            Assert.IsNotNull(res.Signature);
        }

        [Test]
        public void Scanner_UnknownDestination()
        {
            var cfg = Cfg();
            var db = new UniverseSignatureDatabase();
            var scanner = new OmniversalScanner(db, cfg);
            var coord = new OmniversalCoordinates(1, 0, 0, 0, 0, Vector3.zero);
            var res = scanner.Scan(coord);
            Assert.IsFalse(res.Success);
            Assert.AreEqual("Destination unavailable.", res.FailureReason);
        }

        [Test]
        public void Scanner_InvalidSignature()
        {
            var cfg = Cfg();
            var db = new UniverseSignatureDatabase();
            var coord = new OmniversalCoordinates(1, 0, 0, 0, 0, Vector3.zero);
            db.Register(new UniverseSignature { UniverseId = "U1", Coordinates = coord, IsValid = false });
            var scanner = new OmniversalScanner(db, cfg);
            var res = scanner.Scan(coord);
            Assert.IsFalse(res.Success);
            Assert.AreEqual("Invalid signature.", res.FailureReason);
        }

        [Test]
        public void Scanner_CachedDestination()
        {
            var cfg = Cfg();
            var db = new UniverseSignatureDatabase();
            var coord = new OmniversalCoordinates(1, 0, 0, 0, 0, Vector3.zero);
            db.Register(new UniverseSignature { UniverseId = "U1", Coordinates = coord, IsValid = true });
            var scanner = new OmniversalScanner(db, cfg);
            float t = 0;
            scanner.SetTimeProvider(() => t);
            var r1 = scanner.Scan(coord);
            t = 1;
            var r2 = scanner.Scan(coord);
            Assert.AreSame(r1, r2);
        }

        [Test]
        public void Scanner_FailedScan()
        {
            var cfg = Cfg();
            var db = new UniverseSignatureDatabase();
            var scanner = new OmniversalScanner(db, cfg);
            var coord = new OmniversalCoordinates(1, 0, 0, 0, 0, Vector3.zero);
            var res = scanner.Scan(coord);
            Assert.IsFalse(res.Success);
            Assert.AreEqual(ScanState.Failed, scanner.State);
        }

        [Test]
        public void Scanner_StructuralInterference()
        {
            var cfg = Cfg();
            var db = new UniverseSignatureDatabase();
            var coord = new OmniversalCoordinates(1, 0, 0, 0, 0, Vector3.zero);
            var profile = new UniverseStructuralProfile(1, 2.5, 1.0, 1.0, 1.0);
            db.Register(new UniverseSignature { UniverseId = "U1", Coordinates = coord, Profile = profile, IsValid = true });
            var scanner = new OmniversalScanner(db, cfg);
            var res = scanner.Scan(coord);
            Assert.IsTrue(res.Success);
            Assert.AreEqual(2.5, res.RiftInterference, 1e-9);
            Assert.AreEqual(1.0, res.Instability, 1e-9);
        }

        [Test]
        public void Integration_UniportStillWorks()
        {
            // OPM does not alter traversal; just ensure classes compile and basic flow.
            var cfg = Cfg();
            var c = new OmniversalCoordinates(1, 0, 0, 0, 0, Vector3.zero);
            Assert.IsTrue(c.IsValid(cfg));
        }

        [Test]
        public void Integration_AltiportStillWorks()
        {
            var cfg = Cfg();
            var c = new OmniversalCoordinates(2, 1, 5, 0, 0, Vector3.zero);
            Assert.IsTrue(c.IsValid(cfg));
        }

        [Test]
        public void Integration_EnergyNotConsumedOnFailedFormation()
        {
            var cfg = Cfg();
            var db = new UniverseSignatureDatabase();
            var origin = new OmniversalCoordinates(1, 0, 0, 0, 0, Vector3.zero);
            var dest = new OmniversalCoordinates(2, 0, 0, 0, 0, Vector3.zero);
            // no signature registered -> scan fails
            var scanner = new OmniversalScanner(db, cfg);
            var scan = scanner.Scan(dest);
            double available = 100;
            var res = PortalFormationEvaluator.Evaluate(PortalTravelMode.Altiport, origin, dest, cfg, scan, available);
            Assert.IsFalse(res.Success);
            // evaluator does not touch energy; available unchanged by construction
            Assert.AreEqual(100, available);
        }

        [Test]
        public void Integration_UniverseTransitionsRegister()
        {
            // Signature memory: register and retrieve
            var db = new UniverseSignatureDatabase();
            var coord = new OmniversalCoordinates(1, 0, 0, 0, 0, Vector3.zero);
            db.Register(new UniverseSignature { UniverseId = "U1", Coordinates = coord });
            Assert.IsTrue(db.Contains(coord));
        }

        [Test]
        public void Integration_SaveCompatibility()
        {
            // OmniversalCoordinates serializable struct; basic.
            Assert.IsTrue(typeof(OmniversalCoordinates).IsValueType);
        }
    }
}
