using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Game;
using NUnit.Framework;
using Portals.Game;
using Portals.Save;
using UnityEngine;

namespace Portals.Tests
{
    /// <summary>
    /// Regression coverage for the gameplay save/load gaps found during end-to-end
    /// validation: player pose, current universe, harvested resource nodes,
    /// per-owner inventory isolation, repeated loads and corrupt slots.
    /// </summary>
    public class GameplaySaveLoadTests
    {
        private GameObject m_Root;
        private string m_SaveDir;
        private readonly List<ScriptableObject> m_Assets = new List<ScriptableObject>();
        private readonly List<GameObject> m_ExtraObjects = new List<GameObject>();

        [SetUp]
        public void SetUp()
        {
            m_Root = new GameObject("SaveLoadTestRoot");
            m_SaveDir = Path.Combine(Path.GetTempPath(), "portal_save_test_" + Guid.NewGuid().ToString("N"));
            SaveSystem.SetDirectoryOverride(m_SaveDir);
            SaveStateRegistry.Clear();
            GameplayInput.ClearUiBlockers();
        }

        [TearDown]
        public void TearDown()
        {
            SaveStateRegistry.Clear();
            GameplayInput.ClearUiBlockers();
            SaveSystem.ClearDirectoryOverride();
            try { if (Directory.Exists(m_SaveDir)) Directory.Delete(m_SaveDir, true); } catch { }

            for (int i = 0; i < m_ExtraObjects.Count; i++)
                if (m_ExtraObjects[i] != null) UnityEngine.Object.DestroyImmediate(m_ExtraObjects[i]);
            m_ExtraObjects.Clear();

            foreach (ScriptableObject asset in m_Assets)
                if (asset != null) UnityEngine.Object.DestroyImmediate(asset);
            m_Assets.Clear();

            if (m_Root != null) UnityEngine.Object.DestroyImmediate(m_Root);
        }

        // ------------------------------------------------------------------
        // Player pose
        // ------------------------------------------------------------------

        [Test]
        public void PlayerSaveState_RoundTripsPositionAndRotation()
        {
            var player = NewObject("Player");
            player.AddComponent<CharacterController>();
            var state = player.AddComponent<PlayerSaveState>();

            player.transform.SetPositionAndRotation(new Vector3(1f, 2f, 3f),
                Quaternion.Euler(0f, 90f, 0f));

            var data = new SaveGameData();
            state.CaptureInto(data);

            Assert.IsTrue(data.player.hasTransform);
            Assert.AreEqual(1f, data.player.px, 1e-4f);
            Assert.AreEqual(2f, data.player.py, 1e-4f);
            Assert.AreEqual(3f, data.player.pz, 1e-4f);

            player.transform.SetPositionAndRotation(new Vector3(9f, 9f, 9f), Quaternion.identity);
            Assert.IsTrue(state.ApplyFrom(data));

            Assert.AreEqual(1f, player.transform.position.x, 1e-4f);
            Assert.AreEqual(2f, player.transform.position.y, 1e-4f);
            Assert.AreEqual(3f, player.transform.position.z, 1e-4f);
            Assert.AreEqual(90f, player.transform.eulerAngles.y, 1e-3f,
                "Player yaw should be restored.");
        }

        // ------------------------------------------------------------------
        // Current universe + harvested nodes
        // ------------------------------------------------------------------

