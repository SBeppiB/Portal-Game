using System.Collections;
using Game;
using NUnit.Framework;
using Portals.Game;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Portals.Tests
{
    /// <summary>
    /// End-to-end coverage of the real gameplay bootstrap. Loads the actual
    /// MAINSCENE through the engine so the production hooks run exactly as they do
    /// in a built player:
    ///   * GameFlow (BeforeSceneLoad) creates the single persistent host.
    ///   * GameBootstrap (sceneLoaded) creates the gameplay systems, UI and menu.
    ///
    /// This is deliberately NOT a fake, test-only bootstrap: it asserts the same
    /// objects the shipping scene produces, and that none of them are duplicated.
    ///
    /// Note: the portal-system AfterSceneLoad hook runs once at play start, so it
    /// does not re-run for a MAINSCENE loaded later by this test; portal wiring is
    /// covered by the dedicated portal tests instead.
    /// </summary>
    public class GameBootstrapFlowTests
    {
        private bool m_Loaded;
        private GameMenuController m_PreexistingMenuController;
        private bool m_InitialInGameplayScene;
        private bool m_InitialDiagnosticsVisible;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            m_InitialInGameplayScene = GameplayInput.InGameplayScene;
            m_InitialDiagnosticsVisible = GameplayInput.DiagnosticsVisible;

            // Remember a menu controller that already existed so cleanup only
            // removes one this test's bootstrap added.
            GameObject existingHost = GameObject.Find("Portal Game");
            m_PreexistingMenuController = existingHost != null
                ? existingHost.GetComponent<GameMenuController>()
                : null;

            AsyncOperation load = SceneManager.LoadSceneAsync("MAINSCENE", LoadSceneMode.Single);
            Assert.IsNotNull(load, "MAINSCENE must be in Build Settings for this test.");
            yield return load;
            m_Loaded = true;

            // GameBootstrap.Awake runs from the sceneLoaded callback; let it settle.
            yield return null;
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            // Remove a menu controller this test's bootstrap added, preserving any
            // controller that already existed before the test.
            GameObject host = GameObject.Find("Portal Game");
            if (host != null)
            {
                GameMenuController current = host.GetComponent<GameMenuController>();
                if (current != null && current != m_PreexistingMenuController)
                    Object.Destroy(current);
            }

            // Create a clean active scene, then unload MAINSCENE so later tests
            // start from an empty scene instead of the gameplay world.
            Scene cleanup = SceneManager.CreateScene("GameplayBootstrapCleanup");
            SceneManager.SetActiveScene(cleanup);

            if (m_Loaded)
            {
                Scene main = SceneManager.GetSceneByName("MAINSCENE");
                if (main.IsValid() && main.isLoaded)
                    yield return SceneManager.UnloadSceneAsync(main);
            }

            Scene active = SceneManager.GetActiveScene();
            if (!active.IsValid() || active.name == "MAINSCENE")
            {
                Scene fresh = SceneManager.CreateScene("GameplayBootstrapCleanup");
                SceneManager.SetActiveScene(fresh);
            }

            GameplayInput.InGameplayScene = m_InitialInGameplayScene;
            GameplayInput.DiagnosticsVisible = m_InitialDiagnosticsVisible;
        }

        [UnityTest]
        public IEnumerator MainScene_BootstrapsSingleHostAndPlayerSystems()
        {
            // --- GameFlow initialisation ---
            Assert.IsTrue(GameFlow.InGameplay,
                "Loading MAINSCENE must put GameFlow into the Gameplay state.");

            GameObject host = GameObject.Find("Portal Game");
            Assert.IsNotNull(host, "GameFlow must create the persistent 'Portal Game' host.");
            Assert.AreEqual(1, Object.FindObjectsByType<GameHost>(FindObjectsSortMode.None).Length,
                "Exactly one GameHost must exist after bootstrap.");

            // --- GameBootstrap runs exactly once ---
            Assert.AreEqual(1, Object.FindObjectsByType<GameBootstrap>(FindObjectsSortMode.None).Length,
                "GameBootstrap must run exactly once for MAINSCENE.");

            // --- Player-facing UI is never duplicated ---
            Assert.AreEqual(1, Object.FindObjectsByType<CraftingUI>(FindObjectsSortMode.None).Length,
                "CraftingUI must not be duplicated by bootstrap.");
            Assert.AreEqual(1, Object.FindObjectsByType<HomeBaseUI>(FindObjectsSortMode.None).Length,
                "HomeBaseUI must not be duplicated by bootstrap.");
            Assert.AreEqual(1, Object.FindObjectsByType<UniverseSelectionUI>(FindObjectsSortMode.None).Length,
                "UniverseSelectionUI must not be duplicated by bootstrap.");
            Assert.AreEqual(1, Object.FindObjectsByType<GameMenuController>(FindObjectsSortMode.None).Length,
                "GameMenuController must be attached exactly once.");

            // --- Player-facing gameplay systems are available ---
            Assert.IsNotNull(Object.FindFirstObjectByType<PortalTuner>(),
                "The tuner must be available after bootstrap.");
            Assert.IsNotNull(Object.FindFirstObjectByType<HomeBaseManager>(),
                "The home base must be available after bootstrap.");
            Assert.IsNotNull(Object.FindFirstObjectByType<UniverseManager>(),
                "The universe manager must be available after bootstrap.");
            Assert.IsNotNull(Object.FindFirstObjectByType<CraftingStation>(),
                "The crafting station must be available after bootstrap.");
            Assert.IsNotNull(Object.FindFirstObjectByType<PortalUniverseBridge>(),
                "The universe bridge must be available after bootstrap.");

            GameObject player = GameObject.Find("Player");
            Assert.IsNotNull(player, "The scene player must exist after bootstrap.");
            Assert.IsNotNull(player.GetComponent<ResourceInventory>(),
                "The player must carry a ResourceInventory after bootstrap.");
            Assert.IsNotNull(player.GetComponent<PlayerSaveState>(),
                "The player must be save-aware (PlayerSaveState) after bootstrap.");

            yield return null;
        }
    }
}