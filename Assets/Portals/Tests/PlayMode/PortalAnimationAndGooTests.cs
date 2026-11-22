using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using Portals.Core;
using Portals.Integration;
using Portals.Traversal;
using UnityEngine;
using UnityEngine.TestTools;

namespace Portals.Tests
{
    /// <summary>
    /// Covers single-pair enforcement, open/close animations, traversal gating, pooled
    /// endpoint reuse, blue goo residue and the B / expiration close paths.
    /// </summary>
    public class PortalAnimationAndGooTests
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

            foreach (PortalGooResidue goo in Object.FindObjectsByType<PortalGooResidue>(FindObjectsSortMode.None))
                if (goo != null) Object.DestroyImmediate(goo.gameObject);

            // The lifecycle manager may have created fresh endpoints when replacing a pair.
            foreach (PortalEndpoint portal in Object.FindObjectsByType<PortalEndpoint>(FindObjectsSortMode.None))
                if (portal != null) Object.DestroyImmediate(portal.gameObject);
        }

        private PortalLifecycleManager CreateManager()
        {
            PortalLifecycleManager manager = Track(new GameObject("Lifecycle"))
                .AddComponent<PortalLifecycleManager>();
            manager.Initialize();

            PortalPlacer placer = Track(new GameObject("Placer")).AddComponent<PortalPlacer>();
            placer.SetEnergySystem(null);
            manager.Configure(placer, null);
            return manager;
        }

        private LocaversalPortal CreatePortal(string name, Vector3 position,
            out PortalVisual visual, float duration = 0.05f)
        {
            GameObject owner = Track(new GameObject(name));
            owner.transform.position = position;
            LocaversalPortal portal = owner.AddComponent<LocaversalPortal>();
            visual = owner.AddComponent<PortalVisual>();
            PortalVisualColours.Apply(visual, PortalKind.Locaversal, portal.OpeningHalfSize);
            visual.SetAnimationDurations(duration, duration);
            return portal;
        }

        private PortalPair CreateActivePair(PortalLifecycleManager manager,
            out LocaversalPortal a, out LocaversalPortal b,
            out PortalVisual va, out PortalVisual vb, float duration = 0.05f)
        {
            a = CreatePortal("Animated A", new Vector3(0f, 1f, 4f), out va, duration);
            b = CreatePortal("Animated B", new Vector3(6f, 1f, 4f), out vb, duration);
            a.SetPair(b);
            a.SetActive(true);
            b.SetActive(true);
            return manager.RegisterPair(PortalKind.Locaversal, a, b);
        }

        private static GameObject CreateFloor()
        {
            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "Goo Floor";
            floor.transform.position = new Vector3(0f, -0.1f, 2f);
            floor.transform.localScale = new Vector3(30f, 0.2f, 30f);
            Physics.SyncTransforms();
            return floor;
        }

        private static float SurfaceScale(PortalVisual visual) =>
            visual.SurfaceObject != null ? visual.SurfaceObject.transform.localScale.x : -1f;

        [UnityTest]
        public IEnumerator SinglePairEnforcement_OnlyOnePairIsActive()
        {
            PortalLifecycleManager manager = CreateManager();
            CreateActivePair(manager, out LocaversalPortal firstA, out LocaversalPortal firstB, out _, out _);
            PortalPair second = CreateActivePair(manager, out _, out _, out _, out _);

            Assert.That(manager.HasActivePair, Is.True);
            Assert.That(manager.ActivePair, Is.SameAs(second));
            Assert.That(firstA.IsActive || firstB.IsActive, Is.False,
                "The previously active pair must be closed when a second one is registered.");

            yield break;
        }

        [UnityTest]
        public IEnumerator ReplacingAnExistingPair_ClosesItAndSpawnsGooForBothEndpoints()
        {
            GameObject floor = Track(CreateFloor());
            PortalLifecycleManager manager = CreateManager();
            PortalPair old = CreateActivePair(manager, out LocaversalPortal a, out LocaversalPortal b, out _, out _);

            PortalPair replacement = manager.GetOrCreatePair(PortalKind.Locaversal);

            Assert.That(manager.ActivePair, Is.SameAs(replacement));
            Assert.That(manager.ActivePair, Is.Not.SameAs(old));
            Assert.That(a.IsActive || b.IsActive, Is.False);
            Assert.That(replacement.IsComplete, Is.False);

            yield return new WaitForSeconds(0.2f);

            PortalGooResidue[] puddles = Object.FindObjectsByType<PortalGooResidue>(FindObjectsSortMode.None);
            Assert.That(puddles.Length, Is.EqualTo(2), "Each closed endpoint leaves its own puddle.");
            Assert.That(floor, Is.Not.Null);
        }

        [UnityTest]
        public IEnumerator OpeningAnimation_ExpandsSurfaceAndBlocksTraversalUntilComplete()
        {
            PortalLifecycleManager manager = CreateManager();
            LocaversalPortal portal = CreatePortal("Opening", new Vector3(0f, 1f, 0f),
                out PortalVisual visual, 0.1f);
            portal.SetActive(true);

            visual.PlayOpen();

            Assert.That(visual.IsOpening, Is.True);
            Assert.That(SurfaceScale(visual), Is.EqualTo(0f).Within(0.001f));
            Assert.That(visual.FrameObject.transform.localScale.x, Is.EqualTo(0f).Within(0.001f));
            Assert.That(portal.IsTraversable, Is.False, "A portal must not transport while opening.");
            Assert.That(portal.IsActive, Is.True);

            yield return new WaitForSeconds(0.2f);

            Assert.That(visual.IsAnimating, Is.False);
            Assert.That(SurfaceScale(visual), Is.EqualTo(1f).Within(0.001f));
            Assert.That(visual.FrameObject.transform.localScale.x, Is.EqualTo(1f).Within(0.001f));
            Assert.That(portal.IsTraversable, Is.True);
        }

        [UnityTest]
        public IEnumerator OpeningAnimation_RestartingDoesNotLeaveCollapsedState()
        {
            LocaversalPortal portal = CreatePortal("Restart Open", Vector3.zero,
                out PortalVisual visual, 0.01f);
            portal.SetActive(true);

            visual.PlayOpen();
            visual.PlayOpen(); // simulate an interrupted animation
            yield return new WaitForSeconds(0.1f);

            Assert.That(visual.IsAnimating, Is.False);
            Assert.That(SurfaceScale(visual), Is.EqualTo(1f).Within(0.001f));
            Assert.That(portal.IsTraversable, Is.True);
        }

        [UnityTest]
        public IEnumerator ClosingAnimation_ShrinksBothEndpointsTogether()
        {
            PortalLifecycleManager manager = CreateManager();
            PortalPair pair = CreateActivePair(manager, out LocaversalPortal a, out LocaversalPortal b,
                out PortalVisual va, out PortalVisual vb, 0.1f);

            manager.ClosePair(pair);

            Assert.That(va.IsClosing && vb.IsClosing, Is.True, "Both endpoints animate together.");
            Assert.That(a.IsTraversable && b.IsTraversable, Is.False);
            Assert.That(a.IsActive && b.IsActive, Is.False);
            Assert.That(SurfaceScale(va), Is.GreaterThan(0f));

            yield return new WaitForSeconds(0.2f);

            Assert.That(va.IsAnimating || vb.IsAnimating, Is.False);
            Assert.That(SurfaceScale(va), Is.EqualTo(0f).Within(0.001f));
            Assert.That(SurfaceScale(vb), Is.EqualTo(0f).Within(0.001f));
            Assert.That(va.SurfaceObject.activeSelf, Is.False);
            Assert.That(manager.HasActivePair, Is.False);
        }

        [UnityTest]
        public IEnumerator TraversalDisabledDuringClosing_BlocksPortalTraverser()
        {
            GameObject wallA = Track(GameObject.CreatePrimitive(PrimitiveType.Cube));
            wallA.transform.position = new Vector3(0f, 2f, -0.25f);
            wallA.transform.localScale = new Vector3(8f, 8f, 0.5f);
            GameObject wallB = Track(GameObject.CreatePrimitive(PrimitiveType.Cube));
            wallB.transform.position = new Vector3(10f, 2f, -0.25f);
            wallB.transform.localScale = new Vector3(8f, 8f, 0.5f);

            PortalLifecycleManager manager = CreateManager();
            PortalPair pair = CreateActivePair(manager, out LocaversalPortal a, out LocaversalPortal b,
                out PortalVisual va, out PortalVisual vb, 0.02f);
            a.transform.SetPositionAndRotation(new Vector3(0f, 2f, 0.05f), Quaternion.identity);
            b.transform.SetPositionAndRotation(new Vector3(10f, 2f, 0.05f), Quaternion.identity);
            // Visuals were configured after placement, so restore the traversal gate manually.
            a.SetTraversalEnabled(true);
            b.SetTraversalEnabled(true);
            Physics.SyncTransforms();

            GameObject traveller = Track(new GameObject("Traveller"));
            traveller.transform.position = new Vector3(0f, 2f, 3f);
            Rigidbody body = traveller.AddComponent<Rigidbody>();
            body.useGravity = false;
            body.linearVelocity = Vector3.back * 8f;
            CapsuleCollider capsule = traveller.AddComponent<CapsuleCollider>();
            capsule.radius = 0.4f;
            capsule.height = 1.8f;
            PortalTraverser traverser = traveller.AddComponent<PortalTraverser>();
            traverser.AutoDetect = false;
            traverser.SetPortals(a, b);
            traverser.TickDetection(); // prime the previous position

            body.position = new Vector3(0f, 2f, -0.5f);
            Assert.That(traverser.TickDetection(), Is.True, "An open pair must still traverse.");

            // Reset to a fresh approach after the first jump.
            body.position = new Vector3(0f, 2f, 3f);
            traverser.ResetTracking();
            Physics.SyncTransforms();

            manager.ClosePair(pair);
            Assert.That(a.IsTraversable && b.IsTraversable, Is.False);

            body.position = new Vector3(0f, 2f, -0.5f);
            Assert.That(traverser.TickDetection(), Is.False,
                "A closing pair must not transport objects.");
            Assert.That(va.IsClosing && vb.IsClosing, Is.True);

            yield break;
        }

        [UnityTest]
        public IEnumerator Goo_SpawnsOnFloorBeneathClosedPortalAndIsInert()
        {
            GameObject floor = Track(CreateFloor());
            PortalLifecycleManager manager = CreateManager();
            CreateActivePair(manager, out _, out _, out _, out _, 0.01f);
            manager.CloseAll();

            yield return new WaitForSeconds(0.2f);

            PortalGooResidue[] puddles = Object.FindObjectsByType<PortalGooResidue>(FindObjectsSortMode.None);
            Assert.That(puddles.Length, Is.EqualTo(2));
            foreach (PortalGooResidue goo in puddles)
            {
                Assert.That(goo.transform.position.y, Is.EqualTo(0.01f).Within(0.02f));
                Assert.That(Vector3.Dot(goo.transform.up, Vector3.up), Is.GreaterThan(0.99f));
                Assert.That(goo.GetComponent<Collider>(), Is.Null, "Goo must have no collider.");
                Assert.That(goo.gameObject.layer, Is.EqualTo(2));
            }

            Assert.That(floor, Is.Not.Null);
        }

        [UnityTest]
        public IEnumerator Goo_DoesNotSpawnWithoutAFloor()
        {
            PortalLifecycleManager manager = CreateManager();
            CreateActivePair(manager, out _, out _, out _, out _, 0.01f);
            manager.CloseAll();

            yield return new WaitForSeconds(0.2f);

            Assert.That(Object.FindObjectsByType<PortalGooResidue>(FindObjectsSortMode.None).Length,
                Is.EqualTo(0));
        }

        [UnityTest]
        public IEnumerator Goo_FadesAndShrinksOverExactlyFiveSeconds()
        {
            GameObject floor = Track(CreateFloor());
            PortalGooResidue first = PortalGooResidue.SpawnUnder(new Vector3(0f, 1f, 2f));
            PortalGooResidue second = PortalGooResidue.SpawnUnder(new Vector3(3f, 1f, 2f));
            Assert.That(first, Is.Not.Null);
            Assert.That(second, Is.Not.Null);
            Assert.That(first.Duration, Is.EqualTo(5f));

            first.Tick(2.5f);
            Assert.That(first.transform.localScale.x, Is.EqualTo(0.5f).Within(0.02f));
            Assert.That(first.Elapsed, Is.EqualTo(2.5f).Within(0.001f));
            // Independent puddles do not share decay state.
            Assert.That(second.transform.localScale.x, Is.EqualTo(1f).Within(0.001f));

            first.Tick(2.5f);
            Assert.That(first.IsDecaying, Is.False);
            yield return null;
            Assert.That(first == null, Is.True, "Goo must be destroyed after exactly five seconds.");
            Assert.That(second, Is.Not.Null, "Other puddles must remain independent.");
            Assert.That(floor, Is.Not.Null);
        }

        [UnityTest]
        public IEnumerator PooledEndpointReuse_ClearsPreviousAnimationState()
        {
            PortalLifecycleManager manager = CreateManager();
            PortalPair first = CreateActivePair(manager, out LocaversalPortal a, out LocaversalPortal b,
                out PortalVisual va, out PortalVisual vb, 0.01f);

            manager.ClosePair(first);
            yield return new WaitForSeconds(0.2f);

            PortalPair reused = manager.GetOrCreatePair(PortalKind.Locaversal);
            Assert.That(reused.Entrance, Is.SameAs(a).Or.SameAs(b));
            Assert.That(reused.Exit, Is.SameAs(a).Or.SameAs(b));
            Assert.That(reused.Entrance, Is.Not.SameAs(reused.Exit));

            PortalVisual reusedVisual = reused.Entrance.GetComponent<PortalVisual>();
            Assert.That(reusedVisual.IsAnimating, Is.False);
            Assert.That(SurfaceScale(reusedVisual), Is.EqualTo(1f).Within(0.001f));
            Assert.That(reused.Entrance.IsTraversable, Is.False, "Reused endpoint starts closed.");
            Assert.That(va.IsAnimating || vb.IsAnimating, Is.False);
        }

        [UnityTest]
        public IEnumerator BClosureAndExpiration_CloseTheSinglePair()
        {
            PortalLifecycleManager manager = CreateManager();
            PortalPair pair = CreateActivePair(manager, out LocaversalPortal a, out LocaversalPortal b,
                out PortalVisual va, out PortalVisual vb, 0.01f);

            GameObject firingObject = Track(new GameObject("Firing"));
            PortalFiringController firing = firingObject.AddComponent<PortalFiringController>();
            firing.ConfigureLifecycle(manager);

            firing.CloseAllPortals(); // bound to B in Update as well
            Assert.That(manager.HasActivePair, Is.False);
            Assert.That(a.IsActive && b.IsActive, Is.False);
            Assert.That(va.IsClosing && vb.IsClosing, Is.True);

            yield return new WaitForSeconds(0.2f);

            // A fresh pair still expires after its full 30 second lifetime.
            PortalPair timed = CreateActivePair(manager, out LocaversalPortal ta, out LocaversalPortal tb,
                out PortalVisual tv, out _, 0.01f);
            Assert.That(timed.RemainingLifetime, Is.EqualTo(30f).Within(0.01f));
            manager.Tick(30f);
            Assert.That(manager.HasActivePair, Is.False);
            Assert.That(ta.IsActive && tb.IsActive, Is.False);
            Assert.That(tv.IsClosing, Is.True);

            yield break;
        }
    }
}
