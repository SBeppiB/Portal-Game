using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using Portals.Core;
using Portals.Energy;
using Portals.Integration;
using Portals.Traversal;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Portals.Tests
{
    /// <summary>
    /// End-to-end coverage of the automatic playable bootstrap. Loads the actual
    /// MAINSCENE through the engine and asserts that the production GameBootstrap
    /// hooks (PortalSceneSetup.EnsureSetup) produce a playable first-person portal
    /// experience without manual invocation.
    ///
    /// This is deliberately NOT a fake, test-only bootstrap: it asserts the same
    /// objects the shipping scene produces, and that none of them are duplicated.
    /// </summary>
    public class PortalAutoBootstrapTests
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

            // Let GameBootstrap.Awake (and EnsurePortalBootstrap) settle.
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

            Scene cleanup = SceneManager.CreateScene("PortalAutoBootstrapCleanup");
            SceneManager.SetActiveScene(cleanup);

            if (m_Loaded)
            {
                Scene main = SceneManager.GetSceneByName("MAINSCENE");
                if (main.IsValid() && main.isLoaded)
                    yield return SceneManager.UnloadSceneAsync(main);
            }
        }

        [UnityTest]
        public IEnumerator MainScene_Startup_AutomaticallyCreatesOrFindsPlayer()
        {
            yield return null;

            GameObject player = GameObject.Find("Player");
            Assert.IsNotNull(player, "MAINSCENE startup must provide a Player GameObject.");

            var controller = player.GetComponent<CharacterController>();
            Assert.IsNotNull(controller, "Player must have a CharacterController.");
        }

        [UnityTest]
        public IEnumerator MainScene_Startup_PlayerHasMovementComponents()
        {
            yield return null;

            GameObject player = GameObject.Find("Player");
            Assert.IsNotNull(player, "Player must exist.");

            Assert.IsNotNull(player.GetComponent(Type.GetType("PlayerMovementManager, Assembly-CSharp")),
                "Player must have PlayerMovementManager.");
            Assert.IsNotNull(player.GetComponent(Type.GetType("BasicMovementController, Assembly-CSharp")),
                "Player must have BasicMovementController.");
            Assert.IsNotNull(player.GetComponent(Type.GetType("CameraLookController, Assembly-CSharp")),
                "Player must have CameraLookController.");
        }

        [UnityTest]
        public IEnumerator MainScene_Startup_CameraIsConfigured()
        {
            yield return null;

            GameObject player = GameObject.Find("Player");
            Assert.IsNotNull(player, "Player must exist.");

            Component cameraLookComp = player.GetComponent(Type.GetType("CameraLookController, Assembly-CSharp"));
            Assert.IsNotNull(cameraLookComp, "Player must have CameraLookController.");
            var cameraLookField = cameraLookComp.GetType().GetField("playerCamera");
            Assert.IsNotNull(cameraLookField, "CameraLookController must expose playerCamera field.");
            Assert.IsNotNull(cameraLookField.GetValue(cameraLookComp),
                "CameraLookController must have an assigned player camera.");

            Camera cam = (Camera)cameraLookField.GetValue(cameraLookComp);
            Assert.AreEqual(player.transform, cam.transform.parent,
                "The main camera must be parented to the player for mouse look.");
        }

        [UnityTest]
        public IEnumerator MainScene_Startup_PortalSystemsAreInitialized()
        {
            yield return null;

            Assert.IsNotNull(UnityEngine.Object.FindFirstObjectByType<LocaversalPortalSystem>(),
                "LocaversalPortalSystem must exist after bootstrap.");
            Assert.IsNotNull(UnityEngine.Object.FindFirstObjectByType<InterversalPortalSystem>(),
                "InterversalPortalSystem must exist after bootstrap.");
            Assert.IsNotNull(UnityEngine.Object.FindFirstObjectByType<PortalLifecycleManager>(),
                "PortalLifecycleManager must exist after bootstrap.");
            Assert.IsNotNull(UnityEngine.Object.FindFirstObjectByType<PortalPlacer>(),
                "PortalPlacer must exist after bootstrap.");
            Assert.IsNotNull(UnityEngine.Object.FindFirstObjectByType<PortalFiringController>(),
                "PortalFiringController must exist after bootstrap.");
            Assert.IsNotNull(UnityEngine.Object.FindFirstObjectByType<PortalTraverser>(),
                "PortalTraverser must exist after bootstrap.");
            Assert.IsNotNull(UnityEngine.Object.FindFirstObjectByType<PortalHUD>(),
                "PortalHUD must exist after bootstrap.");
        }

        [UnityTest]
        public IEnumerator MainScene_Startup_PortalPairIsLinked()
        {
            yield return null;

            LocaversalPortal portalA = GameObject.Find("Portal A")?.GetComponent<LocaversalPortal>();
            LocaversalPortal portalB = GameObject.Find("Portal B")?.GetComponent<LocaversalPortal>();

            Assert.IsNotNull(portalA, "Portal A must exist after bootstrap.");
            Assert.IsNotNull(portalB, "Portal B must exist after bootstrap.");

            Assert.IsTrue(portalA.HasConfiguredLink, "Portal A must be linked to Portal B.");
            Assert.IsTrue(portalB.HasConfiguredLink, "Portal B must be linked to Portal A.");
            Assert.AreSame(portalB, portalA.PairedPortal, "Portal A's paired portal must be Portal B.");
            Assert.AreSame(portalA, portalB.PairedPortal, "Portal B's paired portal must be Portal A.");
        }

        [UnityTest]
        public IEnumerator MainScene_Startup_EnergyPermitsTwoPortalPlacements()
        {
            yield return null;

            EnergyStorage storage = UnityEngine.Object.FindFirstObjectByType<EnergyStorage>();
            Assert.IsNotNull(storage, "EnergyStorage must exist after bootstrap.");

            PortalEnergySystem energy = UnityEngine.Object.FindFirstObjectByType<PortalEnergySystem>();
            Assert.IsNotNull(energy, "PortalEnergySystem must exist after bootstrap.");

            float locaversalCost = energy.Costs.GetCreationCost(PortalKind.Locaversal);
            float interversalCost = energy.Costs.GetCreationCost(PortalKind.Interversal);
            float totalCost = locaversalCost + interversalCost;

            Assert.GreaterOrEqual(storage.Current, totalCost,
                $"Starting energy ({storage.Current:0.0}) must cover both portal placements ({totalCost:0.0}).");

            Assert.IsTrue(energy.CanAfford(PortalKind.Locaversal),
                "Energy system must afford a Locaversal portal placement.");
            Assert.IsTrue(energy.CanAfford(PortalKind.Interversal),
                "Energy system must afford an Interversal portal placement.");
        }

        [UnityTest]
        public IEnumerator MainScene_Startup_RepeatedBootstrapDoesNotDuplicate()
        {
            yield return null;

            // Count before repeated call.
            int portalsBefore = UnityEngine.Object.FindObjectsByType<LocaversalPortal>(FindObjectsSortMode.None).Length;
            int systemsBefore = UnityEngine.Object.FindObjectsByType<PortalLifecycleManager>(FindObjectsSortMode.None).Length;
            int energyBefore = UnityEngine.Object.FindObjectsByType<EnergyStorage>(FindObjectsSortMode.None).Length;

            // Invoke setup again directly (idempotent).
            PortalSceneSetup.EnsureSetup(out string message);
            Assert.IsNotNull(message, "EnsureSetup must return a status message.");

            yield return null;

            int portalsAfter = UnityEngine.Object.FindObjectsByType<LocaversalPortal>(FindObjectsSortMode.None).Length;
            int systemsAfter = UnityEngine.Object.FindObjectsByType<PortalLifecycleManager>(FindObjectsSortMode.None).Length;
            int energyAfter = UnityEngine.Object.FindObjectsByType<EnergyStorage>(FindObjectsSortMode.None).Length;

            Assert.AreEqual(portalsBefore, portalsAfter,
                "Repeated bootstrap must not create duplicate Locaversal portals.");
            Assert.AreEqual(systemsBefore, systemsAfter,
                "Repeated bootstrap must not create duplicate PortalLifecycleManager instances.");
            Assert.AreEqual(energyBefore, energyAfter,
                "Repeated bootstrap must not create duplicate EnergyStorage instances.");
        }

        [UnityTest]
        public IEnumerator MainScene_Startup_PlayerHasPortalComponents()
        {
            yield return null;

            GameObject player = GameObject.Find("Player");
            Assert.IsNotNull(player, "Player must exist.");

            Assert.IsNotNull(player.GetComponent<PortalPlacer>(),
                "Player must have PortalPlacer.");
            Assert.IsNotNull(player.GetComponent<PortalFiringController>(),
                "Player must have PortalFiringController.");
            Assert.IsNotNull(player.GetComponent<PortalTraverser>(),
                "Player must have PortalTraverser.");
            Assert.IsNotNull(player.GetComponent<PortalHUD>(),
                "Player must have PortalHUD.");
        }
    }
}
