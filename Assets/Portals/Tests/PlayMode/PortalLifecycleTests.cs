using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using Portals.Core;
using UnityEngine;
using UnityEngine.TestTools;

namespace Portals.Tests
{
    public class PortalLifecycleTests
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

        private PortalPair CreateCompletePair(PortalLifecycleManager manager, string name, bool active = true)
        {
            LocaversalPortal a = Track(new GameObject(name + "A")).AddComponent<LocaversalPortal>();
            LocaversalPortal b = Track(new GameObject(name + "B")).AddComponent<LocaversalPortal>();
            a.SetPair(b);
            a.SetActive(active);
            b.SetActive(active);
            return manager.RegisterPair(PortalKind.Locaversal, a, b);
        }

        [UnityTest]
        public IEnumerator PairExpiresAfterThirtySeconds()
        {
            PortalLifecycleManager manager = CreateManager();
            PortalPair pair = CreateCompletePair(manager, "Timed");

            manager.Tick(15f);
            Assert.That(pair.IsComplete, Is.True);
            Assert.That(manager.HasActivePair, Is.True);
            Assert.That(manager.ActivePair, Is.SameAs(pair));

            manager.Tick(15f);
            Assert.That(pair.IsComplete, Is.False, "Both endpoints must close on expiry.");
            Assert.That(manager.HasActivePair, Is.False);

            yield break;
        }

        [UnityTest]
        public IEnumerator PersistentPairSurvivesExpiryAndResumesRemainingTime()
        {
            PortalLifecycleManager manager = CreateManager();
            PortalPair pair = CreateCompletePair(manager, "Persistent");

            manager.Tick(10f);
            Assert.That(pair.RemainingLifetime, Is.EqualTo(20f).Within(0.01f));

            manager.TogglePersistence();
            Assert.That(pair.Persistent, Is.True);
            manager.Tick(100f);
            Assert.That(pair.IsComplete, Is.True);
            Assert.That(pair.RemainingLifetime, Is.EqualTo(20f).Within(0.01f),
                "Remaining lifetime must be preserved while persistent.");

            manager.TogglePersistence();
            Assert.That(pair.Persistent, Is.False);
            manager.Tick(19f);
            Assert.That(pair.IsComplete, Is.True, "Toggling must not close the portal early.");

            manager.Tick(1f);
            Assert.That(pair.IsComplete, Is.False);

            yield break;
        }

        [UnityTest]
        public IEnumerator NewPairsCreatedWhilePersistentStartWithFullLifetime()
        {
            PortalLifecycleManager manager = CreateManager();
            manager.TogglePersistence();

            PortalPair pair = CreateCompletePair(manager, "NewPersistent");
            Assert.That(pair.Persistent, Is.True);
            Assert.That(pair.RemainingLifetime, Is.EqualTo(30f).Within(0.01f));
            manager.Tick(60f);
            Assert.That(pair.IsComplete, Is.True);

            manager.TogglePersistence();
            Assert.That(pair.RemainingLifetime, Is.EqualTo(30f).Within(0.01f));

            yield break;
        }

        [UnityTest]
        public IEnumerator CloseAll_ClosesActivePairAndDeactivatesEndpoints()
        {
            PortalLifecycleManager manager = CreateManager();
            PortalPair pair = CreateCompletePair(manager, "Only");

            manager.CloseAll();

            Assert.That(manager.HasActivePair, Is.False);
            Assert.That(manager.ActivePair, Is.Null);
            Assert.That(pair.IsComplete, Is.False);
            Assert.That(pair.Entrance.IsActive && pair.Exit.IsActive, Is.False);

            yield break;
        }

        [UnityTest]
        public IEnumerator RegisteringAnotherPair_ReplacesTheExistingOne()
        {
            PortalLifecycleManager manager = CreateManager();
            PortalPair first = CreateCompletePair(manager, "First");
            PortalPair second = CreateCompletePair(manager, "Second");

            Assert.That(manager.HasActivePair, Is.True);
            Assert.That(manager.ActivePair, Is.SameAs(second));
            Assert.That(first.IsComplete, Is.False, "A replaced pair must close both endpoints.");
            Assert.That(first.Entrance.IsActive && first.Exit.IsActive, Is.False);
            Assert.That(second.IsComplete, Is.True);

            yield break;
        }

        [UnityTest]
        public IEnumerator GetOrCreatePair_ReusesTheCurrentIncompletePair()
        {
            PortalLifecycleManager manager = CreateManager();
            PortalPair pair = CreateCompletePair(manager, "Incomplete", active: false);

            Assert.That(manager.GetOrCreatePair(PortalKind.Locaversal), Is.SameAs(pair));
            Assert.That(manager.ActivePair, Is.SameAs(pair));
            Assert.That(manager.GetEndpointForFire(pair, true), Is.SameAs(pair.Entrance));
            Assert.That(manager.GetEndpointForFire(pair, false), Is.SameAs(pair.Exit));

            yield break;
        }
    }
}
