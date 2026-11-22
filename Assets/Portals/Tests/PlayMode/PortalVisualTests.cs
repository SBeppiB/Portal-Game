using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using Portals.Core;
using Portals.Integration;
using UnityEngine;
using UnityEngine.TestTools;

namespace Portals.Tests
{
    public class PortalVisualTests
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

        private static Color MaterialColour(Material material) =>
            material.HasProperty("_BaseColor") ? material.GetColor("_BaseColor") : material.GetColor("_Color");

        private PortalVisual CreateVisual(PortalKind kind, Color surface, Color frame)
        {
            GameObject owner = Track(new GameObject(kind + " Portal"));
            PortalEndpoint endpoint = kind == PortalKind.Interversal
                ? (PortalEndpoint)owner.AddComponent<InterversalPortal>()
                : owner.AddComponent<LocaversalPortal>();

            PortalVisual visual = owner.AddComponent<PortalVisual>();
            visual.Configure(surface, frame, endpoint.OpeningHalfSize);
            return visual;
        }

        [UnityTest]
        public IEnumerator SurfaceAndFrame_AreOpaqueAndMatchAperture()
        {
            PortalVisual visual = CreateVisual(PortalKind.Locaversal,
                PortalVisualColours.LocaversalSurface, PortalVisualColours.LocaversalFrame);
            PortalEndpoint portal = visual.GetComponent<PortalEndpoint>();

            Assert.That(visual.SurfaceObject, Is.Not.Null);
            Assert.That(visual.FrameObject, Is.Not.Null);

            MeshRenderer surfaceRenderer = visual.SurfaceObject.GetComponent<MeshRenderer>();
            MeshFilter surfaceFilter = visual.SurfaceObject.GetComponent<MeshFilter>();
            Assert.That(surfaceRenderer, Is.Not.Null);
            Assert.That(surfaceFilter.sharedMesh.vertexCount, Is.GreaterThan(2));

            Material material = surfaceRenderer.sharedMaterial;
            Assert.That(material.renderQueue, Is.EqualTo((int)UnityEngine.Rendering.RenderQueue.Transparent));
            Assert.That(MaterialColour(material).a, Is.EqualTo(1f).Within(0.001f));
            AssertVectorNear(PortalVisualColours.LocaversalSurface, MaterialColour(material), 0.01f);

            // Disc must match the aperture; frame must extend beyond it.
            Vector2 half = portal.OpeningHalfSize;
            Assert.That(surfaceFilter.sharedMesh.bounds.extents.x, Is.EqualTo(half.x).Within(0.01f));
            Assert.That(surfaceFilter.sharedMesh.bounds.extents.y, Is.EqualTo(half.y).Within(0.01f));
            Assert.That(visual.FrameObject.GetComponent<MeshFilter>().sharedMesh.bounds.extents.x,
                Is.GreaterThan(half.x));

            // Visuals must never participate in physics queries.
            Assert.That(visual.SurfaceObject.GetComponent<Collider>(), Is.Null);
            Assert.That(visual.FrameObject.GetComponent<Collider>(), Is.Null);
            Assert.That(visual.SurfaceObject.layer, Is.EqualTo(2));

            yield break;
        }

        private static void AssertVectorNear(Color expected, Color actual, float epsilon)
        {
            Assert.That(Mathf.Abs(expected.r - actual.r), Is.LessThan(epsilon));
            Assert.That(Mathf.Abs(expected.g - actual.g), Is.LessThan(epsilon));
            Assert.That(Mathf.Abs(expected.b - actual.b), Is.LessThan(epsilon));
        }

        [UnityTest]
        public IEnumerator LocaversalAndInterversal_UseDistinctSolidColours()
        {
            PortalVisual local = CreateVisual(PortalKind.Locaversal,
                PortalVisualColours.LocaversalSurface, PortalVisualColours.LocaversalFrame);
            PortalVisual inter = CreateVisual(PortalKind.Interversal,
                PortalVisualColours.InterversalSurface, PortalVisualColours.InterversalFrame);

            AssertVectorNear(PortalVisualColours.LocaversalSurface, local.SurfaceColor, 0.001f);
            AssertVectorNear(PortalVisualColours.InterversalSurface, inter.SurfaceColor, 0.001f);
            Assert.That(Mathf.Abs(local.SurfaceColor.r - inter.SurfaceColor.r) +
                        Mathf.Abs(local.SurfaceColor.g - inter.SurfaceColor.g) +
                        Mathf.Abs(local.SurfaceColor.b - inter.SurfaceColor.b), Is.GreaterThan(0.5f));

            yield break;
        }

        [UnityTest]
        public IEnumerator ClosingAndReopeningPortal_TogglesVisualVisibility()
        {
            PortalVisual visual = CreateVisual(PortalKind.Locaversal,
                PortalVisualColours.LocaversalSurface, PortalVisualColours.LocaversalFrame);
            PortalEndpoint portal = visual.GetComponent<PortalEndpoint>();
            portal.SetActive(true);
            Assert.That(visual.SurfaceObject.activeSelf, Is.True);
            Assert.That(visual.FrameObject.activeSelf, Is.True);

            portal.SetActive(false);
            Assert.That(visual.SurfaceObject.activeSelf, Is.False);
            Assert.That(visual.FrameObject.activeSelf, Is.False);

            portal.SetActive(true);
            Assert.That(visual.SurfaceObject.activeSelf, Is.True);

            yield break;
        }

