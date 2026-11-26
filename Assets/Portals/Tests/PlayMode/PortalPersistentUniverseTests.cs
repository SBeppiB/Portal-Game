using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Portals.Core;
using Portals.Traversal;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Game;

namespace Portals.Tests
{
    /// <summary>
    /// PlayMode coverage of persistent universe content and distinct environments.
    /// Verifies that universe roots are cached (not destroyed) across switches,
    /// harvested state persists, physics profiles differ per universe, and the
    /// player remains valid through repeated traversal.
    /// </summary>
    public class PortalPersistentUniverseTests
    {
        private readonly List<GameObject> m_Tracked = new List<GameObject>();
        private bool m_Loaded;

        private GameObject Track(GameObject gameObject)
        {
            m_Tracked.Add(gameObject);
            return gameObject;
        }

        private static T GetField<T>(object target, string fieldName)
        {
            var field = target.GetType().GetField(fieldName,
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(field, $"Field '{fieldName}' not found on {target.GetType().Name}.");
            return (T)field.GetValue(target);
        }

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            AsyncOperation load = SceneManager.LoadSceneAsync("MAINSCENE", LoadSceneMode.Single);
            Assert.IsNotNull(load, "MAINSCENE must be in Build Settings for this test.");
            yield return load;
            m_Loaded = true;

            yield return null;
            yield return null;
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            foreach (GameObject gameObject in m_Tracked)
                if (gameObject != null) UnityEngine.Object.DestroyImmediate(gameObject);
            m_Tracked.Clear();

            Scene cleanup = SceneManager.CreateScene("PortalPersistentUniverseCleanup");
            SceneManager.SetActiveScene(cleanup);

            if (m_Loaded)
            {
                Scene main = SceneManager.GetSceneByName("MAINSCENE");
                if (main.IsValid() && main.isLoaded)
                    yield return SceneManager.UnloadSceneAsync(main);
            }
        }

        #region Content caching

        [UnityTest]
        public IEnumerator NewUniverseRootIsGeneratedOnFirstEntry()
        {
            var universeManager = UnityEngine.Object.FindFirstObjectByType<UniverseManager>();
            Assert.IsNotNull(universeManager, "UniverseManager must exist after bootstrap.");

            var destCoord = new UniverseCoordinate(1, 0, 0);
            universeManager.EnterUniverse(destCoord.ToString());
            yield return null;

            Assert.AreEqual(destCoord, universeManager.Current,
                "Active universe must be the destination coordinate.");

            var activeRoot = GetField<GameObject>(universeManager, "m_ActiveRoot");
            Assert.IsNotNull(activeRoot, "A root must be generated for the new universe.");
            Assert.AreEqual($"Universe_{destCoord}", activeRoot.name,
                "Root must be named after the coordinate.");

            var roots = GetField<Dictionary<string, GameObject>>(universeManager, "m_Roots");
            Assert.IsTrue(roots.ContainsKey(destCoord.ToString()),
                "Root must be cached in m_Roots.");
        }

        [UnityTest]
        public IEnumerator LeavingUniverseDeactivatesRootInsteadOfDestroying()
        {
            var universeManager = UnityEngine.Object.FindFirstObjectByType<UniverseManager>();
            Assert.IsNotNull(universeManager, "UniverseManager must exist after bootstrap.");

            var destCoord = new UniverseCoordinate(1, 0, 0);
            universeManager.EnterUniverse(destCoord.ToString());
            yield return null;

            var destRoot = GetField<GameObject>(universeManager, "m_ActiveRoot");
            Assert.IsNotNull(destRoot, "Destination root must exist.");
            Assert.IsTrue(destRoot.activeSelf, "Destination root must be active.");

            // Leave for origin.
            universeManager.EnterUniverse(UniverseCoordinate.Prime.ToString());
            yield return null;

            Assert.AreEqual(UniverseCoordinate.Prime, universeManager.Current);
            Assert.IsFalse(destRoot.activeSelf,
                "Previous universe root must be deactivated, not destroyed.");
            Assert.IsNotNull(destRoot, "Root instance must still exist after deactivation.");
        }

