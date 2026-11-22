using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using Portals.Core;
using UnityEngine;
using UnityEngine.TestTools;

namespace Portals.Tests
{
    /// <summary>
    /// Placement coverage for floors, walls, ceilings, slopes, rotated objects,
    /// MeshColliders, moving surfaces and invalid/edge cases.
    /// </summary>
    public class PortalPlacementSurfaceTests
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

        private static void AssertVectorNear(Vector3 expected, Vector3 actual, float epsilon = 0.02f)
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

        private static GameObject Surface(string name, Vector3 position, Vector3 scale, Quaternion rotation)
        {
            GameObject surface = GameObject.CreatePrimitive(PrimitiveType.Cube);
            surface.name = name;
            surface.transform.SetPositionAndRotation(position, rotation);
            surface.transform.localScale = scale;
            Physics.SyncTransforms();
            return surface;
        }

        [UnityTest]
        public IEnumerator PlacesOnFloor_WithNormalAlignment()
        {
            GameObject floor = Track(Surface("Floor", new Vector3(0f, -0.1f, 0f),
                new Vector3(10f, 0.2f, 10f), Quaternion.identity));
            LocaversalPortal[] pair = CreatePair();
            PortalPlacer placer = CreatePlacer();

            Ray ray = new Ray(new Vector3(0f, 5f, 0f), Vector3.down);
            Assert.That(placer.TryPlace(pair[0], ray, out var failure, out string detail), Is.True, $"{failure}: {detail}");

            AssertVectorNear(Vector3.up, pair[0].Forward);
            Assert.That(pair[0].Position.y, Is.EqualTo(0.05f).Within(0.01f));
            PortalSurfaceAnchor anchor = pair[0].GetComponent<PortalSurfaceAnchor>();
            Assert.That(anchor, Is.Not.Null);
            Assert.That(anchor.Surface, Is.SameAs(floor.transform));
            Assert.That(pair[1].IsActive, Is.False);

            yield break;
        }

        [UnityTest]
        public IEnumerator PlacesOnWall_WithNormalAlignment()
        {
            GameObject wall = Track(Surface("Wall", new Vector3(0f, 2f, 0f),
                new Vector3(10f, 4f, 0.2f), Quaternion.identity));
            LocaversalPortal[] pair = CreatePair();
            PortalPlacer placer = CreatePlacer();

            Ray ray = new Ray(new Vector3(0f, 2f, 5f), Vector3.back);
            Assert.That(placer.TryPlace(pair[0], ray, out var failure, out string detail), Is.True, $"{failure}: {detail}");

            AssertVectorNear(Vector3.forward, pair[0].Forward);
            AssertVectorNear(Vector3.up, pair[0].Up);
            Assert.That(pair[0].Position.z, Is.EqualTo(0.15f).Within(0.01f));

            yield break;
        }

        [UnityTest]
        public IEnumerator PlacesOnCeiling_WithNormalAlignment()
        {
            GameObject ceiling = Track(Surface("Ceiling", new Vector3(0f, 4f, 0f),
                new Vector3(10f, 0.2f, 10f), Quaternion.identity));
            LocaversalPortal[] pair = CreatePair();
            PortalPlacer placer = CreatePlacer();

            Ray ray = new Ray(new Vector3(0f, 0f, 0f), Vector3.up);
            Assert.That(placer.TryPlace(pair[0], ray, out var failure, out string detail), Is.True, $"{failure}: {detail}");

            AssertVectorNear(Vector3.down, pair[0].Forward);
            Assert.That(pair[0].Position.y, Is.EqualTo(3.85f).Within(0.01f));

            yield break;
        }

        [UnityTest]
        public IEnumerator PlacesOnSlopedSurface()
        {
            Quaternion tilt = Quaternion.Euler(30f, 0f, 0f);
            GameObject slope = Track(Surface("Slope", Vector3.zero, new Vector3(10f, 0.2f, 10f), tilt));
            LocaversalPortal[] pair = CreatePair();
            PortalPlacer placer = CreatePlacer();

            Vector3 expectedNormal = tilt * Vector3.up;
            Vector3 surfacePoint = slope.transform.TransformPoint(Vector3.up * 0.1f);
            Ray ray = new Ray(surfacePoint + expectedNormal * 5f, -expectedNormal);

            Assert.That(placer.TryPlace(pair[0], ray, out var failure, out string detail), Is.True, $"{failure}: {detail}");
            AssertVectorNear(expectedNormal, pair[0].Forward);

            yield break;
        }

        [UnityTest]
        public IEnumerator PlacesOnArbitrarilyRotatedObject()
        {
            Quaternion rotation = Quaternion.Euler(20f, 35f, 15f);
            GameObject rotated = Track(Surface("Rotated", Vector3.zero, new Vector3(6f, 6f, 6f), rotation));
            LocaversalPortal[] pair = CreatePair();
            PortalPlacer placer = CreatePlacer();

            Vector3 expectedNormal = rotation * Vector3.forward;
            Vector3 surfacePoint = rotated.transform.TransformPoint(Vector3.forward * 3f);
            Ray ray = new Ray(surfacePoint + expectedNormal * 5f, -expectedNormal);

            Assert.That(placer.TryPlace(pair[0], ray, out var failure, out string detail), Is.True, $"{failure}: {detail}");
            AssertVectorNear(expectedNormal, pair[0].Forward);

            yield break;
        }