        [Test]
        public void UniverseManager_RoundTripsCurrentUniverseAndHarvestedNodes()
        {
            var resource = CreateResource("scrap", "Scrap Metal");
            var inventory = NewComponent<ResourceInventory>("Carry");
            var database = CreateDatabase(resource);
            SetField(inventory, "m_Database", database);

            var contentParent = new GameObject("Content").transform;
            contentParent.SetParent(m_Root.transform, false);

            var template = CreateNodeTemplate(resource);

            var managerGo = NewObject("UniverseManager");
            var manager = managerGo.AddComponent<UniverseManager>();
            var generator = managerGo.AddComponent<UniverseContentGenerator>();
            SetField(generator, "m_ResourceNodePrefabs", new[] { template });
            SetField(generator, "m_BaseResourceNodes", 1);
            SetField(generator, "m_SpawnRadius", 5f);
            SetField(manager, "m_Generator", generator);
            SetField(manager, "m_ContentParent", contentParent);

            string coord = "L00-R00-P001";
            manager.EnterUniverse(coord);

            ResourceNode[] nodes = contentParent.GetComponentsInChildren<ResourceNode>(true);
            Assert.AreEqual(1, nodes.Length, "The test universe should spawn exactly one node.");
            Assert.IsNotNull(nodes[0].SpawnKey, "Generated nodes must have a stable spawn key.");

            string savedKey = nodes[0].SpawnKey;

            // Save before harvesting: the node must be reactivated by a later load.
            var beforeHarvest = new SaveGameData();
            manager.CaptureInto(beforeHarvest);
            Assert.AreEqual(coord, beforeHarvest.progression.currentUniverse);

            int harvested = nodes[0].Harvest(inventory);
            Assert.Greater(harvested, 0);
            Assert.IsTrue(nodes[0].IsHarvested);
            Assert.IsFalse(nodes[0].gameObject.activeSelf);

            var afterHarvest = new SaveGameData();
            manager.CaptureInto(afterHarvest);
            Assert.IsTrue(afterHarvest.world.harvestedNodes.Contains(savedKey),
                "Harvested node key must be saved.");

            // Same-scene load of the older save must undo the harvest.
            Assert.IsTrue(manager.ApplyFrom(beforeHarvest));
            Assert.IsFalse(nodes[0].IsHarvested, "Loading an older save must restore unharvested nodes.");
            Assert.IsTrue(nodes[0].gameObject.activeSelf);

            // Fresh manager load of the newer save must restore the harvested state.
            var contentParent2 = new GameObject("Content2").transform;
            contentParent2.SetParent(m_Root.transform, false);

            var manager2Go = NewObject("UniverseManager2");
            var manager2 = manager2Go.AddComponent<UniverseManager>();
            var generator2 = manager2Go.AddComponent<UniverseContentGenerator>();
            SetField(generator2, "m_ResourceNodePrefabs", new[] { template });
            SetField(generator2, "m_BaseResourceNodes", 1);
            SetField(generator2, "m_SpawnRadius", 5f);
            SetField(manager2, "m_Generator", generator2);
            SetField(manager2, "m_ContentParent", contentParent2);

            Assert.IsTrue(manager2.ApplyFrom(afterHarvest));
            Assert.AreEqual(new UniverseCoordinate(0, 0, 1), manager2.Current);

            ResourceNode[] nodes2 = contentParent2.GetComponentsInChildren<ResourceNode>(true);
            Assert.AreEqual(1, nodes2.Length, "Restoring a universe must not duplicate its nodes.");
            Assert.AreEqual(savedKey, nodes2[0].SpawnKey);
            Assert.IsTrue(nodes2[0].IsHarvested, "Restored universe must keep harvested nodes harvested.");
            Assert.IsFalse(nodes2[0].gameObject.activeSelf);
        }

        [Test]
        public void UniverseManager_LegacySaveWithoutUniverse_GeneratesStartingWorld()
        {
            var contentParent = new GameObject("Content").transform;
            contentParent.SetParent(m_Root.transform, false);

            var managerGo = NewObject("UniverseManager");
            var manager = managerGo.AddComponent<UniverseManager>();
            var generator = managerGo.AddComponent<UniverseContentGenerator>();
            SetField(generator, "m_ResourceNodePrefabs", Array.Empty<GameObject>());
            SetField(manager, "m_Generator", generator);
            SetField(manager, "m_ContentParent", contentParent);

            var data = new SaveGameData(); // no currentUniverse (old save)
            Assert.IsTrue(manager.ApplyFrom(data));
            Assert.AreEqual(UniverseCoordinate.Prime, manager.Current);
            Assert.IsNotNull(contentParent.Find("Universe_L00-R00-P000"),
                "The starting universe root must exist for a legacy save.");
        }

        // ------------------------------------------------------------------
        // Inventory isolation and repeated loads
        // ------------------------------------------------------------------

        [Test]
        public void Inventory_RepeatedApplyDoesNotDuplicateOrOverwriteOtherOwner()
        {
            var scrap = CreateResource("scrap", "Scrap Metal");
            var copper = CreateResource("copper", "Copper Wire");
            var database = CreateDatabase(scrap, copper);

            var player = NewComponent<ResourceInventory>("Player");
            var home = NewComponent<ResourceInventory>("Home Base");
            SetField(player, "m_Database", database);
            SetField(home, "m_Database", database);

            player.Add(scrap, 5);
            home.Add(copper, 9);

            var data = new SaveGameData();
            player.CaptureInto(data);
            home.CaptureInto(data);

            var player2 = NewComponent<ResourceInventory>("Player");
            var home2 = NewComponent<ResourceInventory>("Home Base");
            SetField(player2, "m_Database", database);
            SetField(home2, "m_Database", database);

            player2.ApplyFrom(data);
            home2.ApplyFrom(data);
            player2.ApplyFrom(data);
            home2.ApplyFrom(data);

            Assert.AreEqual(5, player2.GetAmount(scrap), "Repeated loads must not duplicate items.");
            Assert.AreEqual(0, player2.GetAmount(copper));
            Assert.AreEqual(9, home2.GetAmount(copper));
            Assert.AreEqual(0, home2.GetAmount(scrap));
        }