        [UnityTest]
        public IEnumerator PooledEndpoint_ReceivesCorrectVisualConfiguration()
        {
            GameObject lifecycleObject = Track(new GameObject("Lifecycle"));
            PortalLifecycleManager manager = lifecycleObject.AddComponent<PortalLifecycleManager>();
            manager.Initialize();

            PortalPlacer placer = Track(new GameObject("Placer")).AddComponent<PortalPlacer>();
            placer.SetEnergySystem(null);
            manager.Configure(placer, null);

            PortalVisual firstVisual = CreateVisual(PortalKind.Interversal,
                PortalVisualColours.InterversalSurface, PortalVisualColours.InterversalFrame);
            InterversalPortal first = (InterversalPortal)firstVisual.GetComponent<PortalEndpoint>();
            GameObject secondObject = Track(new GameObject("Second"));
            InterversalPortal second = secondObject.AddComponent<InterversalPortal>();
            PortalVisual secondVisual = secondObject.AddComponent<PortalVisual>();
            PortalVisualColours.Apply(secondVisual, PortalKind.Interversal, second.OpeningHalfSize);
            first.Configure("VisualTest", second, InterversalMappingMode.ViaPortalFrame, Vector3.zero);
            firstVisual.SetAnimationDurations(0.01f, 0.01f);
            secondVisual.SetAnimationDurations(0.01f, 0.01f);

            PortalPair pair = manager.RegisterPair(PortalKind.Interversal, first, second);
            manager.ClosePair(pair);

            // Endpoints only enter the pool once their shrink animation finishes.
            Assert.That(firstVisual.IsClosing || secondVisual.IsClosing, Is.True);
            yield return new WaitForSeconds(0.2f);

            PortalPair reusedPair = manager.GetOrCreatePair(PortalKind.Interversal);
            PortalEndpoint reused = reusedPair.Entrance;
            PortalVisual reusedVisual = reused.GetComponent<PortalVisual>();

            Assert.That(ReferenceEquals(reused, first) || ReferenceEquals(reused, second), Is.True,
                "A completed close must return its endpoint to the pool for reuse.");
            Assert.That(reusedVisual, Is.Not.Null, "Reused endpoints must keep a PortalVisual.");
            AssertVectorNear(PortalVisualColours.InterversalSurface, reusedVisual.SurfaceColor, 0.001f);
            Assert.That(reusedVisual.SurfaceObject, Is.Not.Null);
            Assert.That(reusedVisual.IsAnimating, Is.False, "Stale animations must not survive reuse.");
            Assert.That(reusedVisual.SurfaceObject.transform.localScale.x, Is.EqualTo(1f).Within(0.001f));

            yield break;
        }

        [UnityTest]
        public IEnumerator Visual_FollowsPortalSurfaceAnchorMovement()
        {
            GameObject platform = Track(new GameObject("Platform"));
            PortalVisual visual = CreateVisual(PortalKind.Locaversal,
                PortalVisualColours.LocaversalSurface, PortalVisualColours.LocaversalFrame);
            PortalEndpoint portal = visual.GetComponent<PortalEndpoint>();

            PortalSurfaceAnchor anchor = portal.gameObject.AddComponent<PortalSurfaceAnchor>();
            anchor.Attach(platform.transform, new Vector3(0f, 0.05f, 0f),
                Quaternion.LookRotation(Vector3.up, Vector3.forward));

            platform.transform.position = new Vector3(5f, 1f, -2f);
            anchor.UpdateAnchor();

            Assert.That((visual.SurfaceObject.transform.position - portal.Position).magnitude,
                Is.LessThan(0.02f));
            Assert.That((visual.FrameObject.transform.position - portal.Position).magnitude,
                Is.LessThan(0.02f));

            yield break;
        }

        [UnityTest]
        public IEnumerator MultiplePairs_RenderIndependently()
        {
            PortalVisual a = CreateVisual(PortalKind.Locaversal,
                PortalVisualColours.LocaversalSurface, PortalVisualColours.LocaversalFrame);
            PortalVisual b = CreateVisual(PortalKind.Interversal,
                PortalVisualColours.InterversalSurface, PortalVisualColours.InterversalFrame);

            a.GetComponent<PortalEndpoint>().SetActive(true);
            b.GetComponent<PortalEndpoint>().SetActive(true);

            Assert.That(a.SurfaceObject, Is.Not.SameAs(b.SurfaceObject));
            AssertVectorNear(PortalVisualColours.LocaversalSurface,
                MaterialColour(a.SurfaceObject.GetComponent<MeshRenderer>().sharedMaterial), 0.01f);
            AssertVectorNear(PortalVisualColours.InterversalSurface,
                MaterialColour(b.SurfaceObject.GetComponent<MeshRenderer>().sharedMaterial), 0.01f);
            Assert.That(a.SurfaceObject.activeSelf && b.SurfaceObject.activeSelf, Is.True);

            yield break;
        }
    }
}