        [UnityTest]
        public IEnumerator PlacesOnMeshCollider()
        {
            GameObject meshSurface = GameObject.CreatePrimitive(PrimitiveType.Cube);
            meshSurface.name = "MeshColliderSurface";
            meshSurface.transform.position = new Vector3(0f, -0.1f, 0f);
            meshSurface.transform.localScale = new Vector3(10f, 0.2f, 10f);
            Object.DestroyImmediate(meshSurface.GetComponent<BoxCollider>());
            MeshCollider meshCollider = meshSurface.AddComponent<MeshCollider>();
            meshCollider.sharedMesh = meshSurface.GetComponent<MeshFilter>().sharedMesh;
            Track(meshSurface);
            Physics.SyncTransforms();

            LocaversalPortal[] pair = CreatePair();
            PortalPlacer placer = CreatePlacer();

            Ray ray = new Ray(new Vector3(0f, 5f, 0f), Vector3.down);
            Assert.That(placer.TryPlace(pair[0], ray, out var failure, out string detail), Is.True, $"{failure}: {detail}");
            AssertVectorNear(Vector3.up, pair[0].Forward);

            yield break;
        }

        [UnityTest]
        public IEnumerator PortalFollowsMovingSurface()
        {
            GameObject platform = Track(Surface("MovingPlatform", Vector3.zero,
                new Vector3(10f, 0.2f, 10f), Quaternion.identity));
            Rigidbody body = platform.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
            Physics.SyncTransforms();

            LocaversalPortal[] pair = CreatePair();
            PortalPlacer placer = CreatePlacer();

            Ray ray = new Ray(new Vector3(0f, 5f, 0f), Vector3.down);
            Assert.That(placer.TryPlace(pair[0], ray, out var failure, out string detail), Is.True, $"{failure}: {detail}");
            Vector3 before = pair[0].Position;
            Quaternion beforeRotation = pair[0].Rotation;

            // Simulate the surface being carried by a platform/transform each frame.
            platform.transform.position += new Vector3(3f, 1f, -2f);
            platform.transform.rotation = Quaternion.Euler(0f, 10f, 0f);
            Physics.SyncTransforms();

            // The anchor updates in LateUpdate; drive one explicitly for deterministic tests.
            pair[0].GetComponent<PortalSurfaceAnchor>().UpdateAnchor();

            AssertVectorNear(before + new Vector3(3f, 1f, -2f), pair[0].Position);
            Quaternion expectedRotation = platform.transform.rotation * beforeRotation;
            Assert.That(Quaternion.Angle(expectedRotation, pair[0].Rotation), Is.LessThan(0.5f));

            yield break;
        }

        [UnityTest]
        public IEnumerator RejectsOpeningThatHangsOverAnEdge()
        {
            Track(Surface("SmallPlatform", Vector3.zero, new Vector3(0.4f, 0.2f, 0.4f), Quaternion.identity));
            LocaversalPortal[] pair = CreatePair();
            PortalPlacer placer = CreatePlacer();

            Ray ray = new Ray(new Vector3(0f, 5f, 0f), Vector3.down);
            Assert.That(placer.TryPlace(pair[0], ray, out PortalPlacementFailure failure, out string detail), Is.False);
            Assert.That(failure, Is.EqualTo(PortalPlacementFailure.OpeningDoesNotFit));
            Assert.That(detail, Does.Contain("rim sample"));
            Assert.That(pair[0].IsActive, Is.False);

            yield break;
        }

        [UnityTest]
        public IEnumerator IgnoredShooterColliderDoesNotBlockPlacement()
        {
            GameObject shooter = Track(Surface("Shooter", new Vector3(0f, 2f, 0f),
                new Vector3(1f, 1f, 1f), Quaternion.identity));
            LocaversalPortal[] pair = CreatePair();
            PortalPlacer placer = CreatePlacer();
            placer.SetIgnoreRoot(shooter.transform);

            Ray ray = new Ray(new Vector3(0f, 5f, 0f), Vector3.down);
            Assert.That(placer.TryPlace(pair[0], ray, out PortalPlacementFailure failure, out string detail), Is.False);
            Assert.That(failure, Is.EqualTo(PortalPlacementFailure.NoSurfaceHit));
            Assert.That(detail, Does.Contain("excluded"));

            yield break;
        }

        [UnityTest]
        public IEnumerator BothEndpointsRemainPairedAfterPlacement()
        {
            Track(Surface("Floor", new Vector3(0f, -0.1f, 0f), new Vector3(10f, 0.2f, 10f), Quaternion.identity));
            Track(Surface("Wall", new Vector3(0f, 2f, 6f), new Vector3(10f, 4f, 0.2f), Quaternion.identity));
            LocaversalPortal[] pair = CreatePair();
            PortalPlacer placer = CreatePlacer();

            Assert.That(placer.TryPlace(pair[0], new Ray(new Vector3(0f, 5f, 0f), Vector3.down),
                out _, out _), Is.True);
            Assert.That(placer.TryPlace(pair[1], new Ray(new Vector3(0f, 2f, 10f), Vector3.back),
                out _, out _), Is.True);

            Assert.That(pair[0].IsActive && pair[1].IsActive, Is.True);
            Assert.That(pair[0].GetLinkedEndpoint(), Is.SameAs(pair[1]));
            Assert.That(pair[1].GetLinkedEndpoint(), Is.SameAs(pair[0]));

            yield break;
        }
    }
}
