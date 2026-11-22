using NUnit.Framework;
using Portals.Core;
using UnityEngine;

namespace Portals.Tests
{
    public class PortalDirectionTests
    {
        private static void AssertVectorNear(Vector3 expected, Vector3 actual, float epsilon = 1e-4f)
        {
            Assert.That((expected - actual).sqrMagnitude, Is.LessThan(epsilon * epsilon),
                $"Expected {expected} but got {actual}.");
        }

        [Test]
        public void BackEntryRotation_MapsBackTravelToExitForward()
        {
            Quaternion entrance = Quaternion.Euler(0f, 30f, 0f);
            Quaternion exit = Quaternion.Euler(0f, -70f, 0f);

            // Entering from the back travels along the entrance's local +Z.
            Vector3 outgoing = PortalMath.ComputeFrameRotation(entrance, exit, false) *
                               (entrance * Vector3.forward);

            AssertVectorNear(exit * Vector3.forward, outgoing);
        }

        [Test]
        public void FrontAndBackCrossings_AreBothDetectedAndLabelled()
        {
            Assert.That(PortalMath.TryFindPlaneCrossing(
                    new Vector3(0f, 0f, 1f), new Vector3(0f, 0f, -1f),
                    out Vector3 frontHit, out _, out bool fromBackFront),
                Is.True);
            Assert.That(fromBackFront, Is.False);
            Assert.That(Mathf.Abs(frontHit.z), Is.LessThan(1e-5f));

            Assert.That(PortalMath.TryFindPlaneCrossing(
                    new Vector3(0f, 0f, -1f), new Vector3(0f, 0f, 1f),
                    out Vector3 backHit, out _, out bool fromBackBack),
                Is.True);
            Assert.That(fromBackBack, Is.True);
            Assert.That(Mathf.Abs(backHit.z), Is.LessThan(1e-5f));
        }

        [Test]
        public void BackEntryPreservesSpeed()
        {
            Quaternion entrance = Quaternion.Euler(15f, 40f, -10f);
            Quaternion exit = Quaternion.Euler(-25f, 110f, 20f);
            Vector3 velocity = entrance * Vector3.forward * 12f;

            Vector3 transformed = PortalMath.ComputeFrameRotation(entrance, exit, false) * velocity;

            Assert.That(transformed.magnitude, Is.EqualTo(12f).Within(1e-4f));
        }
    }
}
