using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Game.Omniverse;

namespace Game.Tests.EditMode
{
    public class OmniverseCoordinateTests
    {
        [Test]
        public void Parse_RoundTrips()
        {
            var c = OmniverseCoordinate.Parse("L12-R04-P067");
            Assert.AreEqual(12, c.Layer);
            Assert.AreEqual(4, c.Ring);
            Assert.AreEqual(67, c.Position);
            Assert.AreEqual("L12-R04-P067", c.ToString());
        }

        [Test]
        public void CircularDistance_Wraparound()
        {
            var a = new OmniverseCoordinate(1, 0, 0);
            var b = new OmniverseCoordinate(1, 0, 179);
            Assert.AreEqual(1, a.GetCircularDistance(b));
        }

        [Test]
        public void Adjacent_WrapsAtBoundary()
        {
            var c = new OmniverseCoordinate(1, 0, 0);
            var adj = c.GetAdjacentUniverses();
            Assert.AreEqual(2, adj.Count);
            Assert.AreEqual(179, adj[0].Position);
            Assert.AreEqual(1, adj[1].Position);
        }

        [Test]
        public void ShortestPath_Wraparound()
        {
            var a = new OmniverseCoordinate(1, 0, 179);
            var b = new OmniverseCoordinate(1, 0, 1);
            var path = a.GetShortestPath(b);
            // shortest way: 179 -> 0 -> 1
            Assert.AreEqual(3, path.Count);
            Assert.AreEqual(179, path[0].Position);
            Assert.AreEqual(0, path[1].Position);
            Assert.AreEqual(1, path[2].Position);
        }

        [Test]
        public void Equality_AndHash()
        {
            var a = new OmniverseCoordinate(2, 3, 5);
            var b = new OmniverseCoordinate(2, 3, 5);
            Assert.IsTrue(a == b);
            Assert.AreEqual(a.GetHashCode(), b.GetHashCode());
        }
    }
}