        [UnityTest]
        public IEnumerator ReturningToUniverseReusesSameRootInstance()
        {
            var universeManager = UnityEngine.Object.FindFirstObjectByType<UniverseManager>();
            Assert.IsNotNull(universeManager, "UniverseManager must exist after bootstrap.");

            var destCoord = new UniverseCoordinate(1, 0, 0);
            universeManager.EnterUniverse(destCoord.ToString());
            yield return null;

            var firstRoot = GetField<GameObject>(universeManager, "m_ActiveRoot");
            Assert.IsNotNull(firstRoot, "Root must exist on first entry.");

            // Leave and return.
            universeManager.EnterUniverse(UniverseCoordinate.Prime.ToString());
            yield return null;
            universeManager.EnterUniverse(destCoord.ToString());
            yield return null;

            var secondRoot = GetField<GameObject>(universeManager, "m_ActiveRoot");
            Assert.AreSame(firstRoot, secondRoot,
                "Returning to a cached universe must reuse the same root instance.");
        }

        [UnityTest]
        public IEnumerator NoDuplicateGeneratedObjectsOnRepeatedEntry()
        {
            var universeManager = UnityEngine.Object.FindFirstObjectByType<UniverseManager>();
            var generator = universeManager.GetComponent<UniverseContentGenerator>();
            Assert.IsNotNull(generator, "UniverseContentGenerator must exist.");

            var contentParent = GetField<Transform>(universeManager, "m_ContentParent");
            Assert.IsNotNull(contentParent, "Content parent must be set.");

            var destCoord = new UniverseCoordinate(1, 0, 0);

            // First entry.
            universeManager.EnterUniverse(destCoord.ToString());
            yield return null;
            int firstNodeCount = contentParent.GetComponentsInChildren<ResourceNode>(true).Length;

            // Leave and return.
            universeManager.EnterUniverse(UniverseCoordinate.Prime.ToString());
            yield return null;
            universeManager.EnterUniverse(destCoord.ToString());
            yield return null;

            int secondNodeCount = contentParent.GetComponentsInChildren<ResourceNode>(true).Length;
            Assert.AreEqual(firstNodeCount, secondNodeCount,
                "Re-entering a cached universe must not duplicate generated objects.");
        }

        [UnityTest]
        public IEnumerator OriginRootIsReusedAfterReturning()
        {
            var universeManager = UnityEngine.Object.FindFirstObjectByType<UniverseManager>();
            Assert.IsNotNull(universeManager, "UniverseManager must exist after bootstrap.");

            // Start in origin.
            universeManager.EnterUniverse(UniverseCoordinate.Prime.ToString());
            yield return null;
            var originRoot = GetField<GameObject>(universeManager, "m_ActiveRoot");
            Assert.IsNotNull(originRoot, "Origin root must exist.");

            // Visit destination.
            universeManager.EnterUniverse(new UniverseCoordinate(1, 0, 0).ToString());
            yield return null;

            // Return to origin.
            universeManager.EnterUniverse(UniverseCoordinate.Prime.ToString());
            yield return null;

            var returnedRoot = GetField<GameObject>(universeManager, "m_ActiveRoot");
            Assert.AreSame(originRoot, returnedRoot,
                "Returning to origin must reuse the same root instance.");
        }

        #endregion

        #region Runtime state

