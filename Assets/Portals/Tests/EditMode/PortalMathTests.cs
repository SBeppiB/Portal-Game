using NUnit.Framework;
using Portals.Core;
using UnityEngine;

namespace Portals.Tests
{
    public class PortalMathTests
    {
        private static void AssertVectorNear(Vector3 expected, Vector3 actual, float epsilon = 1e-4f)
        {
            Assert.That((expected - actual).sqrMagnitude, Is.LessThan(epsilon * epsilon),
                $"Expected {expected} but got {actual}.");
        }

        [Test]
        public void ComputeFrameRotation_MapsIncomingDirectionToExitForward()
        {
            Quaternion entrance = Quaternion.identity;
            Quaternion exit = Quaternion.Euler(0f, 90f, 0f);

            // Moving INTO the entrance is local -Z; it must come OUT along exit +Z.
            Vector3 outgoing = PortalMath.TransformDirection(Vector3.back, entrance, exit);

            AssertVectorNear(exit * Vector3.forward, outgoing);
            Assert.That(outgoing.magnitude, Is.EqualTo(1f).Within(1e-5f));
        }

        [Test]
        public void TransformPoint_ThenInverse_ReturnsOriginalPoint()
        {
            Vector3 entrancePosition = new Vector3(1f, 2f, 3f);
            Quaternion entranceRotation = Quaternion.Euler(10f, 20f, 30f);
            Vector3 exitPosition = new Vector3(-4f, 5f, -6f);
            Quaternion exitRotation = Quaternion.Euler(-15f, 140f, 25f);
            Vector3 point = new Vector3(0.3f, 1.7f, -2.2f);

            Vector3 transformed = PortalMath.TransformPoint(point,
                entrancePosition, entranceRotation, exitPosition, exitRotation);
            Vector3 restored = PortalMath.InverseTransformPoint(transformed,
                entrancePosition, entranceRotation, exitPosition, exitRotation);

            AssertVectorNear(point, restored);
        }

        [Test]
        public void StaticPortals_PreserveLinearSpeed()
        {
            Quaternion entrance = Quaternion.Euler(15f, 75f, -20f);
            Quaternion exit = Quaternion.Euler(-40f, 200f, 35f);
            Vector3 velocity = new Vector3(3f, 5f, -12f);

            Vector3 transformed = PortalMath.TransformDirection(velocity, entrance, exit);

            Assert.That(transformed.magnitude, Is.EqualTo(velocity.magnitude).Within(1e-4f));
        }

        [Test]
        public void MovingPortals_TransformVelocityRelativeToBothFrames()
        {
            Quaternion identity = Quaternion.identity;
            Vector3 entranceVelocity = new Vector3(1f, 0f, 0f);
            Vector3 exitVelocity = new Vector3(0f, 2f, 0f);
            Vector3 velocity = new Vector3(0f, 0f, -10f);

            Vector3 transformed = PortalMath.TransformVelocity(
                velocity, entranceVelocity, exitVelocity, identity, identity);

            // Ry(180) * (-1, 0, -10) = (1, 0, 10), then + (0, 2, 0).
            AssertVectorNear(new Vector3(1f, 2f, 10f), transformed);
        }

        [Test]
        public void AngularVelocity_UsesSameFrameRotation()
        {
            Quaternion entrance = Quaternion.identity;
            Quaternion exit = Quaternion.Euler(0f, 90f, 0f);
            Vector3 angularVelocity = new Vector3(0f, 0f, 5f);

            Vector3 transformed = PortalMath.TransformAngularVelocity(
                angularVelocity, Vector3.zero, Vector3.zero, entrance, exit);

            Assert.That(transformed.magnitude, Is.EqualTo(5f).Within(1e-4f));
            AssertVectorNear(exit * PortalMath.PortalFlip * angularVelocity, transformed);
        }

        [Test]
        public void PlaneCrossing_DetectsFrontToBackOnly()
        {
            Assert.That(PortalMath.TryFindPlaneCrossing(
                new Vector3(0.2f, 0f, 1f), new Vector3(0.2f, 0f, -1f),
                out Vector3 forwardHit, out float t), Is.True);
            Assert.That(Mathf.Abs(forwardHit.z), Is.LessThan(1e-5f));
            Assert.That(t, Is.EqualTo(0.5f).Within(1e-4f));

            Assert.That(PortalMath.TryFindPlaneCrossing(
                new Vector3(0.2f, 0f, -1f), new Vector3(0.2f, 0f, 1f),
                out _, out _), Is.False);

            // Starting exactly on the plane must not count as a new crossing.
            Assert.That(PortalMath.TryFindPlaneCrossing(
                new Vector3(0f, 0f, 0f), new Vector3(0f, 0f, -1f),
                out _, out _), Is.False);
        }

        [Test]
        public void OpeningContainment_UsesEllipseEquation()
        {
            Vector2 halfExtents = new Vector2(1f, 2f);

            Assert.That(PortalMath.IsInsideOpening(new Vector2(0.9f, 0f), halfExtents), Is.True);
            Assert.That(PortalMath.IsInsideOpening(new Vector2(0f, 1.9f), halfExtents), Is.True);
            Assert.That(PortalMath.IsInsideOpening(new Vector2(1.1f, 0f), halfExtents), Is.False);
            Assert.That(PortalMath.IsInsideOpening(new Vector2(0f, 2.1f), halfExtents), Is.False);
            Assert.That(PortalMath.IsInsideOpening(new Vector2(0.5f, 1.5f), halfExtents), Is.True);
            Assert.That(PortalMath.IsInsideOpening(new Vector2(0.8f, 1.6f), halfExtents), Is.False);
        }

        [Test]
        public void TraversalMatrix_MatchesDirectPointTransform()
        {
            Vector3 entrancePosition = new Vector3(2f, -1f, 4f);
            Quaternion entranceRotation = Quaternion.Euler(25f, 130f, -10f);
            Vector3 exitPosition = new Vector3(-7f, 3f, 1f);
            Quaternion exitRotation = Quaternion.Euler(-20f, -65f, 45f);
            Vector3 point = new Vector3(0.5f, -0.25f, 1.5f);

            Matrix4x4 matrix = PortalMath.BuildTraversalMatrix(
                entrancePosition, entranceRotation, exitPosition, exitRotation);
            Vector3 byMatrix = matrix.MultiplyPoint(point);
            Vector3 byMath = PortalMath.TransformPoint(point,
                entrancePosition, entranceRotation, exitPosition, exitRotation);

            AssertVectorNear(byMath, byMatrix);
        }
    }
}
