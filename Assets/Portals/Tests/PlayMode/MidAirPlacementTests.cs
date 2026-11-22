using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using Portals.Core;
using Portals.Traversal;
using UnityEngine;
using UnityEngine.TestTools;

namespace Portals.Tests
{
    public class MidAirPlacementTests
    {
        private readonly List<GameObject> m_Objects = new List<GameObject>();

        private GameObject Track(GameObject gameObject)
        {
            m_Objects.Add(gameObject);
            return gameObject;
        }

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject gameObject in m_Objects)
                if (gameObject != null) Object.DestroyImmediate(gameObject);
            m_Objects.Clear();
        }

        private static void AssertVectorNear(Vector3 expected, Vector3 actual, float epsilon = 0.05f)
        {
            Assert.That((expected - actual).sqrMagnitude, Is.LessThan(epsilon * epsilon),
                $"Expected {expected} but got {actual}.");
        }

        private LocaversalPortal[] CreatePair()
        {
            LocaversalPortal a = Track(new GameObject("PortalA")).AddComponent<LocaversalPortal>();
            LocaversalPortal b = Track(new GameObject("PortalB")).AddComponent<LocaversalPortal>();
            a.SetPair(b);
            a.SetActive(false);
            b.SetActive(false);
            return new[] { a, b };
        }

        private PortalPlacer CreatePlacer()
        {
            PortalPlacer placer = Track(new GameObject("Placer")).AddComponent<PortalPlacer>();
            placer.SetEnergySystem(null);
            return placer;
        }

        [UnityTest]
        public IEnumerator ShotWithNoSurfaceHit_CreatesPortalAtExactlyTwentyUnits()
        {
            LocaversalPortal[] pair = CreatePair();
            PortalPlacer placer = CreatePlacer();

            Vector3 origin = new Vector3(0f, 1f, 0f);
            Vector3 direction = Vector3.forward;
            Ray shot = new Ray(origin, direction);

            Assert.That(placer.TryPlaceFromShot(pair[0], shot,
                    out PortalPlacementFailure failure, out string detail), Is.True,
                $"{failure}: {detail}");
            Assert.That(detail, Does.Contain("Mid-air"));
            AssertVectorNear(origin + direction * 20f, pair[0].Position);
            AssertVectorNear(-direction, pair[0].Forward);
            Assert.That(pair[0].IsActive, Is.True);
            Assert.That(pair[1].IsActive, Is.False);

            yield break;
        }

        [UnityTest]
        public IEnumerator ShotWithSurfaceHit_UsesExistingSurfacePlacement()
        {
            GameObject floor = Track(GameObject.CreatePrimitive(PrimitiveType.Cube));
            floor.name = "Floor";
            floor.transform.position = new Vector3(0f, -0.1f, 0f);
            floor.transform.localScale = new Vector3(10f, 0.2f, 10f);
            Physics.SyncTransforms();

            LocaversalPortal[] pair = CreatePair();
            PortalPlacer placer = CreatePlacer();

            Ray shot = new Ray(new Vector3(0f, 5f, 0f), Vector3.down);
            Assert.That(placer.TryPlaceFromShot(pair[0], shot,
                    out PortalPlacementFailure failure, out string detail), Is.True,
                $"{failure}: {detail}");
            Assert.That(detail, Does.Contain("Placed on"));
            Assert.That(pair[0].Position.y, Is.EqualTo(0.05f).Within(0.01f));
            AssertVectorNear(Vector3.up, pair[0].Forward);

            yield break;
        }

        [UnityTest]
        public IEnumerator MidAirPortals_RemainPairedAndSupportTraversal()
        {
            LocaversalPortal[] pair = CreatePair();
            PortalPlacer placer = CreatePlacer();

            Ray shotA = new Ray(new Vector3(0f, 1f, 0f), Vector3.forward);
            Ray shotB = new Ray(new Vector3(0f, 1f, 0f), Vector3.right);

            Assert.That(placer.TryPlaceFromShot(pair[0], shotA, out _, out _), Is.True);
            Assert.That(placer.TryPlaceFromShot(pair[1], shotB, out _, out _), Is.True);
            Assert.That(pair[0].IsActive && pair[1].IsActive, Is.True);
            Assert.That(pair[0].GetLinkedEndpoint(), Is.SameAs(pair[1]));

            GameObject traveller = Track(new GameObject("Traveller"));
            traveller.transform.position = new Vector3(0f, 1f, 19f);
            Rigidbody body = traveller.AddComponent<Rigidbody>();
            body.useGravity = false;
            CapsuleCollider capsule = traveller.AddComponent<CapsuleCollider>();
            capsule.radius = 0.4f;
            capsule.height = 1.8f;
            body.linearVelocity = Vector3.forward * 10f;

            PortalTraverser traverser = traveller.AddComponent<PortalTraverser>();
            traverser.AutoDetect = false;
            traverser.SetPortals(pair);

            Assert.That(traverser.TickDetection(), Is.False);
            body.position = new Vector3(0f, 1f, 21f);
            Assert.That(traverser.TickDetection(), Is.True);

            Assert.That(Vector3.Distance(body.position, pair[1].Position), Is.LessThan(3f));
            Assert.That(traverser.IgnoredPortal, Is.SameAs(pair[1]));

            yield break;
        }

        [UnityTest]
        public IEnumerator MidAirEndpointInsideCollider_IsRejectedWithClearReason()
        {
            GameObject obstacle = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            obstacle.name = "ObstacleBesideRay";
            obstacle.transform.position = new Vector3(0.8f, 1f, 20f);
            obstacle.transform.localScale = Vector3.one * 0.8f;
            Track(obstacle);
            Physics.SyncTransforms();

            LocaversalPortal[] pair = CreatePair();
            PortalPlacer placer = CreatePlacer();

            Ray shot = new Ray(new Vector3(0f, 1f, 0f), Vector3.forward);
            Assert.That(placer.TryPlaceFromShot(pair[0], shot,
                    out PortalPlacementFailure failure, out string detail), Is.False);
            Assert.That(failure, Is.EqualTo(PortalPlacementFailure.MidAirObstructed));
            Assert.That(detail, Does.Contain("obstructed"));
            Assert.That(pair[0].IsActive, Is.False);

            yield break;
        }
    }
}