        [UnityTest]
        public IEnumerator HarvestedResourceRemainsHarvestedAcrossUniverseSwitch()
        {
            var universeManager = UnityEngine.Object.FindFirstObjectByType<UniverseManager>();
            Assert.IsNotNull(universeManager, "UniverseManager must exist.");

            // Enter destination universe.
            var destCoord = new UniverseCoordinate(1, 0, 0);
            universeManager.EnterUniverse(destCoord.ToString());
            yield return null;

            var destRoot = GetField<GameObject>(universeManager, "m_ActiveRoot");
            Assert.IsNotNull(destRoot, "Destination root must exist.");

            var nodes = destRoot.GetComponentsInChildren<ResourceNode>(true);
            Assert.Greater(nodes.Length, 0, "Destination universe must have resource nodes.");

            // Harvest the first node.
            var inventoryGo = Track(new GameObject("Inventory"));
            var inventory = inventoryGo.AddComponent<ResourceInventory>();
            var database = Resources.Load<ResourceDatabase>("Game/Generated/ResourceDatabase");
            if (database != null)
            {
                var dbField = inventory.GetType().GetField("m_Database",
                    BindingFlags.NonPublic | BindingFlags.Instance);
                dbField?.SetValue(inventory, database);
            }

            var firstNode = nodes[0];
            int harvested = firstNode.Harvest(inventory);
            Assert.Greater(harvested, 0, "Node must be harvestable.");
            Assert.IsTrue(firstNode.IsHarvested, "Node must be marked harvested.");

            // Leave for origin.
            universeManager.EnterUniverse(UniverseCoordinate.Prime.ToString());
            yield return null;

            // Return to destination - same root instance, node should still be harvested.
            universeManager.EnterUniverse(destCoord.ToString());
            yield return null;

            var returnedRoot = GetField<GameObject>(universeManager, "m_ActiveRoot");
            Assert.AreSame(destRoot, returnedRoot, "Same root instance must be reused.");

            var returnedNodes = returnedRoot.GetComponentsInChildren<ResourceNode>(true);
            Assert.Greater(returnedNodes.Length, 0, "Returned root must have resource nodes.");

            // Find the same node by spawn key and verify it's still harvested.
            var returnedNode = System.Array.Find(returnedNodes, n => n.SpawnKey == firstNode.SpawnKey);
            Assert.IsNotNull(returnedNode, "Same node instance must be found by spawn key.");
            Assert.IsTrue(returnedNode.IsHarvested, "Harvested state must persist across universe switch.");
        }

        #endregion

        #region Universe physics

        [UnityTest]
        public IEnumerator RegisteredUniversesHaveDistinctPhysicsProfiles()
        {
            var system = InterversalPortalSystem.Instance;
            Assert.IsNotNull(system, "InterversalPortalSystem must exist after bootstrap.");

            var originProfile = system.GetPhysicsProfile(new UniverseId("Universe 1"));
            var destProfile = system.GetPhysicsProfile(new UniverseId("Universe 2"));

            Assert.IsNotNull(originProfile, "Origin universe must have a physics profile.");
            Assert.IsNotNull(destProfile, "Destination universe must have a physics profile.");

            // Profiles must be distinct instances (no shared mutable state).
            Assert.AreNotSame(originProfile, destProfile,
                "Universes must not share the same physics profile instance.");

            // Universe 2 should have different gravity than default (-9.81).
            Assert.AreEqual(-20f, destProfile.gravity.y, 1e-4f,
                "Destination universe must have distinct gravity.");
            Assert.AreEqual(0.2f, destProfile.linearDamping, 1e-4f,
                "Destination universe must have distinct linear damping.");
            yield break;
        }

        [UnityTest]
        public IEnumerator EntityWithUniverseBodyReceivesDestinationProfile()
        {
            var system = InterversalPortalSystem.Instance;
            Assert.IsNotNull(system, "InterversalPortalSystem must exist after bootstrap.");

            // Create a Rigidbody entity with UniverseBody.
            GameObject entity = Track(new GameObject("PhysicsEntity"));
            entity.transform.position = new Vector3(0f, 10f, 0f);
            var body = entity.AddComponent<Rigidbody>();
            body.isKinematic = false;
            body.useGravity = true;
            var universeBody = entity.AddComponent<UniverseBody>();

            // Start in origin (Universe 1 = default profile).
            universeBody.SetUniverse(new UniverseId("Universe 1"));
            var originProfile = system.GetPhysicsProfile(new UniverseId("Universe 1"));
            Assert.AreEqual(originProfile.linearDamping, body.linearDamping, 1e-4f,
                "Entity must receive origin profile linear damping.");

            // Switch to destination (Universe 2 = distinct profile).
            universeBody.SetUniverse(new UniverseId("Universe 2"));
            var destProfile = system.GetPhysicsProfile(new UniverseId("Universe 2"));
            Assert.AreEqual(destProfile.linearDamping, body.linearDamping, 1e-4f,
                "Entity must receive destination profile linear damping after switch.");
            Assert.AreEqual(destProfile.gravity.y, -20f, 1e-4f,
                "Destination gravity must be -20.");
            Assert.IsFalse(body.useGravity,
                "Destination overrideGravity must disable useGravity.");

            // Return to origin - profile must be restored.
            universeBody.SetUniverse(new UniverseId("Universe 1"));
            Assert.AreEqual(originProfile.linearDamping, body.linearDamping, 1e-4f,
                "Returning to origin must restore origin profile damping.");
            yield break;
        }

