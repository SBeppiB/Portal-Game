using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Game;
using NUnit.Framework;
using Portals.Game;
using Portals.Save;
using UnityEngine;
using UnityEngine.TestTools;

namespace Portals.Tests
{
    /// <summary>
    /// End-to-end save/load flow: mutate the live gameplay objects, quick-save,
    /// tear the objects down, recreate them, load, and verify inventory, storage,
    /// progression, universe and player position are restored without duplication.
    ///
    /// PlayMode tests start in a non-menu scene, so GameFlow's BeforeSceneLoad hook
    /// has already put it into the Gameplay state and created the single persistent
    /// "Portal Game" host before any test runs. The tests rely on that real state
    /// instead of forcing it, and restore the input state they observe so later
    /// tests never inherit a modified global input state.
    /// </summary>
    public class GameplayFlowTests
    {
        private GameObject m_Root;
        private string m_SaveDir;
        private readonly List<ScriptableObject> m_Assets = new List<ScriptableObject>();

        // Input state observed at SetUp, restored verbatim at TearDown.
        private bool m_InitialInGameplayScene;
        private bool m_InitialDiagnosticsVisible;

        [SetUp]
        public void SetUp()
        {
            m_InitialInGameplayScene = GameplayInput.InGameplayScene;
            m_InitialDiagnosticsVisible = GameplayInput.DiagnosticsVisible;

            m_Root = new GameObject("GameplayFlowTestRoot");
            m_SaveDir = Path.Combine(Path.GetTempPath(), "portal_flow_test_" + Guid.NewGuid().ToString("N"));
            SaveSystem.SetDirectoryOverride(m_SaveDir);
            SaveStateRegistry.Clear();
            GameplayInput.ClearUiBlockers();
            GameFlow.LastUsedSlot = -1;

            Assert.IsTrue(GameFlow.InGameplay,
                "PlayMode tests run in a non-menu scene, so GameFlow must already be in Gameplay.");
        }

        [TearDown]
        public void TearDown()
        {
            SaveStateRegistry.Clear();
            GameplayInput.ClearUiBlockers();
            GameFlow.LastUsedSlot = -1;

            // Restore the global input state observed before the test, so a later
            // test cannot inherit an unexpected GameplayInput.Blocked value.
            GameplayInput.InGameplayScene = m_InitialInGameplayScene;
            GameplayInput.DiagnosticsVisible = m_InitialDiagnosticsVisible;

            SaveSystem.ClearDirectoryOverride();
            try { if (Directory.Exists(m_SaveDir)) Directory.Delete(m_SaveDir, true); } catch { }

            foreach (ScriptableObject asset in m_Assets)
                if (asset != null) UnityEngine.Object.Destroy(asset);
            m_Assets.Clear();

            if (m_Root != null) UnityEngine.Object.Destroy(m_Root);
        }

        /// <summary>
        /// Regression guard for the "duplicate host" bug: GameFlow.Initialize must
        /// not create a second "Portal Game" host, and the menu controller must be
        /// attached at most once.
        /// </summary>
        [Test]
        public void GameFlowHost_IsSingletonAndEnsureAttachedIsIdempotent()
        {
            GameObject host = GameObject.Find("Portal Game");
            Assert.IsNotNull(host, "GameFlow must create the persistent 'Portal Game' host at play start.");
            Assert.AreEqual(1, UnityEngine.Object.FindObjectsByType<GameHost>(FindObjectsSortMode.None).Length,
                "Exactly one GameHost must exist.");

            try
            {
                GameMenuController.EnsureAttached();
                GameMenuController.EnsureAttached();

                Assert.AreEqual(1, host.GetComponents<GameMenuController>().Length,
                    "EnsureAttached must not add duplicate menu controllers.");
                Assert.AreEqual(1, UnityEngine.Object.FindObjectsByType<GameHost>(FindObjectsSortMode.None).Length,
                    "Attaching the menu must not create a second host.");
            }
            finally
            {
                GameMenuController controller = host.GetComponent<GameMenuController>();
                if (controller != null) UnityEngine.Object.Destroy(controller);
            }
        }