        // ------------------------------------------------------------------
        // Resource node restore
        // ------------------------------------------------------------------

        [Test]
        public void ResourceNode_RestoreHarvestedTogglesActiveState()
        {
            var resource = CreateResource("scrap", "Scrap Metal");
            var go = NewObject("Node");
            var node = go.AddComponent<ResourceNode>();
            SetField(node, "m_Resource", resource);

            node.RestoreHarvested(true);
            Assert.IsTrue(node.IsHarvested);
            Assert.IsFalse(go.activeSelf);

            node.RestoreHarvested(false);
            Assert.IsFalse(node.IsHarvested);
            Assert.IsTrue(go.activeSelf);
        }

        // ------------------------------------------------------------------
        // Input gating
        // ------------------------------------------------------------------

        [Test]
        public void GameplayInput_UiBlockersBalanceAndBlock()
        {
            Assert.IsFalse(GameplayInput.UiBlocked);

            GameplayInput.PushUiBlocker();
            GameplayInput.PushUiBlocker();
            Assert.IsTrue(GameplayInput.UiBlocked);

            GameplayInput.PopUiBlocker();
            Assert.IsTrue(GameplayInput.UiBlocked, "One blocker must remain.");

            GameplayInput.PopUiBlocker();
            Assert.IsFalse(GameplayInput.UiBlocked);

            // Popping at zero must not go negative.
            GameplayInput.PopUiBlocker();
            Assert.IsFalse(GameplayInput.UiBlocked);
        }

        // ------------------------------------------------------------------
        // Corrupt / missing saves
        // ------------------------------------------------------------------

        [Test]
        public void SaveSystem_CorruptAndMissingSlotsAreHandledSafely()
        {
            Assert.IsNull(SaveSystem.Load(0), "Missing slot must load as null.");

            Directory.CreateDirectory(m_SaveDir);
            File.WriteAllText(SaveSystem.SlotPath(0), "{ this is not valid json ");
            Assert.IsNull(SaveSystem.Load(0), "Corrupt slot must load as null, never throw.");

            File.WriteAllText(SaveSystem.SlotPath(1), "");
            Assert.IsNull(SaveSystem.Load(1), "Empty slot must load as null.");

            Assert.AreEqual(-1, SaveSystem.MostRecentValidSlot(),
                "Corrupt/empty slots must not be reported as the most recent save.");
        }

        [Test]
        public void SaveSystem_ValidSlotRoundTripsThroughDisk()
        {
            var data = new SaveGameData
            {
                saveName = "Round trip",
                sceneName = "MAINSCENE",
                savedAtIso = DateTime.UtcNow.ToString("o")
            };
            data.player.hasTransform = true;
            data.player.px = 4f;
            data.player.py = 5f;
            data.player.pz = 6f;
            data.progression.currentUniverse = "L01-R02-P003";
            data.world.harvestedNodes.Add("L01-R02-P003:r0");

            Assert.IsTrue(SaveSystem.Save(2, data, out string error), error);
            SaveGameData loaded = SaveSystem.Load(2);
            Assert.IsNotNull(loaded);
            Assert.AreEqual(4f, loaded.player.px, 1e-5f);
            Assert.AreEqual("L01-R02-P003", loaded.progression.currentUniverse);
            Assert.IsTrue(loaded.world.harvestedNodes.Contains("L01-R02-P003:r0"));

            Assert.IsTrue(SaveSystem.DeleteSlot(2));
            Assert.IsNull(SaveSystem.Load(2));
        }

        // ------------------------------------------------------------------
        // Helpers
        // ------------------------------------------------------------------

        private GameObject NewObject(string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(m_Root.transform, false);
            return go;
        }

        private T NewComponent<T>(string name) where T : Component
        {
            return NewObject(name).AddComponent<T>();
        }

        private GameObject CreateNodeTemplate(ResourceDefinition resource)
        {
            var go = new GameObject("NodeTemplate");
            go.transform.position = new Vector3(10000f, 10000f, 10000f);
            var node = go.AddComponent<ResourceNode>();
            SetField(node, "m_Resource", resource);
            m_ExtraObjects.Add(go);
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