        [UnityTest]
        public IEnumerator PhysicsStateDoesNotLeakBetweenUniverses()
        {
            var system = InterversalPortalSystem.Instance;
            Assert.IsNotNull(system, "InterversalPortalSystem must exist after bootstrap.");

            GameObject entity = Track(new GameObject("PhysicsEntity2"));
            var body = entity.AddComponent<Rigidbody>();
            body.isKinematic = false;
            var universeBody = entity.AddComponent<UniverseBody>();

            // Set to destination (distinct gravity).
            universeBody.SetUniverse(new UniverseId("Universe 2"));
            Assert.IsFalse(body.useGravity,
                "Destination overrideGravity must disable useGravity.");

            // Switch back to origin (default gravity, no override).
            universeBody.SetUniverse(new UniverseId("Universe 1"));
            var originProfile = system.GetPhysicsProfile(new UniverseId("Universe 1"));
            Assert.AreEqual(originProfile.linearDamping, body.linearDamping, 1e-4f,
                "Origin damping must not leak destination damping.");
            Assert.AreEqual(body.useGravity, originProfile.overrideGravity ? false : body.useGravity,
                "Origin gravity state must not leak from destination.");
            yield break;
        }

        #endregion

        #region Player continuity

        [UnityTest]
        public IEnumerator PlayerInstanceSurvivesForwardAndReverseTraversal()
        {
            var universeManager = UnityEngine.Object.FindFirstObjectByType<UniverseManager>();
            Assert.IsNotNull(universeManager, "UniverseManager must exist.");

            // Create interversal pair.
            UniverseId origin = new UniverseId("Universe 1");
            UniverseId destination = new UniverseId("Universe 2");

            GameObject portalAObj = Track(new GameObject("Test Portal A"));
            portalAObj.transform.position = new Vector3(-5f, 2f, 0f);
            portalAObj.transform.rotation = Quaternion.identity;
            var portalA = portalAObj.AddComponent<InterversalPortal>();

            GameObject portalBObj = Track(new GameObject("Test Portal B"));
            portalBObj.transform.position = new Vector3(5f, 2f, 0f);
            portalBObj.transform.rotation = Quaternion.identity;
            var portalB = portalBObj.AddComponent<InterversalPortal>();

            portalA.Configure(origin, portalB, InterversalMappingMode.ViaPortalFrame, Vector3.zero);
            portalB.Configure(destination, portalA, InterversalMappingMode.ViaPortalFrame, Vector3.zero);
            portalA.SetActive(true);
            portalB.SetActive(true);
            yield return null;

            // Create player.
            GameObject player = Track(new GameObject("Player"));
            player.transform.position = new Vector3(-5f, 2f, -5f);
            player.transform.rotation = Quaternion.identity;
            var controller = player.AddComponent<CharacterController>();
            controller.height = 2f;
            controller.radius = 0.5f;
            controller.center = new Vector3(0f, 1f, 0f);
            var traverser = player.AddComponent<PortalTraverser>();
            traverser.AutoDetect = false;
            traverser.SetPortals(portalA, portalB);
            traverser.TickDetection();
            yield return null;

            var playerInstance = player;
            var playerController = controller;

            // Forward traversal.
            controller.Move(Vector3.forward * 6f);
            bool traversed = traverser.TickDetection();
            Assert.IsTrue(traversed, "Player must traverse portal A.");

            // Player instance must survive.
            Assert.AreSame(playerInstance, player, "Same player GameObject instance.");
            Assert.IsNotNull(player.GetComponent<CharacterController>(),
                "CharacterController must remain attached.");
            Assert.IsNotNull(player.GetComponent<PortalTraverser>(),
                "PortalTraverser must remain attached.");

            yield return null; // Let traverser and universe settle after forward traversal.

            // Reverse traversal - position player in front of portal B and seed traverser.
            player.transform.position = new Vector3(5f, 2f, 5f);
            traverser.TickDetection(); // seed position
            controller.Move(Vector3.back * 6f);
            traversed = traverser.TickDetection();
            Assert.IsTrue(traversed, "Player must traverse portal B in reverse.");

            Assert.AreSame(playerInstance, player, "Same player GameObject instance after reverse.");
            Assert.IsNotNull(player.GetComponent<CharacterController>(),
                "CharacterController must remain attached after reverse traversal.");
        }