        [UnityTest]
        public IEnumerator QuickSaveThenLoadRestoresFullGameplayState()
        {
            var resource = CreateResource("scrap", "Scrap Metal");
            var database = CreateDatabase(resource);

            GameObject player = CreatePlayer(database, out ResourceInventory playerInventory);
            GameObject tunerGo = CreateTuner(out PortalTuner tuner);
            GameObject homeGo = CreateHome(database, out HomeBaseManager home, out ResourceInventory storage);
            GameObject universeGo = CreateUniverse(out UniverseManager universe);

            playerInventory.Add(resource, 7);
            storage.Add(resource, 4);
            home.Discover(new UniverseCoordinate(1, 2, 3));
            tuner.UnlockRingTier(3); // MaxRing 6
            universe.EnterUniverse("L01-R02-P003");
            player.transform.position = new Vector3(1f, 2f, 3f);

            yield return null;

            Assert.IsTrue(GameFlow.QuickSave(out int slot, out string saveError), saveError);
            Assert.GreaterOrEqual(slot, 0);
            Assert.IsNotNull(SaveSystem.Load(slot), "Quick save must produce a loadable slot.");

            // Simulate quitting: destroy every gameplay object, then rebuild them.
            UnityEngine.Object.Destroy(player);
            UnityEngine.Object.Destroy(tunerGo);
            UnityEngine.Object.Destroy(homeGo);
            UnityEngine.Object.Destroy(universeGo);
            yield return null;
            yield return null;

            GameObject player2 = CreatePlayer(database, out ResourceInventory playerInventory2);
            GameObject tunerGo2 = CreateTuner(out PortalTuner tuner2);
            GameObject homeGo2 = CreateHome(database, out HomeBaseManager home2, out ResourceInventory storage2);
            GameObject universeGo2 = CreateUniverse(out UniverseManager universe2);

            // Dirty state that must be overwritten by the load.
            playerInventory2.Add(resource, 99);
            storage2.Add(resource, 99);
            tuner2.UnlockRingTier(4); // MaxRing 8
            universe2.EnterUniverse("L00-R00-P000");
            player2.transform.position = new Vector3(50f, 50f, 50f);

            yield return null;

            Assert.IsTrue(GameFlow.LoadSlot(slot), "Loading a valid slot must succeed.");
            yield return WaitForApply();

            Assert.AreEqual(7, playerInventory2.GetAmount(resource), "Player inventory must be restored.");
            Assert.AreEqual(4, storage2.GetAmount(resource), "Home storage must be restored.");

            Assert.AreEqual(6, tuner2.MaxRing, "Tuner progression must be restored.");
            Assert.AreEqual("L01-R02-P003", universe2.Current.ToString(), "Current universe must be restored.");
            Assert.AreEqual(new Vector3(1f, 2f, 3f), player2.transform.position,
                "Player position must be restored.");

            Assert.IsTrue(home2.Discovered.Contains("L01-R02-P003"),
                "Discovered coordinates must be restored.");

            // Repeated load must be idempotent.
            Assert.IsTrue(GameFlow.LoadSlot(slot));
            yield return WaitForApply();

            Assert.AreEqual(7, playerInventory2.GetAmount(resource));
            Assert.AreEqual(4, storage2.GetAmount(resource));
            Assert.AreEqual(6, tuner2.MaxRing);
            Assert.AreEqual("L01-R02-P003", universe2.Current.ToString());

            UnityEngine.Object.Destroy(player2);
            UnityEngine.Object.Destroy(tunerGo2);
            UnityEngine.Object.Destroy(homeGo2);
            UnityEngine.Object.Destroy(universeGo2);
            yield return null;
        }

        // ------------------------------------------------------------------
        // Helpers
        // ------------------------------------------------------------------

        /// <summary>
        /// GameFlow.ApplyPendingSave waits three frames before restoring so the
        /// freshly loaded scene's systems finish wiring first. Yield a few extra
        /// frames so the deferred apply has certainly completed before asserting.
        /// </summary>
        private static IEnumerator WaitForApply()
        {
            for (int i = 0; i < 5; i++) yield return null;
        }

        private GameObject CreatePlayer(ResourceDatabase database, out ResourceInventory inventory)
        {
            var go = new GameObject("Player");
            go.transform.SetParent(m_Root.transform, false);
            inventory = go.AddComponent<ResourceInventory>();
            SetField(inventory, "m_Database", database);
            go.AddComponent<PlayerSaveState>();
            return go;
        }

        private GameObject CreateTuner(out PortalTuner tuner)
        {
            var go = new GameObject("Tuner");
            go.transform.SetParent(m_Root.transform, false);
            tuner = go.AddComponent<PortalTuner>();
            return go;
        }

        private GameObject CreateHome(ResourceDatabase database, out HomeBaseManager home,
            out ResourceInventory storage)
        {
            var go = new GameObject("Home Base");
            go.transform.SetParent(m_Root.transform, false);
            home = go.AddComponent<HomeBaseManager>();
            storage = go.AddComponent<ResourceInventory>();
            SetField(storage, "m_Database", database);
            SetField(home, "m_Storage", storage);
            return go;
        }

        private GameObject CreateUniverse(out UniverseManager universe)
        {
            var go = new GameObject("Universe Manager");
            go.transform.SetParent(m_Root.transform, false);
            universe = go.AddComponent<UniverseManager>();
            go.AddComponent<UniverseContentGenerator>();
            SetField(universe, "m_ContentParent", m_Root.transform);
            return go;
        }

        private ResourceDefinition CreateResource(string id, string displayName)
        {
            var resource = ScriptableObject.CreateInstance<ResourceDefinition>();
            SetField(resource, "m_Id", id);
            SetField(resource, "m_DisplayName", displayName);
            SetField(resource, "m_Tier", 0);
            m_Assets.Add(resource);
            return resource;
        }

        private ResourceDatabase CreateDatabase(params ResourceDefinition[] resources)
        {
            var database = ScriptableObject.CreateInstance<ResourceDatabase>();
            SetField(database, "m_Resources", new List<ResourceDefinition>(resources));
            m_Assets.Add(database);
            return database;
        }

        private static void SetField(object target, string fieldName, object value)
        {
            Assert.IsNotNull(target, "Cannot set field on a null target.");
            FieldInfo field = target.GetType().GetField(fieldName,
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(field, $"Field '{fieldName}' not found on {target.GetType().Name}.");
            field.SetValue(target, value);
        }
    }
}