using System;
using NUnit.Framework;
using UnityEngine;
using Mechanics;

namespace Mechanics.Tests.EditMode
{
    public class OmniversalCoordinatesTests
    {
        [Test]
        public void ValidConstruction()
        {
            var c = new OmniversalCoordinates(3, 2, 44, 10.0, 5.0, Vector3.zero);
            Assert.IsTrue(c.IsValid());
            Assert.AreEqual(3, c.Layer);
            Assert.AreEqual(2, c.Ring);
            Assert.AreEqual(44, c.AuPosition);
            Assert.AreEqual("L03-R02-P044", c.ToString());
        }

        [Test]
        public void InvalidLayerRejected()
        {
            Assert.IsFalse(new OmniversalCoordinates(0, 2, 44, 10.0, 5.0, Vector3.zero).IsValid());
            Assert.IsFalse(new OmniversalCoordinates(51, 2, 44, 10.0, 5.0, Vector3.zero).IsValid());
        }

        [Test]
        public void InvalidRingRejected()
        {
            Assert.IsFalse(new OmniversalCoordinates(1, -1, 44, 10.0, 5.0, Vector3.zero).IsValid());
            Assert.IsFalse(new OmniversalCoordinates(1, 10, 44, 10.0, 5.0, Vector3.zero).IsValid());
        }

        [Test]
        public void InvalidAuRejected()
        {
            Assert.IsFalse(new OmniversalCoordinates(1, 0, -1, 10.0, 5.0, Vector3.zero).IsValid());
            Assert.IsFalse(new OmniversalCoordinates(1, 0, 180, 10.0, 5.0, Vector3.zero).IsValid());
        }

        [Test]
        public void InvalidAngularRejected()
        {
            Assert.IsFalse(new OmniversalCoordinates(1, 0, 1, -1.0, 5.0, Vector3.zero).IsValid());
            Assert.IsFalse(new OmniversalCoordinates(1, 0, 1, 360.0, 5.0, Vector3.zero).IsValid());
            Assert.IsFalse(new OmniversalCoordinates(1, 0, 1, double.NaN, 5.0, Vector3.zero).IsValid());
        }

        [Test]
        public void InvalidRadialRejected()
        {
            Assert.IsFalse(new OmniversalCoordinates(1, 0, 1, 10.0, -1.0, Vector3.zero).IsValid());
        }

        [Test]
        public void InvalidLocalRejected()
        {
            Assert.IsFalse(new OmniversalCoordinates(1, 0, 1, 10.0, 5.0, new Vector3(float.NaN, 0, 0)).IsValid());
            Assert.IsFalse(new OmniversalCoordinates(1, 0, 1, 10.0, 5.0, new Vector3(float.PositiveInfinity, 0, 0)).IsValid());
        }

        [Test]
        public void PrimeAuValid()
        {
            var c = new OmniversalCoordinates(1, 0, 0, 0, 0, Vector3.zero, true);
            Assert.IsTrue(c.IsPrimeAu);
            Assert.IsTrue(c.IsValid());
            Assert.IsTrue(c.ToString().Contains("PRIME"));
        }

        [Test]
        public void CircularAuDistance_Wraparound()
        {
            Assert.AreEqual(2, OmniversalCoordinates.CircularAuDistance(1, 179));
            Assert.AreEqual(1, OmniversalCoordinates.CircularAuDistance(0, 1));
            Assert.AreEqual(90, OmniversalCoordinates.CircularAuDistance(0, 90));
        }

        [Test]
        public void AuWraparound_Instance()
        {
            var a = new OmniversalCoordinates(1, 0, 1, 0, 0, Vector3.zero);
            var b = new OmniversalCoordinates(1, 0, 179, 0, 0, Vector3.zero);
            Assert.AreEqual(2, a.GetCircularAuDistance(b));
            Assert.AreEqual(2, a.CircularAuDistance(b));
        }

        [Test]
        public void Equality()
        {
            var a = new OmniversalCoordinates(2, 3, 5, 10, 1, Vector3.one);
            var b = new OmniversalCoordinates(2, 3, 5, 10, 1, Vector3.one);
            Assert.IsTrue(a == b);
            Assert.AreEqual(a.GetHashCode(), b.GetHashCode());
        }

        [Test]
        public void LocalSpatialCoordinates()
        {
            var c = new OmniversalCoordinates(1, 0, 0, 0, 0, 3, 4, 0);
            Assert.AreEqual(new Vector3(3, 4, 0), c.LocalSpatialCoordinates);
            Assert.AreEqual(3.0, c.X, 1e-6);
            Assert.AreEqual(4.0, c.Y, 1e-6);
        }
    }
}