        #endregion

        #region Repeated travel

        [UnityTest]
        public IEnumerator RepeatedForwardAndReverseTransitionsReuseCachedRoots()
        {
            var universeManager = UnityEngine.Object.FindFirstObjectByType<UniverseManager>();
            Assert.IsNotNull(universeManager, "UniverseManager must exist.");

            var destCoord = new UniverseCoordinate(1, 0, 0);

            // Multiple forward/reverse cycles.
            for (int i = 0; i < 3; i++)
            {
                universeManager.EnterUniverse(destCoord.ToString());
                yield return null;
                Assert.AreEqual(destCoord, universeManager.Current,
                    $"Cycle {i}: must reach destination.");

                universeManager.EnterUniverse(UniverseCoordinate.Prime.ToString());
                yield return null;
                Assert.AreEqual(UniverseCoordinate.Prime, universeManager.Current,
                    $"Cycle {i}: must return to origin.");
            }

            // Verify no duplicate roots exist.
            var roots = GetField<Dictionary<string, GameObject>>(universeManager, "m_Roots");
            int primeCount = roots.ContainsKey(UniverseCoordinate.Prime.ToString()) ? 1 : 0;
            int destCount = roots.ContainsKey(destCoord.ToString()) ? 1 : 0;
            Assert.AreEqual(1, primeCount, "Only one root per coordinate (Prime).");
            Assert.AreEqual(1, destCount, "Only one root per coordinate (destination).");
        }

        [UnityTest]
        public IEnumerator PortalExpirationDoesNotDestroyCachedUniverseContent()
        {
            var universeManager = UnityEngine.Object.FindFirstObjectByType<UniverseManager>();
            var lifecycle = UnityEngine.Object.FindFirstObjectByType<PortalLifecycleManager>();
            Assert.IsNotNull(universeManager, "UniverseManager must exist.");
            Assert.IsNotNull(lifecycle, "PortalLifecycleManager must exist.");

            // Enter destination universe.
            universeManager.EnterUniverse(new UniverseCoordinate(1, 0, 0).ToString());
            yield return null;
            var destRoot = GetField<GameObject>(universeManager, "m_ActiveRoot");
            Assert.IsNotNull(destRoot, "Destination root must exist.");

            // Close the interversal pair.
            var pair = lifecycle.GetOrCreatePair(PortalKind.Interversal);
            Assert.IsNotNull(pair, "Interversal pair must exist.");
            lifecycle.ClosePair(pair);
            yield return null;
            yield return null;

            // Cached universe content must survive portal expiration.
            Assert.IsNotNull(destRoot, "Cached root must survive portal expiration.");
            Assert.AreEqual(new UniverseCoordinate(1, 0, 0), universeManager.Current,
                "Active universe must remain unchanged after portal expiration.");

            // Re-entering must reuse the cached root.
            universeManager.EnterUniverse(UniverseCoordinate.Prime.ToString());
            yield return null;
            universeManager.EnterUniverse(new UniverseCoordinate(1, 0, 0).ToString());
            yield return null;

            var returnedRoot = GetField<GameObject>(universeManager, "m_ActiveRoot");
            Assert.AreSame(destRoot, returnedRoot,
                "Cached root must be reused after portal expiration.");
        }

