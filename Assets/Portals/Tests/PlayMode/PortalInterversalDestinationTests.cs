using System;
using System.Collections;
using System.Collections.Generic;
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
    /// PlayMode coverage of the first Interversal destination. Loads MAINSCENE
    /// (which bootstraps UniverseManager, PortalUniverseBridge and authored
    /// Interversal portals) and verifies end-to-end universe traversal:
    /// forward and reverse transitions, deterministic content generation,
    /// pair lifecycle safety and invalid-transition recovery.
    /// </summary>
    public class PortalInterversalDestinationTests
    {
        private readonly List<GameObject> m_Tracked = new List<GameObject>();
        private bool m_Loaded;

        private GameObject Track(GameObject gameObject)
        {
            m_Tracked.Add(gameObject);
            return gameObject;
        }

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            AsyncOperation load = SceneManager.LoadSceneAsync("MAINSCENE", LoadSceneMode.Single);
            Assert.IsNotNull(load, "MAINSCENE must be in Build Settings for this test.");
            yield return load;
            m_Loaded = true;

            // Let GameBootstrap / PortalSceneSetup settle.
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

            Scene cleanup = SceneManager.CreateScene("PortalInterversalDestinationCleanup");
            SceneManager.SetActiveScene(cleanup);

            if (m_Loaded)
            {
                Scene main = SceneManager.GetSceneByName("MAINSCENE");
                if (main.IsValid() && main.isLoaded)
                    yield return SceneManager.UnloadSceneAsync(main);
            }
        }

        #region Universe registration

        [UnityTest]
        public IEnumerator TwoDistinctUniversesCanBeRegisteredAndResolved()
        {
            var system = InterversalPortalSystem.Instance;
            Assert.IsNotNull(system, "InterversalPortalSystem must exist after bootstrap.");

            UniverseId origin = new UniverseId("Universe 1");
            UniverseId destination = new UniverseId("Universe 2");

            var originState = system.GetOrCreateUniverse(origin);
            var destState = system.GetOrCreateUniverse(destination);

            Assert.IsNotNull(originState, "Origin universe state must be creatable.");
            Assert.IsNotNull(destState, "Destination universe state must be creatable.");
            Assert.AreNotEqual(origin, destState.Id, "Origin and destination must be distinct identities.");
            Assert.AreEqual(origin, originState.Id);
            Assert.AreEqual(destination, destState.Id);

            Assert.AreSame(originState, system.GetOrCreateUniverse(origin));
            Assert.AreSame(destState, system.GetOrCreateUniverse(destination));

            Assert.IsTrue(system.TryGetUniverse(origin, out UniverseState resolvedOrigin));
            Assert.IsTrue(system.TryGetUniverse(destination, out UniverseState resolvedDest));
            Assert.AreSame(originState, resolvedOrigin);
            Assert.AreSame(destState, resolvedDest);
            yield break;
        }

        #endregion

        #region Portal pair universe identities

        [UnityTest]
        public IEnumerator InterversalPortalPairStoresCorrectUniverses()
        {
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

            Assert.AreEqual(origin, portalA.Universe,
                "Portal A must own the origin universe identity.");
            Assert.AreEqual(destination, portalB.Universe,
                "Portal B must own the destination universe identity.");

            Assert.AreSame(portalB, portalA.GetLinkedEndpoint());
            Assert.AreSame(portalA, portalB.GetLinkedEndpoint());
        }

        #endregion

        #region Forward traversal

        [UnityTest]
        public IEnumerator TraversingForwardEndpointChangesActiveUniverse()
        {
            var universeManager = UnityEngine.Object.FindFirstObjectByType<UniverseManager>();
            Assert.IsNotNull(universeManager, "UniverseManager must exist after bootstrap.");

            // Start in the origin universe (Prime).
            universeManager.EnterUniverse(UniverseCoordinate.Prime.ToString());
            yield return null;
            Assert.AreEqual(UniverseCoordinate.Prime, universeManager.Current,
                "Active universe must start at Prime (origin).");

            // Create interversal pair with distinct universes.
            UniverseId origin = new UniverseId("Universe 1");
            UniverseId destination = new UniverseId("Universe 2");

            GameObject portalAObj = Track(new GameObject("Test Portal A"));
            portalAObj.transform.position = new Vector3(-5f, 2f, 0f);
            // Face +Z so the player moves along Z to cross the plane.
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

            // Create a player in front of portal A (at negative Z), facing +Z.
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
            // Let the bridge's Update() subscribe to this new traverser.
            yield return null;
            traverser.TickDetection(); // seed previous position

            // Move into portal A along +Z (forward traversal).
            controller.Move(Vector3.forward * 6f);
            bool traversed = traverser.TickDetection();
            Assert.IsTrue(traversed, "Player must traverse portal A.");

            // Active universe must change to the destination coordinate.
            var destCoord = new UniverseCoordinate(1, 0, 0);
            Assert.AreEqual(destCoord, universeManager.Current,
                "Active universe must change to destination after forward traversal.");

            // Player must remain in a valid playable state.
            Assert.IsNotNull(controller, "Player CharacterController must survive traversal.");
            Assert.IsNotNull(traverser, "Player PortalTraverser must survive traversal.");
        }

        #endregion

        #region Reverse traversal

        [UnityTest]
        public IEnumerator TraversingReverseEndpointRestoresOrigin()
        {
            var universeManager = UnityEngine.Object.FindFirstObjectByType<UniverseManager>();
            Assert.IsNotNull(universeManager, "UniverseManager must exist after bootstrap.");

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

            GameObject player = Track(new GameObject("Player"));
            // Start near portal B (at positive Z side), facing -Z to approach from front.
            player.transform.position = new Vector3(5f, 2f, 5f);
            player.transform.rotation = Quaternion.identity;
            var controller = player.AddComponent<CharacterController>();
            controller.height = 2f;
            controller.radius = 0.5f;
            controller.center = new Vector3(0f, 1f, 0f);
            var traverser = player.AddComponent<PortalTraverser>();
            traverser.AutoDetect = false;
            traverser.SetPortals(portalA, portalB);
            traverser.TickDetection(); // seed

            // Traverse from B back to A (reverse) - move along -Z.
            controller.Move(Vector3.back * 6f);
            bool traversed = traverser.TickDetection();
            Assert.IsTrue(traversed, "Player must traverse portal B in reverse.");

            // Active universe must restore to origin (Prime).
            Assert.AreEqual(UniverseCoordinate.Prime, universeManager.Current,
                "Active universe must restore to origin after reverse traversal.");
        }

        #endregion

        #region Deterministic generation

        [UnityTest]
        public IEnumerator ReEnteringUniversePreservesDeterministicContent()
        {
            var universeManager = UnityEngine.Object.FindFirstObjectByType<UniverseManager>();
            var generator = universeManager.GetComponent<UniverseContentGenerator>();
            Assert.IsNotNull(generator, "UniverseContentGenerator must exist.");

            var destCoord = new UniverseCoordinate(1, 0, 0);

            // First entry.
            universeManager.EnterUniverse(destCoord.ToString());
            yield return null;
            UniverseModifier firstModifier = generator.LastModifier;

            // Leave and re-enter.
            universeManager.EnterUniverse(UniverseCoordinate.Prime.ToString());
            yield return null;
            universeManager.EnterUniverse(destCoord.ToString());
            yield return null;
            UniverseModifier secondModifier = generator.LastModifier;

            Assert.AreEqual(firstModifier, secondModifier,
                "Re-entering a universe must produce the same deterministic modifier.");
        }

        #endregion

        #region Pair lifecycle safety

        [UnityTest]
        public IEnumerator ClosingPortalPairDoesNotCorruptActiveUniverse()
        {
            var universeManager = UnityEngine.Object.FindFirstObjectByType<UniverseManager>();
            var lifecycle = UnityEngine.Object.FindFirstObjectByType<PortalLifecycleManager>();
            Assert.IsNotNull(universeManager, "UniverseManager must exist.");
            Assert.IsNotNull(lifecycle, "PortalLifecycleManager must exist.");

            // Enter the destination universe first.
            universeManager.EnterUniverse(new UniverseCoordinate(1, 0, 0).ToString());
            yield return null;
            Assert.AreEqual(new UniverseCoordinate(1, 0, 0), universeManager.Current);

            // Close the interversal pair.
            var pair = lifecycle.GetOrCreatePair(PortalKind.Interversal);
            Assert.IsNotNull(pair, "Interversal pair must exist.");
            lifecycle.ClosePair(pair);
            yield return null;
            yield return null;

            // Active universe must remain intact.
            Assert.AreEqual(new UniverseCoordinate(1, 0, 0), universeManager.Current,
                "Active universe must not be corrupted by closing the portal pair.");
        }

        #endregion

        #region Invalid transition recovery

        [UnityTest]
        public IEnumerator FailedOrInvalidTransitionLeavesPlayerValid()
        {
            var universeManager = UnityEngine.Object.FindFirstObjectByType<UniverseManager>();
            Assert.IsNotNull(universeManager, "UniverseManager must exist.");

            // Enter origin first.
            universeManager.EnterUniverse(UniverseCoordinate.Prime.ToString());
            yield return null;
            UniverseCoordinate before = universeManager.Current;

            // Traverse with an unlinked / inactive portal: no universe change should occur.
            GameObject portalObject = Track(new GameObject("Unlinked Interversal Portal"));
            portalObject.transform.position = new Vector3(0f, 2f, 0f);
            var unlinked = portalObject.AddComponent<InterversalPortal>();
            unlinked.Configure(new UniverseId("Universe 99"), null,
                InterversalMappingMode.ViaPortalFrame, Vector3.zero);
            unlinked.SetActive(false); // inactive -> not traversable

            GameObject player = Track(new GameObject("Player"));
            player.transform.position = new Vector3(0f, 2f, -5f);
            var controller = player.AddComponent<CharacterController>();
            controller.height = 2f;
            controller.radius = 0.5f;
            controller.center = new Vector3(0f, 1f, 0f);
            var traverser = player.AddComponent<PortalTraverser>();
            traverser.AutoDetect = false;
            traverser.SetPortals(unlinked);
            traverser.TickDetection(); // seed

            // Try to traverse into an inactive portal: should not traverse.
            controller.Move(Vector3.forward * 6f);
            bool traversed = traverser.TickDetection();
            Assert.IsFalse(traversed, "Inactive portal must not be traversable.");

            // Universe must be unchanged.
            Assert.AreEqual(before, universeManager.Current,
                "Failed transition must leave the active universe unchanged.");

            // Player must remain valid.
            Assert.IsNotNull(controller, "Player must remain valid after failed transition.");
            Assert.IsNotNull(traverser, "Traverser must remain valid after failed transition.");
        }

        #endregion
    }
}