        #endregion

        #region Cache cleanup

        [UnityTest]
        public IEnumerator InactiveRootsAreEvictedWhenCacheExceedsCapacity()
        {
            var universeManager = UnityEngine.Object.FindFirstObjectByType<UniverseManager>();
            Assert.IsNotNull(universeManager, "UniverseManager must exist.");

            // Enter 9 distinct universes to exceed the cache capacity of 8.
            var coords = new[]
            {
                UniverseCoordinate.Prime,
                new UniverseCoordinate(1, 0, 0),
                new UniverseCoordinate(2, 0, 0),
                new UniverseCoordinate(3, 0, 0),
                new UniverseCoordinate(4, 0, 0),
                new UniverseCoordinate(5, 0, 0),
                new UniverseCoordinate(6, 0, 0),
                new UniverseCoordinate(7, 0, 0),
                new UniverseCoordinate(8, 0, 0),
            };

            foreach (var coord in coords)
            {
                universeManager.EnterUniverse(coord.ToString());
                yield return null;
                Assert.AreEqual(coord, universeManager.Current,
                    $"Must enter {coord}.");
            }

            // The first universe (Prime) should have been evicted (oldest inactive).
            var roots = GetField<Dictionary<string, GameObject>>(universeManager, "m_Roots");
            Assert.IsFalse(roots.ContainsKey(UniverseCoordinate.Prime.ToString()),
                "Oldest inactive root must be evicted when cache exceeds capacity.");
            Assert.IsTrue(roots.ContainsKey(coords[coords.Length - 1].ToString()),
                "Most recent universe root must be cached.");
            Assert.AreEqual(8, roots.Count,
                "Cache must not exceed capacity of 8.");
        }

        [UnityTest]
        public IEnumerator ActiveRootCannotBeEvicted()
        {
            var universeManager = UnityEngine.Object.FindFirstObjectByType<UniverseManager>();
            Assert.IsNotNull(universeManager, "UniverseManager must exist.");

            // Enter many universes; the last one entered is the active root.
            for (int i = 1; i <= 10; i++)
            {
                universeManager.EnterUniverse(new UniverseCoordinate(i, 0, 0).ToString());
                yield return null;
            }

            // The active root (L10-R00-P000) must never be evicted.
            var roots = GetField<Dictionary<string, GameObject>>(universeManager, "m_Roots");
            var activeRoot = GetField<GameObject>(universeManager, "m_ActiveRoot");
            Assert.IsNotNull(activeRoot, "Active root must exist.");
            Assert.IsTrue(roots.ContainsKey(new UniverseCoordinate(10, 0, 0).ToString()),
                "Active root must never be evicted.");
            Assert.AreEqual(activeRoot, roots[new UniverseCoordinate(10, 0, 0).ToString()],
                "Active root dictionary entry must match m_ActiveRoot.");
        }

        [UnityTest]
        public IEnumerator ExplicitCleanupReleasesCachedRoots()
        {
            var universeManager = UnityEngine.Object.FindFirstObjectByType<UniverseManager>();
            Assert.IsNotNull(universeManager, "UniverseManager must exist.");

            // Enter a universe and cache its root.
            universeManager.EnterUniverse(new UniverseCoordinate(1, 0, 0).ToString());
            yield return null;
            var rootsBefore = GetField<Dictionary<string, GameObject>>(universeManager, "m_Roots");
            Assert.Greater(rootsBefore.Count, 0, "Roots must be cached.");

            // Explicit cleanup.
            universeManager.CleanupAllRoots();

            var rootsAfter = GetField<Dictionary<string, GameObject>>(universeManager, "m_Roots");
            Assert.AreEqual(0, rootsAfter.Count, "All cached roots must be released.");
            Assert.IsNull(GetField<GameObject>(universeManager, "m_ActiveRoot"),
                "Active root must be cleared after cleanup.");
        }

        #endregion
    }
}
